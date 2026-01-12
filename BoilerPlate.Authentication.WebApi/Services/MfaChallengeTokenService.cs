using System.Security.Cryptography;
using System.Text;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
///     Service for managing MFA challenge tokens with encryption and validation.
///     Challenge tokens are single-use and short-lived (typically 5-10 minutes).
/// </summary>
public class MfaChallengeTokenService
{
    private const string DataProtectionPurpose = "MfaChallengeToken";
    private const int DefaultChallengeTokenExpirationMinutes = 10;
    private readonly BaseAuthDbContext _context;
    private readonly IDataProtector _dataProtector;
    private readonly ILogger<MfaChallengeTokenService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MfaChallengeTokenService" /> class
    /// </summary>
    public MfaChallengeTokenService(
        BaseAuthDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<MfaChallengeTokenService> logger)
    {
        _context = context;
        _dataProtector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        _logger = logger;
    }

    /// <summary>
    ///     Creates and stores an MFA challenge token for a user with encryption
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="plainToken">Plain text challenge token (will be encrypted before storage)</param>
    /// <param name="ipAddress">IP address from which the challenge was issued (optional)</param>
    /// <param name="userAgent">User agent from which the challenge was issued (optional)</param>
    /// <param name="expirationMinutes">Expiration time in minutes (default: 10 minutes)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The challenge token entity that was created</returns>
    public async Task<MfaChallengeToken> CreateChallengeTokenAsync(
        Guid userId,
        Guid tenantId,
        string plainToken,
        string? ipAddress = null,
        string? userAgent = null,
        int expirationMinutes = DefaultChallengeTokenExpirationMinutes,
        CancellationToken cancellationToken = default)
    {
        // Encrypt the token using ASP.NET Core Data Protection
        var encryptedToken = _dataProtector.Protect(plainToken);

        // Create a hash of the token for fast lookup without decrypting
        var tokenHash = ComputeTokenHash(plainToken);

        var challengeToken = new MfaChallengeToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            EncryptedToken = encryptedToken,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow,
            IssuedFromIpAddress = ipAddress,
            IssuedFromUserAgent = userAgent
        };

        _context.MfaChallengeTokens.Add(challengeToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Created MFA challenge token for user {UserId} in tenant {TenantId} with expiration {ExpiresAt}",
            userId, tenantId, challengeToken.ExpiresAt);

        return challengeToken;
    }

    /// <summary>
    ///     Validates and consumes an MFA challenge token. Returns the challenge token entity if valid.
    ///     Challenge tokens are single-use and will be marked as used after validation.
    /// </summary>
    /// <param name="plainToken">Plain text challenge token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The challenge token entity if valid, null otherwise</returns>
    public async Task<MfaChallengeToken?> ValidateAndConsumeChallengeTokenAsync(
        string plainToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainToken)) return null;

        // First, find the token by hash (fast lookup without decrypting)
        var tokenHash = ComputeTokenHash(plainToken);
        var challengeToken = await _context.MfaChallengeTokens
            .FirstOrDefaultAsync(ct => ct.TokenHash == tokenHash, cancellationToken);

        if (challengeToken == null)
        {
            _logger.LogWarning("MFA challenge token not found by hash");
            return null;
        }

        // Check if token is already used (single-use)
        if (challengeToken.IsUsed)
        {
            _logger.LogWarning("MFA challenge token has already been used. Token ID: {TokenId}, UsedAt: {UsedAt}",
                challengeToken.Id, challengeToken.UsedAt);
            return null;
        }

        // Check if token is expired
        if (challengeToken.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("MFA challenge token has expired. Token ID: {TokenId}, ExpiresAt: {ExpiresAt}",
                challengeToken.Id, challengeToken.ExpiresAt);
            return null;
        }

        // Decrypt and verify the token matches (double-check)
        try
        {
            var decryptedToken = _dataProtector.Unprotect(challengeToken.EncryptedToken);
            if (decryptedToken != plainToken)
            {
                _logger.LogWarning("Decrypted MFA challenge token does not match provided token. Token ID: {TokenId}",
                    challengeToken.Id);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt MFA challenge token. Token ID: {TokenId}", challengeToken.Id);
            return null;
        }

        // Mark token as used (single-use)
        challengeToken.IsUsed = true;
        challengeToken.UsedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("MFA challenge token validated and consumed. Token ID: {TokenId}, UserId: {UserId}",
            challengeToken.Id, challengeToken.UserId);

        return challengeToken;
    }

    /// <summary>
    ///     Generates a random challenge token string
    /// </summary>
    /// <returns>Base64-encoded random token string</returns>
    public static string GenerateChallengeToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
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
