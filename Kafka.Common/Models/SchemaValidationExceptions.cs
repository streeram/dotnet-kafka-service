namespace Kafka.Common.Models
{
    /// <summary>
    /// Exception thrown when schema validation fails.
    /// </summary>
    public class SchemaValidationException : Exception
    {
        /// <summary>
        /// Gets the schema subject that failed validation.
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// Gets the schema ID if available.
        /// </summary>
        public int? SchemaId { get; }

        /// <summary>
        /// Initializes a new instance of the SchemaValidationException class.
        /// </summary>
        /// <param name="subject">The schema subject.</param>
        /// <param name="message">The error message.</param>
        public SchemaValidationException(string subject, string message)
            : base(message)
        {
            Subject = subject;
        }

        /// <summary>
        /// Initializes a new instance of the SchemaValidationException class.
        /// </summary>
        /// <param name="subject">The schema subject.</param>
        /// <param name="schemaId">The schema ID.</param>
        /// <param name="message">The error message.</param>
        public SchemaValidationException(string subject, int schemaId, string message)
            : base(message)
        {
            Subject = subject;
            SchemaId = schemaId;
        }

        /// <summary>
        /// Initializes a new instance of the SchemaValidationException class.
        /// </summary>
        /// <param name="subject">The schema subject.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public SchemaValidationException(string subject, string message, Exception innerException)
            : base(message, innerException)
        {
            Subject = subject;
        }

        /// <summary>
        /// Initializes a new instance of the SchemaValidationException class.
        /// </summary>
        /// <param name="subject">The schema subject.</param>
        /// <param name="schemaId">The schema ID.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public SchemaValidationException(string subject, int schemaId, string message, Exception innerException)
            : base(message, innerException)
        {
            Subject = subject;
            SchemaId = schemaId;
        }
    }

    /// <summary>
    /// Exception thrown when custom schema registry operations fail.
    /// </summary>
    public class CustomSchemaRegistryException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the CustomSchemaRegistryException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public CustomSchemaRegistryException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the CustomSchemaRegistryException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public CustomSchemaRegistryException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}