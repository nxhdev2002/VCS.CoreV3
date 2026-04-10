namespace VCS.CoreV3.Ports;

public sealed record WeatherForecastRequestedEvent(string HttpMethod, string RequestPath) : IRedisEvent;

public sealed record WeatherForecastGeneratedEvent(int ForecastCount, double AverageTemperatureC) : IKafkaEvent;