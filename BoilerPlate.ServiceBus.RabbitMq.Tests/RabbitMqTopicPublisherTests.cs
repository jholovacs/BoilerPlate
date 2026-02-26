using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.RabbitMq.Configuration;
using Testcontainers.RabbitMq;
using BoilerPlate.ServiceBus.RabbitMq.Connection;
using BoilerPlate.ServiceBus.RabbitMq.Resolvers;
using BoilerPlate.ServiceBus.RabbitMq.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BoilerPlate.ServiceBus.RabbitMq.Tests;

/// <summary>
///     Integration tests for RabbitMqTopicPublisher using Testcontainers
/// </summary>
public class RabbitMqTopicPublisherTests : IAsyncLifetime
{
    private RabbitMqContainer _rabbitMq = null!;
    private RabbitMqConnectionManager _connectionManager = null!;
    private RabbitMqTopicPublisher _publisher = null!;

    public async Task InitializeAsync()
    {
        _rabbitMq = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .Build();
        await _rabbitMq.StartAsync();

        var connectionString = _rabbitMq.GetConnectionString();
        var options = Options.Create(new RabbitMqOptions { ConnectionString = connectionString });
        var logger = NullLogger<RabbitMqConnectionManager>.Instance;
        _connectionManager = new RabbitMqConnectionManager(options, logger);

        var topicResolver = new RabbitMqTopicNameResolver(new DefaultTopicNameResolver());
        var publisherLogger = NullLogger<RabbitMqTopicPublisher>.Instance;
        _publisher = new RabbitMqTopicPublisher(_connectionManager, topicResolver, publisherLogger);
    }

    public async Task DisposeAsync()
    {
        await _connectionManager.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }

    [Fact]
    public async Task PublishAsync_WithMessage_ShouldNotThrow()
    {
        var message = new TestMessage
        {
            TraceId = "trace-123",
            ReferenceId = "ref-456",
            CreatedTimestamp = DateTime.UtcNow,
            Payload = "test-payload"
        };

        var act = async () => await _publisher.PublishAsync(message);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithMessageAndMetadata_ShouldNotThrow()
    {
        var message = new TestMessage { TraceId = "trace-789", Payload = "with-metadata" };
        var metadata = new Dictionary<string, object> { ["CustomHeader"] = "value" };

        var act = async () => await _publisher.PublishAsync(message, metadata);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        var message = new TestMessage { Payload = "cancelled" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _publisher.PublishAsync(message, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
