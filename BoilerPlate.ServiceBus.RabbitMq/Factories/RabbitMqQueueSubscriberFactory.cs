using BoilerPlate.ServiceBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.ServiceBus.RabbitMq.Factories;

/// <summary>
///     RabbitMQ implementation of IQueueSubscriberFactory
/// </summary>
public class RabbitMqQueueSubscriberFactory : IQueueSubscriberFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMqQueueSubscriberFactory" /> class
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies</param>
    public RabbitMqQueueSubscriberFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public IQueueSubscriber<TMessage> CreateSubscriber<TMessage>()
        where TMessage : class, IMessage
    {
        return _serviceProvider.GetRequiredService<IQueueSubscriber<TMessage>>();
    }
}