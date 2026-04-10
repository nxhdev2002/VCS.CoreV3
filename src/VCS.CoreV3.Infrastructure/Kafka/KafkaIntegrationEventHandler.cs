using KafkaFlow;
using Microsoft.Extensions.DependencyInjection;

namespace VCS.CoreV3.Infrastructure.Kafka;

public sealed class KafkaIntegrationEventHandler : IMessageHandler<KafkaIntegrationEventMessage>
{
    private readonly IIntegrationEventDispatcher _dispatcher;

    public KafkaIntegrationEventHandler([FromKeyedServices("kafka")] IIntegrationEventDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task Handle(IMessageContext context, KafkaIntegrationEventMessage message)
        => _dispatcher.DispatchAsync(
            message.EventType,
            message.Payload,
            message.MessageId,
            message.CorrelationId,
            message.SchemaVersion,
            message.OccurredAtUtc,
            message.RetryCount);
}
