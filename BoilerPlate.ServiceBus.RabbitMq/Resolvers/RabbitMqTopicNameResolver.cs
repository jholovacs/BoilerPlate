using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.RabbitMq.Helpers;

namespace BoilerPlate.ServiceBus.RabbitMq.Resolvers;

/// <summary>
/// RabbitMQ-specific topic name resolver that ensures names are valid for RabbitMQ
/// </summary>
public class RabbitMqTopicNameResolver : ITopicNameResolver
{
    private readonly ITopicNameResolver _baseResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqTopicNameResolver"/> class
    /// </summary>
    /// <param name="baseResolver">The base topic name resolver to use</param>
    public RabbitMqTopicNameResolver(ITopicNameResolver baseResolver)
    {
        _baseResolver = baseResolver ?? throw new ArgumentNullException(nameof(baseResolver));
    }

    /// <inheritdoc />
    public string ResolveTopicName<TMessage>() where TMessage : class, IMessage
    {
        return ResolveTopicName(typeof(TMessage));
    }

    /// <inheritdoc />
    public string ResolveTopicName(Type messageType)
    {
        var name = _baseResolver.ResolveTopicName(messageType);
        return RabbitMqNameSanitizer.Sanitize(name);
    }
}
