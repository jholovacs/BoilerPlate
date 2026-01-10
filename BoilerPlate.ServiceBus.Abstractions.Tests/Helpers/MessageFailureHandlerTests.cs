using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Helpers;

/// <summary>
/// Unit tests for MessageFailureHandler
/// </summary>
public class MessageFailureHandlerTests
{
    private readonly Mock<ILogger> _loggerMock;

    public MessageFailureHandlerTests()
    {
        _loggerMock = new Mock<ILogger>();
    }

    #region Success Handling Tests

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync resets the message failure count to zero when the handler succeeds.
    /// Verifies that:
    /// - The failure count is reset to 0 after successful processing
    /// - ShouldDestroy returns false indicating the message should not be destroyed
    /// - Previous failure counts are cleared on success
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_WhenHandlerSucceeds_ShouldResetFailureCount()
    {
        // Arrange
        var message = new TestMessage { FailureCount = 5 };
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, metadata, ct) => Task.CompletedTask);

        // Act
        var shouldDestroy = await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: null,
            logger: _loggerMock.Object);

        // Assert
        shouldDestroy.Should().BeFalse();
        message.FailureCount.Should().Be(0);
    }

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync returns false (do not destroy) when the handler succeeds.
    /// Verifies that:
    /// - ShouldDestroy is false after successful processing
    /// - The message is not marked for destruction on success
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_WhenHandlerSucceeds_ShouldNotDestroyMessage()
    {
        // Arrange
        var message = new TestMessage();
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, metadata, ct) => Task.CompletedTask);

        // Act
        var shouldDestroy = await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: null,
            logger: _loggerMock.Object);

        // Assert
        shouldDestroy.Should().BeFalse();
    }

    #endregion

    #region Temporary Failure Tests

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync increments the failure count when the handler fails but hasn't exceeded the maximum.
    /// Verifies that:
    /// - The failure count is incremented by one on each failure
    /// - ShouldDestroy returns false when below the maximum failure count
    /// - The message is still eligible for retry
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_WhenHandlerFailsBelowMaxCount_ShouldIncrementFailureCount()
    {
        // Arrange
        var message = new TestMessage { FailureCount = 1 };
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, metadata, ct) => throw new InvalidOperationException("Test error"));

        // Act
        var shouldDestroy = await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: null,
            logger: _loggerMock.Object);

        // Assert
        shouldDestroy.Should().BeFalse();
        message.FailureCount.Should().Be(2);
    }

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync returns false (do not destroy) when failures are below the maximum count.
    /// Verifies that:
    /// - ShouldDestroy is false when failure count hasn't exceeded the maximum
    /// - The message should be retried rather than destroyed
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_WhenHandlerFailsBelowMaxCount_ShouldNotDestroyMessage()
    {
        // Arrange
        var message = new TestMessage { FailureCount = 0 };
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, metadata, ct) => throw new InvalidOperationException("Test error"));

        // Act
        var shouldDestroy = await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: null,
            logger: _loggerMock.Object);

        // Assert
        shouldDestroy.Should().BeFalse();
    }

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync logs a warning when the handler fails.
    /// Verifies that:
    /// - A warning log entry is created with the message "Message processing failed"
    /// - The log includes exception details
    /// - Failure events are properly logged for monitoring
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_WhenHandlerFails_ShouldLogWarning()
    {
        // Arrange
        var message = new TestMessage { TraceId = "trace-123", ReferenceId = "ref-456" };
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, metadata, ct) => throw new InvalidOperationException("Test error"));

        // Act
        await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: null,
            logger: _loggerMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Message processing failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Permanent Failure Tests

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync returns true (destroy message) when the failure count exceeds the maximum.
    /// Verifies that:
    /// - ShouldDestroy is true when failure count exceeds maxFailureCount
    /// - The failure count is still incremented even when exceeding the maximum
    /// - The message is marked for permanent destruction
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_WhenHandlerFailsExceedsMaxCount_ShouldReturnTrue()
    {
        // Arrange
        var message = new TestMessage { FailureCount = 3 };
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, metadata, ct) => throw new InvalidOperationException("Test error"));

        // Act
        var shouldDestroy = await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: null,
            logger: _loggerMock.Object);

        // Assert
        shouldDestroy.Should().BeTrue();
        message.FailureCount.Should().Be(4);
    }

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync logs an error when the failure count exceeds the maximum.
    /// Verifies that:
    /// - An error log entry is created with the message "permanently failed"
    /// - Permanent failures are logged at error level
    /// - The log includes exception details for permanent failures
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_WhenHandlerFailsExceedsMaxCount_ShouldLogError()
    {
        // Arrange
        var message = new TestMessage { TraceId = "trace-123", ReferenceId = "ref-456", FailureCount = 1 };
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, metadata, ct) => throw new InvalidOperationException("Test error"));

        // Act
        await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 1,
            onPermanentFailure: null,
            logger: _loggerMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("permanently failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync invokes the permanent failure callback when the failure count exceeds the maximum.
    /// Verifies that:
    /// - The callback is invoked with the failed message and exception
    /// - The callback receives the correct message and exception details
    /// - Permanent failure handlers can perform custom actions (e.g., dead letter queue)
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_WhenPermanentFailure_ShouldInvokeCallback()
    {
        // Arrange
        var message = new TestMessage { FailureCount = 3 };
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, metadata, ct) => throw new InvalidOperationException("Test error"));
        
        Exception? capturedException = null;
        TestMessage? capturedMessage = null;
        var callback = new Func<TestMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, ex, metadata, ct) =>
            {
                capturedMessage = msg;
                capturedException = ex;
                return Task.CompletedTask;
            });

        // Act
        await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: callback,
            logger: _loggerMock.Object);

        // Assert
        capturedMessage.Should().Be(message);
        capturedException.Should().NotBeNull();
        capturedException!.Message.Should().Be("Test error");
    }

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync handles exceptions thrown by the permanent failure callback gracefully.
    /// Verifies that:
    /// - Exceptions in the callback are caught and logged as errors
    /// - ShouldDestroy still returns true even if the callback throws
    /// - Callback failures do not prevent message destruction
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_WhenCallbackThrows_ShouldLogErrorAndContinue()
    {
        // Arrange
        var message = new TestMessage { FailureCount = 3 };
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, metadata, ct) => throw new InvalidOperationException("Test error"));
        
        var callback = new Func<TestMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, ex, metadata, ct) => throw new Exception("Callback error"));

        // Act
        var shouldDestroy = await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: callback,
            logger: _loggerMock.Object);

        // Assert
        shouldDestroy.Should().BeTrue();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error in permanent failure callback")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Metadata Tests

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync passes metadata to the message handler.
    /// Verifies that:
    /// - Metadata provided to ProcessWithFailureHandlingAsync is passed to the handler
    /// - The same metadata dictionary instance is passed through
    /// - Metadata is available for handler processing
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_ShouldPassMetadataToHandler()
    {
        // Arrange
        var message = new TestMessage();
        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        IDictionary<string, object>? capturedMetadata = null;
        
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, meta, ct) =>
            {
                capturedMetadata = meta;
                return Task.CompletedTask;
            });

        // Act
        await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            metadata,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: null,
            logger: _loggerMock.Object);

        // Assert
        capturedMetadata.Should().BeSameAs(metadata);
    }

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync passes metadata to the permanent failure callback.
    /// Verifies that:
    /// - Metadata is passed to the callback when a permanent failure occurs
    /// - The same metadata dictionary instance is passed to the callback
    /// - Metadata is available for callback processing (e.g., dead letter queue)
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_ShouldPassMetadataToCallback()
    {
        // Arrange
        var message = new TestMessage { FailureCount = 3 };
        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, meta, ct) => throw new InvalidOperationException("Test error"));
        
        IDictionary<string, object>? capturedMetadata = null;
        var callback = new Func<TestMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, ex, meta, ct) =>
            {
                capturedMetadata = meta;
                return Task.CompletedTask;
            });

        // Act
        await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            metadata,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: callback,
            logger: _loggerMock.Object);

        // Assert
        capturedMetadata.Should().BeSameAs(metadata);
    }

    #endregion

    #region Cancellation Tests

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync passes the cancellation token to the message handler.
    /// Verifies that:
    /// - The cancellation token provided is passed to the handler
    /// - Handlers can respect cancellation requests
    /// - Token propagation enables cooperative cancellation
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_ShouldPassCancellationTokenToHandler()
    {
        // Arrange
        var message = new TestMessage();
        var cts = new CancellationTokenSource();
        CancellationToken capturedToken = default;
        
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            (msg, meta, ct) =>
            {
                capturedToken = ct;
                return Task.CompletedTask;
            });

        // Act
        await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: null,
            logger: _loggerMock.Object,
            cancellationToken: cts.Token);

        // Assert
        capturedToken.Should().Be(cts.Token);
    }

    /// <summary>
    /// Tests that ProcessWithFailureHandlingAsync treats OperationCanceledException as a failure when cancellation is requested.
    /// Verifies that:
    /// - Cancellation exceptions are caught and treated as processing failures
    /// - The failure count is incremented when cancellation occurs
    /// - ShouldDestroy returns false (treat cancellation as a retryable failure, not permanent)
    /// </summary>
    [Fact]
    public async Task ProcessWithFailureHandlingAsync_WhenCancellationRequested_ShouldHandleAsFailure()
    {
        // Arrange
        var message = new TestMessage();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel after 50ms
        
        var handler = new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>(
            async (msg, meta, ct) =>
            {
                // Simulate work that will be cancelled
                await Task.Delay(200, ct); // This will throw OperationCanceledException when cancelled
            });

        // Act
        var shouldDestroy = await MessageFailureHandler.ProcessWithFailureHandlingAsync(
            message,
            null,
            handler,
            maxFailureCount: 3,
            onPermanentFailure: null,
            logger: _loggerMock.Object,
            cancellationToken: cts.Token);

        // Assert
        // Cancellation exceptions are caught and treated as failures
        shouldDestroy.Should().BeFalse();
        message.FailureCount.Should().Be(1);
    }

    #endregion
}
