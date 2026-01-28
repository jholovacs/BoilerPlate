using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
///     Service implementation for managing tenant vanity URL mappings
/// </summary>
public class TenantVanityUrlService : ITenantVanityUrlService
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<TenantVanityUrlService>? _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TenantVanityUrlService" /> class
    /// </summary>
    public TenantVanityUrlService(
        BaseAuthDbContext context,
        ILogger<TenantVanityUrlService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TenantVanityUrlDto?> GetTenantVanityUrlByIdAsync(Guid vanityUrlId,
        CancellationToken cancellationToken = default)
    {
        var vanityUrl = await _context.TenantVanityUrls.FindAsync(new object[] { vanityUrlId }, cancellationToken);
        if (vanityUrl == null) return null;

        return MapToDto(vanityUrl);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TenantVanityUrlDto>> GetTenantVanityUrlsByTenantIdAsync(Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var vanityUrls = await _context.TenantVanityUrls
            .Where(v => v.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return vanityUrls.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TenantVanityUrlDto>> GetAllTenantVanityUrlsAsync(
        CancellationToken cancellationToken = default)
    {
        var vanityUrls = await _context.TenantVanityUrls.ToListAsync(cancellationToken);
        return vanityUrls.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolveTenantIdFromHostnameAsync(string hostname, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return null;

        // Normalize hostname to lowercase and trim
        var normalizedHostname = hostname.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedHostname)) return null;

        // Remove port if present (e.g., "tenant1.foo.org:8080" -> "tenant1.foo.org")
        var hostnameWithoutPort = normalizedHostname.Split(':')[0];

        // Find active vanity URL mapping (try exact match first, then try parent hostnames)
        // For example, if hostname is "subdomain.tenant1.foo.org", try:
        // 1. subdomain.tenant1.foo.org
        // 2. tenant1.foo.org
        // 3. foo.org
        var hostnameParts = hostnameWithoutPort.Split('.');
        var possibleHostnames = new List<string> { hostnameWithoutPort };

        // Build parent hostnames (e.g., for "subdomain.tenant1.foo.org", also try "tenant1.foo.org" and "foo.org")
        for (var i = 1; i < hostnameParts.Length; i++)
        {
            var parentHostname = string.Join(".", hostnameParts.Skip(i));
            possibleHostnames.Add(parentHostname);
        }

        // Find the first matching active vanity URL mapping
        var vanityUrlMapping = await _context.TenantVanityUrls
            .Where(v => possibleHostnames.Contains(v.Hostname.ToLowerInvariant()) && v.IsActive)
            .OrderByDescending(v => v.Hostname.Length) // Prefer more specific hostnames (longer matches)
            .FirstOrDefaultAsync(cancellationToken);

        if (vanityUrlMapping != null)
        {
            _logger?.LogDebug("Resolved tenant {TenantId} from vanity URL hostname {Hostname}", vanityUrlMapping.TenantId, hostname);
            return vanityUrlMapping.TenantId;
        }

        _logger?.LogDebug("No tenant found for vanity URL hostname {Hostname}", hostname);
        return null;
    }

    /// <inheritdoc />
    public async Task<TenantVanityUrlDto?> CreateTenantVanityUrlAsync(CreateTenantVanityUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        // Normalize hostname to lowercase
        var normalizedHostname = request.Hostname.Trim().ToLowerInvariant();

        // Remove port if present
        normalizedHostname = normalizedHostname.Split(':')[0];

        // Check if hostname already exists
        var existingVanityUrl = await _context.TenantVanityUrls
            .FirstOrDefaultAsync(v => v.Hostname == normalizedHostname, cancellationToken);

        if (existingVanityUrl != null)
        {
            _logger?.LogWarning("Vanity URL hostname {Hostname} already exists for tenant {TenantId}", normalizedHostname,
                existingVanityUrl.TenantId);
            return null;
        }

        // Verify tenant exists
        var tenant = await _context.Tenants.FindAsync(new object[] { request.TenantId }, cancellationToken);
        if (tenant == null)
        {
            _logger?.LogWarning("Tenant {TenantId} not found when creating vanity URL mapping", request.TenantId);
            return null;
        }

        var vanityUrl = new TenantVanityUrl
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Hostname = normalizedHostname,
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantVanityUrls.Add(vanityUrl);
        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Created vanity URL mapping {Hostname} for tenant {TenantId}", normalizedHostname,
            request.TenantId);

        return MapToDto(vanityUrl);
    }

    /// <inheritdoc />
    public async Task<TenantVanityUrlDto?> UpdateTenantVanityUrlAsync(Guid vanityUrlId,
        UpdateTenantVanityUrlRequest request, CancellationToken cancellationToken = default)
    {
        var vanityUrl = await _context.TenantVanityUrls.FindAsync(new object[] { vanityUrlId }, cancellationToken);
        if (vanityUrl == null) return null;

        // If hostname is being updated, check if new hostname already exists
        if (!string.IsNullOrWhiteSpace(request.Hostname))
        {
            var normalizedHostname = request.Hostname.Trim().ToLowerInvariant();
            normalizedHostname = normalizedHostname.Split(':')[0]; // Remove port if present

            if (normalizedHostname != vanityUrl.Hostname)
            {
                var existingVanityUrl = await _context.TenantVanityUrls
                    .FirstOrDefaultAsync(v => v.Hostname == normalizedHostname && v.Id != vanityUrlId, cancellationToken);

                if (existingVanityUrl != null)
                {
                    _logger?.LogWarning("Vanity URL hostname {Hostname} already exists for tenant {TenantId}", normalizedHostname,
                        existingVanityUrl.TenantId);
                    return null;
                }

                vanityUrl.Hostname = normalizedHostname;
            }
        }

        if (request.Description != null) vanityUrl.Description = request.Description;

        if (request.IsActive.HasValue) vanityUrl.IsActive = request.IsActive.Value;

        vanityUrl.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Updated vanity URL mapping {VanityUrlId}", vanityUrlId);

        return MapToDto(vanityUrl);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTenantVanityUrlAsync(Guid vanityUrlId, CancellationToken cancellationToken = default)
    {
        var vanityUrl = await _context.TenantVanityUrls.FindAsync(new object[] { vanityUrlId }, cancellationToken);
        if (vanityUrl == null) return false;

        _context.TenantVanityUrls.Remove(vanityUrl);
        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Deleted vanity URL mapping {VanityUrlId} for hostname {Hostname}", vanityUrlId, vanityUrl.Hostname);

        return true;
    }

    private static TenantVanityUrlDto MapToDto(TenantVanityUrl vanityUrl)
    {
        return new TenantVanityUrlDto
        {
            Id = vanityUrl.Id,
            TenantId = vanityUrl.TenantId,
            Hostname = vanityUrl.Hostname,
            IsActive = vanityUrl.IsActive,
            Description = vanityUrl.Description,
            CreatedAt = vanityUrl.CreatedAt,
            UpdatedAt = vanityUrl.UpdatedAt
        };
    }
}
