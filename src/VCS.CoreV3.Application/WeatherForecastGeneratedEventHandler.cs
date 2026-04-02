using Microsoft.Extensions.Logging;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application;

public sealed class WeatherForecastGeneratedEventHandler : IIntegrationEventHandler<WeatherForecastGeneratedEvent>
{
    private readonly ILogger<WeatherForecastGeneratedEventHandler> _logger;

    public WeatherForecastGeneratedEventHandler(ILogger<WeatherForecastGeneratedEventHandler> logger)
    {
        _logger = logger;
    }

    public string EventType => EventTypes.WeatherForecastGenerated;

    public Task HandleAsync(IntegrationEventEnvelope<WeatherForecastGeneratedEvent> envelope, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Consumed {EventType} messageId={MessageId} correlationId={CorrelationId} forecastCount={ForecastCount} avgTempC={AverageTemperatureC}",
            envelope.EventType,
            envelope.MessageId,
            envelope.CorrelationId,
            envelope.Payload.ForecastCount,
            envelope.Payload.AverageTemperatureC);

        return Task.CompletedTask;
    }
}