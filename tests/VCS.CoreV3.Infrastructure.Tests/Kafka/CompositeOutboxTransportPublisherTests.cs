using System.Threading;
using System.Threading.Tasks;
using VCS.CoreV3.Infrastructure.Kafka;
using VCS.CoreV3.Ports;
using Xunit;

namespace VCS.CoreV3.Infrastructure.Tests.Kafka;

public sealed class CompositeOutboxTransportPublisherTests
{
    [Fact]
    public async Task PublishAsync_WithRedisOnlyEvent_PublishesToRedisOnly()
    {
        // WeatherForecastRequestedEvent : IRedisEvent
        var redisRecorder = new RecordingPublisher();
        var kafkaRecorder = new RecordingPublisher();
        var sut = new CompositeOutboxTransportPublisher(redisRecorder, kafkaRecorder);

        await sut.PublishAsync(new IntegrationEvent<WeatherForecastRequestedEvent>(
            EventTypes.WeatherForecastRequested, new WeatherForecastRequestedEvent("GET", "/weather"), "corr-1"));

        Assert.Equal(1, redisRecorder.PublishCount);
        Assert.Equal(0, kafkaRecorder.PublishCount);
    }

    [Fact]
    public async Task PublishAsync_WithKafkaOnlyEvent_PublishesToKafkaOnly()
    {
        // WeatherForecastGeneratedEvent : IKafkaEvent
        var redisRecorder = new RecordingPublisher();
        var kafkaRecorder = new RecordingPublisher();
        var sut = new CompositeOutboxTransportPublisher(redisRecorder, kafkaRecorder);

        await sut.PublishAsync(new IntegrationEvent<WeatherForecastGeneratedEvent>(
            EventTypes.WeatherForecastGenerated, new WeatherForecastGeneratedEvent(5, 22.5), "corr-2"));

        Assert.Equal(0, redisRecorder.PublishCount);
        Assert.Equal(1, kafkaRecorder.PublishCount);
    }

    [Fact]
    public async Task PublishAsync_WithBothTransportEvent_PublishesToBothTransports()
    {
        // DualEvent implements both IRedisEvent and IKafkaEvent
        var redisRecorder = new RecordingPublisher();
        var kafkaRecorder = new RecordingPublisher();
        var sut = new CompositeOutboxTransportPublisher(redisRecorder, kafkaRecorder);

        await sut.PublishAsync(new IntegrationEvent<DualTransportEvent>(
            "dual.event.v1", new DualTransportEvent(), "corr-3"));

        Assert.Equal(1, redisRecorder.PublishCount);
        Assert.Equal(1, kafkaRecorder.PublishCount);
    }

    private sealed record DualTransportEvent : IRedisEvent, IKafkaEvent;

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
