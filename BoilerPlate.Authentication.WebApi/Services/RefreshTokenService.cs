using System.Security.Cryptography;
using System.Text;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
///     Service for managing refresh tokens with encryption and validation.
///     Refresh tokens can be reused until they expire or are revoked.
/// </summary>
public class RefreshTokenService
{
    private const string DataProtectionPurpose = "RefreshToken";
    private const string DefaultRefreshTokenExpirationDaysSettingKey = "RefreshToken.ExpirationDays";
    private const int DefaultRefreshTokenExpirationDays = 30;
    private readonly BaseAuthDbContext _context;
    private readonly IDataProtector _dataProtector;
    private readonly ILogger<RefreshTokenService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RefreshTokenService" /> class
    /// </summary>
    public RefreshTokenService(
        BaseAuthDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<RefreshTokenService> logger)
    {
        _context = context;
        _dataProtector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _logger = logger;
    }

    /// <summary>
    ///     Creates and stores a refresh token for a user with encryption
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="plainToken">Plain text refresh token (will be encrypted before storage)</param>
    /// <param name="ipAddress">IP address from which the token was issued (optional)</param>
    /// <param name="userAgent">User agent from which the token was issued (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The refresh token entity that was created</returns>
    public async Task<RefreshToken> CreateRefreshTokenAsync(
        Guid userId,
        Guid tenantId,
        string plainToken,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        // Get refresh token expiration days from tenant settings (default: 30 days)
        var expirationDays = await GetRefreshTokenExpirationDaysAsync(tenantId, cancellationToken);

        // Encrypt the token using ASP.NET Core Data Protection
        var encryptedToken = _dataProtector.Protect(plainToken);

        // Create a hash of the token for fast lookup without decrypting
        var tokenHash = ComputeTokenHash(plainToken);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            EncryptedToken = encryptedToken,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            IsUsed = false,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            IssuedFromIpAddress = ipAddress,
            IssuedFromUserAgent = userAgent
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created refresh token for user {UserId} in tenant {TenantId} with expiration {ExpiresAt}",
            userId, tenantId, refreshToken.ExpiresAt);

        return refreshToken;
    }

    /// <summary>
    ///     Validates a refresh token. Returns the refresh token entity if valid.
    ///     Refresh tokens can be reused until they expire or are revoked.
    /// </summary>
    /// <param name="plainToken">Plain text refresh token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The refresh token entity if valid, null otherwise</returns>
    public async Task<RefreshToken?> ValidateRefreshTokenAsync(
        string plainToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainToken)) return null;

        // First, find the token by hash (fast lookup without decrypting)
        var tokenHash = ComputeTokenHash(plainToken);
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken == null)
        {
            _logger.LogWarning("Refresh token not found by hash");
            return null;
        }

        // Check if token is revoked
        if (refreshToken.IsRevoked)
        {
            _logger.LogWarning("Refresh token has been revoked. Token ID: {TokenId}, RevokedAt: {RevokedAt}",
                refreshToken.Id, refreshToken.RevokedAt);
            return null;
        }

        // Check if token is expired
        if (refreshToken.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token has expired. Token ID: {TokenId}, ExpiresAt: {ExpiresAt}",
                refreshToken.Id, refreshToken.ExpiresAt);
            return null;
        }

        // Decrypt and verify the token matches (double-check)
        try
        {
            var decryptedToken = _dataProtector.Unprotect(refreshToken.EncryptedToken);
            if (decryptedToken != plainToken)
            {
                _logger.LogWarning("Decrypted refresh token does not match provided token. Token ID: {TokenId}",
                    refreshToken.Id);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt refresh token. Token ID: {TokenId}", refreshToken.Id);
            return null;
        }

        // Update last used timestamp for audit purposes (but don't mark as used - tokens are reusable)
        refreshToken.UsedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Refresh token validated. Token ID: {TokenId}, UserId: {UserId}",
            refreshToken.Id, refreshToken.UserId);

        return refreshToken;
    }

    /// <summary>
    ///     Revokes a refresh token by its hash
    /// </summary>
    /// <param name="plainToken">Plain text refresh token to revoke</param>
    /// <param name="userId">User ID who owns the token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the token was found and revoked, false otherwise</returns>
    public async Task<bool> RevokeRefreshTokenAsync(
        string plainToken,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainToken)) return false;

        var tokenHash = ComputeTokenHash(plainToken);
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.UserId == userId, cancellationToken);

        if (refreshToken == null) return false;

        if (refreshToken.IsRevoked) return true; // Already revoked

        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Refresh token revoked. Token ID: {TokenId}, UserId: {UserId}",
            refreshToken.Id, refreshToken.UserId);

        return true;
    }

    /// <summary>
    ///     Revokes all refresh tokens for a user (e.g., on password change or logout)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tokens revoked</returns>
    public async Task<int> RevokeAllUserRefreshTokensAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var refreshTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.TenantId == tenantId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var token in refreshTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Revoked {Count} refresh tokens for user {UserId} in tenant {TenantId}",
            refreshTokens.Count, userId, tenantId);

        return refreshTokens.Count;
    }

    /// <summary>
    ///     Gets the refresh token expiration days from tenant settings, or returns the default (30 days)
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Expiration days (default: 30)</returns>
    private async Task<int> GetRefreshTokenExpirationDaysAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await _context.TenantSettings
                .FirstOrDefaultAsync(
                    ts => ts.TenantId == tenantId && ts.Key == DefaultRefreshTokenExpirationDaysSettingKey,
                    cancellationToken);

            if (setting != null && !string.IsNullOrWhiteSpace(setting.Value) &&
                int.TryParse(setting.Value, out var expirationDays))
            {
                // Validate range (1-365 days)
                if (expirationDays >= 1 && expirationDays <= 365) return expirationDays;

                _logger.LogWarning(
                    "Invalid refresh token expiration days value in tenant setting: {Value}. Using default: {Default}",
                    setting.Value, DefaultRefreshTokenExpirationDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to retrieve refresh token expiration days from tenant settings. Using default: {Default}",
                DefaultRefreshTokenExpirationDays);
        }

        return DefaultRefreshTokenExpirationDays;
    }

    /// <summary>
    ///     Computes a SHA-256 hash of the token for fast lookup without decrypting
    /// </summary>
    /// <param name="token">Plain text token</param>
    /// <returns>Hexadecimal hash string (64 characters)</returns>
    private static string ComputeTokenHash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}