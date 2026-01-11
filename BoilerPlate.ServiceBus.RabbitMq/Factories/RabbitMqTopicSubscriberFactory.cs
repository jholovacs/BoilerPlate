using BoilerPlate.ServiceBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.ServiceBus.RabbitMq.Factories;

/// <summary>
///     RabbitMQ implementation of ITopicSubscriberFactory
/// </summary>
public class RabbitMqTopicSubscriberFactory : ITopicSubscriberFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMqTopicSubscriberFactory" /> class
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies</param>
    public RabbitMqTopicSubscriberFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public ITopicSubscriber<TMessage> CreateSubscriber<TMessage>()
        where TMessage : class, IMessage
    {
        return _serviceProvider.GetRequiredService<ITopicSubscriber<TMessage>>();
    }
}