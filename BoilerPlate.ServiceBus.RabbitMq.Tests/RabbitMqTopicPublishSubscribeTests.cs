using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.RabbitMq.Configuration;
using Testcontainers.RabbitMq;
using BoilerPlate.ServiceBus.RabbitMq.Connection;
using BoilerPlate.ServiceBus.RabbitMq.Resolvers;
using BoilerPlate.ServiceBus.RabbitMq.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BoilerPlate.ServiceBus.RabbitMq.Tests;

/// <summary>
///     Integration tests verifying publish and subscribe flow with RabbitMQ 7.x
/// </summary>
public class RabbitMqTopicPublishSubscribeTests : IAsyncLifetime
{
    private RabbitMqContainer _rabbitMq = null!;
    private RabbitMqConnectionManager _connectionManager = null!;
    private RabbitMqTopicPublisher _publisher = null!;
    private RabbitMqTopicSubscriber<TestMessage> _subscriber = null!;

    public async Task InitializeAsync()
    {
        _rabbitMq = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .Build();
        await _rabbitMq.StartAsync();

        var connectionString = _rabbitMq.GetConnectionString();
        var options = Options.Create(new RabbitMqOptions { ConnectionString = connectionString });
        _connectionManager = new RabbitMqConnectionManager(
            options,
            NullLogger<RabbitMqConnectionManager>.Instance);

        var topicResolver = new RabbitMqTopicNameResolver(new DefaultTopicNameResolver());
        _publisher = new RabbitMqTopicPublisher(
            _connectionManager,
            topicResolver,
            NullLogger<RabbitMqTopicPublisher>.Instance);
        _subscriber = new RabbitMqTopicSubscriber<TestMessage>(
            _connectionManager,
            topicResolver,
            NullLogger<RabbitMqTopicSubscriber<TestMessage>>.Instance);
    }

    public async Task DisposeAsync()
    {
        _subscriber?.Dispose();
        await _connectionManager.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }

    [Fact]
    public async Task Publish_ThenSubscribe_ShouldReceiveMessage()
    {
        TestMessage? receivedMessage = null;
        var receivedTcs = new TaskCompletionSource<bool>();

        await _subscriber.SubscribeAsync(async (msg, metadata, ct) =>
        {
            receivedMessage = msg;
            receivedTcs.TrySetResult(true);
            await Task.CompletedTask;
        }, maxFailureCount: 3, onPermanentFailure: null);

        await _publisher.PublishAsync(new TestMessage
        {
            TraceId = "publish-subscribe-test",
            ReferenceId = "ref-001",
            CreatedTimestamp = DateTime.UtcNow,
            Payload = "hello-from-publisher"
        });

        var received = await Task.WhenAny(receivedTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        received.Should().Be(receivedTcs.Task, "message should be received within 5 seconds");

        receivedMessage.Should().NotBeNull();
        receivedMessage!.Payload.Should().Be("hello-from-publisher");
        receivedMessage.TraceId.Should().Be("publish-subscribe-test");

        await _subscriber.UnsubscribeAsync();
    }
}
