using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
///     Service for generating and validating JWT tokens using RS256 (RSA asymmetric encryption)
/// </summary>
public class JwtTokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly RSA _rsa;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JwtTokenService" /> class
    /// </summary>
    public JwtTokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
        _rsa = RSA.Create();

        // Load RSA keys from configuration
        if (!string.IsNullOrEmpty(_jwtSettings.PrivateKey))
        {
            // Note: .NET's ImportFromPem doesn't support encrypted PEM keys directly
            // If you have an encrypted key, decrypt it first using OpenSSL:
            // openssl rsa -in encrypted_key.pem -out decrypted_key.pem
            // The PrivateKeyPassword setting is reserved for future use or custom decryption implementations
            try
            {
                _rsa.ImportFromPem(_jwtSettings.PrivateKey);
            }
            catch (CryptographicException ex)
            {
                if (!string.IsNullOrEmpty(_jwtSettings.PrivateKeyPassword))
                    throw new InvalidOperationException(
                        "Failed to import private key. Password-protected PEM keys are not directly supported by ImportFromPem. " +
                        "Please decrypt the key first using OpenSSL (openssl rsa -in encrypted_key.pem -out decrypted_key.pem) " +
                        "or provide an unencrypted key. For security, use a secrets manager to store decrypted keys.",
                        ex);
                throw;
            }
        }
        else if (!string.IsNullOrEmpty(_jwtSettings.PublicKey))
        {
            _rsa.ImportFromPem(_jwtSettings.PublicKey);
        }
        else
        {
            // Generate new keys if not provided (for development)
            var keySize = 2048; // RSA-2048 is quantum-resistant enough for most use cases
            _rsa.KeySize = keySize;
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

        var key = new RsaSecurityKey(_rsa)
        {
            KeyId = "auth-key-1"
        };

        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256); // RS256

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

        var key = new RsaSecurityKey(_rsa) { KeyId = "auth-key-1" };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

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
    ///     Gets the RSA public key for token validation
    /// </summary>
    /// <returns>RSA parameters</returns>
    public RSAParameters GetPublicKey()
    {
        return _rsa.ExportParameters(false);
    }

    /// <summary>
    ///     Gets the RSA private key for token signing
    /// </summary>
    /// <returns>RSA parameters</returns>
    public RSAParameters GetPrivateKey()
    {
        return _rsa.ExportParameters(true);
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
                var key = new RsaSecurityKey(_rsa)
                {
                    KeyId = "auth-key-1"
                };

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = false, // Don't validate lifetime for introspection (we'll check manually)
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidAudience = _jwtSettings.Audience,
                    IssuerSigningKey = key,
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
    ///     Exports the public key in PEM format
    /// </summary>
    /// <returns>Public key PEM string</returns>
    public string ExportPublicKeyPem()
    {
        return _rsa.ExportRSAPublicKeyPem();
    }

    /// <summary>
    ///     Exports the private key in PEM format
    /// </summary>
    /// <returns>Private key PEM string</returns>
    public string ExportPrivateKeyPem()
    {
        return _rsa.ExportRSAPrivateKeyPem();
    }
}