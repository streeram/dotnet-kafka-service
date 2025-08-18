using System.Net;

namespace KafkaConsumer.Service.Services;

/// <summary>
/// Interface for third-party API service.
/// </summary>
public interface IThirdPartyApiService
{
    /// <summary>
    /// Sends a payload to the third-party API and returns the response status.
    /// </summary>
    /// <param name="payload">The payload to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The HTTP status code and response content</returns>
    Task<(HttpStatusCode StatusCode, string ResponseContent)> SendPayloadAsync(
        string payload,
        CancellationToken cancellationToken = default);
}