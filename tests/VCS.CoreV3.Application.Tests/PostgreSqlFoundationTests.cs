using System.Text.Json;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VCS.CoreV3.Domain;
using VCS.CoreV3.Infrastructure;
using VCS.CoreV3.Infrastructure.Data;
using VCS.CoreV3.Infrastructure.Redis;
using VCS.CoreV3.Ports;
using VCS.CoreV3.Domain.Entities;

namespace VCS.CoreV3.Application.Tests;

public sealed class PostgreSqlFoundationTests
{
    [Fact]
    public async Task WeatherForecastRepository_AddRangeAndGetRecent_PersistsAndOrdersByDateDescending()
    {
        await using var dbContext = CreateDbContext();
        var repository = new PostgresWeatherForecastRepository(dbContext);
        var forecasts = new[]
        {
            new WeatherForecast(new DateOnly(2026, 4, 1), 15, "Mild"),
            new WeatherForecast(new DateOnly(2026, 4, 3), 22, "Warm"),
            new WeatherForecast(new DateOnly(2026, 4, 2), 18, "Cool")
        };

        await repository.AddRangeAsync(forecasts);

        var recent = await repository.GetRecentAsync(2);

        Assert.Collection(
            recent,
            item =>
            {
                Assert.Equal(new DateOnly(2026, 4, 3), item.Date);
                Assert.Equal(22, item.TemperatureC);
                Assert.Equal("Warm", item.Summary);
            },
            item =>
            {
                Assert.Equal(new DateOnly(2026, 4, 2), item.Date);
                Assert.Equal(18, item.TemperatureC);
                Assert.Equal("Cool", item.Summary);
            });
    }

    [Fact]
    public async Task OutboxStore_EnqueueAsync_PersistsOutboxMessage()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfOutboxStore(dbContext);
        var message = new OutboxMessage(
            Guid.NewGuid(),
            EventTypes.WeatherForecastGenerated,
            "corr-42",
            1,
            "{\"forecastCount\":5}",
            new DateTime(2026, 4, 2, 10, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 2, 10, 31, 0, DateTimeKind.Utc));

        await store.EnqueueAsync(message);

