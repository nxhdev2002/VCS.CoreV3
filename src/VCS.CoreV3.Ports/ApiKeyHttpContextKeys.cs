namespace VCS.CoreV3.Ports;

public static class ApiKeyHttpContextKeys
{
    public const string ResolvedEntity = "ResolvedApiKey";
}

public static class ApiKeyDefaults
{
    public const int DefaultFreeRateLimit = 100;
}
