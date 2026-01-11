using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.RabbitMq.Helpers;

namespace BoilerPlate.ServiceBus.RabbitMq.Resolvers;

/// <summary>
///     RabbitMQ-specific queue name resolver that ensures names are valid for RabbitMQ
/// </summary>
public class RabbitMqQueueNameResolver : IQueueNameResolver
{
    private readonly IQueueNameResolver _baseResolver;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMqQueueNameResolver" /> class
    /// </summary>
    /// <param name="baseResolver">The base queue name resolver to use</param>
    public RabbitMqQueueNameResolver(IQueueNameResolver baseResolver)
    {
        _baseResolver = baseResolver ?? throw new ArgumentNullException(nameof(baseResolver));
    }

    /// <inheritdoc />
    public string ResolveQueueName<TMessage>() where TMessage : class, IMessage
    {
        return ResolveQueueName(typeof(TMessage));
    }

    /// <inheritdoc />
    public string ResolveQueueName(Type messageType)
    {
        var name = _baseResolver.ResolveQueueName(messageType);
        return RabbitMqNameSanitizer.Sanitize(name);
    }
}