        var saved = await dbContext.OutboxMessages.SingleAsync();
        Assert.Equal(message.Id, saved.Id);
        Assert.Equal(message.EventType, saved.EventType);
        Assert.Equal(message.CorrelationId, saved.CorrelationId);
        Assert.Equal(message.Payload, saved.Payload);
        Assert.Equal(message.OccurredAtUtc, saved.OccurredAtUtc);
        Assert.Equal(message.CreatedAtUtc, saved.CreatedAtUtc);
        Assert.Null(saved.ProcessedAtUtc);
        Assert.Equal(0, saved.RetryCount);
    }

    [Fact]
    public async Task OutboxIntegrationEventPublisher_PublishAsync_EnqueuesOutboxMessage()
    {
        await using var dbContext = CreateDbContext();
        var publisher = new OutboxIntegrationEventPublisher(
            new EfOutboxStore(dbContext),
            new SystemTextJsonOutboxMessageSerializer());
        var integrationEvent = new IntegrationEvent<WeatherForecastGeneratedEvent>(
            EventTypes.WeatherForecastGenerated,
            new WeatherForecastGeneratedEvent(7, 20.1),
            "corr-outbox",
            3,
            new DateTime(2026, 4, 2, 7, 0, 0, DateTimeKind.Utc));

        await publisher.PublishAsync(integrationEvent);

        var saved = await dbContext.OutboxMessages.SingleAsync();
        Assert.Equal(EventTypes.WeatherForecastGenerated, saved.EventType);
        Assert.Equal("corr-outbox", saved.CorrelationId);
        Assert.Equal(3, saved.SchemaVersion);
        Assert.Equal(new DateTime(2026, 4, 2, 7, 0, 0, DateTimeKind.Utc), saved.OccurredAtUtc);
        Assert.Null(saved.ProcessedAtUtc);
        Assert.Equal(0, saved.RetryCount);
    }

    [Fact]
    public void OutboxSerializer_Serialize_WritesIntegrationEventEnvelopeShape()
    {
        var serializer = new SystemTextJsonOutboxMessageSerializer();
        var integrationEvent = new IntegrationEvent<WeatherForecastGeneratedEvent>(
            EventTypes.WeatherForecastGenerated,
            new WeatherForecastGeneratedEvent(5, 21.5),
            "corr-99",
            SchemaVersion: 2,
            OccurredAtUtc: new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc));

        var payload = serializer.Serialize(integrationEvent);
        using var document = JsonDocument.Parse(payload);

        Assert.Equal(EventTypes.WeatherForecastGenerated, document.RootElement.GetProperty("eventType").GetString());
        Assert.Equal("corr-99", document.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(5, document.RootElement.GetProperty("payload").GetProperty("forecastCount").GetInt32());
    }

    [Fact]
    public void AddHexagonalArchitecture_Throws_WhenPostgreSqlConnectionStringIsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "localhost:6379"
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddHexagonalArchitecture(configuration));

        Assert.Equal("Connection string 'PostgreSQL' is required.", exception.Message);
    }

    [Fact]
    public void AddHexagonalArchitecture_RegistersOutboxPublisherAndRedisTransportPublisher()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Port=5432;Database=vcs_core_v3_dev;Username=postgres;Password=postgres",
                ["Redis:ConnectionString"] = "localhost:6379"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddHexagonalArchitecture(configuration);

        var appPublisher = services.Single(x => x.ServiceType == typeof(IIntegrationEventPublisher));
        var transportPublisher = services.Single(x => x.ServiceType == typeof(IOutboxTransportPublisher));

        Assert.Equal(typeof(OutboxIntegrationEventPublisher), appPublisher.ImplementationType);
        // IOutboxTransportPublisher is now a factory-registered CompositeOutboxTransportPublisher (Kafka + Redis)
        Assert.NotNull(transportPublisher.ImplementationFactory);
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchPendingAsync_PublishesAndMarksMessageProcessed()
    {
        await using var dbContext = CreateDbContext();
        var publisher = new CapturingPublisher();
        var serializer = new SystemTextJsonOutboxMessageSerializer();
        var integrationEvent = new IntegrationEvent<WeatherForecastGeneratedEvent>(
            EventTypes.WeatherForecastGenerated,
            new WeatherForecastGeneratedEvent(3, 17.4),
            "corr-success");

        dbContext.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            EventType = EventTypes.WeatherForecastGenerated,
            CorrelationId = integrationEvent.CorrelationId,
            SchemaVersion = integrationEvent.SchemaVersion,
            Payload = serializer.Serialize(integrationEvent),
            OccurredAtUtc = integrationEvent.OccurredAtUtc ?? DateTime.UtcNow,
            CreatedAtUtc = new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc)
        });
        await dbContext.SaveChangesAsync();

        var dispatcher = CreateDispatcher(dbContext, publisher, serializer);

        var dispatched = await dispatcher.DispatchPendingAsync();

        var saved = await dbContext.OutboxMessages.SingleAsync();
        Assert.Equal(1, dispatched);
        Assert.Single(publisher.GeneratedEvents);
        Assert.NotNull(saved.ProcessedAtUtc);
        Assert.Null(saved.LastError);
        Assert.Null(saved.LockedAtUtc);
        Assert.Null(saved.LockToken);
        Assert.Equal(0, saved.RetryCount);
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchPendingAsync_IncrementsRetryAndKeepsMessagePendingOnFailure()
    {
        await using var dbContext = CreateDbContext();
        var publisher = new ThrowingPublisher();
        var serializer = new SystemTextJsonOutboxMessageSerializer();
        var integrationEvent = new IntegrationEvent<WeatherForecastGeneratedEvent>(
            EventTypes.WeatherForecastGenerated,
            new WeatherForecastGeneratedEvent(4, 19.2),
            "corr-failure");

        dbContext.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            EventType = EventTypes.WeatherForecastGenerated,
            CorrelationId = integrationEvent.CorrelationId,
            SchemaVersion = integrationEvent.SchemaVersion,
            Payload = serializer.Serialize(integrationEvent),
            OccurredAtUtc = integrationEvent.OccurredAtUtc ?? DateTime.UtcNow,
            CreatedAtUtc = new DateTime(2026, 4, 2, 9, 5, 0, DateTimeKind.Utc)
        });
        await dbContext.SaveChangesAsync();

        var dispatcher = CreateDispatcher(dbContext, publisher, serializer);

        var dispatched = await dispatcher.DispatchPendingAsync();

        var saved = await dbContext.OutboxMessages.SingleAsync();
        Assert.Equal(0, dispatched);
        Assert.Null(saved.ProcessedAtUtc);
        Assert.Equal(1, saved.RetryCount);
        Assert.Equal("Synthetic publish failure.", saved.LastError);
        Assert.Null(saved.LockedAtUtc);
        Assert.Null(saved.LockToken);
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchPendingAsync_ReclaimsExpiredLocksOnly()
    {
        await using var dbContext = CreateDbContext();
        var publisher = new CapturingPublisher();
        var serializer = new SystemTextJsonOutboxMessageSerializer();
        var nowUtc = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc);

        dbContext.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            EventType = EventTypes.WeatherForecastGenerated,
            CorrelationId = "corr-unlocked",
            SchemaVersion = 1,
            Payload = serializer.Serialize(new IntegrationEvent<WeatherForecastGeneratedEvent>(
                EventTypes.WeatherForecastGenerated,
                new WeatherForecastGeneratedEvent(1, 12.4),
                "corr-unlocked",
                OccurredAtUtc: nowUtc)),
            OccurredAtUtc = nowUtc,
            CreatedAtUtc = nowUtc.AddMinutes(-3)
        });

        dbContext.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            EventType = EventTypes.WeatherForecastGenerated,
            CorrelationId = "corr-locked-active",
            SchemaVersion = 1,
            Payload = serializer.Serialize(new IntegrationEvent<WeatherForecastGeneratedEvent>(
                EventTypes.WeatherForecastGenerated,
                new WeatherForecastGeneratedEvent(2, 13.1),
                "corr-locked-active",
                OccurredAtUtc: nowUtc)),
            OccurredAtUtc = nowUtc,
            CreatedAtUtc = nowUtc.AddMinutes(-2),
            LockedAtUtc = DateTime.UtcNow,
            LockToken = "active-lock"
        });

        dbContext.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            EventType = EventTypes.WeatherForecastGenerated,
            CorrelationId = "corr-locked-expired",
            SchemaVersion = 1,
            Payload = serializer.Serialize(new IntegrationEvent<WeatherForecastGeneratedEvent>(
                EventTypes.WeatherForecastGenerated,
                new WeatherForecastGeneratedEvent(3, 14.6),
                "corr-locked-expired",
                OccurredAtUtc: nowUtc)),
            OccurredAtUtc = nowUtc,
            CreatedAtUtc = nowUtc.AddMinutes(-1),
            LockedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            LockToken = "expired-lock"
        });
        await dbContext.SaveChangesAsync();

        var dispatcher = new OutboxDispatcher(
            dbContext,
            publisher,
            serializer,
            Options.Create(new OutboxOptions { LockTimeoutSeconds = 30, BatchSize = 10 }),
            NullLogger<OutboxDispatcher>.Instance);

        var dispatched = await dispatcher.DispatchPendingAsync();

        Assert.Equal(2, dispatched);
        Assert.Equal(2, publisher.GeneratedEvents.Count);

        var activeLock = await dbContext.OutboxMessages.SingleAsync(x => x.CorrelationId == "corr-locked-active");
        Assert.Null(activeLock.ProcessedAtUtc);
        Assert.Equal("active-lock", activeLock.LockToken);

        var expiredLock = await dbContext.OutboxMessages.SingleAsync(x => x.CorrelationId == "corr-locked-expired");
        Assert.NotNull(expiredLock.ProcessedAtUtc);
        Assert.Null(expiredLock.LockedAtUtc);
        Assert.Null(expiredLock.LockToken);
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchPendingAsync_MultiInstance_PublishesMessageOnce()
    {
        var rootConnectionString = GetPostgreSqlIntegrationConnectionString();
        if (string.IsNullOrWhiteSpace(rootConnectionString))
        {
            return;
        }

        var databaseName = $"vcs_core_v3_multi_instance_{Guid.NewGuid():N}";
        var connectionString = BuildDatabaseConnectionString(rootConnectionString, databaseName);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var setupContext = new AppDbContext(options, new NullCurrentUser(), TimeProvider.System);
        await setupContext.Database.EnsureDeletedAsync();
        await setupContext.Database.EnsureCreatedAsync();

        try
        {
            var serializer = new SystemTextJsonOutboxMessageSerializer();
            var integrationEvent = new IntegrationEvent<WeatherForecastGeneratedEvent>(
                EventTypes.WeatherForecastGenerated,
                new WeatherForecastGeneratedEvent(11, 24.7),
                "corr-multi-instance");

            setupContext.OutboxMessages.Add(new OutboxMessageEntity
            {
                Id = Guid.NewGuid(),
                EventType = EventTypes.WeatherForecastGenerated,
                CorrelationId = integrationEvent.CorrelationId,
                SchemaVersion = integrationEvent.SchemaVersion,
                Payload = serializer.Serialize(integrationEvent),
                OccurredAtUtc = integrationEvent.OccurredAtUtc ?? DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();

            var publisher = new CountingPublisher();
            var dispatcherOptions = Options.Create(new OutboxOptions
            {
                BatchSize = 1,
                LockTimeoutSeconds = 60
            });

            async Task<int> DispatchWithNewContextAsync()
            {
                await using var context = new AppDbContext(options, new NullCurrentUser(), TimeProvider.System);
                var dispatcher = new OutboxDispatcher(
                    context,
                    publisher,
                    serializer,
                    dispatcherOptions,
                    NullLogger<OutboxDispatcher>.Instance);

                return await dispatcher.DispatchPendingAsync();
            }

            var dispatcherOne = DispatchWithNewContextAsync();
            var dispatcherTwo = DispatchWithNewContextAsync();
            var results = await Task.WhenAll(dispatcherOne, dispatcherTwo);

            Assert.Equal(1, results.Sum());
            Assert.Equal(1, publisher.PublishedCount);

            await using var assertContext = new AppDbContext(options, new NullCurrentUser(), TimeProvider.System);
            var saved = await assertContext.OutboxMessages.SingleAsync();
            Assert.NotNull(saved.ProcessedAtUtc);
            Assert.Equal(0, saved.RetryCount);
            Assert.Null(saved.LastError);
        }
        finally
        {
            await setupContext.Database.EnsureDeletedAsync();
        }
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options, new NullCurrentUser(), TimeProvider.System);
    }

    private static string? GetPostgreSqlIntegrationConnectionString()
    {
        return Environment.GetEnvironmentVariable("VCS_TEST_POSTGRESQL_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL");
    }

    private static string BuildDatabaseConnectionString(string rootConnectionString, string databaseName)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = rootConnectionString
        };

        builder["Database"] = databaseName;
        return builder.ConnectionString;
    }

    private static OutboxDispatcher CreateDispatcher(
        AppDbContext dbContext,
        IOutboxTransportPublisher publisher,
        IOutboxMessageSerializer serializer)
    {
        return new OutboxDispatcher(
            dbContext,
            publisher,
            serializer,
            Options.Create(new OutboxOptions()),
            NullLogger<OutboxDispatcher>.Instance);
    }

    private sealed class CapturingPublisher : IOutboxTransportPublisher
    {
        public List<IntegrationEvent<WeatherForecastGeneratedEvent>> GeneratedEvents { get; } = new();

        public Task PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken cancellationToken = default)
        {
            if (integrationEvent is IntegrationEvent<WeatherForecastGeneratedEvent> generated)
            {
                GeneratedEvents.Add(generated);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IOutboxTransportPublisher
    {
        public Task PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Synthetic publish failure.");
        }
    }

    private sealed class CountingPublisher : IOutboxTransportPublisher
    {
        private int _publishedCount;

        public int PublishedCount => _publishedCount;

        public async Task PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _publishedCount);
            await Task.Delay(50, cancellationToken);
        }
    }
}