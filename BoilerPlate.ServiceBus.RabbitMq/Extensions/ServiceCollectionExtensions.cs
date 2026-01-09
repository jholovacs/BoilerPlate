using BoilerPlate.ServiceBus.Abstractions;
using BoilerPlate.ServiceBus.RabbitMq.Configuration;
using BoilerPlate.ServiceBus.RabbitMq.Connection;
using BoilerPlate.ServiceBus.RabbitMq.Factories;
using BoilerPlate.ServiceBus.RabbitMq.Resolvers;
using BoilerPlate.ServiceBus.RabbitMq.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.ServiceBus.RabbitMq.Extensions;

/// <summary>
///     Extension methods for configuring RabbitMQ service bus
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds RabbitMQ service bus services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRabbitMqServiceBus(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure RabbitMQ options
        services.Configure<RabbitMqOptions>(options =>
        {
            configuration.GetSection(RabbitMqOptions.SectionName).Bind(options);

            // Override with environment variable if provided
            var envConnectionString = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(envConnectionString)) options.ConnectionString = envConnectionString;
        });

        // Register connection manager as singleton
        services.AddSingleton<RabbitMqConnectionManager>();

        // Register base name resolvers
        var baseTopicResolver = new DefaultTopicNameResolver();
        var baseQueueResolver = new DefaultQueueNameResolver();

        // Register RabbitMQ-specific name resolvers that sanitize names
        services.AddSingleton<ITopicNameResolver>(sp =>
            new RabbitMqTopicNameResolver(baseTopicResolver));
        services.AddSingleton<IQueueNameResolver>(sp =>
            new RabbitMqQueueNameResolver(baseQueueResolver));

        // Register publishers (non-generic)
        services.AddScoped<ITopicPublisher, RabbitMqTopicPublisher>();
        services.AddScoped<IQueuePublisher, RabbitMqQueuePublisher>();

        // Register subscribers (still generic)
        services.AddScoped(typeof(ITopicSubscriber<>), typeof(RabbitMqTopicSubscriber<>));
        services.AddScoped(typeof(IQueueSubscriber<>), typeof(RabbitMqQueueSubscriber<>));

        // Register subscriber factories as singletons
        services.AddSingleton<ITopicSubscriberFactory, RabbitMqTopicSubscriberFactory>();
        services.AddSingleton<IQueueSubscriberFactory, RabbitMqQueueSubscriberFactory>();

        return services;
    }
}