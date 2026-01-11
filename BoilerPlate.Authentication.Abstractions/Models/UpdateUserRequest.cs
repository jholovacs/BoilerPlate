namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     Request model for updating user information
/// </summary>
public class UpdateUserRequest
{
    /// <summary>
    ///     First name
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    ///     Last name
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    ///     Email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    ///     Phone number
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    ///     Indicates if the user is active
    /// </summary>
    public bool? IsActive { get; set; }
}