using Microsoft.EntityFrameworkCore;
using VCS.CoreV3.Domain;
using VCS.CoreV3.Infrastructure.Data.Entities;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Data;

public sealed class PostgresWeatherForecastRepository(AppDbContext dbContext) : IWeatherForecastRepository
{
    public async Task<IReadOnlyList<WeatherForecast>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        var size = Math.Max(1, take);

        var entities = await dbContext.WeatherForecasts
            .OrderByDescending(x => x.Date)
            .Take(size)
            .ToListAsync(cancellationToken);

        return entities
            .Select(static x => new WeatherForecast(x.Date, x.TemperatureC, x.Summary))
            .ToArray();
    }

    public async Task AddRangeAsync(IEnumerable<WeatherForecast> forecasts, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        var entities = forecasts
            .Select(x => new WeatherForecastEntity
            {
                Id = Guid.NewGuid(),
                Date = x.Date,
                TemperatureC = x.TemperatureC,
                Summary = x.Summary,
                CreatedAtUtc = utcNow
            })
            .ToArray();

        await dbContext.WeatherForecasts.AddRangeAsync(entities, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}