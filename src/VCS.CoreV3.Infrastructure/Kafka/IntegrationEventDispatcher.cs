using Microsoft.Extensions.DependencyInjection;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Kafka;

public sealed class IntegrationEventDispatcher : IIntegrationEventDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IIntegrationEventSerializer _serializer;
    private readonly Type? _transportFilter;

    public IntegrationEventDispatcher(
        IServiceScopeFactory scopeFactory,
        IIntegrationEventSerializer serializer,
        Type? transportFilter = null)
    {
        _scopeFactory = scopeFactory;
        _serializer = serializer;
        _transportFilter = transportFilter;
    }

    public Task DispatchAsync(
        string eventType,
        string payload,
        string messageId,
        string correlationId,
        int schemaVersion,
        DateTime occurredAtUtc,
        int retryCount,
        CancellationToken cancellationToken = default)
    {
        return eventType switch
        {
            EventTypes.WeatherForecastRequested => DispatchTypedAsync<WeatherForecastRequestedEvent>(
                payload, messageId, eventType, correlationId, schemaVersion, occurredAtUtc, retryCount, cancellationToken),
            EventTypes.WeatherForecastGenerated => DispatchTypedAsync<WeatherForecastGeneratedEvent>(
                payload, messageId, eventType, correlationId, schemaVersion, occurredAtUtc, retryCount, cancellationToken),
            EventTypes.ApiKeyCreated => DispatchTypedAsync<ApiKeyCreatedEvent>(
                payload, messageId, eventType, correlationId, schemaVersion, occurredAtUtc, retryCount, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    private async Task DispatchTypedAsync<TPayload>(
        string payload,
        string messageId,
        string eventType,
        string correlationId,
        int schemaVersion,
        DateTime occurredAtUtc,
        int retryCount,
        CancellationToken cancellationToken)
    {
        if (_transportFilter is not null && !typeof(TPayload).IsAssignableTo(_transportFilter))
            return;

        var typedPayload = _serializer.Deserialize<TPayload>(payload);
        var envelope = new IntegrationEventEnvelope<TPayload>(
            messageId, eventType, occurredAtUtc, correlationId, schemaVersion, typedPayload, retryCount);

        using var scope = _scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler<TPayload>>();

        foreach (var handler in handlers)
        {
            if (!string.Equals(handler.EventType, eventType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await handler.HandleAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
    }
}
