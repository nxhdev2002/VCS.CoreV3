namespace VCS.CoreV3.Ports;

public sealed record OutboxMessage(
    Guid Id,
    string EventType,
    string CorrelationId,
    int SchemaVersion,
    string Payload,
    DateTime OccurredAtUtc,
    DateTime CreatedAtUtc);

public interface IOutboxStore
{
    Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

public interface IOutboxMessageSerializer
{
    string Serialize<TPayload>(IntegrationEvent<TPayload> integrationEvent);
    IntegrationEvent<TPayload> Deserialize<TPayload>(string payload);
}

public interface IIntegrationUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}

public interface IOutboxDispatcher
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default);
}

public interface IOutboxTransportPublisher
{
    Task PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken cancellationToken = default);
}