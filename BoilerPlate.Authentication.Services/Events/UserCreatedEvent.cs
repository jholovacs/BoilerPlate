using BoilerPlate.ServiceBus.Abstractions;

namespace BoilerPlate.Authentication.Services.Events;

/// <summary>
///     Event published when a user is created
/// </summary>
public class UserCreatedEvent : IMessage
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
    ///     Indicates whether the user is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <inheritdoc />
    public string? TraceId { get; set; }

    /// <inheritdoc />
    public string? ReferenceId { get; set; }

    /// <inheritdoc />
    public DateTime CreatedTimestamp { get; set; }

    /// <inheritdoc />
    public int FailureCount { get; set; }
}