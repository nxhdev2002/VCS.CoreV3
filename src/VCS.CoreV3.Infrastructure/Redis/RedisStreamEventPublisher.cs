using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Redis;

public sealed class RedisStreamEventPublisher : IOutboxTransportPublisher
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IIntegrationEventSerializer _serializer;
    private readonly RedisStreamOptions _options;
    private readonly ILogger<RedisStreamEventPublisher> _logger;

    public RedisStreamEventPublisher(
        IConnectionMultiplexer connectionMultiplexer,
        IIntegrationEventSerializer serializer,
        IOptions<RedisStreamOptions> options,
        ILogger<RedisStreamEventPublisher> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _serializer = serializer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = _connectionMultiplexer.GetDatabase();
        var messageId = Guid.NewGuid().ToString("N");
        var occurredAtUtc = integrationEvent.OccurredAtUtc ?? DateTime.UtcNow;

        var fields = new NameValueEntry[]
        {
            new("messageId", messageId),
            new("eventType", integrationEvent.EventType),
            new("occurredAtUtc", occurredAtUtc.ToString("O")),
            new("correlationId", integrationEvent.CorrelationId),
            new("schemaVersion", integrationEvent.SchemaVersion),
            new("retryCount", 0),
            new("payload", _serializer.Serialize(integrationEvent.Payload))
        };

        await database.StreamAddAsync(
            _options.StreamName,
            fields,
            maxLength: _options.MaxStreamLength,
            useApproximateMaxLength: true).ConfigureAwait(false);

        _logger.LogInformation(
            "Published {EventType} messageId={MessageId} correlationId={CorrelationId} stream={StreamName}",
            integrationEvent.EventType,
            messageId,
            integrationEvent.CorrelationId,
            _options.StreamName);
    }
}