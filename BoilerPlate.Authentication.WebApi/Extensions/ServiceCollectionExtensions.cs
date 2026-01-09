using BoilerPlate.Authentication.Database.Extensions;
using BoilerPlate.Authentication.Services.Extensions;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace BoilerPlate.Authentication.WebApi.Extensions;

/// <summary>
/// Extension methods for configuring services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds JWT authentication with RS256 (RSA asymmetric encryption)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException($"JWT settings not found in configuration section '{JwtSettings.SectionName}'");

        // Override expiration minutes from environment variable if provided
        var envExpirationMinutes = configuration["JWT_EXPIRATION_MINUTES"];
        if (!string.IsNullOrWhiteSpace(envExpirationMinutes) && int.TryParse(envExpirationMinutes, out var expirationMinutes))
        {
            jwtSettings.ExpirationMinutes = expirationMinutes;
        }

        // Override private key from environment variable if provided
        var envPrivateKey = configuration["JWT_PRIVATE_KEY"];
        if (!string.IsNullOrWhiteSpace(envPrivateKey))
        {
            jwtSettings.PrivateKey = envPrivateKey;
        }

        // Override public key from environment variable if provided
        var envPublicKey = configuration["JWT_PUBLIC_KEY"];
        if (!string.IsNullOrWhiteSpace(envPublicKey))
        {
            jwtSettings.PublicKey = envPublicKey;
        }

        // Override private key password from environment variable if provided
        var envPrivateKeyPassword = configuration["JWT_PRIVATE_KEY_PASSWORD"];
        if (!string.IsNullOrWhiteSpace(envPrivateKeyPassword))
        {
            jwtSettings.PrivateKeyPassword = envPrivateKeyPassword;
        }

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

        // Configure RSA key
        RSA rsa = RSA.Create();
        string? privateKeyToUse = jwtSettings.PrivateKey;
        
        // Note: .NET's ImportFromPem doesn't support encrypted PEM keys directly
        // If you have an encrypted key, decrypt it first using OpenSSL:
        // openssl rsa -in encrypted_key.pem -out decrypted_key.pem
        // The JWT_PRIVATE_KEY_PASSWORD environment variable is reserved for future use
        // or custom decryption implementations

        if (!string.IsNullOrEmpty(privateKeyToUse))
        {
            try
            {
                rsa.ImportFromPem(privateKeyToUse);
            }
            catch (CryptographicException ex)
            {
                if (!string.IsNullOrEmpty(jwtSettings.PrivateKeyPassword))
                {
                    throw new InvalidOperationException(
                        "Failed to import private key. Password-protected PEM keys are not directly supported by ImportFromPem. " +
                        "Please decrypt the key first using OpenSSL (openssl rsa -in encrypted_key.pem -out decrypted_key.pem) " +
                        "or provide an unencrypted key. For security, use a secrets manager to store decrypted keys.", ex);
                }
                throw;
            }
        }
        else if (!string.IsNullOrEmpty(jwtSettings.PublicKey))
        {
            rsa.ImportFromPem(jwtSettings.PublicKey);
        }
        else
        {
            // Generate new keys if not provided (for development)
            rsa.KeySize = 2048; // RSA-2048 is quantum-resistant enough for most use cases
        }

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
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

    /// <summary>
    /// Adds authentication database and services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add database context
        services.AddAuthenticationDatabase(configuration);

        // Add authentication services
        services.AddAuthenticationServices();

        return services;
    }
}
