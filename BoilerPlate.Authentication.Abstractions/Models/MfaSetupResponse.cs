namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Response model for MFA setup containing QR code data and manual entry key
/// </summary>
public class MfaSetupResponse
{
    /// <summary>
    ///     QR code URI for authenticator app scanning
    ///     Format: otpauth://totp/{Issuer}:{Account}?secret={Secret}&issuer={Issuer}
    /// </summary>
    public required string QrCodeUri { get; set; }

    /// <summary>
    ///     Manual entry key for authenticator apps (base32 encoded)
    ///     Users can manually enter this if they can't scan the QR code
    /// </summary>
    public required string ManualEntryKey { get; set; }

    /// <summary>
    ///     Account identifier (typically email or username)
    /// </summary>
    public required string Account { get; set; }

    /// <summary>
    ///     Issuer name (application name)
    /// </summary>
    public required string Issuer { get; set; }
}

/// <summary>
///     Request model for verifying MFA setup code
/// </summary>
public class MfaVerifySetupRequest
{
    /// <summary>
    ///     TOTP code from authenticator app (6 digits)
    /// </summary>
    public required string Code { get; set; }
}

/// <summary>
///     Request model for verifying MFA code during login
/// </summary>
public class MfaVerifyRequest
{
    /// <summary>
    ///     MFA challenge token (returned after password verification)
    /// </summary>
    public required string ChallengeToken { get; set; }

    /// <summary>
    ///     TOTP code from authenticator app (6 digits)
    /// </summary>
    public required string Code { get; set; }
}

/// <summary>
///     Response model for MFA backup codes
/// </summary>
public class MfaBackupCodesResponse
{
    /// <summary>
    ///     List of backup codes (single-use codes for MFA recovery)
    /// </summary>
    public required IEnumerable<string> BackupCodes { get; set; }
}

/// <summary>
///     Request model for verifying backup code
/// </summary>
public class MfaBackupCodeVerifyRequest
{
    /// <summary>
    ///     MFA challenge token (returned after password verification)
    /// </summary>
    public required string ChallengeToken { get; set; }

    /// <summary>
    ///     Backup code to verify
    /// </summary>
    public required string BackupCode { get; set; }
}

/// <summary>
///     Response model for MFA status
/// </summary>
public class MfaStatusResponse
{
    /// <summary>
    ///     Indicates whether MFA is enabled for the user
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Indicates whether MFA is required for the user (tenant-level setting)
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    ///     Number of backup codes remaining
    /// </summary>
    public int RemainingBackupCodes { get; set; }
}
