using System.Threading.Tasks;
using KafkaFlow;
using KafkaFlow.Producers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using VCS.CoreV3.Infrastructure.Kafka;
using VCS.CoreV3.Infrastructure.Redis;
using VCS.CoreV3.Ports;
using Xunit;

namespace VCS.CoreV3.Infrastructure.Tests.Kafka;

public sealed class KafkaFlowTransportPublisherTests
{
    [Fact]
    public async Task PublishAsync_WithValidEvent_InvokesProducer()
    {
        var (sut, producer) = BuildSut();

        await sut.PublishAsync(MakeEvent());

        await producer.Received(1).ProduceAsync(Arg.Any<object>(), Arg.Any<object>());
    }

    [Fact]
    public async Task PublishAsync_MapsEventTypeAndCorrelationIdIntoMessage()
    {
        var (sut, producer) = BuildSut();
        KafkaIntegrationEventMessage? captured = null;

        producer
            .When(p => p.ProduceAsync(Arg.Any<object>(), Arg.Any<object>()))
            .Do(ci => captured = ci.ArgAt<object>(1) as KafkaIntegrationEventMessage);

        await sut.PublishAsync(MakeEvent());

        Assert.NotNull(captured);
        Assert.Equal(EventTypes.WeatherForecastRequested, captured.EventType);
        Assert.Equal("corr-1", captured.CorrelationId);
    }

    private static (KafkaFlowTransportPublisher sut, IMessageProducer producer) BuildSut()
    {
        var producer = Substitute.For<IMessageProducer>();
        var producerAccessor = Substitute.For<IProducerAccessor>();
        producerAccessor.GetProducer("vcs-producer").Returns(producer);

        var options = Options.Create(new KafkaOptions { Topic = "vcs.events", ProducerName = "vcs-producer" });
        var serializer = new SystemTextJsonIntegrationEventSerializer();
        var sut = new KafkaFlowTransportPublisher(
            producerAccessor,
            serializer,
            options,
            NullLogger<KafkaFlowTransportPublisher>.Instance);

        return (sut, producer);
    }

    private static IntegrationEvent<WeatherForecastRequestedEvent> MakeEvent() =>
        new(EventTypes.WeatherForecastRequested, new WeatherForecastRequestedEvent("GET", "/weather"), "corr-1");
}
