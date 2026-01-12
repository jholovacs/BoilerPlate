namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Request model for user login
/// </summary>
public class LoginRequest
{
    /// <summary>
    ///     Tenant ID (UUID) - optional if email domain mapping is configured
    ///     If not provided and UserNameOrEmail is an email address, the tenant will be resolved from the email domain
    /// </summary>
    public Guid? TenantId { get; set; }

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