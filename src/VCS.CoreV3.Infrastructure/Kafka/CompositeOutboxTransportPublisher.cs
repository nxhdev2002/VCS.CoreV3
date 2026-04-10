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
        await _redis.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
        await _kafka.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
    }
}
