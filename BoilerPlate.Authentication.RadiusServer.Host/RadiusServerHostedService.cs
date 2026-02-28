using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.RadiusServer.Host;

/// <summary>
///     Hosted service that starts and stops the RADIUS server.
/// </summary>
public class RadiusServerHostedService : IHostedService
{
    private readonly AuthBackedRadiusServer _server;
    private readonly ILogger<RadiusServerHostedService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RadiusServerHostedService" /> class
    /// </summary>
    public RadiusServerHostedService(AuthBackedRadiusServer server, ILogger<RadiusServerHostedService> logger)
    {
        _server = server;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting RADIUS server...");
        return _server.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping RADIUS server...");
        return _server.StopAsync(cancellationToken);
    }
}
