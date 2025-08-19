using Confluent.Kafka;

namespace KafkaConsumer.Service.Services;

/// <summary>
/// Interface for Kafka producer service in the consumer application.
/// </summary>
public interface IConsumerKafkaProducerService
{
    /// <summary>
    /// Produces a message to the specified topic.
    /// </summary>
    /// <param name="topic">The topic to produce to</param>
    /// <param name="key">The message key</param>
    /// <param name="message">The message content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The delivery result</returns>
    Task<DeliveryResult<string, string>> ProduceAsync(
        string topic,
        string key,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a message to the specified topic with headers.
    /// </summary>
    /// <param name="topic">The topic to produce to</param>
    /// <param name="key">The message key</param>
    /// <param name="message">The message content</param>
    /// <param name="headers">The message headers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The delivery result</returns>
    Task<DeliveryResult<string, string>> ProduceAsync(
        string topic,
        string key,
        string message,
        Headers headers,
        CancellationToken cancellationToken = default);
}