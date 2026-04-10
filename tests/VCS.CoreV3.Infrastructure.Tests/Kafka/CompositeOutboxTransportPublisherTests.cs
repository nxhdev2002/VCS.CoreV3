using System.Threading;
using System.Threading.Tasks;
using VCS.CoreV3.Infrastructure.Kafka;
using VCS.CoreV3.Ports;
using Xunit;

namespace VCS.CoreV3.Infrastructure.Tests.Kafka;

public sealed class CompositeOutboxTransportPublisherTests
{
    [Fact]
    public async Task PublishAsync_AlwaysPublishesToRedisPublisher()
    {
        var redisRecorder = new RecordingPublisher();
        var kafkaRecorder = new RecordingPublisher();
        var sut = new CompositeOutboxTransportPublisher(redisRecorder, kafkaRecorder);

        await sut.PublishAsync(MakeEvent());

        Assert.Equal(1, redisRecorder.PublishCount);
    }

    [Fact]
    public async Task PublishAsync_AlwaysPublishesToKafkaPublisher()
    {
        var redisRecorder = new RecordingPublisher();
        var kafkaRecorder = new RecordingPublisher();
        var sut = new CompositeOutboxTransportPublisher(redisRecorder, kafkaRecorder);

        await sut.PublishAsync(MakeEvent());

        Assert.Equal(1, kafkaRecorder.PublishCount);
    }

    [Fact]
    public async Task PublishAsync_PublishesIdenticalEventToBothTransports()
    {
        var redisRecorder = new RecordingPublisher();
        var kafkaRecorder = new RecordingPublisher();
        var sut = new CompositeOutboxTransportPublisher(redisRecorder, kafkaRecorder);

        await sut.PublishAsync(MakeEvent());

        Assert.Equal(EventTypes.WeatherForecastRequested, redisRecorder.LastEventType);
        Assert.Equal(EventTypes.WeatherForecastRequested, kafkaRecorder.LastEventType);
    }

    private static IntegrationEvent<WeatherForecastRequestedEvent> MakeEvent() =>
        new(EventTypes.WeatherForecastRequested, new WeatherForecastRequestedEvent("GET", "/weather"), "corr-1");

    private sealed class RecordingPublisher : IOutboxTransportPublisher
    {
        public int PublishCount { get; private set; }
        public string? LastEventType { get; private set; }

        public Task PublishAsync<TPayload>(IntegrationEvent<TPayload> evt, CancellationToken ct = default)
        {
            PublishCount++;
            LastEventType = evt.EventType;
            return Task.CompletedTask;
        }
    }
}
