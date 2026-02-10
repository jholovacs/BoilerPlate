using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
///     Service implementation for managing tenant vanity URL mappings.
///     Supports custom ports: hostnames may be stored as "host" or "host:port" (e.g. "app.example.com:5000").
///     Resolution matches exact "host:port" first, then matches by host part so "host" matches any port.
/// </summary>
public class TenantVanityUrlService : ITenantVanityUrlService
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<TenantVanityUrlService>? _logger;

    /// <summary>
    ///     Returns the host part of a host string, stripping the port if present.
    ///     Handles IPv4 (host:port), IPv6 ([::1]:port), and hostnames without port.
    /// </summary>
    private static string GetHostPart(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return hostname;
        var s = hostname.Trim();
        // IPv6: [::1] or [::1]:8080
        if (s.StartsWith('['))
        {
            var close = s.IndexOf(']');
            if (close >= 0 && close + 1 < s.Length && s[close + 1] == ':')
                return s[..(close + 1)]; // [::1]
            return close >= 0 ? s[..(close + 1)] : s;
        }
        // IPv4 or hostname: first colon separates host and port
        var colon = s.IndexOf(':');
        return colon >= 0 ? s[..colon] : s;
    }

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

        // Normalize to lowercase and trim; support "host" or "host:port" (including IPv6 [::1]:port)
        var normalizedHostname = hostname.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedHostname)) return null;

        var hostnameWithoutPort = GetHostPart(normalizedHostname);

        // Build possible hostnames for matching: request host (no port) plus parent hostnames
        // e.g. "subdomain.tenant1.foo.org" -> ["subdomain.tenant1.foo.org", "tenant1.foo.org", "foo.org"]
        var hostnameParts = hostnameWithoutPort.Split('.');
        var possibleHostnames = new List<string> { hostnameWithoutPort };
        for (var i = 1; i < hostnameParts.Length; i++)
        {
            var parentHostname = string.Join(".", hostnameParts.Skip(i));
            possibleHostnames.Add(parentHostname);
        }

        // Load all active vanity URLs so we can try exact match first, then host-only match
        var activeVanityUrls = await _context.TenantVanityUrls
            .Where(v => v.IsActive)
            .ToListAsync(cancellationToken);

        // 1. Exact match (e.g. stored "app.example.com:5000" matches request "app.example.com:5000")
        var exactMatch = activeVanityUrls
            .FirstOrDefault(v => v.Hostname.Equals(normalizedHostname, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            _logger?.LogDebug("Resolved tenant {TenantId} from vanity URL (exact match) {Hostname}", exactMatch.TenantId, hostname);
            return exactMatch.TenantId;
        }

        // 2. Match by host part (stored "app.example.com" matches "app.example.com:8080"; prefer longest/specific match)
        var hostPartMatch = activeVanityUrls
            .Where(v =>
            {
                var storedHostPart = GetHostPart(v.Hostname);
                return possibleHostnames.Contains(storedHostPart, StringComparer.OrdinalIgnoreCase);
            })
            .OrderByDescending(v => GetHostPart(v.Hostname).Length)
            .FirstOrDefault();

        if (hostPartMatch != null)
        {
            _logger?.LogDebug("Resolved tenant {TenantId} from vanity URL hostname {Hostname}", hostPartMatch.TenantId, hostname);
            return hostPartMatch.TenantId;
        }

        _logger?.LogDebug("No tenant found for vanity URL hostname {Hostname}", hostname);
        return null;
    }

    /// <inheritdoc />
    public async Task<TenantVanityUrlDto?> CreateTenantVanityUrlAsync(CreateTenantVanityUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        // Normalize hostname to lowercase; allow optional port (e.g. "app.example.com:5000")
        var normalizedHostname = request.Hostname.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedHostname))
            return null;

        // Check if hostname already exists (exact string, with or without port)
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

        // If hostname is being updated, normalize and check uniqueness (allow optional port)
        if (!string.IsNullOrWhiteSpace(request.Hostname))
        {
            var normalizedHostname = request.Hostname.Trim().ToLowerInvariant();

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
