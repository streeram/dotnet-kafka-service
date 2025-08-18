using Confluent.SchemaRegistry;

namespace KafkaProducer.Api.Services
{
    /// <summary>
    /// Interface for schema validation operations.
    /// </summary>
    public interface ISchemaValidationService
    {
        /// <summary>
        /// Validates a message against the schema for the specified subject.
        /// </summary>
        /// <typeparam name="T">The type of the message to validate.</typeparam>
        /// <param name="subject">The schema subject name.</param>
        /// <param name="message">The message to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if validation passes, false otherwise.</returns>
        Task<bool> ValidateMessageAsync<T>(string subject, T message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the latest schema for the specified subject.
        /// </summary>
        /// <param name="subject">The schema subject name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The schema information.</returns>
        Task<Schema> GetLatestSchemaAsync(string subject, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a JSON message against the schema for the specified subject.
        /// </summary>
        /// <param name="subject">The schema subject name.</param>
        /// <param name="jsonMessage">The JSON message to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if validation passes, false otherwise.</returns>
        Task<bool> ValidateJsonMessageAsync(string subject, string jsonMessage, CancellationToken cancellationToken = default);
    }
}