using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     Represents an OAuth2 client application registered with the authorization server.
///     Clients are identified by a client_id and may have a client_secret for confidential clients.
/// </summary>
public class OAuthClient
{
    /// <summary>
    ///     Unique identifier for the OAuth client (UUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     The client identifier (client_id) used in OAuth2 requests.
    ///     This must be unique across all clients.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string ClientId { get; set; }

    /// <summary>
    ///     The client secret (client_secret) for confidential clients.
    ///     This should be hashed before storage. Null for public clients (e.g., mobile apps).
    /// </summary>
    [MaxLength(500)]
    public string? ClientSecretHash { get; set; }

    /// <summary>
    ///     The display name of the client application
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    /// <summary>
    ///     Description of the client application
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    ///     List of allowed redirect URIs (comma-separated or JSON array).
    ///     The authorization server will only redirect to these URIs.
    /// </summary>
    [Required]
    [MaxLength(2000)] // Support multiple URIs
    public required string RedirectUris { get; set; }

    /// <summary>
    ///     Indicates whether this is a confidential client (has client_secret) or public client.
    ///     Public clients (e.g., mobile apps, SPAs) cannot securely store secrets.
    /// </summary>
    public bool IsConfidential { get; set; } = true;

    /// <summary>
    ///     Indicates whether this client is active and can be used for authentication.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     The date and time (UTC) when this client was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     The date and time (UTC) when this client was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     The ID of the tenant that owns this client (optional, for multi-tenant scenarios).
    ///     If null, this is a system-wide client.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Navigation property to the tenant
    /// </summary>
    public Tenant? Tenant { get; set; }
}