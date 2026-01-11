using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.WebApi.Models;

/// <summary>
///     Request model for updating an existing OAuth client
/// </summary>
public class UpdateOAuthClientRequest
{
    /// <summary>
    ///     The new display name of the client application (optional)
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>
    ///     The new description of the client application (optional, can be set to empty string to clear)
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    ///     The new list of allowed redirect URIs (comma-separated, optional)
    /// </summary>
    [MaxLength(2000)]
    public string? RedirectUris { get; set; }

    /// <summary>
    ///     The new active status (optional)
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    ///     Optional new client secret. If provided, will update the secret (confidential clients only).
    ///     Will be hashed before storage.
    /// </summary>
    [MaxLength(200)]
    public string? NewClientSecret { get; set; }
}