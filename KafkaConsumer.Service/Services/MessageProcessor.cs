using System.Net;
using System.Text.Json;
using Kafka.Common.Models;

namespace KafkaConsumer.Service.Services;

public class MessageProcessor : IMessageProcessor
{
    private readonly ILogger<MessageProcessor> _logger;
    private readonly IThirdPartyApiService _thirdPartyApiService;

    public MessageProcessor(
        ILogger<MessageProcessor> logger,
        IThirdPartyApiService thirdPartyApiService)
    {
        _logger = logger;
        _thirdPartyApiService = thirdPartyApiService;
    }

    public async Task ProcessAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            // Check if this is a retry message
            var retryMessage = TryExtractRetryMessage(message);
            string actualMessage;
            int retryAttempt = 0;

            if (retryMessage != null)
            {
                actualMessage = retryMessage.OriginalMessage;
                retryAttempt = retryMessage.RetryAttempt;
                _logger.LogInformation(
                    "Processing retry message: attempt {RetryAttempt}/{MaxRetryAttempts}",
                    retryAttempt, retryMessage.MaxRetryAttempts);
            }
            else
            {
                actualMessage = message;
                _logger.LogInformation("Processing new message");
            }

            // Deserialize the actual event message
            var eventMessage = JsonSerializer.Deserialize<EventMessage>(actualMessage);
            _logger.LogInformation($"Processing event: {eventMessage?.Type} with ID: {eventMessage?.Id}");

            // Call third-party API with the payload
            var (statusCode, responseContent) = await _thirdPartyApiService.SendPayloadAsync(
                actualMessage, cancellationToken);

            _logger.LogInformation(
                "Third-party API call completed with status: {StatusCode}",
                statusCode);

            // Check if the API call was successful
            if (IsSuccessStatusCode(statusCode))
            {
                _logger.LogInformation($"Successfully processed event: {eventMessage?.Id}");
                // Add any additional success processing logic here
            }
            else
            {
                // API call failed - throw exception with status code information
                var apiException = new ThirdPartyApiException(
                    $"Third-party API returned status {statusCode}: {responseContent}",
                    statusCode,
                    responseContent);

                throw apiException;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message");
            throw new MessageProcessingException("Invalid message format", ex);
        }
        catch (ThirdPartyApiException)
        {
            // Re-throw API exceptions as-is so they can be handled by error handling service
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message");
            throw;
        }
    }

    private RetryMessage? TryExtractRetryMessage(string message)
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

    private static bool IsSuccessStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode >= 200 && (int)statusCode <= 299;
    }
}

/// <summary>
/// Exception thrown when third-party API calls fail.
/// </summary>
public class ThirdPartyApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ApiResponse { get; }

    public ThirdPartyApiException(string message, HttpStatusCode statusCode, string? apiResponse = null)
        : base(message)
    {
        StatusCode = statusCode;
        ApiResponse = apiResponse;
    }

    public ThirdPartyApiException(string message, HttpStatusCode statusCode, string? apiResponse, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ApiResponse = apiResponse;
    }
}

/// <summary>
/// Exception thrown when message processing fails due to invalid format or other processing issues.
/// </summary>
public class MessageProcessingException : Exception
{
    public MessageProcessingException(string message) : base(message) { }
    public MessageProcessingException(string message, Exception innerException) : base(message, innerException) { }
}