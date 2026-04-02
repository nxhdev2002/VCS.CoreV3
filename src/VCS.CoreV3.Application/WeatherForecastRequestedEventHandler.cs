using Microsoft.Extensions.Logging;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application;

public sealed class WeatherForecastRequestedEventHandler : IIntegrationEventHandler<WeatherForecastRequestedEvent>
{
    private readonly ILogger<WeatherForecastRequestedEventHandler> _logger;

    public WeatherForecastRequestedEventHandler(ILogger<WeatherForecastRequestedEventHandler> logger)
    {
        _logger = logger;
    }

    public string EventType => EventTypes.WeatherForecastRequested;

    public Task HandleAsync(IntegrationEventEnvelope<WeatherForecastRequestedEvent> envelope, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Consumed {EventType} messageId={MessageId} correlationId={CorrelationId} method={HttpMethod} path={RequestPath}",
            envelope.EventType,
            envelope.MessageId,
            envelope.CorrelationId,
            envelope.Payload.HttpMethod,
            envelope.Payload.RequestPath);

        return Task.CompletedTask;
    }
}