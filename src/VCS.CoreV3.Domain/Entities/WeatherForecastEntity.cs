namespace VCS.CoreV3.Domain.Entities;

public sealed class WeatherForecastEntity
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}