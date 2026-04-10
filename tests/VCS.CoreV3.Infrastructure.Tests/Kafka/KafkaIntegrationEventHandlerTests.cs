using System;
using System.Threading;
using System.Threading.Tasks;
using KafkaFlow;
using NSubstitute;
using VCS.CoreV3.Infrastructure.Kafka;
using VCS.CoreV3.Ports;
using Xunit;
namespace VCS.CoreV3.Infrastructure.Tests.Kafka;

public sealed class KafkaIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_WithValidMessage_DelegatesDispatchToDispatcher()
    {
        var dispatcher = Substitute.For<IIntegrationEventDispatcher>();
        var sut = new KafkaIntegrationEventHandler(dispatcher);
        var context = Substitute.For<IMessageContext>();
        var message = new KafkaIntegrationEventMessage
        {
            MessageId = "msg-1",
            EventType = EventTypes.WeatherForecastRequested,
            CorrelationId = "corr-1",
            SchemaVersion = 1,
            Payload = "{}",
            OccurredAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            RetryCount = 0
        };

        await sut.Handle(context, message);

        await dispatcher.Received(1).DispatchAsync(
            message.EventType,
            message.Payload,
            message.MessageId,
            message.CorrelationId,
            message.SchemaVersion,
            message.OccurredAtUtc,
            message.RetryCount,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithZeroRetryCount_PassesRetryCountToDispatcher()
    {
        var dispatcher = Substitute.For<IIntegrationEventDispatcher>();
        var sut = new KafkaIntegrationEventHandler(dispatcher);
        var context = Substitute.For<IMessageContext>();
        var message = new KafkaIntegrationEventMessage
        {
            MessageId = "msg-2",
            EventType = EventTypes.ApiKeyCreated,
            CorrelationId = "corr-2",
            SchemaVersion = 1,
            Payload = "{}",
            OccurredAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            RetryCount = 0
        };

        await sut.Handle(context, message);

        await dispatcher.Received(1).DispatchAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<DateTime>(),
            Arg.Is<int>(r => r == 0),
            Arg.Any<CancellationToken>());
    }
}
