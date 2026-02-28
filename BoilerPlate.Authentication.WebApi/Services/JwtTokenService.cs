using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Strathweb.Dilithium.IdentityModel;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
///     Service for generating and validating JWT tokens using ML-DSA (post-quantum digital signatures).
///     Replaces RSA/RS256 with ML-DSA-65 (NIST FIPS 204) for PQC resistance.
/// </summary>
public class JwtTokenService
{
    private const string KeyId = "auth-key-1";
    private const string Algorithm = "ML-DSA-65";

    private readonly JwtSettings _jwtSettings;
    private readonly MlDsaSecurityKey _securityKey;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JwtTokenService" /> class
    /// </summary>
    public JwtTokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;

        var mldsaJwk = ResolveMldsaJwk(_jwtSettings, jwtSettings);

        if (!string.IsNullOrEmpty(mldsaJwk))
        {
            var jwkJson = DecodeJwkJson(mldsaJwk);
            var jwk = new JsonWebKey(jwkJson);
            _securityKey = new MlDsaSecurityKey(jwk);
        }
        else
        {
            // Generate new keys if not provided (for development)
            _securityKey = new MlDsaSecurityKey(Algorithm);
        }
    }

    private static string? ResolveMldsaJwk(JwtSettings jwtSettings, IOptions<JwtSettings> _)
    {
        return jwtSettings.MldsaJwk;
    }

    private static string DecodeJwkJson(string value)
    {
        if (value.Contains("{"))
            return value; // Already JSON

        try
        {
            var bytes = Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return value;
        }
    }

    /// <summary>
    ///     Generates a JWT token for a user
    /// </summary>
    /// <param name="user">Application user</param>
    /// <param name="roles">User roles</param>
    /// <returns>JWT token string</returns>
    public string GenerateToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var rolesList = roles?.ToList() ?? new List<string>();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),

            // Tenant ID claim - required for multi-tenancy
            new("tenant_id", user.TenantId.ToString()),
            new("http://schemas.microsoft.com/identity/claims/tenantid", user.TenantId.ToString()),

            // User ID claim
            new("user_id", user.Id.ToString())
        };

        // Add roles as individual claims (for authorization attributes)
        foreach (var role in rolesList)
            if (!string.IsNullOrWhiteSpace(role))
                claims.Add(new Claim(ClaimTypes.Role, role));

        // Add roles as a JSON array claim for easier parsing
        if (rolesList.Any())
        {
            var rolesJson = JsonSerializer.Serialize(rolesList);
            claims.Add(new Claim("roles", rolesJson));
        }

        // Add custom claims
        if (!string.IsNullOrEmpty(user.FirstName)) claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));

        if (!string.IsNullOrEmpty(user.LastName)) claims.Add(new Claim(ClaimTypes.Surname, user.LastName));

        // Calculate expiration time
        var expirationTime = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);

        // Add expiry claim (Unix timestamp in seconds)
        var expiryUnixTimestamp = ((DateTimeOffset)expirationTime).ToUnixTimeSeconds();
        claims.Add(new Claim(JwtRegisteredClaimNames.Exp, expiryUnixTimestamp.ToString(), ClaimValueTypes.Integer64));

        var credentials = new SigningCredentials(_securityKey, Algorithm);

        var token = new JwtSecurityToken(
            _jwtSettings.Issuer,
            _jwtSettings.Audience,
            claims,
            expires: expirationTime,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    ///     Generates a JWT token for OAuth2 clients (e.g. RabbitMQ Management).
    ///     Supports custom issuer, audience, and additional scope claims.
    /// </summary>
    public string GenerateToken(ApplicationUser user, IEnumerable<string> roles, string? issuerOverride = null,
        string? audienceOverride = null, IEnumerable<string>? additionalScopeClaims = null)
    {
        var rolesList = roles?.ToList() ?? new List<string>();
        var issuer = !string.IsNullOrWhiteSpace(issuerOverride) ? issuerOverride : _jwtSettings.Issuer;
        var audience = !string.IsNullOrWhiteSpace(audienceOverride) ? audienceOverride : _jwtSettings.Audience;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new("tenant_id", user.TenantId.ToString()),
            new("http://schemas.microsoft.com/identity/claims/tenantid", user.TenantId.ToString()),
            new("user_id", user.Id.ToString())
        };

        foreach (var role in rolesList)
            if (!string.IsNullOrWhiteSpace(role))
                claims.Add(new Claim(ClaimTypes.Role, role));

        if (rolesList.Any())
            claims.Add(new Claim("roles", JsonSerializer.Serialize(rolesList)));

        if (!string.IsNullOrEmpty(user.FirstName)) claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
        if (!string.IsNullOrEmpty(user.LastName)) claims.Add(new Claim(ClaimTypes.Surname, user.LastName));

        if (additionalScopeClaims != null)
            foreach (var scope in additionalScopeClaims)
                if (!string.IsNullOrWhiteSpace(scope))
                    claims.Add(new Claim("scope", scope));

        var expirationTime = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes);
        claims.Add(new Claim(JwtRegisteredClaimNames.Exp,
            ((DateTimeOffset)expirationTime).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));

        var credentials = new SigningCredentials(_securityKey, Algorithm);

        var token = new JwtSecurityToken(issuer, audience, claims, expires: expirationTime,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    ///     Generates a refresh token
    /// </summary>
    /// <returns>Refresh token string</returns>
    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    /// <summary>
    ///     Gets the public key as a JSON Web Key for JWKS / token validation
    /// </summary>
    public JsonWebKey GetPublicKeyJwk()
    {
        return _securityKey.ToJsonWebKey(includePrivateKey: false);
    }

    /// <summary>
    ///     Validates and decodes a JWT token (for introspection).
    ///     Validates the token signature, issuer, and audience, but not the lifetime (allows introspection of expired tokens).
    /// </summary>
    /// <param name="token">The JWT token string</param>
    /// <param name="validateSignature">Whether to validate the token signature. Default is true.</param>
    /// <returns>The decoded JWT security token, or null if invalid</returns>
    public JwtSecurityToken? ValidateAndDecodeToken(string token, bool validateSignature = true)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            if (!tokenHandler.CanReadToken(token)) return null;

            if (validateSignature)
            {
                var publicJwk = _securityKey.ToJsonWebKey(includePrivateKey: false);
                var validationKey = new MlDsaSecurityKey(publicJwk);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = false, // Don't validate lifetime for introspection (we'll check manually)
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidAudience = _jwtSettings.Audience,
                    IssuerSigningKey = validationKey,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (validatedToken is JwtSecurityToken jwtToken) return jwtToken;
            }
            else
            {
                // Just decode without validation (for introspection when signature is already validated elsewhere)
                var jwtToken = tokenHandler.ReadJwtToken(token);
                return jwtToken;
            }
        }
        catch (SecurityTokenException)
        {
            // Token signature, issuer, or audience is invalid
            return null;
        }
        catch (Exception)
        {
            // Token is invalid or expired
            return null;
        }

        return null;
    }

    /// <summary>
    ///     Exports the public key as JWK JSON for JWKS endpoint
    /// </summary>
    /// <returns>Public key as JSON string</returns>
    public string ExportPublicKeyJwk()
    {
        var jwk = _securityKey.ToJsonWebKey(includePrivateKey: false);
        return JsonSerializer.Serialize(jwk);
    }

    /// <summary>
    ///     Exports the full key pair as JWK JSON (for backup/configuration)
    /// </summary>
    /// <returns>Full JWK as JSON string</returns>
    public string ExportFullKeyJwk()
    {
        var jwk = _securityKey.ToJsonWebKey(includePrivateKey: true);
        return JsonSerializer.Serialize(jwk);
    }
}
