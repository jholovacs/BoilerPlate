using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.RadiusServer.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.RadiusServer;

/// <summary>
///     RADIUS auth provider that uses IAuthenticationService for Access-Request validation.
/// </summary>
public class AuthServiceRadiusProvider : IRadiusAuthProvider
{
    private readonly IAuthenticationService _authenticationService;
    private readonly RadiusServerOptions _options;
    private readonly ILogger<AuthServiceRadiusProvider> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthServiceRadiusProvider" /> class
    /// </summary>
    public AuthServiceRadiusProvider(
        IAuthenticationService authenticationService,
        IOptions<RadiusServerOptions> options,
        ILogger<AuthServiceRadiusProvider> logger)
    {
        _authenticationService = authenticationService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogDebug("RADIUS Access-Request failed: username or password empty");
            return false;
        }

        var effectiveTenantId = tenantId ?? _options.DefaultTenantId;
        if (!effectiveTenantId.HasValue)
        {
            _logger.LogDebug("RADIUS Access-Request failed: tenant ID required");
            return false;
        }

        var loginRequest = new LoginRequest
        {
            TenantId = effectiveTenantId,
            UserNameOrEmail = username,
            Password = password,
            RememberMe = false
        };

        var result = await _authenticationService.LoginAsync(loginRequest, cancellationToken);

        if (result.Succeeded)
            _logger.LogDebug("RADIUS Access-Accept for user {Username} in tenant {TenantId}", username, effectiveTenantId);
        else
            _logger.LogDebug("RADIUS Access-Reject for user {Username} in tenant {TenantId}", username, effectiveTenantId);

        return result.Succeeded;
    }
}
