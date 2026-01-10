using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Implementations.Null;

/// <summary>
/// Unit tests for NullTopicPublisher
/// </summary>
public class NullTopicPublisherTests
{
    private readonly NullTopicPublisher _publisher;

    public NullTopicPublisherTests()
    {
        _publisher = new NullTopicPublisher();
    }

    [Fact]
    public async Task PublishAsync_WithMessage_ShouldCompleteSuccessfully()
    {
        // Arrange
        var message = new TestMessage
        {
            TraceId = "trace-123",
            ReferenceId = "ref-456",
            CreatedTimestamp = DateTime.UtcNow
        };

        // Act
        var act = async () => await _publisher.PublishAsync(message);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithMessageAndMetadata_ShouldCompleteSuccessfully()
    {
        // Arrange
        var message = new TestMessage { TraceId = "trace-123" };
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var act = async () => await _publisher.PublishAsync(message, metadata);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithMessage_ShouldReturnCompletedTask()
    {
        // Arrange
        var message = new TestMessage();

        // Act
        var task = _publisher.PublishAsync(message);

        // Assert
        task.Should().Be(Task.CompletedTask);
        await task;
    }

    [Fact]
    public async Task PublishAsync_WithMessageAndMetadata_ShouldReturnCompletedTask()
    {
        // Arrange
        var message = new TestMessage();
        var metadata = new Dictionary<string, object>();

        // Act
        var task = _publisher.PublishAsync(message, metadata);

        // Assert
        task.Should().Be(Task.CompletedTask);
        await task;
    }

    [Fact]
    public async Task PublishAsync_WithCancellationToken_ShouldNotThrow()
    {
        // Arrange
        var message = new TestMessage();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _publisher.PublishAsync(message, cts.Token);

        // Assert
        // Even with cancelled token, null publisher should complete (no-op)
        await act.Should().NotThrowAsync();
    }
}
