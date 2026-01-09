using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
/// Service for generating and validating JWT tokens using RS256 (RSA asymmetric encryption)
/// </summary>
public class JwtTokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly RSA _rsa;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtTokenService"/> class
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
                {
                    throw new InvalidOperationException(
                        "Failed to import private key. Password-protected PEM keys are not directly supported by ImportFromPem. " +
                        "Please decrypt the key first using OpenSSL (openssl rsa -in encrypted_key.pem -out decrypted_key.pem) " +
                        "or provide an unencrypted key. For security, use a secrets manager to store decrypted keys.", ex);
                }
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
    /// Generates a JWT token for a user
    /// </summary>
    /// <param name="user">Application user</param>
    /// <param name="roles">User roles</param>
    /// <returns>JWT token string</returns>
    public string GenerateToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var rolesList = roles?.ToList() ?? new List<string>();

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            
            // Tenant ID claim - required for multi-tenancy
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("http://schemas.microsoft.com/identity/claims/tenantid", user.TenantId.ToString()),
            
            // User ID claim
            new Claim("user_id", user.Id.ToString())
        };

        // Add roles as individual claims (for authorization attributes)
        foreach (var role in rolesList)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        // Add roles as a JSON array claim for easier parsing
        if (rolesList.Any())
        {
            var rolesJson = System.Text.Json.JsonSerializer.Serialize(rolesList);
            claims.Add(new Claim("roles", rolesJson));
        }

        // Add custom claims
        if (!string.IsNullOrEmpty(user.FirstName))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
        }

        if (!string.IsNullOrEmpty(user.LastName))
        {
            claims.Add(new Claim(ClaimTypes.Surname, user.LastName));
        }

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
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expirationTime,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a refresh token
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
    /// Gets the RSA public key for token validation
    /// </summary>
    /// <returns>RSA parameters</returns>
    public RSAParameters GetPublicKey()
    {
        return _rsa.ExportParameters(false);
    }

    /// <summary>
    /// Gets the RSA private key for token signing
    /// </summary>
    /// <returns>RSA parameters</returns>
    public RSAParameters GetPrivateKey()
    {
        return _rsa.ExportParameters(true);
    }

    /// <summary>
    /// Exports the public key in PEM format
    /// </summary>
    /// <returns>Public key PEM string</returns>
    public string ExportPublicKeyPem()
    {
        return _rsa.ExportRSAPublicKeyPem();
    }

    /// <summary>
    /// Exports the private key in PEM format
    /// </summary>
    /// <returns>Private key PEM string</returns>
    public string ExportPrivateKeyPem()
    {
        return _rsa.ExportRSAPrivateKeyPem();
    }
}
