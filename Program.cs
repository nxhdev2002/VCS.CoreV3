using VCS.CoreV3.Adapters.Web;
using VCS.CoreV3.Infrastructure;
using VCS.CoreV3.Infrastructure.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHexagonalArchitecture(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapWeatherForecastEndpoint();
app.MapHealthChecks("/health");

await app.RunAsync();
