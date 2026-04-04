using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using VCS.CoreV3.Domain.Entities;
using VCS.CoreV3.Infrastructure;
using VCS.CoreV3.Infrastructure.Data;
using VCS.CoreV3.Infrastructure.Redis;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application.Tests;

public sealed class WeatherForecastEventHandlerTests
{
    [Fact]
    public async Task GeneratedHandler_ExposesEventType_AndHandlesEnvelope()
    {
        var sut = new WeatherForecastGeneratedEventHandler(NullLogger<WeatherForecastGeneratedEventHandler>.Instance);
        var envelope = new IntegrationEventEnvelope<WeatherForecastGeneratedEvent>(
            MessageId: "msg-1",
            EventType: EventTypes.WeatherForecastGenerated,
            OccurredAtUtc: DateTime.UtcNow,
            CorrelationId: "corr-1",
            SchemaVersion: 1,
            Payload: new WeatherForecastGeneratedEvent(5, 22.4),
            RetryCount: 0);

        await sut.HandleAsync(envelope);

        Assert.Equal(EventTypes.WeatherForecastGenerated, sut.EventType);
    }

    [Fact]
    public async Task RequestedHandler_ExposesEventType_AndHandlesEnvelope()
    {
        var sut = new WeatherForecastRequestedEventHandler(NullLogger<WeatherForecastRequestedEventHandler>.Instance);
        var envelope = new IntegrationEventEnvelope<WeatherForecastRequestedEvent>(
            MessageId: "msg-2",
            EventType: EventTypes.WeatherForecastRequested,
            OccurredAtUtc: DateTime.UtcNow,
            CorrelationId: "corr-2",
            SchemaVersion: 1,
            Payload: new WeatherForecastRequestedEvent("GET", "/weatherforecast"),
            RetryCount: 0);

        await sut.HandleAsync(envelope);

        Assert.Equal(EventTypes.WeatherForecastRequested, sut.EventType);
    }
}

public sealed class ApiKeyCreatedEventHandlerTests
{
    // --- unit tests (in-memory stub) ---

    [Fact]
    public void Handler_ExposesCorrectEventType()
    {
        var sut = CreateHandler(new CapturingApiKeyRepository());

        Assert.Equal(EventTypes.ApiKeyCreated, sut.EventType);
    }

    [Fact]
    public async Task HandleAsync_CreatesApiKey_WithCorrectUserId()
    {
        var userId = Guid.NewGuid();
        var repo = new CapturingApiKeyRepository();
        var sut = CreateHandler(repo);

        await sut.HandleAsync(BuildEnvelope(userId.ToString()));

        Assert.Single(repo.Created);
        Assert.Equal(userId, repo.Created[0].UserId);
    }

    [Fact]
    public async Task HandleAsync_CreatesApiKey_WithFreeDefaults()
    {
        var repo = new CapturingApiKeyRepository();
        var sut = CreateHandler(repo);

        await sut.HandleAsync(BuildEnvelope(Guid.NewGuid().ToString()));

        var entity = repo.Created[0];
        Assert.Equal("free", entity.Plan);
        Assert.Equal(100, entity.RateLimit);
        Assert.False(entity.IsRevoked);
        Assert.Null(entity.ExpiredAt);
    }

    [Fact]
    public async Task HandleAsync_CreatesApiKey_WithValidSha256KeyHash()
    {
        var repo = new CapturingApiKeyRepository();
        var sut = CreateHandler(repo);

        await sut.HandleAsync(BuildEnvelope(Guid.NewGuid().ToString()));

        var keyHash = repo.Created[0].KeyHash;
        Assert.Equal(64, keyHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", keyHash);
    }

    [Fact]
    public async Task HandleAsync_GeneratesUniqueIdAndKeyHashPerCall()
    {
        var repo = new CapturingApiKeyRepository();
        var sut = CreateHandler(repo);

        await sut.HandleAsync(BuildEnvelope(Guid.NewGuid().ToString()));
        await sut.HandleAsync(BuildEnvelope(Guid.NewGuid().ToString()));

        Assert.NotEqual(repo.Created[0].Id, repo.Created[1].Id);
        Assert.NotEqual(repo.Created[0].KeyHash, repo.Created[1].KeyHash);
    }

    // --- integration tests (PostgresApiKeyRepository + EF InMemory) ---

    [Fact]
    public async Task HandleAsync_PersistsApiKeyRow_ToDatabase()
    {
        var userId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var sut = CreateHandler(new PostgresApiKeyRepository(dbContext));

        await sut.HandleAsync(BuildEnvelope(userId.ToString()));

        var saved = await dbContext.ApiKeys.SingleAsync();
        Assert.Equal(userId, saved.UserId);
        Assert.Equal("free", saved.Plan);
        Assert.Equal(ApiKeyDefaults.DefaultFreeRateLimit, saved.RateLimit);
        Assert.Equal(64, saved.KeyHash.Length);
        Assert.False(saved.IsRevoked);
        Assert.Null(saved.ExpiredAt);
        Assert.True(saved.CreationTime <= DateTime.UtcNow);
    }

    [Fact]
    public async Task HandleAsync_StoredKeyHash_IsFoundByGetByKeyHashAsync()
    {
        await using var dbContext = CreateDbContext();
        var repo = new PostgresApiKeyRepository(dbContext);
        var sut = CreateHandler(repo);

        await sut.HandleAsync(BuildEnvelope(Guid.NewGuid().ToString()));

        var saved = await dbContext.ApiKeys.SingleAsync();
        var found = await repo.GetByKeyHashAsync(saved.KeyHash);

        Assert.NotNull(found);
        Assert.Equal(saved.Id, found.Id);
    }

    // --- helpers ---

    private static ApiKeyCreatedEventHandler CreateHandler(IApiKeyRepository repo)
        => new(repo, NullLogger<ApiKeyCreatedEventHandler>.Instance);

    private static IntegrationEventEnvelope<ApiKeyCreatedEvent> BuildEnvelope(string userId)
        => new(
            MessageId: Guid.NewGuid().ToString(),
            EventType: EventTypes.ApiKeyCreated,
            OccurredAtUtc: DateTime.UtcNow,
            CorrelationId: "corr-api-key",
            SchemaVersion: 1,
            Payload: new ApiKeyCreatedEvent(userId),
            RetryCount: 0);

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options, new NullCurrentUser(), TimeProvider.System);
    }

    private sealed class CapturingApiKeyRepository : IApiKeyRepository
    {
        public List<ApiKeyEntity> Created { get; } = new();

        public Task CreateAsync(ApiKeyEntity entity, CancellationToken ct = default)
        {
            Created.Add(entity);
            return Task.CompletedTask;
        }

        public Task<ApiKeyEntity?> GetByKeyHashAsync(string keyHash) => Task.FromResult<ApiKeyEntity?>(null);
        public Task<bool> RevokeAsync(Guid id) => Task.FromResult(false);
        public Task<bool> UpdateRateLimitAsync(Guid id, int newRateLimit) => Task.FromResult(false);
    }
}

