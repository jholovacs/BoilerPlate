using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Implementations.Null;

/// <summary>
/// Unit tests for NullQueuePublisher
/// </summary>
public class NullQueuePublisherTests
{
    private readonly NullQueuePublisher _publisher;

    public NullQueuePublisherTests()
    {
        _publisher = new NullQueuePublisher();
    }

    /// <summary>
    /// Tests that NullQueuePublisher.PublishAsync completes successfully without throwing exceptions.
    /// Verifies that:
    /// - The method executes without errors (no-op implementation)
    /// - Messages can be "published" without actually being sent anywhere
    /// </summary>
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

    /// <summary>
    /// Tests that NullQueuePublisher.PublishAsync accepts messages with metadata and completes successfully.
    /// Verifies that:
    /// - The method accepts both message and metadata parameters
    /// - The method executes without errors (no-op implementation)
    /// </summary>
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

    /// <summary>
    /// Tests that NullQueuePublisher.PublishAsync returns Task.CompletedTask immediately.
    /// Verifies that:
    /// - The returned task is already completed
    /// - No actual work is performed (instant return)
    /// </summary>
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

    /// <summary>
    /// Tests that NullQueuePublisher.PublishAsync with metadata returns Task.CompletedTask immediately.
    /// Verifies that:
    /// - The returned task is already completed even with metadata
    /// - No actual work is performed (instant return)
    /// </summary>
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

    /// <summary>
    /// Tests that NullQueuePublisher.PublishAsync handles cancelled cancellation tokens gracefully.
    /// Verifies that:
    /// - Cancelled tokens do not cause exceptions
    /// - The method completes successfully even when cancellation is requested (no-op behavior)
    /// </summary>
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
