namespace Kafka.Common.Models;

public class KafkaSettings
{
    public string BootstrapServers { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string TokenEndpointUrl { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string LogicalCluster { get; init; } = string.Empty;
    public string IdentityPoolId { get; init; } = string.Empty;
    public string ConsumerGroupId { get; init; } = string.Empty;
    public List<string> Topics { get; init; } = new();
}
