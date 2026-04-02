using Microsoft.EntityFrameworkCore;
using VCS.CoreV3.Infrastructure.Data.Entities;

namespace VCS.CoreV3.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<WeatherForecastEntity> WeatherForecasts => Set<WeatherForecastEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

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
            entity.Property(x => x.Payload).HasColumnType("jsonb");
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => x.ProcessedAtUtc);
        });
    }
}