using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.Abstractions.Extensions;
using BoilerPlate.ServiceBus.Abstractions.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoilerPlate.ServiceBus.Abstractions.Tests.Extensions;

/// <summary>
/// Unit tests for ServiceCollectionExtensions
/// </summary>
public class ServiceCollectionExtensionsTests
{
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
