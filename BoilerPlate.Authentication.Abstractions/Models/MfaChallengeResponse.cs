namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Response model for MFA challenge (returned when MFA is required during login)
/// </summary>
public class MfaChallengeResponse
{
    /// <summary>
    ///     Temporary challenge token (short-lived, used to verify MFA code)
    ///     This is NOT a JWT access token - it can only be used for MFA verification
    /// </summary>
    public required string ChallengeToken { get; set; }

    /// <summary>
    ///     Indicates that MFA verification is required
    /// </summary>
    public bool RequiresMfa { get; set; } = true;

    /// <summary>
    ///     Message indicating what the user needs to do
    /// </summary>
    public string Message { get; set; } = "Multi-factor authentication is required. Please enter the code from your authenticator app.";
}
