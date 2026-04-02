using VCS.CoreV3.Application;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application.Tests;

public sealed class GetWeatherForecastUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsFiveForecasts_AndPublishesGeneratedEvent()
    {
        var publisher = new CapturingEventPublisher();
        var correlationAccessor = new FixedCorrelationContextAccessor { CorrelationId = "corr-123" };
        var sut = new GetWeatherForecastUseCase(publisher, correlationAccessor);

        var forecasts = (await sut.ExecuteAsync()).ToArray();
        var publishedEvent = publisher.LastEvent;

        Assert.Equal(5, forecasts.Length);
        Assert.All(forecasts, x => Assert.InRange(x.TemperatureC, -20, 54));
        Assert.NotNull(publishedEvent);
        Assert.Equal("corr-123", publishedEvent.CorrelationId);
        Assert.Equal(EventTypes.WeatherForecastGenerated, publishedEvent.EventType);
        Assert.Equal(5, publishedEvent.Payload.ForecastCount);
        Assert.Equal(forecasts.Average(x => x.TemperatureC), publishedEvent.Payload.AverageTemperatureC, 10);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesCorrelationId_WhenContextIsMissing()
    {
        var publisher = new CapturingEventPublisher();
        var correlationAccessor = new FixedCorrelationContextAccessor { CorrelationId = null };
        var sut = new GetWeatherForecastUseCase(publisher, correlationAccessor);

        _ = await sut.ExecuteAsync();

        var correlationId = publisher.LastEvent?.CorrelationId;
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.True(Guid.TryParseExact(correlationId, "N", out _));
    }

    private sealed class CapturingEventPublisher : IIntegrationEventPublisher
    {
        public IntegrationEvent<WeatherForecastGeneratedEvent>? LastEvent { get; private set; }

        public Task PublishAsync<TPayload>(IntegrationEvent<TPayload> integrationEvent, CancellationToken cancellationToken = default)
        {
            if (integrationEvent is IntegrationEvent<WeatherForecastGeneratedEvent> generated)
            {
                LastEvent = generated;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FixedCorrelationContextAccessor : ICorrelationContextAccessor
    {
        public string? CorrelationId { get; set; }
    }
}