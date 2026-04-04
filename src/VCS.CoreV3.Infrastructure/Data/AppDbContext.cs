using Microsoft.EntityFrameworkCore;
using VCS.CoreV3.Domain.Entities;

namespace VCS.CoreV3.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WeatherForecastEntity> WeatherForecasts => Set<WeatherForecastEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();

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
            entity.ToTable("OutboxEvents");
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
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasIndex(x => x.KeyHash);
            entity.HasIndex(x => new { x.KeyHash, x.ExpiredAt, x.IsRevoked });
        });
    }
}