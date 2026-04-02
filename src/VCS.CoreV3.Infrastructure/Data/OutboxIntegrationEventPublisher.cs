using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Data;

public sealed class OutboxIntegrationEventPublisher(
    IOutboxStore outboxStore,
    IOutboxMessageSerializer serializer) : IIntegrationEventPublisher
{
    public Task PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken cancellationToken = default)
    {
        var occurredAtUtc = integrationEvent.OccurredAtUtc ?? DateTime.UtcNow;
        var payload = serializer.Serialize(integrationEvent);

        var message = new OutboxMessage(
            Guid.NewGuid(),
            integrationEvent.EventType,
            integrationEvent.CorrelationId,
            integrationEvent.SchemaVersion,
            payload,
            occurredAtUtc,
            DateTime.UtcNow);

        return outboxStore.EnqueueAsync(message, cancellationToken);
    }
}