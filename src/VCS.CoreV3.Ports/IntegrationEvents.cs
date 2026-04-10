namespace VCS.CoreV3.Ports;

/// <summary>Marker interface: event payload is routed through Kafka.</summary>
public interface IKafkaEvent { }

/// <summary>Marker interface: event payload is routed through Redis Streams.</summary>
public interface IRedisEvent { }

public sealed record IntegrationEvent<TPayload>(
    string EventType,
    TPayload Payload,
    string CorrelationId,
    int SchemaVersion = 1,
    DateTime? OccurredAtUtc = null);

public sealed record IntegrationEventEnvelope<TPayload>(
    string MessageId,
    string EventType,
    DateTime OccurredAtUtc,
    string CorrelationId,
    int SchemaVersion,
    TPayload Payload,
    int RetryCount);

public interface IIntegrationEventPublisher
{
    Task PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken cancellationToken = default);
}

public interface IIntegrationEventSerializer
{
    string Serialize<TPayload>(TPayload payload);
    TPayload Deserialize<TPayload>(string payload);
}

public interface IIntegrationEventHandler<TPayload>
{
    string EventType { get; }
    Task HandleAsync(IntegrationEventEnvelope<TPayload> envelope, CancellationToken cancellationToken = default);
}

public interface ICorrelationContextAccessor
{
    string? CorrelationId { get; set; }
}