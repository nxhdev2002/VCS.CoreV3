using Microsoft.Extensions.Logging.Abstractions;
using VCS.CoreV3.Application;
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