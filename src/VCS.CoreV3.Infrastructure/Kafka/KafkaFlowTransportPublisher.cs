using KafkaFlow.Producers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Kafka;

public sealed class KafkaFlowTransportPublisher : IOutboxTransportPublisher
{
    private readonly IProducerAccessor _producerAccessor;
    private readonly IIntegrationEventSerializer _serializer;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaFlowTransportPublisher> _logger;

    public KafkaFlowTransportPublisher(
        IProducerAccessor producerAccessor,
        IIntegrationEventSerializer serializer,
        IOptions<KafkaOptions> options,
        ILogger<KafkaFlowTransportPublisher> logger)
    {
        _producerAccessor = producerAccessor;
        _serializer = serializer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var messageId = Guid.NewGuid().ToString("N");
        var occurredAtUtc = integrationEvent.OccurredAtUtc ?? DateTime.UtcNow;

        var message = new KafkaIntegrationEventMessage
        {
            MessageId = messageId,
            EventType = integrationEvent.EventType,
            CorrelationId = integrationEvent.CorrelationId,
            SchemaVersion = integrationEvent.SchemaVersion,
            Payload = _serializer.Serialize(integrationEvent.Payload),
            OccurredAtUtc = occurredAtUtc,
            RetryCount = 0
        };

        var producer = _producerAccessor.GetProducer(_options.ProducerName);
        await producer.ProduceAsync(integrationEvent.CorrelationId, message).ConfigureAwait(false);

        _logger.LogInformation(
            "Published {EventType} messageId={MessageId} correlationId={CorrelationId} topic={Topic}",
            integrationEvent.EventType,
            messageId,
            integrationEvent.CorrelationId,
            _options.Topic);
    }
}
