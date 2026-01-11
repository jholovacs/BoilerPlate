using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Request model for creating a new OAuth client
/// </summary>
public class CreateOAuthClientRequest
{
    /// <summary>
    ///     The client identifier (client_id) used in OAuth2 requests. Must be unique.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string ClientId { get; set; }

    /// <summary>
    ///     The client secret (client_secret) for confidential clients. Will be hashed before storage.
    ///     Required for confidential clients, optional for public clients.
    /// </summary>
    [MaxLength(200)]
    public string? ClientSecret { get; set; }

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
    ///     List of allowed redirect URIs (comma-separated, e.g., "https://myapp.com/callback,https://myapp.com/auth")
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public required string RedirectUris { get; set; }

    /// <summary>
    ///     Indicates whether this is a confidential client (has client_secret) or public client.
    ///     Public clients (e.g., mobile apps, SPAs) cannot securely store secrets.
    /// </summary>
    public bool IsConfidential { get; set; } = true;
}