namespace VCS.CoreV3.Infrastructure.Kafka;

public interface IIntegrationEventDispatcher
{
    Task DispatchAsync(
        string eventType,
        string payload,
        string messageId,
        string correlationId,
        int schemaVersion,
        DateTime occurredAtUtc,
        int retryCount,
        CancellationToken cancellationToken = default);
}
