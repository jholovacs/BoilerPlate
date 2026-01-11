using System.Security.Cryptography;
using System.Text;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
///     Service for managing OAuth2 authorization codes.
///     Authorization codes are short-lived, single-use tokens used in the Authorization Code Grant flow (RFC 6749 Section
///     4.1).
/// </summary>
public class AuthorizationCodeService
{
    private const int AuthorizationCodeExpirationMinutes = 10; // Authorization codes expire after 10 minutes
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<AuthorizationCodeService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthorizationCodeService" /> class
    /// </summary>
    public AuthorizationCodeService(
        BaseAuthDbContext context,
        ILogger<AuthorizationCodeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    ///     Creates and stores a new authorization code.
    /// </summary>
    /// <param name="userId">The ID of the user who authorized the request</param>
    /// <param name="tenantId">The ID of the tenant</param>
    /// <param name="clientId">The OAuth client identifier</param>
    /// <param name="redirectUri">The redirect URI</param>
    /// <param name="scope">The requested scopes</param>
    /// <param name="state">The state parameter for CSRF protection</param>
    /// <param name="codeChallenge">PKCE code challenge (optional)</param>
    /// <param name="codeChallengeMethod">PKCE code challenge method (optional)</param>
    /// <param name="ipAddress">The IP address from which the request was made</param>
    /// <param name="userAgent">The user agent from which the request was made</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created AuthorizationCode entity</returns>
    public async Task<AuthorizationCode> CreateAuthorizationCodeAsync(
        Guid userId,
        Guid tenantId,
        string clientId,
        string redirectUri,
        string? scope,
        string? state,
        string? codeChallenge,
        string? codeChallengeMethod,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        // Generate a cryptographically secure random authorization code
        var codeBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(codeBytes);
        }

        // Convert to Base64URL (RFC 4648 Section 5)
        var code = Convert.ToBase64String(codeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var authorizationCode = new AuthorizationCode
        {
            Id = Guid.NewGuid(),
            Code = code,
            UserId = userId,
            TenantId = tenantId,
            ClientId = clientId,
            RedirectUri = redirectUri,
            Scope = scope,
            State = state,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            ExpiresAt = DateTime.UtcNow.AddMinutes(AuthorizationCodeExpirationMinutes),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow,
            IssuedFromIpAddress = ipAddress,
            IssuedFromUserAgent = userAgent
        };

        _context.AuthorizationCodes.Add(authorizationCode);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Created authorization code for user {UserId} in tenant {TenantId} for client {ClientId}",
            userId, tenantId, clientId);

        return authorizationCode;
    }

    /// <summary>
    ///     Validates and consumes an authorization code.
    ///     Authorization codes are single-use only.
    /// </summary>
    /// <param name="code">The authorization code to validate</param>
    /// <param name="clientId">The client identifier</param>
    /// <param name="redirectUri">The redirect URI (must match the one from the authorization request)</param>
    /// <param name="codeVerifier">PKCE code verifier (required if code_challenge was provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The AuthorizationCode entity if valid, null otherwise</returns>
    public async Task<AuthorizationCode?> ValidateAndConsumeAuthorizationCodeAsync(
        string code,
        string clientId,
        string redirectUri,
        string? codeVerifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        // Find the authorization code
        var authorizationCode = await _context.AuthorizationCodes
            .FirstOrDefaultAsync(ac => ac.Code == code, cancellationToken);

        if (authorizationCode == null)
        {
            _logger.LogWarning("Authorization code not found");
            return null;
        }

        // Check if code has been used (single-use only)
        if (authorizationCode.IsUsed)
        {
            _logger.LogWarning("Authorization code has already been used. Code ID: {CodeId}", authorizationCode.Id);
            return null;
        }

        // Check if code has expired
        if (authorizationCode.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Authorization code has expired. Code ID: {CodeId}, ExpiresAt: {ExpiresAt}",
                authorizationCode.Id, authorizationCode.ExpiresAt);
            return null;
        }

        // Verify client ID matches
        if (authorizationCode.ClientId != clientId)
        {
            _logger.LogWarning("Client ID mismatch. Expected: {ExpectedClientId}, Got: {ActualClientId}",
                authorizationCode.ClientId, clientId);
            return null;
        }

        // Verify redirect URI matches exactly
        if (authorizationCode.RedirectUri != redirectUri)
        {
            _logger.LogWarning("Redirect URI mismatch. Expected: {ExpectedRedirectUri}, Got: {ActualRedirectUri}",
                authorizationCode.RedirectUri, redirectUri);
            return null;
        }

        // Validate PKCE if code challenge was provided
        if (!string.IsNullOrWhiteSpace(authorizationCode.CodeChallenge))
        {
            if (string.IsNullOrWhiteSpace(codeVerifier))
            {
                _logger.LogWarning("Code verifier is required when code challenge is present");
                return null;
            }

            var isValid = ValidateCodeVerifier(codeVerifier, authorizationCode.CodeChallenge,
                authorizationCode.CodeChallengeMethod);
            if (!isValid)
            {
                _logger.LogWarning("Code verifier validation failed");
                return null;
            }
        }

        // Mark code as used (single-use)
        authorizationCode.IsUsed = true;
        authorizationCode.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Authorization code validated and consumed. Code ID: {CodeId}, UserId: {UserId}",
            authorizationCode.Id, authorizationCode.UserId);

        return authorizationCode;
    }

    /// <summary>
    ///     Validates a PKCE code verifier against the code challenge.
    /// </summary>
    /// <param name="codeVerifier">The code verifier</param>
    /// <param name="codeChallenge">The code challenge</param>
    /// <param name="codeChallengeMethod">The code challenge method ("S256" or "plain")</param>
    /// <returns>True if valid, false otherwise</returns>
    private static bool ValidateCodeVerifier(string codeVerifier, string codeChallenge, string? codeChallengeMethod)
    {
        if (string.IsNullOrWhiteSpace(codeChallengeMethod) || codeChallengeMethod == "plain")
            // Plain method: code_verifier must equal code_challenge
            return codeVerifier == codeChallenge;

        if (codeChallengeMethod == "S256")
        {
            // S256 method: SHA256(code_verifier) must equal code_challenge (base64url encoded)
            var verifierBytes = Encoding.UTF8.GetBytes(codeVerifier);
            var hashBytes = SHA256.HashData(verifierBytes);
            var hashBase64Url = Convert.ToBase64String(hashBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            return hashBase64Url == codeChallenge;
        }

        // Unknown method
        return false;
    }

    /// <summary>
    ///     Cleans up expired authorization codes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of codes deleted</returns>
    public async Task<int> CleanupExpiredCodesAsync(CancellationToken cancellationToken = default)
    {
        var expiredCodes = await _context.AuthorizationCodes
            .Where(ac => ac.ExpiresAt < DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        if (expiredCodes.Any())
        {
            _context.AuthorizationCodes.RemoveRange(expiredCodes);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cleaned up {Count} expired authorization codes", expiredCodes.Count);
        }

        return expiredCodes.Count;
    }
}