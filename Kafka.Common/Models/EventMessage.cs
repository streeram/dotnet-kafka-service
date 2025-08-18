namespace Kafka.Common.Models
{
    /// <summary>
    /// Represents an event message for Kafka messaging.
    /// </summary>
    /// <param name="Id">The unique identifier for the event message.</param>
    /// <param name="Type">The type of the event message.</param>
    /// <param name="Data">The data payload of the event message.</param>
    /// <param name="Timestamp">The timestamp when the event message was created.</param>
    public record EventMessage(
        string Id,
        string Type,
        object Data,
        DateTime Timestamp);
}