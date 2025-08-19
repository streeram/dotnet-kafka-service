using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Confluent.Kafka;
using Kafka.Common.Models;
using Microsoft.Extensions.Options;

namespace KafkaConsumer.Service.Services;
/// <summary>
/// Service for handling failed messages and determining retry vs DLQ routing.
/// </summary>
public class ErrorHandlingService : IErrorHandlingService
{
    private readonly IConsumerKafkaProducerService _producerService;
    private readonly KafkaSettings _settings;
    private readonly ILogger<ErrorHandlingService> _logger;

    public ErrorHandlingService(
        IConsumerKafkaProducerService producerService,
        IOptions<KafkaSettings> settings,
        ILogger<ErrorHandlingService> logger)
    {
        _producerService = producerService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task HandleFailedMessageAsync(
        ConsumeResult<string, string> consumeResult,
        Exception exception,
        HttpStatusCode? apiStatusCode = null,
        string? apiResponse = null,
        CancellationToken cancellationToken = default)
    {
        var originalTopic = consumeResult.Topic;
        var originalKey = consumeResult.Message.Key ?? string.Empty;
        var originalMessage = consumeResult.Message.Value;

        // Try to extract retry information from the message
        var retryAttempt = ExtractRetryAttempt(originalMessage);
        var isRetryMessage = retryAttempt > 0;

        _logger.LogWarning(
            "Handling failed message from topic {Topic}, partition {Partition}, offset {Offset}. " +
            "Retry attempt: {RetryAttempt}, API Status: {ApiStatusCode}",
            originalTopic, consumeResult.Partition, consumeResult.Offset, retryAttempt, apiStatusCode);

        if (ShouldRetry(exception, apiStatusCode, retryAttempt))
        {
            await SendToRetryTopicAsync(
                originalTopic, originalKey, originalMessage,
                retryAttempt, exception, apiStatusCode, apiResponse, cancellationToken);
        }
        else
        {
            await SendToDeadLetterQueueAsync(
                originalTopic, originalKey, originalMessage,
                retryAttempt, exception, apiStatusCode, apiResponse, cancellationToken);
        }
    }

    public bool ShouldRetry(Exception exception, HttpStatusCode? apiStatusCode, int retryAttempt)
    {
        // Check if we've exceeded max retry attempts
        if (retryAttempt >= _settings.RetryConfiguration.MaxRetryAttempts)
        {
            _logger.LogInformation(
                "Max retry attempts ({MaxRetries}) exceeded for message. Sending to DLQ.",
                _settings.RetryConfiguration.MaxRetryAttempts);
            return false;
        }

        // If we have an API status code, use it to determine retry behavior
        if (apiStatusCode.HasValue)
        {
            var statusCode = (int)apiStatusCode.Value;

            // Check if it's a status code that should go directly to DLQ
            if (_settings.ThirdPartyApiConfiguration.DeadLetterStatusCodes.Contains(statusCode))
            {
                _logger.LogInformation(
                    "API returned status code {StatusCode} which is configured for DLQ. Not retrying.",
                    statusCode);
                return false;
            }

            // Check if it's a retryable status code
            if (_settings.ThirdPartyApiConfiguration.RetryableStatusCodes.Contains(statusCode))
            {
                _logger.LogInformation(
                    "API returned status code {StatusCode} which is configured for retry. Attempt {RetryAttempt}.",
                    statusCode, retryAttempt + 1);
                return true;
            }

            // For other status codes, don't retry
            _logger.LogInformation(
                "API returned status code {StatusCode} which is not configured for retry. Sending to DLQ.",
                statusCode);
            return false;
        }

        // For non-API exceptions, retry for transient errors
        if (IsTransientException(exception))
        {
            _logger.LogInformation(
                "Exception {ExceptionType} is considered transient. Retrying attempt {RetryAttempt}.",
                exception.GetType().Name, retryAttempt + 1);
            return true;
        }

        _logger.LogInformation(
            "Exception {ExceptionType} is not considered transient. Sending to DLQ.",
            exception.GetType().Name);
        return false;
    }

    private async Task SendToRetryTopicAsync(
        string originalTopic,
        string originalKey,
        string originalMessage,
        int currentRetryAttempt,
        Exception exception,
        HttpStatusCode? apiStatusCode,
        string? apiResponse,
        CancellationToken cancellationToken)
    {
        var retryTopic = GetRetryTopicName(originalTopic);
        var nextRetryAttempt = currentRetryAttempt + 1;

        // Calculate exponential backoff delay
        var delayMs = CalculateRetryDelay(nextRetryAttempt);
        var processAfterTimestamp = DateTimeOffset.UtcNow.AddMilliseconds(delayMs);

        // For retry topics, we send the original message with retry metadata in headers
        var messageToSend = GetOriginalMessageContent(originalMessage);

        try
        {
            var headers = new Headers
            {
                { "x-retry-attempt", BitConverter.GetBytes(nextRetryAttempt) },
                { "x-process-after", BitConverter.GetBytes(processAfterTimestamp.ToUnixTimeMilliseconds()) },
                { "x-original-topic", System.Text.Encoding.UTF8.GetBytes(GetOriginalTopicName(originalTopic)) },
                { "x-first-attempt-timestamp", BitConverter.GetBytes(GetFirstAttemptTimestamp(originalMessage).ToUnixTimeMilliseconds()) },
                { "x-last-error-type", System.Text.Encoding.UTF8.GetBytes(exception.GetType().Name) },
                { "x-last-error-message", System.Text.Encoding.UTF8.GetBytes(exception.Message) }
            };

            if (apiStatusCode.HasValue)
            {
                headers.Add("x-api-status-code", BitConverter.GetBytes((int)apiStatusCode.Value));
            }

            if (!string.IsNullOrEmpty(apiResponse))
            {
                headers.Add("x-api-response", System.Text.Encoding.UTF8.GetBytes(apiResponse));
            }

            await _producerService.ProduceAsync(retryTopic, originalKey, messageToSend, headers, cancellationToken);

            _logger.LogInformation(
                "Message sent to retry topic {RetryTopic}. Retry attempt: {RetryAttempt}, " +
                "Delay: {DelayMs}ms, Process after: {ProcessAfter}",
                retryTopic, nextRetryAttempt, delayMs, processAfterTimestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send message to retry topic {RetryTopic}. Sending to DLQ instead.",
                retryTopic);

            // If we can't send to retry topic, send to DLQ
            await SendToDeadLetterQueueAsync(
                originalTopic, originalKey, originalMessage,
                currentRetryAttempt, exception, apiStatusCode, apiResponse, cancellationToken);
        }
    }

    private async Task SendToDeadLetterQueueAsync(
        string originalTopic,
        string originalKey,
        string originalMessage,
        int retryAttempt,
        Exception exception,
        HttpStatusCode? apiStatusCode,
        string? apiResponse,
        CancellationToken cancellationToken)
    {
        var dlqTopic = GetDeadLetterQueueTopicName(originalTopic);
        var dlqMessage = CreateRetryMessage(
            originalTopic, originalKey, originalMessage,
            retryAttempt, exception, apiStatusCode, apiResponse, isFinalFailure: true);

        var dlqMessageJson = JsonSerializer.Serialize(dlqMessage);

        try
        {
            await _producerService.ProduceAsync(dlqTopic, originalKey, dlqMessageJson, cancellationToken);
            _logger.LogWarning(
                "Message sent to dead letter queue {DlqTopic} after {RetryAttempts} retry attempts",
                dlqTopic, retryAttempt);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "Failed to send message to dead letter queue {DlqTopic}. Message may be lost!",
                dlqTopic);
            throw;
        }
    }

