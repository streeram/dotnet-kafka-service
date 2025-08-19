using Confluent.Kafka;
using Kafka.Common.Models;
using Microsoft.Extensions.Options;

namespace KafkaConsumer.Service.Services;

/// <summary>
/// Kafka producer service for the consumer application.
/// </summary>
public class ConsumerKafkaProducerService : IConsumerKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<ConsumerKafkaProducerService> _logger;
    private bool _disposed = false;

    /// <summary>
    /// Constructor for the Kafka producer service.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="logger"></param>
    public ConsumerKafkaProducerService(
        IOptions<KafkaSettings> settings,
        ILogger<ConsumerKafkaProducerService> logger)
    {
        var kafkaSettings = settings.Value;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaSettings.BootstrapServers,
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.OAuthBearer,
            SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc,
            SaslOauthbearerClientId = kafkaSettings.ClientId,
            SaslOauthbearerClientSecret = kafkaSettings.ClientSecret,
            SaslOauthbearerTokenEndpointUrl = kafkaSettings.TokenEndpointUrl,
            SaslOauthbearerScope = kafkaSettings.Scope,
            SaslOauthbearerExtensions = $"logicalCluster={kafkaSettings.LogicalCluster},identityPoolId={kafkaSettings.IdentityPoolId}",
            EnableIdempotence = true,
            Acks = Acks.All,
            CompressionType = CompressionType.Snappy,
            LingerMs = 20,
            BatchSize = 32768,
            MessageMaxBytes = 1000000,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100,
            SocketKeepaliveEnable = true,
            MetadataMaxAgeMs = 180000,
            ConnectionsMaxIdleMs = 180000
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError($"Kafka producer error: {e.Reason}"))
            .SetLogHandler((_, log) => _logger.LogDebug($"Kafka producer log: {log.Message}"))
            .Build();
    }

    /// <summary>
    /// Produces a message to the specified Kafka topic with a key and custom headers.
    /// This method uses the Confluent.Kafka library to send messages asynchronously.
    /// It includes error handling and logging for successful and failed deliveries.
    /// The message is serialized to JSON and includes headers for correlation ID, timestamp, and producer information.
    /// The method is designed to be used in a consumer application that needs to produce messages
    /// to Kafka topics, such as for event publishing or data processing.
    /// </summary>
    /// <param name="topic"></param>
    /// <param name="key"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<DeliveryResult<string, string>> ProduceAsync(
        string topic,
        string key,
        string message,
        CancellationToken cancellationToken = default)
    {
        var defaultHeaders = new Headers
        {
            { "correlation-id", System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) },
            { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()) },
            { "producer", "consumer-service"u8.ToArray() }
        };

        return await ProduceAsync(topic, key, message, defaultHeaders, cancellationToken);
    }

    /// <summary>
    /// Produces a message to the specified Kafka topic with a key and custom headers.
    /// </summary>
    /// <param name="topic">The topic to produce to</param>
    /// <param name="key">The message key</param>
    /// <param name="message">The message content</param>
    /// <param name="headers">The message headers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The delivery result</returns>
    public async Task<DeliveryResult<string, string>> ProduceAsync(
        string topic,
        string key,
        string message,
        Headers headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Add default headers if they don't exist
            if (!headers.Any(h => h.Key == "correlation-id"))
            {
                headers.Add("correlation-id", System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
            }
            if (!headers.Any(h => h.Key == "timestamp"))
            {
                headers.Add("timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
            }
            if (!headers.Any(h => h.Key == "producer"))
            {
                headers.Add("producer", "consumer-service"u8.ToArray());
            }

            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = message,
                Headers = headers
            };

            var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            _logger.LogInformation($"Message delivered to {result.TopicPartitionOffset}");
            return result;
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, $"Failed to deliver message to topic {topic}: {ex.Error.Reason}");
            throw;
        }
    }

    /// <summary>
    /// Implement IDisposable to clean up resources.
    /// This method ensures that the Kafka producer is properly disposed of when the service is no longer needed.
    /// It flushes any pending messages and releases the producer resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}