using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VCS.CoreV3.Infrastructure.Kafka;
using VCS.CoreV3.Infrastructure.Redis;
using VCS.CoreV3.Ports;
using Xunit;

namespace VCS.CoreV3.Infrastructure.Tests.Kafka;

public sealed class IntegrationEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WithWeatherForecastRequestedEventType_InvokesMatchingHandler()
    {
        var handler = new CapturingHandler<WeatherForecastRequestedEvent>(EventTypes.WeatherForecastRequested);
        var sut = BuildSut(services =>
            services.AddScoped<IIntegrationEventHandler<WeatherForecastRequestedEvent>>(_ => handler));

        var serializer = new SystemTextJsonIntegrationEventSerializer();
        var payload = serializer.Serialize(new WeatherForecastRequestedEvent("GET", "/weather"));

        await sut.DispatchAsync(EventTypes.WeatherForecastRequested, payload, "msg-1", "corr-1", 1, DateTime.UtcNow, 0);

        Assert.True(handler.WasInvoked);
        Assert.Equal(EventTypes.WeatherForecastRequested, handler.ReceivedEnvelope!.EventType);
    }

    [Fact]
    public async Task DispatchAsync_WithApiKeyCreatedEventType_InvokesMatchingHandler()
    {
        var handler = new CapturingHandler<ApiKeyCreatedEvent>(EventTypes.ApiKeyCreated);
        var sut = BuildSut(services =>
            services.AddScoped<IIntegrationEventHandler<ApiKeyCreatedEvent>>(_ => handler));

        var serializer = new SystemTextJsonIntegrationEventSerializer();
        var payload = serializer.Serialize(new ApiKeyCreatedEvent("user-42"));

        await sut.DispatchAsync(EventTypes.ApiKeyCreated, payload, "msg-2", "corr-2", 1, DateTime.UtcNow, 0);

        Assert.True(handler.WasInvoked);
        Assert.Equal(EventTypes.ApiKeyCreated, handler.ReceivedEnvelope!.EventType);
    }

    [Fact]
    public async Task DispatchAsync_WhenMultipleHandlersRegistered_InvokesAll()
    {
        var handler1 = new CapturingHandler<WeatherForecastGeneratedEvent>(EventTypes.WeatherForecastGenerated);
        var handler2 = new CapturingHandler<WeatherForecastGeneratedEvent>(EventTypes.WeatherForecastGenerated);
        var sut = BuildSut(services =>
        {
            services.AddScoped<IIntegrationEventHandler<WeatherForecastGeneratedEvent>>(_ => handler1);
            services.AddScoped<IIntegrationEventHandler<WeatherForecastGeneratedEvent>>(_ => handler2);
        });

        var serializer = new SystemTextJsonIntegrationEventSerializer();
        var payload = serializer.Serialize(new WeatherForecastGeneratedEvent(5, 22.5));

        await sut.DispatchAsync(EventTypes.WeatherForecastGenerated, payload, "msg-3", "corr-3", 1, DateTime.UtcNow, 0);

        Assert.True(handler1.WasInvoked);
        Assert.True(handler2.WasInvoked);
    }

    [Fact]
    public async Task DispatchAsync_WithUnknownEventType_CompletesWithoutThrowing()
    {
        var sut = BuildSut(_ => { });

        var ex = await Record.ExceptionAsync(() =>
            sut.DispatchAsync("unknown.event.v99", "{}", "msg-4", "corr-4", 1, DateTime.UtcNow, 0));

        Assert.Null(ex);
    }

    [Fact]
    public async Task DispatchAsync_WithKafkaFilter_SkipsEventNotImplementingIKafkaEvent()
    {
        // WeatherForecastRequestedEvent : IRedisEvent (not IKafkaEvent) — should be skipped
        var handler = new CapturingHandler<WeatherForecastRequestedEvent>(EventTypes.WeatherForecastRequested);
        var sut = BuildSut(
            services => services.AddScoped<IIntegrationEventHandler<WeatherForecastRequestedEvent>>(_ => handler),
            transportFilter: typeof(IKafkaEvent));

        var serializer = new SystemTextJsonIntegrationEventSerializer();
        var payload = serializer.Serialize(new WeatherForecastRequestedEvent("GET", "/weather"));

        await sut.DispatchAsync(EventTypes.WeatherForecastRequested, payload, "msg-5", "corr-5", 1, DateTime.UtcNow, 0);

        Assert.False(handler.WasInvoked);
    }

    [Fact]
    public async Task DispatchAsync_WithRedisFilter_SkipsEventNotImplementingIRedisEvent()
    {
        // WeatherForecastGeneratedEvent : IKafkaEvent (not IRedisEvent) — should be skipped
        var handler = new CapturingHandler<WeatherForecastGeneratedEvent>(EventTypes.WeatherForecastGenerated);
        var sut = BuildSut(
            services => services.AddScoped<IIntegrationEventHandler<WeatherForecastGeneratedEvent>>(_ => handler),
            transportFilter: typeof(IRedisEvent));

        var serializer = new SystemTextJsonIntegrationEventSerializer();
        var payload = serializer.Serialize(new WeatherForecastGeneratedEvent(5, 22.5));

        await sut.DispatchAsync(EventTypes.WeatherForecastGenerated, payload, "msg-6", "corr-6", 1, DateTime.UtcNow, 0);

        Assert.False(handler.WasInvoked);
    }

    [Fact]
    public async Task DispatchAsync_WithKafkaFilter_InvokesHandlerForKafkaEvent()
    {
        // WeatherForecastGeneratedEvent : IKafkaEvent — should be dispatched
        var handler = new CapturingHandler<WeatherForecastGeneratedEvent>(EventTypes.WeatherForecastGenerated);
        var sut = BuildSut(
            services => services.AddScoped<IIntegrationEventHandler<WeatherForecastGeneratedEvent>>(_ => handler),
            transportFilter: typeof(IKafkaEvent));

        var serializer = new SystemTextJsonIntegrationEventSerializer();
        var payload = serializer.Serialize(new WeatherForecastGeneratedEvent(3, 18.0));

        await sut.DispatchAsync(EventTypes.WeatherForecastGenerated, payload, "msg-7", "corr-7", 1, DateTime.UtcNow, 0);

        Assert.True(handler.WasInvoked);
    }

    private static IntegrationEventDispatcher BuildSut(Action<IServiceCollection> register, Type? transportFilter = null)
    {
        var services = new ServiceCollection();
        register(services);
        var provider = services.BuildServiceProvider();
        var serializer = new SystemTextJsonIntegrationEventSerializer();
        return new IntegrationEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(), serializer, transportFilter);
    }

    private sealed class CapturingHandler<TPayload>(string eventType) : IIntegrationEventHandler<TPayload>
    {
        public string EventType => eventType;
        public bool WasInvoked { get; private set; }
        public IntegrationEventEnvelope<TPayload>? ReceivedEnvelope { get; private set; }

        public Task HandleAsync(IntegrationEventEnvelope<TPayload> envelope, CancellationToken ct = default)
        {
            WasInvoked = true;
            ReceivedEnvelope = envelope;
            return Task.CompletedTask;
        }
    }
}
