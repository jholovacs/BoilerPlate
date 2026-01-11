namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Request model for user login
/// </summary>
public class LoginRequest
{
    /// <summary>
    ///     Tenant ID (UUID) - required for multi-tenancy
    /// </summary>
    public required Guid TenantId { get; set; }

    /// <summary>
    ///     Username or email
    /// </summary>
    public required string UserNameOrEmail { get; set; }

    /// <summary>
    ///     Password
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    ///     Remember me option
    /// </summary>
    public bool RememberMe { get; set; }
}