using Confluent.Kafka;

namespace KafkaConsumer.Service.Services;

/// <summary>
/// Interface for the retry consumer service that handles time-based message processing.
/// </summary>
public interface IRetryConsumerService
{
    /// <summary>
    /// Starts the retry consumer that processes messages from retry topics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the retry consumer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks if a message is ready to be processed based on its timestamp.
    /// </summary>
    /// <param name="consumeResult">The consumed message</param>
    /// <returns>True if the message is ready to be processed</returns>
    bool IsMessageReadyForProcessing(ConsumeResult<string, string> consumeResult);
}