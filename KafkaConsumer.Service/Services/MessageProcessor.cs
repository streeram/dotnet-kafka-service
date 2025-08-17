using System.Text.Json;
using Kafka.Common.Models;

namespace KafkaConsumer.Service.Services;

public class MessageProcessor : IMessageProcessor
{
    private readonly ILogger<MessageProcessor> _logger;

    public MessageProcessor(ILogger<MessageProcessor> logger)
    {
        _logger = logger;
    }

    public async Task ProcessAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var eventMessage = JsonSerializer.Deserialize<EventMessage>(message);
            _logger.LogInformation($"Processing event: {eventMessage?.Type} with ID: {eventMessage?.Id}");
            
            // Add your business logic here
            await Task.Delay(100, cancellationToken); // Simulate processing
            
            _logger.LogInformation($"Successfully processed event: {eventMessage?.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            throw;
        }
    }
}
