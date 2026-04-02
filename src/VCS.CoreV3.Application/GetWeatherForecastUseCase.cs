using VCS.CoreV3.Domain;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Application;

public sealed class GetWeatherForecastUseCase : IGetWeatherForecastUseCase
{
    private const int ForecastLength = 5;
    private static readonly string[] Summaries =
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly Random _random = Random.Shared;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ICorrelationContextAccessor _correlationContextAccessor;

    public GetWeatherForecastUseCase(
        IIntegrationEventPublisher eventPublisher,
        ICorrelationContextAccessor correlationContextAccessor)
    {
        _eventPublisher = eventPublisher;
        _correlationContextAccessor = correlationContextAccessor;
    }

    public async Task<IEnumerable<WeatherForecastDto>> ExecuteAsync()
    {
        var forecasts = Enumerable.Range(1, ForecastLength)
            .Select(CreateForecast)
            .ToArray();

        var correlationId = _correlationContextAccessor.CorrelationId ?? Guid.NewGuid().ToString("N");
        var averageTemperatureC = forecasts.Length == 0 ? 0 : forecasts.Average(static x => x.TemperatureC);

        await _eventPublisher.PublishAsync(
            new IntegrationEvent<WeatherForecastGeneratedEvent>(
                EventTypes.WeatherForecastGenerated,
                new WeatherForecastGeneratedEvent(forecasts.Length, averageTemperatureC),
                correlationId));

        return forecasts;
    }

    private WeatherForecastDto CreateForecast(int index)
    {
        var date = DateOnly.FromDateTime(DateTime.Now.AddDays(index));
        var temperatureC = _random.Next(-20, 55);
        var summary = Summaries[_random.Next(Summaries.Length)];
        var domainForecast = new WeatherForecast(date, temperatureC, summary);
        return new WeatherForecastDto(domainForecast.Date, domainForecast.TemperatureC, domainForecast.Summary, domainForecast.TemperatureF);
    }
}
