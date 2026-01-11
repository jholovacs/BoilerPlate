using BoilerPlate.ServiceBus.Abstractions;

namespace BoilerPlate.Authentication.Services.Events;

/// <summary>
///     Event published when a user is disabled (IsActive changes from true to false)
/// </summary>
public class UserDisabledEvent : IMessage
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
    ///     Date and time when the user was originally created
    /// </summary>
    public DateTime UserCreatedAt { get; set; }

    /// <summary>
    ///     Date and time when the user was disabled
    /// </summary>
    public DateTime DisabledAt { get; set; }

    /// <summary>
    ///     List of roles the user had at the time of disabling
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <inheritdoc />
    public string? TraceId { get; set; }

    /// <inheritdoc />
    public string? ReferenceId { get; set; }

    /// <inheritdoc />
    public DateTime CreatedTimestamp { get; set; }

    /// <inheritdoc />
    public int FailureCount { get; set; }
}