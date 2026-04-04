using Scalar.AspNetCore;
using VCS.CoreV3.Adapters.Web;
using VCS.CoreV3.Infrastructure;
using VCS.CoreV3.Infrastructure.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApiService(builder.Configuration);
builder.Services.AddHexagonalArchitecture(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis");

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options => options
    .WithTitle("VCS CoreV3 API")
    .AddPreferredSecuritySchemes("ApiKey")
    .AddApiKeyAuthentication("ApiKey", apiKey =>
    {
        apiKey.Value = string.Empty;
    })
);

app.UseHttpsRedirection();
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapWeatherForecastEndpoint();
app.MapHealthChecks("/health");

await app.RunAsync();
