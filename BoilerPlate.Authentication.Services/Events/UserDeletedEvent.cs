using BoilerPlate.ServiceBus.Abstractions;

namespace BoilerPlate.Authentication.Services.Events;

/// <summary>
/// Event published when a user is deleted
/// </summary>
public class UserDeletedEvent : IMessage
{
    /// <inheritdoc />
    public string? TraceId { get; set; }

    /// <inheritdoc />
    public string? ReferenceId { get; set; }

    /// <inheritdoc />
    public DateTime CreatedTimestamp { get; set; }

    /// <inheritdoc />
    public int FailureCount { get; set; }

    /// <summary>
    /// User ID (UUID)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Tenant ID (UUID)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Username (for reference, user may already be deleted)
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Email address (for reference, user may already be deleted)
    /// </summary>
    public string? Email { get; set; }
}
