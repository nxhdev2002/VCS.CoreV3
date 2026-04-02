namespace VCS.CoreV3.Ports;

public interface IGetWeatherForecastUseCase
{
    Task<IEnumerable<WeatherForecastDto>> ExecuteAsync();
}
