using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure.Redis;

public sealed class CorrelationContextAccessor : ICorrelationContextAccessor
{
    public string? CorrelationId { get; set; }
}