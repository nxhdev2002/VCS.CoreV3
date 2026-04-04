using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
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
        services.AddScoped<IIntegrationEventHandler<ApiKeyCreatedEvent>, ApiKeyCreatedEventHandler>();

        return services;
    }

    public static IServiceCollection AddOpenApiService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((doc, context, cancellationToken) =>
            {
                doc.Info = new OpenApiInfo
                {
                    Title = "VCS CoreV3 API",
                    Version = "v1",
                    Description = "API for VCS CoreV3 — Hexagonal Architecture with Event Sourcing"
                };

                doc.Components ??= new OpenApiComponents();
                doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                doc.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "X-Api-Key",
                    Description = "API key required to access protected endpoints"
                };

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
