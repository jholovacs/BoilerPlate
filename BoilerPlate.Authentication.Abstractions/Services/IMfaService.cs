using BoilerPlate.Authentication.Abstractions.Models;

namespace BoilerPlate.Authentication.Abstractions.Services;

/// <summary>
///     Service interface for multi-factor authentication (MFA) operations
/// </summary>
public interface IMfaService
{
    /// <summary>
    ///     Generates MFA setup data (QR code URI and manual entry key) for a user
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>MFA setup response with QR code data</returns>
    Task<MfaSetupResponse> GenerateSetupAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies the MFA setup code and enables MFA for the user
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="code">TOTP code from authenticator app</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if code is valid and MFA was enabled, false otherwise</returns>
    Task<bool> VerifyAndEnableAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Disables MFA for a user
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if MFA was disabled, false if user not found</returns>
    Task<bool> DisableAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies an MFA code during login
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="code">TOTP code from authenticator app</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if code is valid, false otherwise</returns>
    Task<bool> VerifyCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies a backup code during login
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="backupCode">Backup code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if backup code is valid, false otherwise</returns>
    Task<bool> VerifyBackupCodeAsync(Guid userId, string backupCode, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generates new backup codes for a user (invalidates old ones)
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of new backup codes</returns>
    Task<IEnumerable<string>> GenerateBackupCodesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets MFA status for a user
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>MFA status response</returns>
    Task<MfaStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if MFA is enabled for a user
    /// </summary>
    /// <param name="userId">User ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if MFA is enabled, false otherwise</returns>
    Task<bool> IsEnabledAsync(Guid userId, CancellationToken cancellationToken = default);
}
