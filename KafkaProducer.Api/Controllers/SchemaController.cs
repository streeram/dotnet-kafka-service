using Kafka.Common.Models;
using KafkaProducer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KafkaProducer.Api.Controllers
{
    /// <summary>
    /// Controller for managing schema validation operations.
    /// Provides endpoints to retrieve the latest schema for a subject and validate messages against schemas.
    /// Uses ISchemaValidationService to interact with the schema registry.
    /// Logs errors and validation results using ILogger.
    /// Handles exceptions and returns appropriate HTTP status codes and messages.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SchemaController : ControllerBase
    {
        private readonly ISchemaValidationService _schemaValidationService;
        private readonly ILogger<SchemaController> _logger;

        /// <summary>
        /// SchemaController constructor.
        /// Initializes the controller with the schema validation service and logger.
        /// The service is used to retrieve schemas and validate messages against them.
        /// The logger is used to log errors and validation results.
        /// </summary>
        /// <param name="schemaValidationService"></param>
        /// <param name="logger"></param>
        public SchemaController(
            ISchemaValidationService schemaValidationService,
            ILogger<SchemaController> logger)
        {
            _schemaValidationService = schemaValidationService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the latest schema for a given subject.
        /// This endpoint retrieves the latest schema from the schema registry for the specified subject.
        /// If the schema is found, it returns the schema type and schema string.
        /// If the schema is not found, it returns a 404 Not Found status with an error message.
        /// Handles exceptions related to schema retrieval and logs errors using ILogger.
        /// Returns a 500 Internal Server Error status for unexpected errors.
        /// </summary>
        /// <param name="subject"></param>
        /// <returns></returns>
        [HttpGet("{subject}/latest")]
        public async Task<IActionResult> GetLatestSchema(string subject)
        {
            try
            {
                var schema = await _schemaValidationService.GetLatestSchemaAsync(subject);
                return Ok(new
                {
                    schemaType = schema.SchemaType,
                    schema = schema.SchemaString
                });
            }
            catch (CustomSchemaRegistryException ex)
            {
                _logger.LogError(ex, "Failed to retrieve schema for subject: {Subject}", subject);
                return NotFound(new { error = ex.Message, subject });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving schema for subject: {Subject}", subject);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Validates a message against the schema for a given subject.
        /// This endpoint checks if the provided message conforms to the schema registered for the specified subject.
        /// If validation passes, it returns a success response with a valid flag.
        /// If validation fails, it returns a 400 Bad Request status with an error message.
        /// Handles exceptions related to schema validation and logs warnings for validation failures.
        /// Returns a 503 Service Unavailable status for schema registry errors and a 500 Internal Server Error status for unexpected errors.   
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [HttpPost("{subject}/validate")]
        public async Task<IActionResult> ValidateMessage(string subject, [FromBody] object message)
        {
            try
            {
                var isValid = await _schemaValidationService.ValidateMessageAsync(subject, message);
                return Ok(new
                {
                    valid = isValid,
                    subject,
                    message = "Schema validation passed"
                });
            }
            catch (SchemaValidationException ex)
            {
                _logger.LogWarning(ex, "Schema validation failed for subject: {Subject}", subject);
                return BadRequest(new
                {
                    valid = false,
                    subject,
                    error = ex.Message
                });
            }
            catch (CustomSchemaRegistryException ex)
            {
                _logger.LogError(ex, "Schema registry error for subject: {Subject}", subject);
                return StatusCode(503, new { error = ex.Message, subject });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error validating message for subject: {Subject}", subject);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}