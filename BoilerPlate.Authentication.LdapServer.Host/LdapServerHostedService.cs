using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.LdapServer.Host;

/// <summary>
///     Hosted service that starts and stops the LDAP server.
/// </summary>
public class LdapServerHostedService : IHostedService
{
    private readonly AuthBackedLdapServer _server;
    private readonly ILogger<LdapServerHostedService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LdapServerHostedService" /> class
    /// </summary>
    public LdapServerHostedService(AuthBackedLdapServer server, ILogger<LdapServerHostedService> logger)
    {
        _server = server;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting LDAP server...");
        return _server.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping LDAP server...");
        return _server.StopAsync(cancellationToken);
    }
}
