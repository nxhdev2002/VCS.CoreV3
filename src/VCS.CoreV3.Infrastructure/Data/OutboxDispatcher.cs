using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VCS.CoreV3.Infrastructure.Data.Entities;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Data;

public sealed class OutboxDispatcher(
    AppDbContext dbContext,
    IOutboxTransportPublisher transportPublisher,
    IOutboxMessageSerializer serializer,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger) : IOutboxDispatcher
{
    private readonly OutboxOptions _options = options.Value;

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var pendingMessages = dbContext.Database.IsRelational()
            ? await ClaimPendingMessagesRelationalAsync(cancellationToken)
            : await ClaimPendingMessagesInMemoryAsync(cancellationToken);

        logger.LogDebug("Claimed {ClaimedCount} outbox messages for dispatch.", pendingMessages.Count);

        var dispatchedCount = 0;

        foreach (var message in pendingMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await PublishAsync(message, cancellationToken);
                message.ProcessedAtUtc = DateTime.UtcNow;
                message.LastError = null;
                message.LockedAtUtc = null;
                message.LockToken = null;
                dispatchedCount++;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.LastError = ex.Message;
                message.LockedAtUtc = null;
                message.LockToken = null;

                logger.LogWarning(
                    ex,
                    "Failed to dispatch outbox message {MessageId} eventType={EventType} retry={RetryCount}",
                    message.Id,
                    message.EventType,
                    message.RetryCount);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return dispatchedCount;
    }

    private Task<List<OutboxMessageEntity>> ClaimPendingMessagesInMemoryAsync(CancellationToken cancellationToken)
    {
        var lockExpiryUtc = DateTime.UtcNow.AddSeconds(-Math.Max(1, _options.LockTimeoutSeconds));

        // In-memory provider is only used by tests and does not support raw SQL claim queries.
        return dbContext.OutboxMessages
            .Where(x =>
                x.ProcessedAtUtc == null &&
                x.RetryCount < _options.MaxRetries &&
                (x.LockedAtUtc == null || x.LockedAtUtc < lockExpiryUtc))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private Task<List<OutboxMessageEntity>> ClaimPendingMessagesRelationalAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var lockExpiryUtc = nowUtc.AddSeconds(-Math.Max(1, _options.LockTimeoutSeconds));
        var lockToken = Guid.NewGuid().ToString("N");

        return dbContext.OutboxMessages
            .FromSqlInterpolated($@"
                WITH candidates AS (
                    SELECT ""Id""
                    FROM ""OutboxEvents""
                    WHERE ""ProcessedAtUtc"" IS NULL
                      AND ""RetryCount"" < {_options.MaxRetries}
                      AND (""LockedAtUtc"" IS NULL OR ""LockedAtUtc"" < {lockExpiryUtc})
                    ORDER BY ""CreatedAtUtc""
                    LIMIT {_options.BatchSize}
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE ""OutboxEvents"" AS o
                SET ""LockedAtUtc"" = {nowUtc},
                    ""LockToken"" = {lockToken}
                FROM candidates
                WHERE o.""Id"" = candidates.""Id""
                RETURNING o.*")
            .ToListAsync(cancellationToken);
    }

    private Task PublishAsync(OutboxMessageEntity message, CancellationToken cancellationToken)
    {
        return message.EventType switch
        {
            EventTypes.WeatherForecastRequested => PublishTypedAsync<WeatherForecastRequestedEvent>(message.Payload, cancellationToken),
            EventTypes.WeatherForecastGenerated => PublishTypedAsync<WeatherForecastGeneratedEvent>(message.Payload, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported outbox event type '{message.EventType}'.")
        };
    }

    private Task PublishTypedAsync<TPayload>(string payload, CancellationToken cancellationToken)
    {
        var integrationEvent = serializer.Deserialize<TPayload>(payload);
        return transportPublisher.PublishAsync(integrationEvent, cancellationToken);
    }
}