using Confluent.SchemaRegistry;
using Kafka.Common.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using NJsonSchema;

namespace KafkaProducer.Api.Services
{
    /// <summary>
    /// Service for validating messages against schemas in Confluent Schema Registry.
    /// </summary>
    public class SchemaValidationService : ISchemaValidationService, IDisposable
    {
        private readonly ISchemaRegistryClient _schemaRegistryClient;
        private readonly KafkaSettings _settings;
        private readonly ILogger<SchemaValidationService> _logger;
        private readonly Dictionary<string, JsonSchema> _schemaCache;
        private readonly SemaphoreSlim _cacheSemaphore;

        /// <summary>
        /// Initializes the schema registry client and cache.
        /// Uses the provided KafkaSettings and ILogger for configuration and logging.
        /// The schema registry URL and authentication details are read from the settings.
        /// The service caches schemas to improve performance and reduce registry calls.
        /// The cache is protected by a semaphore to ensure thread safety.
        /// If schema validation is disabled, it skips validation and logs a debug message.
        /// Logs detailed information about schema retrieval and validation processes.
        /// Throws SchemaValidationException if validation fails.
        /// Throws CustomSchemaRegistryException if schema retrieval fails.
        /// Disposes the schema registry client and cache semaphore when the service is no longer needed.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="logger"></param>
        public SchemaValidationService(
            IOptions<KafkaSettings> settings,
            ILogger<SchemaValidationService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _schemaCache = new Dictionary<string, JsonSchema>();
            _cacheSemaphore = new SemaphoreSlim(1, 1);

            var config = new SchemaRegistryConfig
            {
                Url = _settings.SchemaRegistryUrl,
                BasicAuthUserInfo = $"{_settings.SchemaRegistryApiKey}:{_settings.SchemaRegistryApiSecret}"
            };

            _schemaRegistryClient = new CachedSchemaRegistryClient(config);
        }

        /// <summary>
        /// Validates a message against the schema for the specified subject.
        /// This method serializes the message to JSON and validates it against the schema.
        /// If schema validation is disabled, it returns true without validation.
        /// Throws SchemaValidationException if validation fails.
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="message"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="SchemaValidationException"></exception>
        public async Task<bool> ValidateMessageAsync<T>(string subject, T message, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_settings.EnableSchemaValidation)
                {
                    _logger.LogDebug("Schema validation is disabled, skipping validation for subject: {Subject}", subject);
                    return true;
                }

                var jsonMessage = JsonSerializer.Serialize(message);
                return await ValidateJsonMessageAsync(subject, jsonMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating message for subject: {Subject}", subject);
                throw new SchemaValidationException(subject, $"Failed to validate message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates a JSON message against the schema for the specified subject.
        /// This method retrieves the schema from the registry, validates the JSON message,
        /// and returns true if validation passes. If schema validation is disabled, it returns true without validation.
        /// Throws SchemaValidationException if validation fails.
        /// Logs detailed information about the validation process.
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="jsonMessage"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="SchemaValidationException"></exception>
        public async Task<bool> ValidateJsonMessageAsync(string subject, string jsonMessage, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_settings.EnableSchemaValidation)
                {
                    _logger.LogDebug("Schema validation is disabled, skipping validation for subject: {Subject}", subject);
                    return true;
                }

                var jsonSchema = await GetJsonSchemaAsync(subject, cancellationToken);
                var validationErrors = jsonSchema.Validate(jsonMessage);

                if (validationErrors.Any())
                {
                    var errorMessages = string.Join("; ", validationErrors.Select(e => $"{e.Path}: {e.Kind}"));
                    _logger.LogWarning("Schema validation failed for subject {Subject}. Errors: {Errors}", subject, errorMessages);
                    throw new SchemaValidationException(subject, $"Schema validation failed: {errorMessages}");
                }

                _logger.LogDebug("Schema validation passed for subject: {Subject}", subject);
                return true;
            }
            catch (SchemaValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating JSON message for subject: {Subject}", subject);
                throw new SchemaValidationException(subject, $"Failed to validate JSON message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the latest schema for the specified subject.
        /// This method retrieves the schema from the registry and caches it for future use.
        /// If schema retrieval fails, it throws a CustomSchemaRegistryException.
        /// Logs detailed information about the schema retrieval process.
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="CustomSchemaRegistryException"></exception>
        public async Task<Schema> GetLatestSchemaAsync(string subject, CancellationToken cancellationToken = default)
        {
            try
            {
                var schema = await _schemaRegistryClient.GetLatestSchemaAsync(subject);
                _logger.LogDebug("Retrieved latest schema for subject: {Subject}, ID: {SchemaId}", subject, schema.Id);
                return schema;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest schema for subject: {Subject}", subject);
                throw new CustomSchemaRegistryException($"Failed to retrieve schema for subject '{subject}': {ex.Message}", ex);
            }
        }

        private async Task<JsonSchema> GetJsonSchemaAsync(string subject, CancellationToken cancellationToken = default)
        {
            await _cacheSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_schemaCache.TryGetValue(subject, out var cachedSchema))
                {
                    return cachedSchema;
                }

                var schema = await GetLatestSchemaAsync(subject, cancellationToken);
                var jsonSchema = await JsonSchema.FromJsonAsync(schema.SchemaString, cancellationToken);

                _schemaCache[subject] = jsonSchema;
                _logger.LogDebug("Cached JSON schema for subject: {Subject}", subject);

                return jsonSchema;
            }
            finally
            {
                _cacheSemaphore.Release();
            }
        }

        /// <summary>
        /// Disposes the schema registry client and cache semaphore.
        /// This method should be called when the service is no longer needed to release resources.
        /// </summary>
        public void Dispose()
        {
            _schemaRegistryClient.Dispose();
            _cacheSemaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}