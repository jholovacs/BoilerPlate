namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
/// Request model for user registration
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// Tenant ID (UUID) - required for multi-tenancy
    /// </summary>
    public required Guid TenantId { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    public required string UserName { get; set; }

    /// <summary>
    /// Password
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// Confirm password
    /// </summary>
    public required string ConfirmPassword { get; set; }

    /// <summary>
    /// First name
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Phone number
    /// </summary>
    public string? PhoneNumber { get; set; }
}
