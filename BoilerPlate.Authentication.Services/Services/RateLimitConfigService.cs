using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
///     Service for managing rate limit configuration. Provides cached access for the rate limiting middleware
///     and CRUD operations for Service Administrators.
/// </summary>
public class RateLimitConfigService : IRateLimitConfigService
{
    /// <summary>
    ///     Endpoint keys for rate-limited paths. Used for display names and validation.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> EndpointDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["oauth/token"] = "OAuth Token",
        ["jwt/validate"] = "JWT Validate",
        ["oauth/authorize"] = "OAuth Authorize"
    };

    private const int CacheSeconds = 30;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(CacheSeconds);

    private readonly BaseAuthDbContext _context;
    private readonly ILogger<RateLimitConfigService> _logger;
    private static readonly Dictionary<string, (RateLimitConfigDto Config, DateTime ExpiresAt)> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheLock = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="RateLimitConfigService" /> class
    /// </summary>
    public RateLimitConfigService(BaseAuthDbContext context, ILogger<RateLimitConfigService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RateLimitConfigDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _context.RateLimitConfigs
            .OrderBy(c => c.EndpointKey)
            .ToListAsync(cancellationToken);

        return configs.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<RateLimitConfigDto?> GetForEndpointAsync(string endpointKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpointKey)) return null;

        lock (CacheLock)
        {
            if (Cache.TryGetValue(endpointKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            {
                return cached.Config.IsEnabled ? cached.Config : null;
            }
        }

        var entity = await _context.RateLimitConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.EndpointKey == endpointKey, cancellationToken);

        if (entity == null) return null;

        var dto = ToDto(entity);
        lock (CacheLock)
        {
            Cache[endpointKey] = (dto, DateTime.UtcNow.Add(CacheDuration));
        }

        return entity.IsEnabled ? dto : null;
    }

    /// <inheritdoc />
    public async Task<RateLimitConfigDto> UpdateAsync(string endpointKey, UpdateRateLimitConfigRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpointKey))
            throw new ArgumentException("Endpoint key is required", nameof(endpointKey));

        var entity = await _context.RateLimitConfigs
            .FirstOrDefaultAsync(c => c.EndpointKey == endpointKey, cancellationToken);

        if (entity == null)
            throw new InvalidOperationException($"Rate limit configuration for endpoint '{endpointKey}' not found");

        if (request.PermittedRequests.HasValue)
        {
            if (request.PermittedRequests.Value < 1 || request.PermittedRequests.Value > 10000)
                throw new ArgumentOutOfRangeException(nameof(request.PermittedRequests), "Must be between 1 and 10000");
            entity.PermittedRequests = request.PermittedRequests.Value;
        }

        if (request.WindowSeconds.HasValue)
        {
            if (request.WindowSeconds.Value < 1 || request.WindowSeconds.Value > 3600)
                throw new ArgumentOutOfRangeException(nameof(request.WindowSeconds), "Must be between 1 and 3600");
            entity.WindowSeconds = request.WindowSeconds.Value;
        }

        if (request.IsEnabled.HasValue)
            entity.IsEnabled = request.IsEnabled.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        InvalidateCache();
        _logger.LogInformation("Rate limit config updated for {EndpointKey}: {PermittedRequests} per {WindowSeconds}s, Enabled={IsEnabled}",
            endpointKey, entity.PermittedRequests, entity.WindowSeconds, entity.IsEnabled);

        return ToDto(entity);
    }

    /// <inheritdoc />
    public void InvalidateCache()
    {
        lock (CacheLock)
        {
            Cache.Clear();
        }
    }

    private static RateLimitConfigDto ToDto(RateLimitConfig entity)
    {
        return new RateLimitConfigDto
        {
            Id = entity.Id,
            EndpointKey = entity.EndpointKey,
            DisplayName = EndpointDisplayNames.TryGetValue(entity.EndpointKey, out var name) ? name : entity.EndpointKey,
            PermittedRequests = entity.PermittedRequests,
            WindowSeconds = entity.WindowSeconds,
            IsEnabled = entity.IsEnabled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
