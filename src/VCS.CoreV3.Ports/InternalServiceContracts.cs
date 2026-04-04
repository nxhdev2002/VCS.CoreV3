namespace VCS.CoreV3.Ports;

public static class InternalServiceHeaders
{
    public const string ServiceName = "X-Service-Name";
    public const string ServiceKey = "X-Service-Key";
}

public interface IInternalServiceKeyValidator
{
    bool Validate(string serviceName, string serviceKey);
}
