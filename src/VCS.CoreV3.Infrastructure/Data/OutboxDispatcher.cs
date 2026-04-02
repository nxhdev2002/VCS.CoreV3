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
        var pendingMessages = await dbContext.OutboxMessages
            .Where(x => x.ProcessedAtUtc == null && x.RetryCount < _options.MaxRetries)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        var dispatchedCount = 0;

        foreach (var message in pendingMessages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await PublishAsync(message, cancellationToken);
                message.ProcessedAtUtc = DateTime.UtcNow;
                message.LastError = null;
                dispatchedCount++;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.LastError = ex.Message;

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