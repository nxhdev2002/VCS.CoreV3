namespace VCS.CoreV3.Ports;

public sealed record WeatherForecastRequestedEvent(string HttpMethod, string RequestPath);

public sealed record WeatherForecastGeneratedEvent(int ForecastCount, double AverageTemperatureC);