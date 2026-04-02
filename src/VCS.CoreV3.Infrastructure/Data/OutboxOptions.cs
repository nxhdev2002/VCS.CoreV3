namespace VCS.CoreV3.Infrastructure.Data;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 20;
    public int PollIntervalMilliseconds { get; set; } = 2000;
    public int MaxRetries { get; set; } = 5;
}