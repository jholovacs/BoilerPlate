namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Data transfer object for OAuth client information (excluding sensitive client secret)
/// </summary>
public class OAuthClientDto
{
    /// <summary>
    ///     Unique identifier for the OAuth client (UUID)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     The client identifier (client_id) used in OAuth2 requests
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    ///     The display name of the client application
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Description of the client application
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     List of allowed redirect URIs (comma-separated)
    /// </summary>
    public required string RedirectUris { get; set; }

    /// <summary>
    ///     Indicates whether this is a confidential client (has client_secret) or public client
    /// </summary>
    public bool IsConfidential { get; set; }

    /// <summary>
    ///     Indicates whether this client is active and can be used for authentication
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    ///     The date and time (UTC) when this client was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     The date and time (UTC) when this client was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    ///     The ID of the tenant that owns this client (null for system-wide clients)
    /// </summary>
    public Guid? TenantId { get; set; }
}