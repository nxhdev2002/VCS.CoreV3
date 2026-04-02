using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Adapters.Web;

internal static class WeatherForecastEndpoint
{
    public static void MapWeatherForecastEndpoint(this WebApplication app)
    {
        app.MapGet("/weatherforecast", async (
            HttpContext httpContext,
            IGetWeatherForecastUseCase useCase,
            ICorrelationContextAccessor correlationContextAccessor,
            IIntegrationEventPublisher eventPublisher,
            CancellationToken cancellationToken) =>
        {
            var correlationId = httpContext.Request.Headers["X-Correlation-Id"].ToString();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = httpContext.TraceIdentifier;
            }

            correlationContextAccessor.CorrelationId = correlationId;

            await eventPublisher.PublishAsync(
                new IntegrationEvent<WeatherForecastRequestedEvent>(
                    EventTypes.WeatherForecastRequested,
                    new WeatherForecastRequestedEvent(httpContext.Request.Method, httpContext.Request.Path),
                    correlationId),
                cancellationToken);

            var forecasts = await useCase.ExecuteAsync();
            return Results.Ok(forecasts);
        })
        .WithName("GetWeatherForecast");
    }
}
