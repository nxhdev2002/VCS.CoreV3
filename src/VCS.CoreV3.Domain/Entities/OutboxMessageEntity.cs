namespace VCS.CoreV3.Domain.Entities;

public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public string? LockToken { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}