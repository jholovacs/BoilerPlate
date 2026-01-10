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
