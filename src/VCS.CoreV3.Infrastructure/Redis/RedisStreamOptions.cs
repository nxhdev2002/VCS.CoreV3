namespace VCS.CoreV3.Infrastructure.Redis;

public sealed class RedisStreamOptions
{
    public const string SectionName = "RedisStreams";

    public string StreamName { get; set; } = "vcs.events";
    public string DeadLetterStreamName { get; set; } = "vcs.events.dlq";
    public string ConsumerGroup { get; set; } = "vcs-core-v3";
    public string ConsumerNamePrefix { get; set; } = "api";
    public int ReadBatchSize { get; set; } = 10;
    public int BlockMilliseconds { get; set; } = 2000;
    public int MaxRetries { get; set; } = 3;
    public int MaxStreamLength { get; set; } = 10000;
}