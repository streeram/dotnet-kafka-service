using Confluent.Kafka;

namespace KafkaProducer.Api.Services;

public interface IKafkaProducerService
{
    Task<DeliveryResult<string, string>> ProduceAsync<T>(
        string topic,
        string key,
        T message,
        CancellationToken cancellationToken = default);

    Task<DeliveryResult<string, string>> ProduceWithSchemaValidationAsync<T>(
        string topic,
        string key,
        T message,
        string? schemaSubject = null,
        CancellationToken cancellationToken = default);
}