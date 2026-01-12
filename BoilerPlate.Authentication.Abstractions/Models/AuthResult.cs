namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Authentication result
/// </summary>
public class AuthResult
{
    /// <summary>
    ///     Indicates if the operation was successful
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    ///     Error messages if the operation failed
    /// </summary>
    public IEnumerable<string> Errors { get; set; } = Enumerable.Empty<string>();

    /// <summary>
    ///     Authentication token (if applicable)
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    ///     MFA challenge token (if MFA is required)
    /// </summary>
    public string? ChallengeToken { get; set; }

    /// <summary>
    ///     Indicates if MFA verification is required
    /// </summary>
    public bool RequiresMfa { get; set; }

    /// <summary>
    ///     User information
    /// </summary>
    public UserDto? User { get; set; }
}