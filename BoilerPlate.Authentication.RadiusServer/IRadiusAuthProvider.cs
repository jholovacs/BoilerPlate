namespace BoilerPlate.Authentication.RadiusServer;

/// <summary>
///     Provides authentication for RADIUS Access-Request packets.
///     Implementations integrate with BoilerPlate authentication services.
/// </summary>
public interface IRadiusAuthProvider
{
    /// <summary>
    ///     Validates credentials for RADIUS Access-Request.
    /// </summary>
    /// <param name="username">Username from User-Name attribute</param>
    /// <param name="password">Password from User-Password attribute (PAP)</param>
    /// <param name="tenantId">Tenant ID (from default config or parsed from username realm)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if credentials are valid</returns>
    Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
