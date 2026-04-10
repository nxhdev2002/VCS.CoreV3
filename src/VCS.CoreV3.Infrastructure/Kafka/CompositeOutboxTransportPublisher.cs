using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Kafka;

public sealed class CompositeOutboxTransportPublisher : IOutboxTransportPublisher
{
    private readonly IOutboxTransportPublisher _redis;
    private readonly IOutboxTransportPublisher _kafka;

    public CompositeOutboxTransportPublisher(IOutboxTransportPublisher redis, IOutboxTransportPublisher kafka)
    {
        _redis = redis;
        _kafka = kafka;
    }

    public async Task PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken cancellationToken = default)
    {
        if (typeof(TPayload).IsAssignableTo(typeof(IRedisEvent)))
            await _redis.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);

        if (typeof(TPayload).IsAssignableTo(typeof(IKafkaEvent)))
            await _kafka.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
    }
}
