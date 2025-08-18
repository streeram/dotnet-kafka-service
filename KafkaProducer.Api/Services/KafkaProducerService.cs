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
    private readonly ISchemaValidationService _schemaValidationService;

    public KafkaProducerService(
        IOptions<KafkaSettings> settings,
        ILogger<KafkaProducerService> logger,
        ISchemaValidationService schemaValidationService)
    {
        _settings = settings.Value;
        _logger = logger;
        _schemaValidationService = schemaValidationService;

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

    public async Task<DeliveryResult<string, string>> ProduceWithSchemaValidationAsync<T>(
        string topic,
        string key,
        T message,
        string? schemaSubject = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Determine schema subject - use provided subject or default to topic-value
            var subject = schemaSubject ?? $"{topic}-value";

            _logger.LogDebug("Validating message against schema subject: {Subject}", subject);

            // Validate message against schema if validation is enabled
            if (_settings.EnableSchemaValidation)
            {
                await _schemaValidationService.ValidateMessageAsync(subject, message, cancellationToken);
                _logger.LogDebug("Schema validation passed for subject: {Subject}", subject);
            }

            // Serialize and prepare the message
            var jsonMessage = JsonSerializer.Serialize(message);
            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = jsonMessage,
                Headers = new Headers
                {
                    { "correlation-id", System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) },
                    { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()) },
                    { "schema-subject", System.Text.Encoding.UTF8.GetBytes(subject) }
                }
            };

            var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            _logger.LogInformation("Message with schema validation delivered to {TopicPartitionOffset} using subject: {Subject}",
                result.TopicPartitionOffset, subject);
            return result;
        }
        catch (SchemaValidationException ex)
        {
            _logger.LogError(ex, "Schema validation failed for subject: {Subject}", ex.Subject);
            throw;
        }
        catch (CustomSchemaRegistryException ex)
        {
            _logger.LogError(ex, "Schema registry error occurred");
            throw;
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Failed to deliver message: {ErrorReason}", ex.Error.Reason);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while producing message with schema validation");
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _producer?.Dispose();
    }
}