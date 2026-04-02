namespace VCS.CoreV3.Ports;

public sealed record WeatherForecastDto(DateOnly Date, int TemperatureC, string Summary, int TemperatureF);
