namespace KafkaConsumer.Service.Services;

public interface IMessageProcessor
{
    Task ProcessAsync(string message, CancellationToken cancellationToken);
}
