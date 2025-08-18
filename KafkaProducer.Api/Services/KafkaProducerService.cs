using Confluent.Kafka;
using Kafka.Common.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace KafkaProducer.Api.Services;

/// <summary>
/// Service for producing messages to Kafka topics.
/// This service handles message serialization, schema validation, and error handling during message production.
/// It uses Confluent.Kafka library to interact with Kafka and supports OAuth Bearer authentication.
/// The service is designed to be used in a dependency injection context and implements IDisposable to clean up resources.
/// It provides methods to produce messages with or without schema validation, allowing flexibility in message handling.
/// The service logs all operations, including successful message deliveries and errors encountered during production.
/// It supports cancellation tokens for asynchronous operations, allowing graceful shutdowns and cancellations.
/// The Kafka producer is configured with performance optimizations such as idempotence, compression, and retry policies.
/// The service can be extended to include additional features such as custom headers, message transformations,
/// and integration with other Kafka components like consumers or schema registries.
/// The KafkaProducerService class implements the IKafkaProducerService interface, providing a clear contract for
/// producing messages to Kafka topics.
/// It is designed to be thread-safe and can handle concurrent message production requests.
/// The service can be configured via options, allowing customization of Kafka settings such as bootstrap servers,
/// security protocols, and schema validation settings.
/// The ProduceAsync method allows sending messages to a specified topic with a key and message body,
/// while the ProduceWithSchemaValidationAsync method adds schema validation capabilities.
/// </summary>
public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaProducerService> _logger;
    private readonly ISchemaValidationService _schemaValidationService;

    /// <summary>
    /// KafkaProducerService constructor.
    /// Initializes the Kafka producer with the provided settings and logger.
    /// Configures the producer with security settings, idempotence, and other performance optimizations.
    /// Sets up error and log handlers to capture Kafka events.
    /// The producer is configured to use OAuth Bearer authentication with the specified client ID, secret,
    /// token endpoint URL, scope, logical cluster, and identity pool ID.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="logger"></param>
    /// <param name="schemaValidationService"></param>
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

    /// <summary>
    /// Produces a message to the specified Kafka topic.
    /// This method serializes the message to JSON, sets headers for correlation ID and timestamp,
    /// and sends it to the Kafka topic using the provided key.
    /// It handles errors during message production and logs the result.
    /// The message is sent asynchronously, and the method returns a DeliveryResult containing the topic partition and offset of the produced message.
    /// If an error occurs during production, it throws a ProduceException with details about the failure.
    /// The method supports cancellation via a CancellationToken.
    /// </summary>
    /// <param name="topic"></param>
    /// <param name="key"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
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

    /// <summary>
    /// Produces a message to the specified Kafka topic with schema validation.
    /// This method serializes the message to JSON, validates it against the schema for the specified subject,
    /// and sends it to the Kafka topic using the provided key.
    /// It sets headers for correlation ID, timestamp, and schema subject.
    /// If schema validation is enabled, it validates the message before sending.
    /// If validation fails, it throws a SchemaValidationException.
    /// The method handles errors during message production and logs the result.
    /// The message is sent asynchronously, and the method returns a DeliveryResult containing the topic partition and offset of the produced message.
    /// If an error occurs during production, it throws a ProduceException with details about the failure.
    /// The method supports cancellation via a CancellationToken.
    /// </summary>
    /// <param name="topic"></param>
    /// <param name="key"></param>
    /// <param name="message"></param>
    /// <param name="schemaSubject"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
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

    /// <summary>
    /// Disposes the Kafka producer service.
    /// This method flushes any pending messages to Kafka and disposes of the producer instance.
    /// It ensures that all messages are sent before the service is disposed.    
    /// </summary>
    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
        GC.SuppressFinalize(this);
    }
}