    private long CalculateRetryDelay(int retryAttempt)
    {
        if (!_settings.RetryConfiguration.UseExponentialBackoff)
        {
            return _settings.RetryConfiguration.RetryDelayMs;
        }

        // Calculate exponential backoff: baseDelay * (multiplier ^ (attempt - 1))
        var baseDelayMs = Math.Max(_settings.RetryConfiguration.RetryDelayMs, _settings.RetryConfiguration.MinRetryDelayMs);
        var exponentialDelay = baseDelayMs * Math.Pow(_settings.RetryConfiguration.BackoffMultiplier, retryAttempt - 1);

        // Apply jitter (±10% randomization) to prevent thundering herd
        var jitterFactor = 1.0 + (Random.Shared.NextDouble() - 0.5) * 0.2; // ±10%
        var delayWithJitter = exponentialDelay * jitterFactor;

        // Ensure delay is within bounds
        var finalDelay = Math.Min(delayWithJitter, _settings.RetryConfiguration.MaxRetryDelayMs);
        finalDelay = Math.Max(finalDelay, _settings.RetryConfiguration.MinRetryDelayMs);

        return (long)finalDelay;
    }

    private string GetOriginalMessageContent(string message)
    {
        // If this is already a retry message, extract the original content
        var retryMessage = TryExtractRetryMessage(message);
        return retryMessage?.OriginalMessage ?? message;
    }

