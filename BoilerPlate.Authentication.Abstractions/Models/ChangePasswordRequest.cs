namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Request model for changing user password
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>
    ///     Tenant ID (UUID) - required for multi-tenancy
    /// </summary>
    public required Guid TenantId { get; set; }

    /// <summary>
    ///     Current password
    /// </summary>
    public required string CurrentPassword { get; set; }

    /// <summary>
    ///     New password
    /// </summary>
    public required string NewPassword { get; set; }

    /// <summary>
    ///     Confirm new password
    /// </summary>
    public required string ConfirmNewPassword { get; set; }
}