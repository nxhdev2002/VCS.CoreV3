using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Data;

public sealed class EfOutboxStore(AppDbContext dbContext) : IOutboxStore
{
    public async Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var entity = new OutboxMessageEntity
        {
            Id = message.Id,
            EventType = message.EventType,
            CorrelationId = message.CorrelationId,
            SchemaVersion = message.SchemaVersion,
            Payload = message.Payload,
            OccurredAtUtc = message.OccurredAtUtc,
            CreatedAtUtc = message.CreatedAtUtc,
            RetryCount = 0
        };

        await dbContext.OutboxMessages.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}