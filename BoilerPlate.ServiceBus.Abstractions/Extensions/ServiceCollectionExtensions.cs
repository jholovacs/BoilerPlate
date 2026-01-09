using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.ServiceBus.Abstractions.Extensions;

/// <summary>
/// Extension methods for registering null service bus implementations
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers null (no-op) implementations of all service bus interfaces
    /// Useful for development or when messaging is not needed
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddNullServiceBus(this IServiceCollection services)
    {
        // Register null publishers (non-generic)
        services.AddSingleton<ITopicPublisher, NullTopicPublisher>();
        services.AddSingleton<IQueuePublisher, NullQueuePublisher>();
        
        // Register null subscribers (still generic)
        services.AddScoped(typeof(ITopicSubscriber<>), typeof(NullTopicSubscriber<>));
        services.AddScoped(typeof(IQueueSubscriber<>), typeof(NullQueueSubscriber<>));

        // Register null subscriber factories
        services.AddSingleton<ITopicSubscriberFactory, NullTopicSubscriberFactory>();
        services.AddSingleton<IQueueSubscriberFactory, NullQueueSubscriberFactory>();

        return services;
    }
}
