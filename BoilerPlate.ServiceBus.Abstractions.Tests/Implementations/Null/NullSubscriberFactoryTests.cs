using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Implementations.Null;

/// <summary>
/// Unit tests for NullTopicSubscriberFactory and NullQueueSubscriberFactory
/// </summary>
public class NullSubscriberFactoryTests
{
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
