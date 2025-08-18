using System.Net;
using System.Text;
using Kafka.Common.Models;
using Microsoft.Extensions.Options;

namespace KafkaConsumer.Service.Services;

/// <summary>
/// Implementation of third-party API service.
/// </summary>
public class ThirdPartyApiService : IThirdPartyApiService
{
    private readonly HttpClient _httpClient;
    private readonly ThirdPartyApiConfiguration _config;
    private readonly ILogger<ThirdPartyApiService> _logger;

    public ThirdPartyApiService(
        HttpClient httpClient,
        IOptions<KafkaSettings> settings,
        ILogger<ThirdPartyApiService> logger)
    {
        _httpClient = httpClient;
        _config = settings.Value.ThirdPartyApiConfiguration;
        _logger = logger;

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_config.TimeoutMs);

        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _config.ApiKey);
        }
    }

    public async Task<(HttpStatusCode StatusCode, string ResponseContent)> SendPayloadAsync(
        string payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending payload to third-party API");

            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/process", content, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation(
                "Third-party API responded with status: {StatusCode}",
                response.StatusCode);

            return (response.StatusCode, responseContent);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request exception when calling third-party API");
            return (HttpStatusCode.ServiceUnavailable, ex.Message);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout when calling third-party API");
            return (HttpStatusCode.RequestTimeout, "Request timeout");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request was cancelled");
            return (HttpStatusCode.RequestTimeout, "Request cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when calling third-party API");
            return (HttpStatusCode.InternalServerError, ex.Message);
        }
    }
}