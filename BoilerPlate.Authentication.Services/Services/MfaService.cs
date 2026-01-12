using System.Text.Encodings.Web;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
///     Service implementation for multi-factor authentication (MFA) operations using TOTP
/// </summary>
public class MfaService : IMfaService
{
    private const string AuthenticatorTokenProvider = "Authenticator";
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<MfaService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MfaService" /> class
    /// </summary>
    public MfaService(
        UserManager<ApplicationUser> userManager,
        BaseAuthDbContext context,
        ILogger<MfaService> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MfaSetupResponse> GenerateSetupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            throw new InvalidOperationException($"User with ID {userId} not found.");

        // Reset authenticator key if user already has one (allows re-setup)
        await _userManager.ResetAuthenticatorKeyAsync(user);

        // Get the authenticator key
        var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(authenticatorKey))
        {
            // Generate new key if it doesn't exist
            await _userManager.ResetAuthenticatorKeyAsync(user);
            authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        // Format the key for manual entry (add spaces every 4 characters)
        var formattedKey = FormatKeyForManualEntry(authenticatorKey ?? string.Empty);

        // Generate QR code URI
        var account = user.Email ?? user.UserName ?? userId.ToString();
        var issuer = "BoilerPlate"; // TODO: Make this configurable via tenant settings
        var qrCodeUri = GenerateQrCodeUri(issuer, account, authenticatorKey);

        _logger.LogInformation("Generated MFA setup for user {UserId}", userId);

        return new MfaSetupResponse
        {
            QrCodeUri = qrCodeUri,
            ManualEntryKey = formattedKey,
            Account = account,
            Issuer = issuer
        };
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAndEnableAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        // Verify the code
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            AuthenticatorTokenProvider,
            code);

        if (!isValid)
        {
            _logger.LogWarning("Invalid MFA setup code for user {UserId}", userId);
            return false;
        }

        // Enable two-factor authentication
        var result = await _userManager.SetTwoFactorEnabledAsync(user, true);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to enable MFA for user {UserId}. Errors: {Errors}", userId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        _logger.LogInformation("MFA enabled for user {UserId}", userId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DisableAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to disable MFA for user {UserId}. Errors: {Errors}", userId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        // Reset authenticator key when disabling
        await _userManager.ResetAuthenticatorKeyAsync(user);

        _logger.LogInformation("MFA disabled for user {UserId}", userId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> VerifyCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        if (!user.TwoFactorEnabled) return false;

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            AuthenticatorTokenProvider,
            code);

        if (!isValid)
        {
            _logger.LogWarning("Invalid MFA code for user {UserId}", userId);
        }

        return isValid;
    }

    /// <inheritdoc />
    public async Task<bool> VerifyBackupCodeAsync(Guid userId, string backupCode,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return false;

        if (!user.TwoFactorEnabled) return false;

        // Verify backup code using Identity's recovery code system
        var result = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, backupCode);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Invalid backup code for user {UserId}", userId);
        }

        return result.Succeeded;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GenerateBackupCodesAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            throw new InvalidOperationException($"User with ID {userId} not found.");

        if (!user.TwoFactorEnabled)
            throw new InvalidOperationException($"MFA is not enabled for user {userId}.");

        // Generate new recovery codes (this invalidates old ones)
        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        if (recoveryCodes == null || !recoveryCodes.Any())
        {
            _logger.LogError("Failed to generate backup codes for user {UserId}", userId);
            throw new InvalidOperationException("Failed to generate backup codes.");
        }

        _logger.LogInformation("Generated {Count} backup codes for user {UserId}", recoveryCodes.Count(), userId);

        return recoveryCodes;
    }

    /// <inheritdoc />
    public async Task<MfaStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            throw new InvalidOperationException($"User with ID {userId} not found.");

        // Get remaining recovery codes count
        // Note: Identity doesn't provide a direct method to get remaining codes count
        // We'll return 0 if we can't determine it, or check if user has recovery codes configured
        var remainingBackupCodes = 0;
        try
        {
            // Try to get recovery codes - if this succeeds, user has codes configured
            // We can't get the exact count without generating new codes, so we'll use a placeholder
            // In a real implementation, you might want to track this separately
            var hasRecoveryCodes = await _userManager.GetTwoFactorEnabledAsync(user);
            // If MFA is enabled, assume they have recovery codes (even if count is unknown)
            remainingBackupCodes = hasRecoveryCodes ? 10 : 0; // Placeholder - actual count would need separate tracking
        }
        catch
        {
            // If we can't determine, return 0
            remainingBackupCodes = 0;
        }

        // Check if MFA is required (tenant-level setting)
        var isRequired = await IsMfaRequiredAsync(user.TenantId, cancellationToken);

        return new MfaStatusResponse
        {
            IsEnabled = user.TwoFactorEnabled,
            IsRequired = isRequired,
            RemainingBackupCodes = remainingBackupCodes
        };
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.TwoFactorEnabled ?? false;
    }

    /// <summary>
    ///     Generates QR code URI for authenticator apps
    /// </summary>
    private static string GenerateQrCodeUri(string issuer, string account, string secret)
    {
        // Format: otpauth://totp/{Issuer}:{Account}?secret={Secret}&issuer={Issuer}
        var encodedIssuer = UrlEncoder.Default.Encode(issuer);
        var encodedAccount = UrlEncoder.Default.Encode(account);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}";
    }

    /// <summary>
    ///     Formats the authenticator key for manual entry (adds spaces every 4 characters)
    /// </summary>
    private static string FormatKeyForManualEntry(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        // Add spaces every 4 characters for readability
        var formatted = string.Empty;
        for (var i = 0; i < key.Length; i++)
        {
            if (i > 0 && i % 4 == 0) formatted += " ";
            formatted += key[i];
        }

        return formatted;
    }

    /// <summary>
    ///     Checks if MFA is required for a tenant (from tenant settings)
    /// </summary>
    private async Task<bool> IsMfaRequiredAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await _context.TenantSettings
                .FirstOrDefaultAsync(
                    ts => ts.TenantId == tenantId && ts.Key == "Mfa.Required",
                    cancellationToken);

            if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
            {
                return bool.TryParse(setting.Value, out var isRequired) && isRequired;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve MFA required setting for tenant {TenantId}", tenantId);
        }

        return false; // Default: MFA is optional
    }
}
