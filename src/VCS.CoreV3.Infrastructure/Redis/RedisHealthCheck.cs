using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace VCS.CoreV3.Infrastructure.Redis;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_connectionMultiplexer.IsConnected)
        {
            return HealthCheckResult.Unhealthy("Redis connection is not available.");
        }

        var database = _connectionMultiplexer.GetDatabase();
        var ping = await database.PingAsync().ConfigureAwait(false);
        return HealthCheckResult.Healthy($"Redis ping succeeded in {ping.TotalMilliseconds:F0}ms.");
    }
}