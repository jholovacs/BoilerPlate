using BoilerPlate.ServiceBus.Abstractions;

namespace BoilerPlate.Authentication.Services.Events;

/// <summary>
///     Event published when a tenant is onboarded (created with default roles)
/// </summary>
public class TenantOnboardedEvent : IMessage
{
    /// <summary>
    ///     Tenant ID (UUID)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Tenant name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Tenant description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Indicates whether the tenant is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     Date and time when the tenant was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     List of default roles created for the tenant
    /// </summary>
    public List<string> DefaultRoles { get; set; } = new();

    /// <inheritdoc />
    public string? TraceId { get; set; }

    /// <inheritdoc />
    public string? ReferenceId { get; set; }

    /// <inheritdoc />
    public DateTime CreatedTimestamp { get; set; }

    /// <inheritdoc />
    public int FailureCount { get; set; }
}