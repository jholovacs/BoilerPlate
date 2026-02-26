using System.Security.Cryptography;
using System.Text;
using BoilerPlate.Diagnostics.WebApi.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace BoilerPlate.Diagnostics.WebApi.Extensions;

/// <summary>
///     DI extensions for JWT validation and authorization (tokens from Authentication WebApi).
///     Supports JWT_PUBLIC_KEY (env), JWT_ISSUER_URL (fetch from JWKS), or JWT_JWKS_URL (direct JWKS fetch).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds JWT Bearer validation and authorization policies for diagnostics OData.
    ///     Uses the same JwtSettings section as the Authentication WebApi so tokens issued there are accepted.
    /// </summary>
    public static IServiceCollection AddDiagnosticsJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Always register the authorization policy (required by [Authorize(Policy = ...)] on controllers)
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.DiagnosticsODataAccess, policy =>
                policy.RequireRole("Service Administrator", "Tenant Administrator"));
            options.AddPolicy(AuthorizationPolicies.ServiceAdministratorOnly, policy =>
                policy.RequireRole("Service Administrator"));
        });

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        if (string.IsNullOrWhiteSpace(jwtSettings.Issuer)) jwtSettings.Issuer = "BoilerPlate.Authentication";
        if (string.IsNullOrWhiteSpace(jwtSettings.Audience)) jwtSettings.Audience = "BoilerPlate.API";

        SecurityKey? signingKey = null;

        // 1. Try JWT_PUBLIC_KEY (explicit PEM or base64)
        var envPublicKey = configuration["JWT_PUBLIC_KEY"] ?? Environment.GetEnvironmentVariable("JWT_PUBLIC_KEY");
        if (!string.IsNullOrWhiteSpace(envPublicKey))
        {
            if (!envPublicKey.Contains("-----BEGIN"))
            {
                try
                {
                    var keyBytes = Convert.FromBase64String(envPublicKey);
                    envPublicKey = Encoding.UTF8.GetString(keyBytes);
                }
                catch (FormatException) { /* treat as PEM */ }
            }
            envPublicKey = envPublicKey.Replace("\\n", "\n").Replace("\r\n", "\n").Replace("\r", "\n");
            try
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(envPublicKey);
                signingKey = new RsaSecurityKey(rsa) { KeyId = "auth-key-1" };
            }
            catch { /* fall through to JWKS */ }
        }

        // 2. Try JWKS fetch (JWT_ISSUER_URL or JWT_JWKS_URL)
        if (signingKey == null)
        {
            var jwksUrl = configuration["JWT_JWKS_URL"] ?? Environment.GetEnvironmentVariable("JWT_JWKS_URL");
            if (string.IsNullOrWhiteSpace(jwksUrl))
            {
                var issuerUrl = configuration["JWT_ISSUER_URL"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER_URL");
                if (!string.IsNullOrWhiteSpace(issuerUrl))
                    jwksUrl = $"{issuerUrl.TrimEnd('/')}/.well-known/jwks.json";
            }
            if (!string.IsNullOrWhiteSpace(jwksUrl))
                signingKey = FetchKeyFromJwks(jwksUrl);
        }

        if (signingKey == null)
        {
            AddAuthenticationWithRejectAllScheme(services);
            return services;
        }

        var key = signingKey;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var path = context.HttpContext.Request.Path;
                    if (path.StartsWithSegments("/hubs"))
                    {
                        var token = context.Request.Query["access_token"].FirstOrDefault();
                        if (!string.IsNullOrEmpty(token))
                            context.Token = token;
                    }
                    return Task.CompletedTask;
                }
            };
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }

    /// <summary>
    ///     Adds JWT Bearer auth with a random key when JWT is not configured.
    ///     Ensures DefaultChallengeScheme exists so [Authorize] doesn't throw; all requests get 401.
    /// </summary>
    private static void AddAuthenticationWithRejectAllScheme(IServiceCollection services)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(rsa.ExportRSAPublicKey(), out _);
        var key = new RsaSecurityKey(rsa) { KeyId = "placeholder" };

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false
            };
        });
    }

    /// <summary>
    ///     Fetches the signing key from the auth service JWKS endpoint.
    ///     Used when JWT_PUBLIC_KEY is not set but JWT_ISSUER_URL or JWT_JWKS_URL is configured.
    /// </summary>
    private static SecurityKey? FetchKeyFromJwks(string jwksUrl)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var json = client.GetStringAsync(jwksUrl).GetAwaiter().GetResult();
            var jwks = new JsonWebKeySet(json);
            return jwks.GetSigningKeys().FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
