namespace BoilerPlate.ServiceBus.RabbitMq.Configuration;

/// <summary>
///     Configuration options for RabbitMQ connection
/// </summary>
public class RabbitMqOptions
{
    /// <summary>
    ///     Configuration section name
    /// </summary>
    public const string SectionName = "RabbitMq";

    /// <summary>
    ///     RabbitMQ connection string
    ///     Can be overridden by RABBITMQ_CONNECTION_STRING environment variable
    ///     Format: amqp://username:password@host:port/vhost
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}