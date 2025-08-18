using Kafka.Common.Models;
using KafkaProducer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KafkaProducer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IKafkaProducerService _kafkaProducer;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IKafkaProducerService kafkaProducer,
        ILogger<EventsController> logger)
    {
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    [HttpPost("publish")]
    public async Task<IActionResult> PublishEvent([FromBody] EventMessage message)
    {
        try
        {
            var result = await _kafkaProducer.ProduceAsync(
                "events-topic",
                message.Id,
                message);

            return Ok(new
            {
                success = true,
                partition = result.Partition.Value,
                offset = result.Offset.Value,
                schemaValidated = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event");
            return StatusCode(500, new { error = "Failed to publish event" });
        }
    }

    [HttpPost("publish-with-validation")]
    public async Task<IActionResult> PublishEventWithValidation(
        [FromBody] EventMessage message,
        [FromQuery] string? schemaSubject = null)
    {
        try
        {
            var result = await _kafkaProducer.ProduceWithSchemaValidationAsync(
                "events-topic",
                message.Id,
                message,
                schemaSubject);

            return Ok(new
            {
                success = true,
                partition = result.Partition.Value,
                offset = result.Offset.Value,
                schemaValidated = true,
                schemaSubject = schemaSubject ?? "events-topic-value"
            });
        }
        catch (SchemaValidationException ex)
        {
            _logger.LogWarning(ex, "Schema validation failed for subject: {Subject}", ex.Subject);
            return BadRequest(new
            {
                error = "Schema validation failed",
                subject = ex.Subject,
                details = ex.Message
            });
        }
        catch (CustomSchemaRegistryException ex)
        {
            _logger.LogError(ex, "Schema registry error occurred");
            return StatusCode(503, new { error = "Schema registry unavailable", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event with schema validation");
            return StatusCode(500, new { error = "Failed to publish event" });
        }
    }
}