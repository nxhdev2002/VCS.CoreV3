using VCS.CoreV3.Domain;

namespace VCS.CoreV3.Ports;

public interface IWeatherForecastRepository
{
    Task<IReadOnlyList<WeatherForecast>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<WeatherForecast> forecasts, CancellationToken cancellationToken = default);
}