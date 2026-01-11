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
    ///     User information
    /// </summary>
    public UserDto? User { get; set; }
}