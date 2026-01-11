using BoilerPlate.ServiceBus.Abstractions;

namespace BoilerPlate.Authentication.Services.Events;

/// <summary>
///     Event published when a tenant is offboarded (deleted along with all associated users and roles)
/// </summary>
public class TenantOffboardedEvent : IMessage
{
    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Tenant name (before deletion)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Tenant description (before deletion)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Number of users that were deleted as part of the offboarding
    /// </summary>
    public int UsersDeletedCount { get; set; }

    /// <summary>
    ///     Number of roles that were deleted as part of the offboarding
    /// </summary>
    public int RolesDeletedCount { get; set; }

    /// <summary>
    ///     Date and time when the tenant was originally created
    /// </summary>
    public DateTime TenantCreatedAt { get; set; }

    /// <summary>
    ///     Date and time when the tenant was offboarded (deleted)
    /// </summary>
    public DateTime OffboardedAt { get; set; }

    /// <inheritdoc />
    public string? TraceId { get; set; }

    /// <inheritdoc />
    public string? ReferenceId { get; set; }

    /// <inheritdoc />
    public DateTime CreatedTimestamp { get; set; }

    /// <inheritdoc />
    public int FailureCount { get; set; }
}