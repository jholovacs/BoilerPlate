using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
///     Service implementation for managing tenant email domain mappings
/// </summary>
public class TenantEmailDomainService : ITenantEmailDomainService
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<TenantEmailDomainService>? _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TenantEmailDomainService" /> class
    /// </summary>
    public TenantEmailDomainService(
        BaseAuthDbContext context,
        ILogger<TenantEmailDomainService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TenantEmailDomainDto?> GetTenantEmailDomainByIdAsync(Guid domainId,
        CancellationToken cancellationToken = default)
    {
        var domain = await _context.TenantEmailDomains.FindAsync(new object[] { domainId }, cancellationToken);
        if (domain == null) return null;

        return MapToDto(domain);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TenantEmailDomainDto>> GetTenantEmailDomainsByTenantIdAsync(Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var domains = await _context.TenantEmailDomains
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return domains.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TenantEmailDomainDto>> GetAllTenantEmailDomainsAsync(
        CancellationToken cancellationToken = default)
    {
        var domains = await _context.TenantEmailDomains.ToListAsync(cancellationToken);
        return domains.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolveTenantIdFromEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        // Extract domain from email address
        var emailParts = email.Split('@');
        if (emailParts.Length != 2) return null;

        var domain = emailParts[1].Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(domain)) return null;

        // Find active domain mapping (try exact match first, then try parent domains)
        // For example, if email is user@subdomain.example.com, try:
        // 1. subdomain.example.com
        // 2. example.com
        var domainParts = domain.Split('.');
        var possibleDomains = new List<string> { domain };

        // Build parent domains (e.g., for "subdomain.example.com", also try "example.com")
        for (var i = 1; i < domainParts.Length; i++)
        {
            var parentDomain = string.Join(".", domainParts.Skip(i));
            possibleDomains.Add(parentDomain);
        }

        // Find the first matching active domain mapping
        var domainMapping = await _context.TenantEmailDomains
            .Where(d => possibleDomains.Contains(d.Domain.ToLowerInvariant()) && d.IsActive)
            .OrderByDescending(d => d.Domain.Length) // Prefer more specific domains (longer matches)
            .FirstOrDefaultAsync(cancellationToken);

        if (domainMapping != null)
        {
            _logger?.LogDebug("Resolved tenant {TenantId} from email domain {Domain}", domainMapping.TenantId, domain);
            return domainMapping.TenantId;
        }

        _logger?.LogDebug("No tenant found for email domain {Domain}", domain);
        return null;
    }

    /// <inheritdoc />
    public async Task<TenantEmailDomainDto?> CreateTenantEmailDomainAsync(CreateTenantEmailDomainRequest request,
        CancellationToken cancellationToken = default)
    {
        // Normalize domain to lowercase
        var normalizedDomain = request.Domain.Trim().ToLowerInvariant();

        // Check if domain already exists
        var existingDomain = await _context.TenantEmailDomains
            .FirstOrDefaultAsync(d => d.Domain == normalizedDomain, cancellationToken);

        if (existingDomain != null)
        {
            _logger?.LogWarning("Email domain {Domain} already exists for tenant {TenantId}", normalizedDomain,
                existingDomain.TenantId);
            return null;
        }

        // Verify tenant exists
        var tenant = await _context.Tenants.FindAsync(new object[] { request.TenantId }, cancellationToken);
        if (tenant == null)
        {
            _logger?.LogWarning("Tenant {TenantId} not found when creating email domain mapping", request.TenantId);
            return null;
        }

        var domain = new TenantEmailDomain
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Domain = normalizedDomain,
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.TenantEmailDomains.Add(domain);
        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Created email domain mapping {Domain} for tenant {TenantId}", normalizedDomain,
            request.TenantId);

        return MapToDto(domain);
    }

    /// <inheritdoc />
    public async Task<TenantEmailDomainDto?> UpdateTenantEmailDomainAsync(Guid domainId,
        UpdateTenantEmailDomainRequest request, CancellationToken cancellationToken = default)
    {
        var domain = await _context.TenantEmailDomains.FindAsync(new object[] { domainId }, cancellationToken);
        if (domain == null) return null;

        // If domain is being updated, check if new domain already exists
        if (!string.IsNullOrWhiteSpace(request.Domain))
        {
            var normalizedDomain = request.Domain.Trim().ToLowerInvariant();
            if (normalizedDomain != domain.Domain)
            {
                var existingDomain = await _context.TenantEmailDomains
                    .FirstOrDefaultAsync(d => d.Domain == normalizedDomain && d.Id != domainId, cancellationToken);

                if (existingDomain != null)
                {
                    _logger?.LogWarning("Email domain {Domain} already exists for tenant {TenantId}", normalizedDomain,
                        existingDomain.TenantId);
                    return null;
                }

                domain.Domain = normalizedDomain;
            }
        }

        if (request.Description != null) domain.Description = request.Description;

        if (request.IsActive.HasValue) domain.IsActive = request.IsActive.Value;

        domain.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Updated email domain mapping {DomainId}", domainId);

        return MapToDto(domain);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTenantEmailDomainAsync(Guid domainId, CancellationToken cancellationToken = default)
    {
        var domain = await _context.TenantEmailDomains.FindAsync(new object[] { domainId }, cancellationToken);
        if (domain == null) return false;

        _context.TenantEmailDomains.Remove(domain);
        await _context.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Deleted email domain mapping {DomainId} for domain {Domain}", domainId, domain.Domain);

        return true;
    }

    private static TenantEmailDomainDto MapToDto(TenantEmailDomain domain)
    {
        return new TenantEmailDomainDto
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            Domain = domain.Domain,
            IsActive = domain.IsActive,
            Description = domain.Description,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };
    }
}
