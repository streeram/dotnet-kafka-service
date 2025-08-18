using Kafka.Common.Models;
using KafkaProducer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KafkaProducer.Api.Controllers;

/// <summary>
/// EventsController handles publishing events to Kafka.
/// It provides endpoints to publish events with and without schema validation.
/// Uses IKafkaProducerService to produce messages to Kafka topics.
/// Logs errors and results using ILogger.
/// Handles exceptions and returns appropriate HTTP status codes and messages.
/// The controller is decorated with ApiController and Route attributes to define the API endpoints.
/// The PublishEvent endpoint publishes an event without schema validation.
/// The PublishEventWithValidation endpoint publishes an event with schema validation.
/// It accepts an EventMessage object in the request body and an optional schema subject query parameter.
/// The PublishEventWithValidation endpoint validates the message against the schema before publishing.
/// If schema validation fails, it returns a BadRequest with details.
/// If the schema registry is unavailable, it returns a ServiceUnavailable status.
/// If the event is published successfully, it returns an Ok status with partition and offset information.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IKafkaProducerService _kafkaProducer;
    private readonly ILogger<EventsController> _logger;

    /// <summary>
    /// EventsController constructor.
    /// Initializes the controller with the Kafka producer service and logger.
    /// </summary>
    /// <param name="kafkaProducer"></param>
    /// <param name="logger"></param>
    public EventsController(
        IKafkaProducerService kafkaProducer,
        ILogger<EventsController> logger)
    {
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    /// <summary>
    /// Publishes an event to Kafka without schema validation.
    /// This endpoint accepts an EventMessage object in the request body.
    /// It produces the message to the "events-topic" Kafka topic using the provided key.
    /// If the message is published successfully, it returns an Ok status with partition and offset information.
    /// If an error occurs during publishing, it logs the error and returns a 500 Internal Server Error status with an error message.
    /// This method does not perform any schema validation on the message before publishing it.
    /// It is useful for scenarios where schema validation is not required or has been handled
    /// elsewhere in the application.
    /// It uses the IKafkaProducerService to produce messages to Kafka topics.
    /// The method returns a JSON response with success status
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Publishes an event to Kafka with schema validation.
    /// This endpoint accepts an EventMessage object in the request body and an optional schema subject query parameter.
    /// It validates the message against the schema before publishing it to the "events-topic" Kafka topic.
    /// If schema validation passes, it produces the message and returns an Ok status with partition and offset information.
    /// If schema validation fails, it returns a BadRequest with details about the validation error.
    /// If the schema registry is unavailable, it returns a ServiceUnavailable status.
    /// If an unexpected error occurs, it logs the error and returns a 500 Internal Server Error status with an error message.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="schemaSubject"></param>
    /// <returns></returns>
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