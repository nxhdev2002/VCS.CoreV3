using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VCS.CoreV3.Domain.Abstractions;
using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Data;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : DbContext(options)
{
    public DbSet<WeatherForecastEntity> WeatherForecasts => Set<WeatherForecastEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var userId = currentUser.IsAuthenticated ? currentUser.UserId : (Guid?)null;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is ICreation creation)
            {
                creation.CreationTime = now;
                creation.CreatorId = userId;
            }

            if (entry.State == EntityState.Modified && entry.Entity is IModification modification)
            {
                modification.LastModificationTime = now;
                modification.LastModifierId = userId;
            }

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletion softDelete)
            {
                entry.State = EntityState.Modified;
                softDelete.IsDeleted = true;
                softDelete.DeletionTime = now;
                softDelete.DeleterId = userId;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeatherForecastEntity>(entity =>
        {
            entity.ToTable("weather_forecasts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Summary).HasMaxLength(120);
            entity.HasIndex(x => x.Date);
        });

        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.ToTable("outbox_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(200);
            entity.Property(x => x.CorrelationId).HasMaxLength(120);
            entity.Property(x => x.LockToken).HasMaxLength(64);
            entity.Property(x => x.Payload).HasColumnType("jsonb");
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => x.ProcessedAtUtc);
            entity.HasIndex(x => new { x.ProcessedAtUtc, x.LockedAtUtc, x.CreatedAtUtc });
        });

        modelBuilder.Entity<ApiKeyEntity>(entity =>
        {
            entity.ToTable("api_keys");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KeyHash).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Plan).HasMaxLength(100).HasDefaultValue("free");
            entity.Property(x => x.RateLimit).HasDefaultValue(1000);
            entity.Property(x => x.IsRevoked).HasDefaultValue(false);
            entity.Property(x => x.CreationTime).IsRequired();
            entity.HasIndex(x => x.KeyHash).IsUnique();
            entity.HasIndex(x => new { x.KeyHash, x.ExpiredAt, x.IsRevoked });
        });

        // Apply global query filter for any entity implementing ISoftDeletion
        foreach (var clrType in modelBuilder.Model.GetEntityTypes().Select(e => e.ClrType))
        {
            if (!typeof(ISoftDeletion).IsAssignableFrom(clrType))
                continue;

            var param = Expression.Parameter(clrType, "e");
            var prop = Expression.Property(param, nameof(ISoftDeletion.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(prop), param);
            modelBuilder.Entity(clrType).HasQueryFilter(filter);
        }
    }
}