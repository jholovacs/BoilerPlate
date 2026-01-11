using System.Security.Cryptography;
using System.Text;
using BoilerPlate.Authentication.Services.Extensions;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BoilerPlate.Authentication.WebApi.Extensions;

/// <summary>
///     Extension methods for configuring services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds JWT authentication with RS256 (RSA asymmetric encryption)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                          ?? throw new InvalidOperationException(
                              $"JWT settings not found in configuration section '{JwtSettings.SectionName}'");

        // Override expiration minutes from environment variable if provided
        var envExpirationMinutes = configuration["JWT_EXPIRATION_MINUTES"];
        if (!string.IsNullOrWhiteSpace(envExpirationMinutes) &&
            int.TryParse(envExpirationMinutes, out var expirationMinutes))
            jwtSettings.ExpirationMinutes = expirationMinutes;

        // Override private key from environment variable if provided
        var envPrivateKey = configuration["JWT_PRIVATE_KEY"];
        if (!string.IsNullOrWhiteSpace(envPrivateKey))
        {
            // Check if it's base64 encoded (common in Docker environments)
            // Base64-encoded keys won't contain "-----BEGIN" markers
            if (!envPrivateKey.Contains("-----BEGIN"))
                try
                {
                    var keyBytes = Convert.FromBase64String(envPrivateKey);
                    envPrivateKey = Encoding.UTF8.GetString(keyBytes);
                }
                catch (FormatException)
                {
                    // Not valid base64, treat as plain text PEM
                    // Will be handled by newline normalization below
                }

            // Normalize newlines - handle both literal \n and actual newlines
            // Also normalize Windows (\r\n) and Unix (\n) line endings
            envPrivateKey = envPrivateKey.Replace("\\n", "\n")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");
            jwtSettings.PrivateKey = envPrivateKey;
        }

        // Override public key from environment variable if provided
        var envPublicKey = configuration["JWT_PUBLIC_KEY"];
        if (!string.IsNullOrWhiteSpace(envPublicKey))
        {
            // Check if it's base64 encoded
            if (!envPublicKey.Contains("-----BEGIN"))
                try
                {
                    var keyBytes = Convert.FromBase64String(envPublicKey);
                    envPublicKey = Encoding.UTF8.GetString(keyBytes);
                }
                catch (FormatException)
                {
                    // Not valid base64, treat as plain text PEM
                    // Will be handled by newline normalization below
                }

            // Normalize newlines - handle both literal \n and actual newlines
            envPublicKey = envPublicKey.Replace("\\n", "\n")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");
            jwtSettings.PublicKey = envPublicKey;
        }

        // Override private key password from environment variable if provided
        var envPrivateKeyPassword = configuration["JWT_PRIVATE_KEY_PASSWORD"];
        if (!string.IsNullOrWhiteSpace(envPrivateKeyPassword)) jwtSettings.PrivateKeyPassword = envPrivateKeyPassword;

        services.Configure<JwtSettings>(options =>
        {
            options.Issuer = jwtSettings.Issuer;
            options.Audience = jwtSettings.Audience;
            options.ExpirationMinutes = jwtSettings.ExpirationMinutes;
            options.RefreshTokenExpirationDays = jwtSettings.RefreshTokenExpirationDays;
            options.PrivateKey = jwtSettings.PrivateKey;
            options.PublicKey = jwtSettings.PublicKey;
            options.PrivateKeyPassword = jwtSettings.PrivateKeyPassword;
        });
        services.AddSingleton<JwtTokenService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<JwtSettings>>();
            return new JwtTokenService(settings);
        });

        // Register refresh token service with Data Protection for encryption
        // Data Protection is automatically registered by ASP.NET Core, but we can configure it if needed
        services.AddScoped<RefreshTokenService>();

        // Register authorization code service for OAuth2 Authorization Code Grant flow
        services.AddScoped<AuthorizationCodeService>();
        services.AddScoped<OAuthClientService>();

        // Configure RSA key
        var rsa = RSA.Create();
        var privateKeyToUse = jwtSettings.PrivateKey;

        // Note: .NET's ImportFromPem doesn't support encrypted PEM keys directly
        // If you have an encrypted key, decrypt it first using OpenSSL:
        // openssl rsa -in encrypted_key.pem -out decrypted_key.pem
        // The JWT_PRIVATE_KEY_PASSWORD environment variable is reserved for future use
        // or custom decryption implementations

        if (!string.IsNullOrEmpty(privateKeyToUse))
            try
            {
                rsa.ImportFromPem(privateKeyToUse);
            }
            catch (CryptographicException ex)
            {
                if (!string.IsNullOrEmpty(jwtSettings.PrivateKeyPassword))
                    throw new InvalidOperationException(
                        "Failed to import private key. Password-protected PEM keys are not directly supported by ImportFromPem. " +
                        "Please decrypt the key first using OpenSSL (openssl rsa -in encrypted_key.pem -out decrypted_key.pem) " +
                        "or provide an unencrypted key. For security, use a secrets manager to store decrypted keys.",
                        ex);
                throw;
            }
        else if (!string.IsNullOrEmpty(jwtSettings.PublicKey))
            rsa.ImportFromPem(jwtSettings.PublicKey);
        else
            // Generate new keys if not provided (for development)
            rsa.KeySize = 2048; // RSA-2048 is quantum-resistant enough for most use cases

        var key = new RsaSecurityKey(rsa)
        {
            KeyId = "auth-key-1"
        };

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.Zero // Remove default 5-minute clock skew
                };

                // Events for debugging
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                            context.Response.Headers.Append("Token-Expired", "true");
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    ///     Adds authentication services (IAuthenticationService, IUserService, etc.)
    ///     Note: This does NOT add the database context or Identity - those should be added separately
    ///     via AddAuthenticationDatabasePostgreSql or AddAuthenticationDatabase
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add authentication services (IAuthenticationService, IUserService, etc.)
        // Do NOT call AddAuthenticationDatabase here - it should be called separately to avoid duplicate Identity registration
        services.AddAuthenticationServices();

        return services;
    }
}