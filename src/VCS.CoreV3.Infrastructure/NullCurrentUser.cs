using VCS.CoreV3.Ports;

namespace VCS.CoreV3.Infrastructure;

public sealed class NullCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;
    public Guid UserId => Guid.Empty;
    public string Plan => string.Empty;
    public int RateLimit => 0;
}
