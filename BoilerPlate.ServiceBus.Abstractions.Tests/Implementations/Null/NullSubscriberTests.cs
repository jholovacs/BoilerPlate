using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Implementations.Null;

/// <summary>
///     Unit tests for NullTopicSubscriber and NullQueueSubscriber
/// </summary>
public class NullSubscriberTests
{
    /// <summary>
    ///     Tests that NullTopicSubscriber.SubscribeAsync completes successfully without throwing exceptions.
    ///     Verifies that:
    ///     - The subscription operation completes successfully (no-op implementation)
    ///     - No actual subscription is performed
    /// </summary>
    [Fact]
    public async Task NullTopicSubscriber_SubscribeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var subscriber = new NullTopicSubscriber<TestMessage>();
        var handler =
            new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>((msg, metadata, ct) =>
                Task.CompletedTask);

        // Act
        var act = async () => await subscriber.SubscribeAsync(handler, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    ///     Tests that NullTopicSubscriber.SubscribeAsync with failure handling completes successfully.
    ///     Verifies that:
    ///     - The subscription operation accepts failure handling parameters
    ///     - The method executes without errors (no-op implementation)
    /// </summary>
    [Fact]
    public async Task NullTopicSubscriber_SubscribeAsync_WithFailureHandling_ShouldCompleteSuccessfully()
    {
        // Arrange
        var subscriber = new NullTopicSubscriber<TestMessage>();
        var handler =
            new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>((msg, metadata, ct) =>
                Task.CompletedTask);
        var onPermanentFailure =
            new Func<TestMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>((msg, ex, metadata,
                ct) => Task.CompletedTask);

        // Act
        var act = async () => await subscriber.SubscribeAsync(handler, 3, onPermanentFailure);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    ///     Tests that NullTopicSubscriber.UnsubscribeAsync completes successfully without throwing exceptions.
    ///     Verifies that:
    ///     - The unsubscribe operation completes successfully (no-op implementation)
    ///     - No actual unsubscription is performed
    /// </summary>
    [Fact]
    public async Task NullTopicSubscriber_UnsubscribeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var subscriber = new NullTopicSubscriber<TestMessage>();

        // Act
        var act = async () => await subscriber.UnsubscribeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    ///     Tests that NullQueueSubscriber.SubscribeAsync completes successfully without throwing exceptions.
    ///     Verifies that:
    ///     - The subscription operation completes successfully (no-op implementation)
    ///     - No actual subscription is performed
    /// </summary>
    [Fact]
    public async Task NullQueueSubscriber_SubscribeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var subscriber = new NullQueueSubscriber<TestMessage>();
        var handler =
            new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>((msg, metadata, ct) =>
                Task.CompletedTask);

        // Act
        var act = async () => await subscriber.SubscribeAsync(handler, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    ///     Tests that NullQueueSubscriber.SubscribeAsync with failure handling completes successfully.
    ///     Verifies that:
    ///     - The subscription operation accepts failure handling parameters
    ///     - The method executes without errors (no-op implementation)
    /// </summary>
    [Fact]
    public async Task NullQueueSubscriber_SubscribeAsync_WithFailureHandling_ShouldCompleteSuccessfully()
    {
        // Arrange
        var subscriber = new NullQueueSubscriber<TestMessage>();
        var handler =
            new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>((msg, metadata, ct) =>
                Task.CompletedTask);
        var onPermanentFailure =
            new Func<TestMessage, Exception, IDictionary<string, object>?, CancellationToken, Task>((msg, ex, metadata,
                ct) => Task.CompletedTask);

        // Act
        var act = async () => await subscriber.SubscribeAsync(handler, 3, onPermanentFailure);

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    ///     Tests that NullQueueSubscriber.UnsubscribeAsync completes successfully without throwing exceptions.
    ///     Verifies that:
    ///     - The unsubscribe operation completes successfully (no-op implementation)
    ///     - No actual unsubscription is performed
    /// </summary>
    [Fact]
    public async Task NullQueueSubscriber_UnsubscribeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var subscriber = new NullQueueSubscriber<TestMessage>();

        // Act
        var act = async () => await subscriber.UnsubscribeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    ///     Tests that NullTopicSubscriber.SubscribeAsync does not invoke the provided message handler.
    ///     Verifies that:
    ///     - Handlers are not invoked after subscription (no-op behavior)
    ///     - No messages are processed even after a delay
    /// </summary>
    [Fact]
    public async Task NullTopicSubscriber_SubscribeAsync_ShouldNotInvokeHandler()
    {
        // Arrange
        var subscriber = new NullTopicSubscriber<TestMessage>();
        var handlerInvoked = false;
        var handler =
            new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>((msg, metadata, ct) =>
            {
                handlerInvoked = true;
                return Task.CompletedTask;
            });

        // Act
        await subscriber.SubscribeAsync(handler, CancellationToken.None);
        await Task.Delay(50); // Give it time if handler would be invoked

        // Assert
        handlerInvoked.Should().BeFalse();
    }

    /// <summary>
    ///     Tests that NullQueueSubscriber.SubscribeAsync does not invoke the provided message handler.
    ///     Verifies that:
    ///     - Handlers are not invoked after subscription (no-op behavior)
    ///     - No messages are processed even after a delay
    /// </summary>
    [Fact]
    public async Task NullQueueSubscriber_SubscribeAsync_ShouldNotInvokeHandler()
    {
        // Arrange
        var subscriber = new NullQueueSubscriber<TestMessage>();
        var handlerInvoked = false;
        var handler =
            new Func<TestMessage, IDictionary<string, object>?, CancellationToken, Task>((msg, metadata, ct) =>
            {
                handlerInvoked = true;
                return Task.CompletedTask;
            });

        // Act
        await subscriber.SubscribeAsync(handler, CancellationToken.None);
        await Task.Delay(50); // Give it time if handler would be invoked

        // Assert
        handlerInvoked.Should().BeFalse();
    }
}