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

    private static IntegrationEventDispatcher BuildSut(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        var provider = services.BuildServiceProvider();
        var serializer = new SystemTextJsonIntegrationEventSerializer();
        return new IntegrationEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(), serializer);
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
