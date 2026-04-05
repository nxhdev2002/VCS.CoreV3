namespace VCS.CoreV3.Infrastructure.InternalService;

public sealed class InternalApiOptions
{
    public const string SectionName = "InternalApi";

    public Dictionary<string, string> AllowedServices { get; set; } = [];
}
