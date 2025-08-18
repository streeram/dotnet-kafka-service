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

    public async Task<DeliveryResult<string, string>> ProduceAsync(
        string topic,
        string key,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = message,
                Headers = new Headers
                {
                    { "correlation-id", System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) },
                    { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()) },
                    { "producer", System.Text.Encoding.UTF8.GetBytes("consumer-service") }
                }
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _producer?.Flush(TimeSpan.FromSeconds(10));
            _producer?.Dispose();
            _disposed = true;
        }
    }
}