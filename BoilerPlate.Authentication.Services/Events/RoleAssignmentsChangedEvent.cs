using BoilerPlate.ServiceBus.Abstractions;

namespace BoilerPlate.Authentication.Services.Events;

/// <summary>
///     Event published when role assignments change for a user
/// </summary>
public class RoleAssignmentsChangedEvent : IMessage
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
    ///     List of roles that were assigned
    /// </summary>
    public List<string> AssignedRoles { get; set; } = new();

    /// <summary>
    ///     List of roles that were removed
    /// </summary>
    public List<string> RemovedRoles { get; set; } = new();

    /// <summary>
    ///     Complete list of roles the user has after the change
    /// </summary>
    public List<string> CurrentRoles { get; set; } = new();

    /// <inheritdoc />
    public string? TraceId { get; set; }

    /// <inheritdoc />
    public string? ReferenceId { get; set; }

    /// <inheritdoc />
    public DateTime CreatedTimestamp { get; set; }

    /// <inheritdoc />
    public int FailureCount { get; set; }
}