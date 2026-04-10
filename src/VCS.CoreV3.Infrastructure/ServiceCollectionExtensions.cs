using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using KafkaFlow;
using KafkaFlow.Serializer;
using KafkaFlow.TypedHandler;
using StackExchange.Redis;
using VCS.CoreV3.Application;
using VCS.CoreV3.Infrastructure.Data;
using VCS.CoreV3.Infrastructure.InternalService;
using VCS.CoreV3.Infrastructure.Kafka;
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

        services.AddSingleton(TimeProvider.System);

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

        // Transport: Redis Streams (existing)
        services.AddSingleton<RedisStreamEventPublisher>();

        // Transport: KafkaFlow
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<KafkaFlowTransportPublisher>();

        // Transport-specific dispatchers (keyed by transport name)
        services.AddKeyedSingleton<IIntegrationEventDispatcher>("redis", (sp, _) =>
            new IntegrationEventDispatcher(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IIntegrationEventSerializer>(),
                typeof(IRedisEvent)));
        services.AddKeyedSingleton<IIntegrationEventDispatcher>("kafka", (sp, _) =>
            new IntegrationEventDispatcher(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IIntegrationEventSerializer>(),
                typeof(IKafkaEvent)));

        // Composite publisher — dual-publishes to both transports
        services.AddSingleton<IOutboxTransportPublisher>(sp =>
            new CompositeOutboxTransportPublisher(
                sp.GetRequiredService<RedisStreamEventPublisher>(),
                sp.GetRequiredService<KafkaFlowTransportPublisher>()));

        // KafkaFlow cluster
        services.AddKafka(kafka => kafka
            .AddCluster(cluster =>
            {
                var kafkaOptions = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>()
                    ?? new KafkaOptions();

                cluster
                    .WithBrokers(kafkaOptions.Brokers)
                    .AddProducer(kafkaOptions.ProducerName, producer => producer
                        .DefaultTopic(kafkaOptions.Topic)
                        .AddMiddlewares(middlewares => middlewares
                            .AddSerializer<JsonCoreSerializer>()))
                    .AddConsumer(consumer => consumer
                        .Topic(kafkaOptions.Topic)
                        .WithGroupId(kafkaOptions.ConsumerGroupId)
                        .WithBufferSize(kafkaOptions.BufferSize)
                        .WithWorkersCount(kafkaOptions.WorkersCount)
                        .AddMiddlewares(middlewares => middlewares
                            .AddDeserializer<JsonCoreDeserializer>()
                            .AddTypedHandlers(handlers => handlers
                                .WithHandlerLifetime(InstanceLifetime.Scoped)
                                .AddHandler<KafkaIntegrationEventHandler>())));
            }));

        services.AddScoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddHostedService<RedisStreamConsumerWorker>();
        services.AddHostedService<OutboxDispatcherWorker>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<IApiKeyRepository, PostgresApiKeyRepository>();
        services.AddScoped<ICorrelationContextAccessor, CorrelationContextAccessor>();
        services.AddScoped<IGetWeatherForecastUseCase, GetWeatherForecastUseCase>();
        services.AddScoped<ICreateApiKeyUseCase, CreateApiKeyUseCase>();
        services.AddScoped<IWeatherForecastRepository, PostgresWeatherForecastRepository>();
        services.AddScoped<IOutboxStore, EfOutboxStore>();
        services.AddScoped<IIntegrationUnitOfWork, EfIntegrationUnitOfWork>();
        services.AddScoped<IIntegrationEventHandler<WeatherForecastRequestedEvent>, WeatherForecastRequestedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<WeatherForecastGeneratedEvent>, WeatherForecastGeneratedEventHandler>();
        services.AddScoped<IIntegrationEventHandler<ApiKeyCreatedEvent>, ApiKeyCreatedEventHandler>();

        return services;
    }

    public static IServiceCollection AddInternalApiAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<InternalApiOptions>(configuration.GetSection(InternalApiOptions.SectionName));
        services.AddSingleton<IInternalServiceKeyValidator, InternalServiceKeyValidator>();
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
