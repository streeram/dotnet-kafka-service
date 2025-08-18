using Confluent.Kafka;

namespace KafkaProducer.Api.Services;

/// <summary>
/// IKafkaProducerService interface defines methods for producing messages to Kafka topics.
/// It includes methods for producing messages with and without schema validation.
/// </summary>
public interface IKafkaProducerService
{
    /// <summary>
    /// Produces a message to a Kafka topic with the specified key.
    /// </summary>
    /// <param name="topic"></param>
    /// <param name="key"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task<DeliveryResult<string, string>> ProduceAsync<T>(
        string topic,
        string key,
        T message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a message to a Kafka topic with the specified key and performs schema validation.
    /// </summary>
    /// <param name="topic"></param>
    /// <param name="key"></param>
    /// <param name="message"></param>
    /// <param name="schemaSubject"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task<DeliveryResult<string, string>> ProduceWithSchemaValidationAsync<T>(
        string topic,
        string key,
        T message,
        string? schemaSubject = null,
        CancellationToken cancellationToken = default);
}