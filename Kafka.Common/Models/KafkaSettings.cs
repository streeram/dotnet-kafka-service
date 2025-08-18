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
        /// Gets or sets the Schema Registry URL for schema validation.
        /// </summary>
        public string SchemaRegistryUrl { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the Schema Registry API key for authentication.
        /// </summary>
        public string SchemaRegistryApiKey { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the Schema Registry API secret for authentication.
        /// </summary>
        public string SchemaRegistryApiSecret { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets whether schema validation is enabled.
        /// </summary>
        public bool EnableSchemaValidation { get; init; } = true;
    }
}