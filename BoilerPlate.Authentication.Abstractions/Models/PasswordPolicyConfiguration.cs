namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Configuration for password policy (complexity, expiration, history)
/// </summary>
public class PasswordPolicyConfiguration
{
    /// <summary>
    ///     Minimum password length (default: 10)
    /// </summary>
    public int MinimumLength { get; set; } = 10;

    /// <summary>
    ///     Require at least one digit (default: true)
    /// </summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>
    ///     Require at least one lowercase letter (default: true)
    /// </summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>
    ///     Require at least one uppercase letter (default: true)
    /// </summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>
    ///     Require at least one non-alphanumeric character (default: true)
    /// </summary>
    public bool RequireNonAlphanumeric { get; set; } = true;

    /// <summary>
    ///     Maximum password lifetime in days (default: 120, 0 = no expiration)
    /// </summary>
    public int MaximumLifetimeDays { get; set; } = 120;

    /// <summary>
    ///     Enable password history to prevent reuse (default: false)
    /// </summary>
    public bool EnablePasswordHistory { get; set; } = false;

    /// <summary>
    ///     Number of previous passwords to remember (default: 12)
    /// </summary>
    public int PasswordHistoryCount { get; set; } = 12;
}
