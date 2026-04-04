namespace VCS.CoreV3.Ports;

public static class EventTypes
{
    public const string WeatherForecastRequested = "weather_forecast.requested.v1";
    public const string WeatherForecastGenerated = "weather_forecast.generated.v1";
    public const string ApiKeyCreated = "api_key.created.v1";
}