    private string GetOriginalTopicName(string currentTopic)
    {
        // Remove retry and DLQ suffixes to get the original topic name
        return currentTopic
            .Replace(_settings.RetryConfiguration.RetryTopicSuffix, "")
            .Replace(_settings.RetryConfiguration.DeadLetterTopicSuffix, "");
    }

    private DateTimeOffset GetFirstAttemptTimestamp(string message)
    {
        var retryMessage = TryExtractRetryMessage(message);
        return retryMessage?.FirstAttemptTimestamp ?? DateTimeOffset.UtcNow;
    }

    // ... existing code ...

    private RetryMessage CreateRetryMessage(
        string originalTopic,
        string originalKey,
        string originalMessage,
        int retryAttempt,
        Exception exception,
        HttpStatusCode? apiStatusCode,
        string? apiResponse,
        bool isFinalFailure = false)
    {
        var now = DateTimeOffset.UtcNow;

        // Try to extract existing retry message to preserve first attempt timestamp
        var existingRetryMessage = TryExtractRetryMessage(originalMessage);
        var firstAttemptTimestamp = existingRetryMessage?.FirstAttemptTimestamp ?? now;

        return new RetryMessage
        {
            OriginalMessage = existingRetryMessage?.OriginalMessage ?? originalMessage,
            OriginalTopic = existingRetryMessage?.OriginalTopic ?? originalTopic,
            OriginalKey = originalKey,
            RetryAttempt = retryAttempt,
            MaxRetryAttempts = _settings.RetryConfiguration.MaxRetryAttempts,
            FirstAttemptTimestamp = firstAttemptTimestamp,
            LastAttemptTimestamp = now,
            LastError = new ErrorInfo
            {
                Message = exception.Message,
                ExceptionType = exception.GetType().Name,
                HttpStatusCode = apiStatusCode.HasValue ? (int)apiStatusCode.Value : null,
                ApiResponse = apiResponse,
                Timestamp = now
            },
            Metadata = new Dictionary<string, string>
            {
                ["isFinalFailure"] = isFinalFailure.ToString(),
                ["processingHost"] = Environment.MachineName,
                ["consumerGroupId"] = _settings.ConsumerGroupId
            }
        };
    }

    private int ExtractRetryAttempt(string message)
    {
        var retryMessage = TryExtractRetryMessage(message);
        return retryMessage?.RetryAttempt ?? 0;
    }

    private static RetryMessage? TryExtractRetryMessage(string message)
    {
        try
        {
            return JsonSerializer.Deserialize<RetryMessage>(message);
        }
        catch
        {
            // If deserialization fails, it's not a retry message
            return null;
        }
    }

    private string GetRetryTopicName(string originalTopic)
    {
        // Remove existing suffixes if present
        var baseTopic = originalTopic
            .Replace(_settings.RetryConfiguration.RetryTopicSuffix, "")
            .Replace(_settings.RetryConfiguration.DeadLetterTopicSuffix, "");

        return baseTopic + _settings.RetryConfiguration.RetryTopicSuffix;
    }

    private string GetDeadLetterQueueTopicName(string originalTopic)
    {
        // Remove existing suffixes if present
        var baseTopic = originalTopic
            .Replace(_settings.RetryConfiguration.RetryTopicSuffix, "")
            .Replace(_settings.RetryConfiguration.DeadLetterTopicSuffix, "");

        return baseTopic + _settings.RetryConfiguration.DeadLetterTopicSuffix;
    }

    private static bool IsTransientException(Exception exception)
    {
        return exception switch
        {
            TimeoutException => true,
            HttpRequestException => true,
            TaskCanceledException => true,
            SocketException => true,
            _ => false
        };
    }
}