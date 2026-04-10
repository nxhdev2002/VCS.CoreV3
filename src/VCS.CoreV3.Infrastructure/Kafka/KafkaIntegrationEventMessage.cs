namespace VCS.CoreV3.Infrastructure.Kafka;

public sealed class KafkaIntegrationEventMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public int RetryCount { get; set; }
}
