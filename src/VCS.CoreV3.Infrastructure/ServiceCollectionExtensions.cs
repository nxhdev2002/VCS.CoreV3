using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using VCS.CoreV3.Application;
using VCS.CoreV3.Infrastructure.Data;
using VCS.CoreV3.Infrastructure.Redis;
using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHexagonalArchitecture(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnectionString = configuration.GetConnectionString("PostgreSQL");
        if (string.IsNullOrWhiteSpace(postgresConnectionString))
        {
            throw new InvalidOperationException("Connection string 'PostgreSQL' is required.");
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(postgresConnectionString);
        });

        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<RedisStreamOptions>(configuration.GetSection(RedisStreamOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var redisOptions = provider.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
        });

        services.AddSingleton<IIntegrationEventSerializer, SystemTextJsonIntegrationEventSerializer>();
        services.AddScoped<IOutboxMessageSerializer, SystemTextJsonOutboxMessageSerializer>();
        services.AddSingleton<IOutboxTransportPublisher, RedisStreamEventPublisher>();
        services.AddScoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddHostedService<RedisStreamConsumerWorker>();
        services.AddHostedService<OutboxDispatcherWorker>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<IApiKeyRepository, PostgresApiKeyRepository>();
        services.AddScoped<ICorrelationContextAccessor, CorrelationContextAccessor>();
        services.AddScoped<IGetWeatherForecastUseCase, GetWeatherForecastUseCase>();
        services.AddScoped<IWeatherForecastRepository, PostgresWeatherForecastRepository>();
        services.AddScoped<IOutboxStore, EfOutboxStore>();
        services.AddScoped<IIntegrationUnitOfWork, EfIntegrationUnitOfWork>();
        services.AddScoped<IIntegrationEventHandler<WeatherForecastRequestedEvent>, WeatherForecastRequestedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<WeatherForecastGeneratedEvent>, WeatherForecastGeneratedEventHandler>();

        return services;
    }
}
