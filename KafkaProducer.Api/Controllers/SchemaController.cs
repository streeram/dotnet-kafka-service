using Confluent.SchemaRegistry;
using Kafka.Common.Models;
using KafkaProducer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KafkaProducer.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchemaController : ControllerBase
    {
        private readonly ISchemaValidationService _schemaValidationService;
        private readonly ILogger<SchemaController> _logger;

        public SchemaController(
            ISchemaValidationService schemaValidationService,
            ILogger<SchemaController> logger)
        {
            _schemaValidationService = schemaValidationService;
            _logger = logger;
        }

        [HttpGet("{subject}/latest")]
        public async Task<IActionResult> GetLatestSchema(string subject)
        {
            try
            {
                var schema = await _schemaValidationService.GetLatestSchemaAsync(subject);
                return Ok(new
                {
                    id = schema.Id,
                    subject = schema.Subject,
                    version = schema.Version,
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

        [HttpPost("{subject}/validate")]
        public async Task<IActionResult> ValidateMessage(string subject, [FromBody] object message)
        {
            try
            {
                var isValid = await _schemaValidationService.ValidateMessageAsync(subject, message);
                return Ok(new
                {
                    valid = isValid,
                    subject = subject,
                    message = "Schema validation passed"
                });
            }
            catch (SchemaValidationException ex)
            {
                _logger.LogWarning(ex, "Schema validation failed for subject: {Subject}", subject);
                return BadRequest(new
                {
                    valid = false,
                    subject = subject,
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