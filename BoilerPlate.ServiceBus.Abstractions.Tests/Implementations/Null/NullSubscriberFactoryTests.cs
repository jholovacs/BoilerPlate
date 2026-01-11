using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Implementations.Null;

/// <summary>
///     Unit tests for NullTopicSubscriberFactory and NullQueueSubscriberFactory
/// </summary>
public class NullSubscriberFactoryTests
{
    /// <summary>
    ///     Tests that NullTopicSubscriberFactory.CreateSubscriber returns a NullTopicSubscriber instance.
    ///     Verifies that:
    ///     - The factory creates a subscriber instance
    ///     - The returned subscriber is of type NullTopicSubscriber for the specified message type
    /// </summary>
    [Fact]
    public void NullTopicSubscriberFactory_CreateSubscriber_ShouldReturnNullTopicSubscriber()
    {
        // Arrange
        var factory = new NullTopicSubscriberFactory();

        // Act
        var subscriber = factory.CreateSubscriber<TestMessage>();

        // Assert
        subscriber.Should().NotBeNull();
        subscriber.Should().BeOfType<NullTopicSubscriber<TestMessage>>();
    }

    /// <summary>
    ///     Tests that NullTopicSubscriberFactory.CreateSubscriber returns different subscriber instances for different message
    ///     types.
    ///     Verifies that:
    ///     - Each message type gets its own subscriber instance
    ///     - Subscribers are correctly typed for their respective message types
    ///     - Different message types produce different subscriber instances
    /// </summary>
    [Fact]
    public void NullTopicSubscriberFactory_CreateSubscriber_WithDifferentTypes_ShouldReturnCorrectSubscriber()
    {
        // Arrange
        var factory = new NullTopicSubscriberFactory();

        // Act
        var subscriber1 = factory.CreateSubscriber<TestMessage>();
        var subscriber2 = factory.CreateSubscriber<UserCreatedEvent>();

        // Assert
        subscriber1.Should().BeOfType<NullTopicSubscriber<TestMessage>>();
        subscriber2.Should().BeOfType<NullTopicSubscriber<UserCreatedEvent>>();
        subscriber1.Should().NotBeSameAs(subscriber2);
    }

    /// <summary>
    ///     Tests that NullQueueSubscriberFactory.CreateSubscriber returns a NullQueueSubscriber instance.
    ///     Verifies that:
    ///     - The factory creates a subscriber instance
    ///     - The returned subscriber is of type NullQueueSubscriber for the specified message type
    /// </summary>
    [Fact]
    public void NullQueueSubscriberFactory_CreateSubscriber_ShouldReturnNullQueueSubscriber()
    {
        // Arrange
        var factory = new NullQueueSubscriberFactory();

        // Act
        var subscriber = factory.CreateSubscriber<TestMessage>();

        // Assert
        subscriber.Should().NotBeNull();
        subscriber.Should().BeOfType<NullQueueSubscriber<TestMessage>>();
    }

    /// <summary>
    ///     Tests that NullQueueSubscriberFactory.CreateSubscriber returns different subscriber instances for different message
    ///     types.
    ///     Verifies that:
    ///     - Each message type gets its own subscriber instance
    ///     - Subscribers are correctly typed for their respective message types
    ///     - Different message types produce different subscriber instances
    /// </summary>
    [Fact]
    public void NullQueueSubscriberFactory_CreateSubscriber_WithDifferentTypes_ShouldReturnCorrectSubscriber()
    {
        // Arrange
        var factory = new NullQueueSubscriberFactory();

        // Act
        var subscriber1 = factory.CreateSubscriber<TestMessage>();
        var subscriber2 = factory.CreateSubscriber<UserCreatedEvent>();

        // Assert
        subscriber1.Should().BeOfType<NullQueueSubscriber<TestMessage>>();
        subscriber2.Should().BeOfType<NullQueueSubscriber<UserCreatedEvent>>();
        subscriber1.Should().NotBeSameAs(subscriber2);
    }
}