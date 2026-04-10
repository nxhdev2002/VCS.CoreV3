namespace VCS.CoreV3.Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string[] Brokers { get; set; } = ["localhost:9092"];
    public string Topic { get; set; } = "vcs.events";
    public string ConsumerGroupId { get; set; } = "vcs-core-v3";
    public string ProducerName { get; set; } = "vcs-producer";
    public int WorkersCount { get; set; } = 3;
    public int BufferSize { get; set; } = 100;
}
