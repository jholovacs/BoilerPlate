using BoilerPlate.ServiceBus.Abstractions.Extensions;
using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Extensions;

/// <summary>
///     Unit tests for ServiceCollectionExtensions
/// </summary>
public class ServiceCollectionExtensionsTests
{
    /// <summary>
    ///     Tests that AddNullServiceBus registers ITopicPublisher in the service collection.
    ///     Verifies that:
    ///     - The service is registered and can be resolved
    ///     - The resolved service is of type NullTopicPublisher
    /// </summary>
    [Fact]
    public void AddNullServiceBus_ShouldRegisterITopicPublisher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNullServiceBus();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var publisher = serviceProvider.GetService<ITopicPublisher>();

        publisher.Should().NotBeNull();
        publisher.Should().BeOfType<NullTopicPublisher>();
    }

    /// <summary>
    ///     Tests that AddNullServiceBus registers IQueuePublisher in the service collection.
    ///     Verifies that:
    ///     - The service is registered and can be resolved
    ///     - The resolved service is of type NullQueuePublisher
    /// </summary>
    [Fact]
    public void AddNullServiceBus_ShouldRegisterIQueuePublisher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNullServiceBus();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var publisher = serviceProvider.GetService<IQueuePublisher>();

        publisher.Should().NotBeNull();
        publisher.Should().BeOfType<NullQueuePublisher>();
    }

    /// <summary>
    ///     Tests that AddNullServiceBus registers ITopicSubscriber in the service collection.
    ///     Verifies that:
    ///     - The service is registered and can be resolved for a specific message type
    ///     - The resolved service is of type NullTopicSubscriber for the specified message type
    /// </summary>
    [Fact]
    public void AddNullServiceBus_ShouldRegisterITopicSubscriber()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNullServiceBus();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var subscriber = serviceProvider.GetService<ITopicSubscriber<TestMessage>>();

        subscriber.Should().NotBeNull();
        subscriber.Should().BeOfType<NullTopicSubscriber<TestMessage>>();
    }

    /// <summary>
    ///     Tests that AddNullServiceBus registers IQueueSubscriber in the service collection.
    ///     Verifies that:
    ///     - The service is registered and can be resolved for a specific message type
    ///     - The resolved service is of type NullQueueSubscriber for the specified message type
    /// </summary>
    [Fact]
    public void AddNullServiceBus_ShouldRegisterIQueueSubscriber()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNullServiceBus();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var subscriber = serviceProvider.GetService<IQueueSubscriber<TestMessage>>();

        subscriber.Should().NotBeNull();
        subscriber.Should().BeOfType<NullQueueSubscriber<TestMessage>>();
    }

    /// <summary>
    ///     Tests that AddNullServiceBus registers ITopicSubscriberFactory in the service collection.
    ///     Verifies that:
    ///     - The factory is registered and can be resolved
    ///     - The resolved factory is of type NullTopicSubscriberFactory
    /// </summary>
    [Fact]
    public void AddNullServiceBus_ShouldRegisterITopicSubscriberFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNullServiceBus();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetService<ITopicSubscriberFactory>();

        factory.Should().NotBeNull();
        factory.Should().BeOfType<NullTopicSubscriberFactory>();
    }

    /// <summary>
    ///     Tests that AddNullServiceBus registers IQueueSubscriberFactory in the service collection.
    ///     Verifies that:
    ///     - The factory is registered and can be resolved
    ///     - The resolved factory is of type NullQueueSubscriberFactory
    /// </summary>
    [Fact]
    public void AddNullServiceBus_ShouldRegisterIQueueSubscriberFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNullServiceBus();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetService<IQueueSubscriberFactory>();

        factory.Should().NotBeNull();
        factory.Should().BeOfType<NullQueueSubscriberFactory>();
    }

    /// <summary>
    ///     Tests that publishers registered by AddNullServiceBus are singleton instances.
    ///     Verifies that:
    ///     - Multiple resolutions of ITopicPublisher return the same instance
    ///     - Publishers maintain singleton lifetime
    /// </summary>
    [Fact]
    public void AddNullServiceBus_PublishersShouldBeSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNullServiceBus();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var publisher1 = serviceProvider.GetService<ITopicPublisher>();
        var publisher2 = serviceProvider.GetService<ITopicPublisher>();

        publisher1.Should().BeSameAs(publisher2);
    }

    /// <summary>
    ///     Tests that subscribers registered by AddNullServiceBus are scoped instances.
    ///     Verifies that:
    ///     - Subscribers resolved from different scopes are different instances
    ///     - Subscribers maintain scoped lifetime
    /// </summary>
    [Fact]
    public void AddNullServiceBus_SubscribersShouldBeScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNullServiceBus();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var subscriber1 = scope1.ServiceProvider.GetService<ITopicSubscriber<TestMessage>>();
        var subscriber2 = scope2.ServiceProvider.GetService<ITopicSubscriber<TestMessage>>();

        subscriber1.Should().NotBeSameAs(subscriber2);
    }

    /// <summary>
    ///     Tests that subscriber factories registered by AddNullServiceBus are singleton instances.
    ///     Verifies that:
    ///     - Multiple resolutions of ITopicSubscriberFactory return the same instance
    ///     - Factories maintain singleton lifetime
    /// </summary>
    [Fact]
    public void AddNullServiceBus_SubscriberFactoriesShouldBeSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNullServiceBus();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var factory1 = serviceProvider.GetService<ITopicSubscriberFactory>();
        var factory2 = serviceProvider.GetService<ITopicSubscriberFactory>();

        factory1.Should().BeSameAs(factory2);
    }

    /// <summary>
    ///     Tests that AddNullServiceBus returns the same ServiceCollection instance for method chaining.
    ///     Verifies that:
    ///     - The method supports fluent API pattern
    ///     - The returned instance is the same as the input ServiceCollection
    /// </summary>
    [Fact]
    public void AddNullServiceBus_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddNullServiceBus();

        // Assert
        result.Should().BeSameAs(services);
    }
}