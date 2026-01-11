using BoilerPlate.ServiceBus.Abstractions;

namespace BoilerPlate.Authentication.Services.Events;

/// <summary>
///     Event published when a user is modified
/// </summary>
public class UserModifiedEvent : IMessage
{
    /// <summary>
    ///     User ID (UUID)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Username
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    ///     Email address (new value)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    ///     Email address (old value)
    /// </summary>
    public string? OldEmail { get; set; }

    /// <summary>
    ///     First name (new value)
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    ///     First name (old value)
    /// </summary>
    public string? OldFirstName { get; set; }

    /// <summary>
    ///     Last name (new value)
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    ///     Last name (old value)
    /// </summary>
    public string? OldLastName { get; set; }

    /// <summary>
    ///     Phone number (new value)
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    ///     Phone number (old value)
    /// </summary>
    public string? OldPhoneNumber { get; set; }

    /// <summary>
    ///     Indicates whether the user is active (new value)
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Indicates whether the user was active (old value)
    /// </summary>
    public bool? OldIsActive { get; set; }

    /// <summary>
    ///     List of changed properties (for tracking what was modified)
    /// </summary>
    public List<string> ChangedProperties { get; set; } = new();

    /// <inheritdoc />
    public string? TraceId { get; set; }

    /// <inheritdoc />
    public string? ReferenceId { get; set; }

    /// <inheritdoc />
    public DateTime CreatedTimestamp { get; set; }

    /// <inheritdoc />
    public int FailureCount { get; set; }
}