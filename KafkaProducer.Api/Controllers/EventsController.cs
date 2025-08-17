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
                offset = result.Offset.Value
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event");
            return StatusCode(500, new { error = "Failed to publish event" });
        }
    }
}
