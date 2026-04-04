namespace VCS.CoreV3.Ports;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string Plan { get; }
    int RateLimit { get; }
}
