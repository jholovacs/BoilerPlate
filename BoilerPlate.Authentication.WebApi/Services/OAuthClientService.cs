using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
///     Service for managing OAuth2 client applications, including secret hashing and validation.
///     Uses ASP.NET Core Identity's PasswordHasher for secure secret hashing (PBKDF2 with HMAC-SHA256).
/// </summary>
public class OAuthClientService
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<OAuthClientService> _logger;
    private readonly PasswordHasher<OAuthClient> _passwordHasher;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OAuthClientService" /> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger.</param>
    public OAuthClientService(BaseAuthDbContext context, ILogger<OAuthClientService> logger)
    {
        _context = context;
        _passwordHasher = new PasswordHasher<OAuthClient>();
        _logger = logger;
    }

    /// <summary>
    ///     Hashes a client secret using PBKDF2 with HMAC-SHA256 (same algorithm as ASP.NET Core Identity passwords).
    /// </summary>
    /// <param name="clientSecret">The plain text client secret to hash.</param>
    /// <returns>The hashed client secret.</returns>
    public string HashClientSecret(string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("Client secret cannot be null or empty.", nameof(clientSecret));

        // Use a dummy client instance for hashing (PasswordHasher uses the type for salt generation)
        var dummyClient = new OAuthClient
        {
            Id = Guid.Empty,
            ClientId = "dummy",
            Name = "Dummy",
            RedirectUris = "https://dummy.com"
        };
        return _passwordHasher.HashPassword(dummyClient, clientSecret);
    }

    /// <summary>
    ///     Verifies a client secret against a stored hash.
    /// </summary>
    /// <param name="client">The OAuth client entity.</param>
    /// <param name="clientSecret">The plain text client secret to verify.</param>
    /// <returns>True if the secret matches, false otherwise.</returns>
    public bool VerifyClientSecret(OAuthClient client, string clientSecret)
    {
        if (client == null) throw new ArgumentNullException(nameof(client));

        if (string.IsNullOrWhiteSpace(clientSecret)) return false;

        if (string.IsNullOrWhiteSpace(client.ClientSecretHash))
            // If no hash is stored, this is a public client
            return false;

        var result = _passwordHasher.VerifyHashedPassword(client, client.ClientSecretHash, clientSecret);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }

    /// <summary>
    ///     Creates a new OAuth client with a hashed secret.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="clientSecret">The plain text client secret (will be hashed).</param>
    /// <param name="name">The display name of the client.</param>
    /// <param name="description">The description of the client.</param>
    /// <param name="redirectUris">Comma-separated list of allowed redirect URIs.</param>
    /// <param name="isConfidential">Whether this is a confidential client (has secret).</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenant scenarios.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created OAuth client entity, or null if client ID already exists.</returns>
    public async Task<OAuthClient?> CreateClientAsync(
        string clientId,
        string? clientSecret,
        string name,
        string? description,
        string redirectUris,
        bool isConfidential,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        // Check if client ID already exists
        var existingClient = await _context.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

        if (existingClient != null)
        {
            _logger.LogWarning("OAuth client creation failed: Client ID '{ClientId}' already exists.", clientId);
            return null;
        }

        // Validate confidential clients must have a secret
        if (isConfidential && string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogWarning(
                "OAuth client creation failed: Confidential client '{ClientId}' requires a client secret.", clientId);
            return null;
        }

        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            ClientSecretHash = isConfidential && !string.IsNullOrWhiteSpace(clientSecret)
                ? HashClientSecret(clientSecret)
                : null,
            Name = name,
            Description = description,
            RedirectUris = redirectUris,
            IsConfidential = isConfidential,
            IsActive = true,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };

        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OAuth client created: {ClientId} (Confidential: {IsConfidential})", clientId,
            isConfidential);

        return client;
    }

    /// <summary>
    ///     Updates an existing OAuth client.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="name">The new display name (optional).</param>
    /// <param name="description">The new description (optional).</param>
    /// <param name="redirectUris">The new redirect URIs (optional).</param>
    /// <param name="isActive">The new active status (optional).</param>
    /// <param name="newClientSecret">Optional new client secret (will be hashed). If provided, updates the secret.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the client was updated, false if not found.</returns>
    public async Task<bool> UpdateClientAsync(
        string clientId,
        string? name = null,
        string? description = null,
        string? redirectUris = null,
        bool? isActive = null,
        string? newClientSecret = null,
        CancellationToken cancellationToken = default)
    {
        var client = await _context.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

        if (client == null)
        {
            _logger.LogWarning("OAuth client update failed: Client ID '{ClientId}' not found.", clientId);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(name)) client.Name = name;

        if (description != null) // Allow clearing description
            client.Description = description;

        if (!string.IsNullOrWhiteSpace(redirectUris)) client.RedirectUris = redirectUris;

        if (isActive.HasValue) client.IsActive = isActive.Value;

        if (!string.IsNullOrWhiteSpace(newClientSecret))
        {
            if (!client.IsConfidential)
            {
                _logger.LogWarning("OAuth client update failed: Cannot set secret for public client '{ClientId}'.",
                    clientId);
                return false;
            }

            client.ClientSecretHash = HashClientSecret(newClientSecret);
        }

        client.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OAuth client updated: {ClientId}", clientId);

        return true;
    }

    /// <summary>
    ///     Deletes an OAuth client.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the client was deleted, false if not found.</returns>
    public async Task<bool> DeleteClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var client = await _context.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

        if (client == null)
        {
            _logger.LogWarning("OAuth client deletion failed: Client ID '{ClientId}' not found.", clientId);
            return false;
        }

        _context.OAuthClients.Remove(client);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OAuth client deleted: {ClientId}", clientId);

        return true;
    }

    /// <summary>
    ///     Gets an OAuth client by client ID.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The OAuth client entity, or null if not found.</returns>
    public async Task<OAuthClient?> GetClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return await _context.OAuthClients
            .Include(c => c.Tenant)
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);
    }

    /// <summary>
    ///     Gets all OAuth clients, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to filter by.</param>
    /// <param name="includeInactive">Whether to include inactive clients.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of OAuth clients.</returns>
    public async Task<List<OAuthClient>> GetClientsAsync(
        Guid? tenantId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.OAuthClients
            .Include(c => c.Tenant)
            .AsQueryable();

        if (tenantId.HasValue) query = query.Where(c => c.TenantId == tenantId);

        if (!includeInactive) query = query.Where(c => c.IsActive);

        return await query.ToListAsync(cancellationToken);
    }
}