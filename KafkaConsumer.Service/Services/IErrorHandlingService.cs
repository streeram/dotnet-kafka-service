using System.Net;
using Confluent.Kafka;

namespace KafkaConsumer.Service.Services;

/// <summary>
/// Interface for error handling service that determines message routing based on errors.
/// </summary>
public interface IErrorHandlingService
{
    /// <summary>
    /// Handles a failed message by determining whether to retry or send to DLQ.
    /// </summary>
    /// <param name="consumeResult">The original consume result</param>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="apiStatusCode">The HTTP status code from the third-party API (if applicable)</param>
    /// <param name="apiResponse">The response content from the third-party API (if applicable)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the async operation</returns>
    Task HandleFailedMessageAsync(
        ConsumeResult<string, string> consumeResult,
        Exception exception,
        HttpStatusCode? apiStatusCode = null,
        string? apiResponse = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if a message should be retried based on the error type and API response.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="apiStatusCode">The HTTP status code from the third-party API (if applicable)</param>
    /// <param name="retryAttempt">The current retry attempt number</param>
    /// <returns>True if the message should be retried, false if it should go to DLQ</returns>
    bool ShouldRetry(Exception exception, HttpStatusCode? apiStatusCode, int retryAttempt);
}