public sealed class ApiKeyCreatedEventEndToEndTests
{
    [Fact]
    public async Task ApiKeyCreatedEvent_PublishedToRedisStream_IsConsumedAndApiKeyCreatedInDatabase()
    {
        var redisConnectionString = GetRedisConnectionString();
        if (string.IsNullOrWhiteSpace(redisConnectionString)) return;

        var streamName = $"vcs.e2e.{Guid.NewGuid():N}";
        var consumerGroup = "e2e-test-consumer-group";
        var sharedDbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();

        var redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
        var db = redis.GetDatabase();

        try
        {
            // 1. Create consumer group before publishing so ">" reads new messages
            await db.StreamCreateConsumerGroupAsync(streamName, consumerGroup, "$", createStream: true);

            // 2. Publish ApiKeyCreatedEvent directly to Redis (simulates external system)
            var serializer = new SystemTextJsonIntegrationEventSerializer();
            var payload = serializer.Serialize(new ApiKeyCreatedEvent(userId.ToString()));

            await db.StreamAddAsync(streamName, new NameValueEntry[]
            {
                new("messageId", Guid.NewGuid().ToString("N")),
                new("eventType", EventTypes.ApiKeyCreated),
                new("occurredAtUtc", DateTime.UtcNow.ToString("O")),
                new("correlationId", "corr-e2e"),
                new("schemaVersion", 1),
                new("retryCount", 0),
                new("payload", payload)
            });

            // Assert: message is visible in Redis before consuming
            var streamEntries = await db.StreamRangeAsync(streamName, "-", "+");
            Assert.Single(streamEntries);

            // 3. Build DI container for the consumer worker
            var services = new ServiceCollection();
            services.AddSingleton<IConnectionMultiplexer>(redis);
            services.AddSingleton<IIntegrationEventSerializer>(serializer);
            services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(sharedDbName));
            services.AddScoped<IApiKeyRepository, PostgresApiKeyRepository>();
            services.AddScoped<IIntegrationEventHandler<ApiKeyCreatedEvent>, ApiKeyCreatedEventHandler>();
            services.AddLogging(b => b.ClearProviders());

            await using var provider = services.BuildServiceProvider();

            var streamOptions = Options.Create(new RedisStreamOptions
            {
                StreamName = streamName,
                ConsumerGroup = consumerGroup,
                ConsumerNamePrefix = "e2e-test",
                ReadBatchSize = 10,
                BlockMilliseconds = 100,
                MaxRetries = 3,
                MaxStreamLength = 1000
            });

            var worker = new RedisStreamConsumerWorker(
                redis,
                serializer,
                provider.GetRequiredService<IServiceScopeFactory>(),
                streamOptions,
                NullLogger<RedisStreamConsumerWorker>.Instance);

            // 4. Start worker and poll DB until ApiKey row appears
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await worker.StartAsync(cts.Token);

            using var assertScope = provider.CreateScope();
            var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline && !await assertDb.ApiKeys.AnyAsync())
            {
                await Task.Delay(100);
            }

            await cts.CancelAsync();
            await worker.StopAsync(CancellationToken.None);

            // 5. Assert DB: ApiKey row created with correct data
            var saved = await assertDb.ApiKeys.SingleAsync();
            Assert.Equal(userId, saved.UserId);
            Assert.Equal("free", saved.Plan);
            Assert.Equal(ApiKeyDefaults.DefaultFreeRateLimit, saved.RateLimit);
            Assert.Equal(64, saved.KeyHash.Length);
            Assert.False(saved.IsRevoked);
            Assert.Null(saved.ExpiredAt);

            // 6. Assert Redis: message acknowledged, no pending messages remain
            var pending = await db.StreamPendingAsync(streamName, consumerGroup);
            Assert.Equal(0L, pending.PendingMessageCount);
        }
        finally
        {
            await db.KeyDeleteAsync(streamName);
            await redis.DisposeAsync();
        }
    }

    private static string? GetRedisConnectionString()
        => Environment.GetEnvironmentVariable("VCS_TEST_REDIS_CONNECTION")
           ?? Environment.GetEnvironmentVariable("Redis__ConnectionString");
}