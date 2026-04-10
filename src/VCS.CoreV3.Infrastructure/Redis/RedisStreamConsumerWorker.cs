using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using VCS.CoreV3.Infrastructure.Kafka;

namespace VCS.CoreV3.Infrastructure.Redis;

public sealed class RedisStreamConsumerWorker : BackgroundService
{
    private const string MessageIdField = "messageId";
    private const string EventTypeField = "eventType";
    private const string OccurredAtUtcField = "occurredAtUtc";
    private const string CorrelationIdField = "correlationId";
    private const string SchemaVersionField = "schemaVersion";
    private const string RetryCountField = "retryCount";
    private const string PayloadField = "payload";

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IIntegrationEventDispatcher _dispatcher;
    private readonly RedisStreamOptions _options;
    private readonly ILogger<RedisStreamConsumerWorker> _logger;
    private readonly string _consumerName;

    public RedisStreamConsumerWorker(
        IConnectionMultiplexer connectionMultiplexer,
        [FromKeyedServices("redis")] IIntegrationEventDispatcher dispatcher,
        IOptions<RedisStreamOptions> options,
        ILogger<RedisStreamConsumerWorker> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
        _consumerName = $"{_options.ConsumerNamePrefix}-{Environment.MachineName}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var database = _connectionMultiplexer.GetDatabase();
        await EnsureConsumerGroupAsync(database).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pendingEntries = await database.StreamReadGroupAsync(
                    _options.StreamName,
                    _options.ConsumerGroup,
                    _consumerName,
                    "0-0",
                    count: _options.ReadBatchSize).ConfigureAwait(false);

                if (pendingEntries.Length > 0)
                {
                    foreach (var pendingEntry in pendingEntries)
                    {
                        await ProcessEntryAsync(database, pendingEntry, stoppingToken).ConfigureAwait(false);
                    }

                    continue;
                }

                var entries = await database.StreamReadGroupAsync(
                    _options.StreamName,
                    _options.ConsumerGroup,
                    _consumerName,
                    ">",
                    count: _options.ReadBatchSize).ConfigureAwait(false);

                if (entries.Length == 0)
                {
                    await Task.Delay(_options.BlockMilliseconds, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                foreach (var entry in entries)
                {
                    await ProcessEntryAsync(database, entry, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while consuming Redis stream entries.");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessEntryAsync(IDatabase database, StreamEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var eventType = GetFieldValue(entry, EventTypeField);
            var payload = GetFieldValue(entry, PayloadField);
            var messageId = GetFieldValue(entry, MessageIdField);
            var correlationId = GetFieldValue(entry, CorrelationIdField);
            var schemaVersion = ParseInt(GetFieldValue(entry, SchemaVersionField));
            var retryCount = ParseInt(GetFieldValue(entry, RetryCountField));
            var occurredAtUtc = DateTime.Parse(
                GetFieldValue(entry, OccurredAtUtcField),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);

            await _dispatcher.DispatchAsync(
                eventType, payload, messageId, correlationId, schemaVersion, occurredAtUtc, retryCount, cancellationToken)
                .ConfigureAwait(false);

            await database.StreamAcknowledgeAsync(_options.StreamName, _options.ConsumerGroup, entry.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(database, entry, ex, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleFailureAsync(IDatabase database, StreamEntry entry, Exception exception, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var messageId = GetFieldValue(entry, MessageIdField);
        var eventType = GetFieldValue(entry, EventTypeField);
        var correlationId = GetFieldValue(entry, CorrelationIdField);
        var occurredAtUtc = GetFieldValue(entry, OccurredAtUtcField);
        var schemaVersion = ParseInt(GetFieldValue(entry, SchemaVersionField));
        var payload = GetFieldValue(entry, PayloadField);
        var retryCount = ParseInt(GetFieldValue(entry, RetryCountField));
        var nextRetry = retryCount + 1;

        var targetStream = nextRetry > _options.MaxRetries ? _options.DeadLetterStreamName : _options.StreamName;
        var targetFields = new NameValueEntry[]
        {
            new("messageId", messageId),
            new("eventType", eventType),
            new("occurredAtUtc", occurredAtUtc),
            new("correlationId", correlationId),
            new("schemaVersion", schemaVersion),
            new("retryCount", nextRetry),
            new("payload", payload),
            new("failedAtUtc", DateTime.UtcNow.ToString("O")),
            new("error", exception.Message)
        };

        await database.StreamAddAsync(
            targetStream,
            targetFields,
            maxLength: _options.MaxStreamLength,
            useApproximateMaxLength: true).ConfigureAwait(false);

        await database.StreamAcknowledgeAsync(_options.StreamName, _options.ConsumerGroup, entry.Id).ConfigureAwait(false);

        _logger.LogWarning(
            exception,
            "Processing failed for event {EventType} messageId={MessageId} correlationId={CorrelationId} retry={RetryCount} target={TargetStream}",
            eventType,
            messageId,
            correlationId,
            nextRetry,
            targetStream);
    }

    private async Task EnsureConsumerGroupAsync(IDatabase database)
    {
        try
        {
            await database.StreamCreateConsumerGroupAsync(
                _options.StreamName,
                _options.ConsumerGroup,
                "$",
                createStream: true).ConfigureAwait(false);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Redis consumer group {ConsumerGroup} already exists for stream {StreamName}", _options.ConsumerGroup, _options.StreamName);
        }
    }

    private static string GetFieldValue(StreamEntry entry, string fieldName)
    {
        foreach (var field in entry.Values)
        {
            if (string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return field.Value.ToString();
            }
        }

        throw new InvalidOperationException($"Missing stream field '{fieldName}'.");
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }
}