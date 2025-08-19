using Confluent.Kafka;
using Kafka.Common.Models;
using KafkaConsumer.Service.Services;
using Microsoft.Extensions.Options;

namespace KafkaConsumer.Service.Services;

/// <summary>
/// Service that handles retry message processing with time-based delays.
/// </summary>
public class RetryConsumerService : IRetryConsumerService, IDisposable
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<RetryConsumerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private IConsumer<string, string>? _consumer;
    private bool _disposed = false;
    private Task? _consumerTask;
    private CancellationTokenSource? _cancellationTokenSource;

    public RetryConsumerService(
        IOptions<KafkaSettings> settings,
        ILogger<RetryConsumerService> logger,
        IServiceProvider serviceProvider)
    {
        _settings = settings.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_consumerTask != null)
        {
            _logger.LogWarning("Retry consumer is already running");
            return;
        }

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = $"{_settings.ConsumerGroupId}-retry",
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.OAuthBearer,
            SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc,
            SaslOauthbearerClientId = _settings.ClientId,
            SaslOauthbearerClientSecret = _settings.ClientSecret,
            SaslOauthbearerTokenEndpointUrl = _settings.TokenEndpointUrl,
            SaslOauthbearerScope = _settings.Scope,
            SaslOauthbearerExtensions = $"logicalCluster={_settings.LogicalCluster},identityPoolId={_settings.IdentityPoolId}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            FetchMinBytes = 1024,
            FetchWaitMaxMs = _settings.RetryConfiguration.RetryConsumerPollingIntervalMs,
            MaxPartitionFetchBytes = 1048576,
            SessionTimeoutMs = 45000,
            HeartbeatIntervalMs = 3000,
            SocketKeepaliveEnable = true,
            ReconnectBackoffMs = 50,
            ReconnectBackoffMaxMs = 10000
        };

        _consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError($"Retry consumer error: {e.Reason}"))
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                _logger.LogInformation($"Retry consumer partitions assigned: [{string.Join(", ", partitions)}]");
            })
            .SetPartitionsRevokedHandler((c, partitions) =>
            {
                _logger.LogInformation($"Retry consumer partitions revoked: [{string.Join(", ", partitions)}]");
            })
            .Build();

        // Subscribe to all retry topics
        var retryTopics = _settings.Topics
            .Select(topic => topic + _settings.RetryConfiguration.RetryTopicSuffix)
            .ToList();

        _consumer.Subscribe(retryTopics);
        _logger.LogInformation("Retry consumer subscribed to topics: {Topics}", string.Join(", ", retryTopics));

        _consumerTask = Task.Run(() => StartRetryConsumerLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_consumerTask == null)
        {
            return;
        }

        _logger.LogInformation("Stopping retry consumer");

        _cancellationTokenSource?.Cancel();

        try
        {
            await _consumerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        _consumer?.Close();
        _consumer?.Dispose();
        _consumer = null;
        _consumerTask = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _logger.LogInformation("Retry consumer stopped");
    }

    public bool IsMessageReadyForProcessing(ConsumeResult<string, string> consumeResult)
    {
        if (consumeResult.Message.Headers == null)
        {
            _logger.LogWarning("Retry message missing headers, processing immediately");
            return true;
        }

        var processAfterHeader = consumeResult.Message.Headers
            .FirstOrDefault(h => h.Key == "x-process-after");

        if (processAfterHeader == null)
        {
            _logger.LogWarning("Retry message missing x-process-after header, processing immediately");
            return true;
        }

        try
        {
            var processAfterTimestamp = BitConverter.ToInt64(processAfterHeader.GetValueBytes());
            var processAfterTime = DateTimeOffset.FromUnixTimeMilliseconds(processAfterTimestamp);
            var now = DateTimeOffset.UtcNow;

            var isReady = now >= processAfterTime;

            if (!isReady)
            {
                var remainingDelay = processAfterTime - now;
                _logger.LogDebug(
                    "Message not ready for processing. Current time: {Now}, Process after: {ProcessAfter}, " +
                    "Remaining delay: {RemainingDelay}ms",
                    now, processAfterTime, remainingDelay.TotalMilliseconds);
            }

            return isReady;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing x-process-after header, processing immediately");
            return true;
        }
    }

    private async Task StartRetryConsumerLoop(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting retry consumer loop");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer!.Consume(cancellationToken);

                    if (consumeResult?.Message != null)
                    {
                        await ProcessRetryMessage(consumeResult, cancellationToken);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, $"Retry consumer consume error: {ex.Error.Reason}");
                    if (ex.Error.IsFatal) break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in retry consumer loop");
                    await Task.Delay(1000, cancellationToken); // Brief delay before retrying
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Retry consumer loop cancelled");
        }
        finally
        {
            _logger.LogInformation("Retry consumer loop ended");
        }
    }

    private async Task ProcessRetryMessage(
        ConsumeResult<string, string> consumeResult,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if the message is ready for processing
            if (!IsMessageReadyForProcessing(consumeResult))
            {
                // Message is not ready yet, don't commit the offset
                // The message will be reprocessed in the next poll
                return;
            }

            _logger.LogInformation(
                "Processing retry message: Key={Key}, Partition={Partition}, Offset={Offset}",
                consumeResult.Message.Key, consumeResult.Partition, consumeResult.Offset);

            using var scope = _serviceProvider.CreateScope();
            var messageProcessor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();

            // Process the original message content
            await messageProcessor.ProcessAsync(consumeResult.Message.Value, cancellationToken);

            // If processing succeeds, commit the offset
            _consumer!.Commit(consumeResult);

            _logger.LogInformation(
                "Successfully processed retry message at {TopicPartitionOffset}",
                consumeResult.TopicPartitionOffset);
        }
        catch (ThirdPartyApiException apiEx)
        {
            _logger.LogWarning(apiEx,
                "Third-party API error processing retry message at {TopicPartitionOffset}. Status: {StatusCode}",
                consumeResult.TopicPartitionOffset, apiEx.StatusCode);

            await HandleRetryMessageFailure(consumeResult, apiEx, apiEx.StatusCode, apiEx.ApiResponse, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing retry message at {TopicPartitionOffset}",
                consumeResult.TopicPartitionOffset);

            await HandleRetryMessageFailure(consumeResult, ex, null, null, cancellationToken);
        }
    }

    private async Task HandleRetryMessageFailure(
        ConsumeResult<string, string> consumeResult,
        Exception exception,
        System.Net.HttpStatusCode? apiStatusCode,
        string? apiResponse,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var errorHandlingService = scope.ServiceProvider.GetRequiredService<IErrorHandlingService>();

        // Extract retry information from headers
        var retryAttempt = ExtractRetryAttemptFromHeaders(consumeResult.Message.Headers);
        var originalTopic = ExtractOriginalTopicFromHeaders(consumeResult.Message.Headers);

        // Create a new consume result with the original topic for error handling
        var originalConsumeResult = new ConsumeResult<string, string>
        {
            Topic = originalTopic ?? GetOriginalTopicFromRetryTopic(consumeResult.Topic),
            Partition = consumeResult.Partition,
            Offset = consumeResult.Offset,
            Message = consumeResult.Message
        };

        await errorHandlingService.HandleFailedMessageAsync(
            originalConsumeResult, exception, apiStatusCode, apiResponse, cancellationToken);

        // Commit the offset since we've handled the failure
        _consumer!.Commit(consumeResult);
    }

    private int ExtractRetryAttemptFromHeaders(Headers? headers)
    {
        if (headers == null) return 0;

        var retryAttemptHeader = headers.FirstOrDefault(h => h.Key == "x-retry-attempt");
        if (retryAttemptHeader == null) return 0;

        try
        {
            return BitConverter.ToInt32(retryAttemptHeader.GetValueBytes());
        }
        catch
        {
            return 0;
        }
    }

    private string? ExtractOriginalTopicFromHeaders(Headers? headers)
    {
        if (headers == null) return null;

        var originalTopicHeader = headers.FirstOrDefault(h => h.Key == "x-original-topic");
        if (originalTopicHeader == null) return null;

        try
        {
            return System.Text.Encoding.UTF8.GetString(originalTopicHeader.GetValueBytes());
        }
        catch
        {
            return null;
        }
    }

    private string GetOriginalTopicFromRetryTopic(string retryTopic)
    {
        return retryTopic.Replace(_settings.RetryConfiguration.RetryTopicSuffix, "");
    }

    public void Dispose()
    {
        if (_disposed) return;

        StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}