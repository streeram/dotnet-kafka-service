namespace Kafka.Common.Models
{
    /// <summary>
    /// Configuration settings for Kafka connection and authentication.
    /// </summary>
    public class KafkaSettings
    {
        /// <summary>
        /// Gets or sets the Kafka bootstrap servers.
        /// </summary>
        public string BootstrapServers { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the client ID for authentication.
        /// </summary>
        public string ClientId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the client secret for authentication.
        /// </summary>
        public string ClientSecret { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the token endpoint URL for OAuth authentication.
        /// </summary>
        public string TokenEndpointUrl { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the OAuth scope for authentication.
        /// </summary>
        public string Scope { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the logical cluster identifier.
        /// </summary>
        public string LogicalCluster { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the identity pool ID for authentication.
        /// </summary>
        public string IdentityPoolId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the consumer group ID for Kafka consumers.
        /// </summary>
        public string ConsumerGroupId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of Kafka topics to subscribe to.
        /// </summary>
        public List<string> Topics { get; init; } = [];

        /// <summary>
        /// Gets or sets whether schema validation is enabled.
        /// </summary>
        public bool EnableSchemaValidation { get; init; } = false;

        /// <summary>
        /// Gets or sets the schema registry URL.
        /// </summary>
        public string SchemaRegistryUrl { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the schema registry API key.
        /// </summary>
        public string SchemaRegistryApiKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the schema registry API secret.
        /// </summary>
        public string SchemaRegistryApiSecret { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the retry configuration.
        /// </summary>
        public RetryConfiguration RetryConfiguration { get; init; } = new();

        /// <summary>
        /// Gets or sets the third-party API configuration.
        /// </summary>
        public ThirdPartyApiConfiguration ThirdPartyApiConfiguration { get; init; } = new();
    }

    /// <summary>
    /// Configuration for retry and dead letter queue handling.
    /// </summary>
    public class RetryConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum number of retry attempts.
        /// </summary>
        public int MaxRetryAttempts { get; init; } = 3;

        /// <summary>
        /// Gets or sets the suffix for retry topics.
        /// </summary>
        public string RetryTopicSuffix { get; init; } = "-retry";

        /// <summary>
        /// Gets or sets the suffix for dead letter queue topics.
        /// </summary>
        public string DeadLetterTopicSuffix { get; init; } = "-dlq";

        /// <summary>
        /// Gets or sets the delay in milliseconds before retrying.
        /// </summary>
        public int RetryDelayMs { get; init; } = 5000;

        /// <summary>
        /// Gets or sets whether to use exponential backoff for retry delays.
        /// </summary>
        public bool UseExponentialBackoff { get; init; } = true;

        /// <summary>
        /// Gets or sets the backoff multiplier for exponential backoff.
        /// </summary>
        public double BackoffMultiplier { get; init; } = 2.0;
    }

    /// <summary>
    /// Configuration for third-party API integration.
    /// </summary>
    public class ThirdPartyApiConfiguration
    {
        /// <summary>
        /// Gets or sets the base URL for the third-party API.
        /// </summary>
        public string BaseUrl { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the API key for authentication.
        /// </summary>
        public string ApiKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the timeout in milliseconds for API calls.
        /// </summary>
        public int TimeoutMs { get; init; } = 30000;

        /// <summary>
        /// Gets or sets the HTTP status codes that should trigger a retry.
        /// </summary>
        public List<int> RetryableStatusCodes { get; init; } = [500, 502, 503, 504, 408, 429];

        /// <summary>
        /// Gets or sets the HTTP status codes that should send the message to DLQ.
        /// </summary>
        public List<int> DeadLetterStatusCodes { get; init; } = [400, 401, 403, 404, 422];
    }
}