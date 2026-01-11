using System.ComponentModel.DataAnnotations;

namespace BoilerPlate.Authentication.Database.Entities;

/// <summary>
///     Represents a user's consent decision for an OAuth2 client application.
///     Allows the authorization server to skip the consent screen for previously authorized clients and scopes.
/// </summary>
public class UserConsent
{
    /// <summary>
    ///     Unique identifier for the consent record (UUID)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     The ID of the user who granted consent
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    ///     The ID of the tenant that the user belongs to
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    ///     The client identifier (client_id) that consent was granted for
    /// </summary>
    [Required]
    [MaxLength(200)]
    public required string ClientId { get; set; }

    /// <summary>
    ///     The scopes that the user consented to (space-delimited, e.g., "api.read api.write")
    /// </summary>
    [MaxLength(1000)]
    public string? Scope { get; set; }

    /// <summary>
    ///     The date and time (UTC) when consent was first granted
    /// </summary>
    [Required]
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     The date and time (UTC) when consent was last confirmed/renewed
    /// </summary>
    [Required]
    public DateTime LastConfirmedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     The date and time (UTC) when consent expires (null for no expiration)
    ///     Typically, consent should be re-requested after a certain period (e.g., 90 days)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///     Navigation property to the user
    /// </summary>
    public ApplicationUser? User { get; set; }

    /// <summary>
    ///     Navigation property to the tenant
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    ///     Checks if the consent is still valid (not expired and within the valid period).
    /// </summary>
    /// <returns>True if consent is valid, false otherwise.</returns>
    public bool IsValid()
    {
        if (ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value) return false;

        // Default expiration: 90 days from last confirmation (if ExpiresAt is not set)
        if (!ExpiresAt.HasValue)
        {
            var defaultExpiration = LastConfirmedAt.AddDays(90);
            return DateTime.UtcNow <= defaultExpiration;
        }

        return true;
    }

    /// <summary>
    ///     Checks if the requested scopes are covered by this consent.
    /// </summary>
    /// <param name="requestedScopes">The scopes requested by the client (space-delimited).</param>
    /// <returns>True if all requested scopes are covered, false otherwise.</returns>
    public bool CoversScopes(string? requestedScopes)
    {
        if (string.IsNullOrWhiteSpace(requestedScopes))
            // No scopes requested, consent covers it
            return true;

        if (string.IsNullOrWhiteSpace(Scope))
            // This consent has no scopes, so it doesn't cover any requested scopes
            return false;

        var requestedScopeList = requestedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var consentedScopeList = Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // All requested scopes must be in the consented scopes
        return requestedScopeList.IsSubsetOf(consentedScopeList);
    }
}