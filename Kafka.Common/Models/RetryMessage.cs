using System.Text.Json.Serialization;

namespace Kafka.Common.Models;

/// <summary>
/// Represents a message that needs to be retried, containing the original message and retry metadata.
/// </summary>
public class RetryMessage
{
    /// <summary>
    /// Gets or sets the original message content.
    /// </summary>
    [JsonPropertyName("originalMessage")]
    public string OriginalMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original topic name.
    /// </summary>
    [JsonPropertyName("originalTopic")]
    public string OriginalTopic { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original message key.
    /// </summary>
    [JsonPropertyName("originalKey")]
    public string OriginalKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current retry attempt number.
    /// </summary>
    [JsonPropertyName("retryAttempt")]
    public int RetryAttempt { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts allowed.
    /// </summary>
    [JsonPropertyName("maxRetryAttempts")]
    public int MaxRetryAttempts { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was first processed.
    /// </summary>
    [JsonPropertyName("firstAttemptTimestamp")]
    public DateTimeOffset FirstAttemptTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last retry attempt.
    /// </summary>
    [JsonPropertyName("lastAttemptTimestamp")]
    public DateTimeOffset LastAttemptTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the error information from the last attempt.
    /// </summary>
    [JsonPropertyName("lastError")]
    public ErrorInfo LastError { get; set; } = new();

    /// <summary>
    /// Gets or sets additional metadata for the retry message.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Represents error information for failed message processing.
/// </summary>
public class ErrorInfo
{
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception type.
    /// </summary>
    [JsonPropertyName("exceptionType")]
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP status code if applicable.
    /// </summary>
    [JsonPropertyName("httpStatusCode")]
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Gets or sets the API response body if applicable.
    /// </summary>
    [JsonPropertyName("apiResponse")]
    public string? ApiResponse { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}