using Confluent.Kafka;
using Kafka.Common.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace KafkaProducer.Api.Services;

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(
        IOptions<KafkaSettings> settings, 
        ILogger<KafkaProducerService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        var config = new ProducerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.OAuthBearer,
            SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc,
            SaslOauthbearerClientId = _settings.ClientId,
            SaslOauthbearerClientSecret = _settings.ClientSecret,
            SaslOauthbearerTokenEndpointUrl = _settings.TokenEndpointUrl,
            SaslOauthbearerScope = _settings.Scope,
            SaslOauthbearerExtensions = $"logicalCluster={_settings.LogicalCluster},identityPoolId={_settings.IdentityPoolId}",
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
            .SetErrorHandler((_, e) => _logger.LogError($"Kafka error: {e.Reason}"))
            .SetLogHandler((_, log) => _logger.LogDebug($"Kafka log: {log.Message}"))
            .Build();
    }

    public async Task<DeliveryResult<string, string>> ProduceAsync<T>(
        string topic, 
        string key, 
        T message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jsonMessage = JsonSerializer.Serialize(message);
            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = jsonMessage,
                Headers = new Headers
                {
                    { "correlation-id", System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) },
                    { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()) }
                }
            };

            var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            _logger.LogInformation($"Message delivered to {result.TopicPartitionOffset}");
            return result;
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, $"Failed to deliver message: {ex.Error.Reason}");
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}
