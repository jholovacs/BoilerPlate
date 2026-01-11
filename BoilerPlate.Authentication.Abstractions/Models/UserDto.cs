namespace BoilerPlate.Authentication.Abstractions.Models;

/// <summary>
///     User data transfer object
/// </summary>
public class UserDto
{
    /// <summary>
    ///     User ID (UUID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Username
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    ///     Email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    ///     First name
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    ///     Last name
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    ///     Phone number
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    ///     Email confirmed
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    ///     Phone number confirmed
    /// </summary>
    public bool PhoneNumberConfirmed { get; set; }

    /// <summary>
    ///     Indicates if the user is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Date and time when the user was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Date and time when the user was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     User roles
    /// </summary>
    public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
}