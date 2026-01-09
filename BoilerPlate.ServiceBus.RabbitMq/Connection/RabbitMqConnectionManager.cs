using BoilerPlate.ServiceBus.RabbitMq.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;

namespace BoilerPlate.ServiceBus.RabbitMq.Connection;

/// <summary>
/// Manages RabbitMQ connections and channels
/// </summary>
public class RabbitMqConnectionManager : IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConnectionManager> _logger;
    private IConnection? _connection;
    private readonly object _lock = new();
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqConnectionManager"/> class
    /// </summary>
    public RabbitMqConnectionManager(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConnectionManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates a connection to RabbitMQ
    /// </summary>
    public IConnection GetConnection()
    {
        if (_connection?.IsOpen == true)
        {
            return _connection;
        }

        lock (_lock)
        {
            if (_connection?.IsOpen == true)
            {
                return _connection;
            }

            try
            {
                var connectionString = GetConnectionString();
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(connectionString),
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = factory.CreateConnection();
                _logger.LogInformation("RabbitMQ connection established");

                return _connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create RabbitMQ connection");
                throw;
            }
        }
    }

    /// <summary>
    /// Creates a new channel
    /// </summary>
    public IModel CreateChannel()
    {
        var connection = GetConnection();
        var channel = connection.CreateModel();
        return channel;
    }

    /// <summary>
    /// Gets the connection string from environment variable or configuration
    /// </summary>
    private string GetConnectionString()
    {
        // Check environment variable first
        var envConnectionString = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(envConnectionString))
        {
            return envConnectionString;
        }

        // Fall back to configuration
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException(
                "RabbitMQ connection string must be provided via RABBITMQ_CONNECTION_STRING environment variable " +
                "or RabbitMq:ConnectionString configuration");
        }

        return _options.ConnectionString;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _connection?.Close();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
