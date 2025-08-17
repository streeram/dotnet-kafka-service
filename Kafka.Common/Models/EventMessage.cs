namespace Kafka.Common.Models;

public record EventMessage(
    string Id, 
    string Type, 
    object Data, 
    DateTime Timestamp);
