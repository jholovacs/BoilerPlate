using BoilerPlate.ServiceBus.RabbitMq.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BoilerPlate.ServiceBus.RabbitMq.Connection;

/// <summary>
///     Manages RabbitMQ connections and channels
/// </summary>
public class RabbitMqConnectionManager : IAsyncDisposable, IDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ILogger<RabbitMqConnectionManager> _logger;
    private readonly RabbitMqOptions _options;
    private IConnection? _connection;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMqConnectionManager" /> class
    /// </summary>
    public RabbitMqConnectionManager(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConnectionManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _connectionLock.Wait();
        try
        {
            if (_disposed) return;

            _connection?.Dispose();
            _disposed = true;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;

            if (_connection != null)
            {
                await _connection.CloseAsync().ConfigureAwait(false);
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
            _disposed = true;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    ///     Gets or creates a connection to RabbitMQ
    /// </summary>
    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.IsOpen == true) return _connection;

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection?.IsOpen == true) return _connection;

            var connectionString = GetConnectionString();
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("RabbitMQ connection established");

            return _connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RabbitMQ connection");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    ///     Creates a new channel
    /// </summary>
    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.CreateChannelAsync(null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets the connection string from environment variable or configuration
    /// </summary>
    private string GetConnectionString()
    {
        // Check environment variable first
        var envConnectionString = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(envConnectionString)) return envConnectionString;

        // Fall back to configuration
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException(
                "RabbitMQ connection string must be provided via RABBITMQ_CONNECTION_STRING environment variable " +
                "or RabbitMq:ConnectionString configuration");

        return _options.ConnectionString;
    }
}