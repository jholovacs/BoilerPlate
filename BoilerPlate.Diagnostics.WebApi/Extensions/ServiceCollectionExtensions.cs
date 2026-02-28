using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoilerPlate.Diagnostics.WebApi.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Strathweb.Dilithium.IdentityModel;

namespace BoilerPlate.Diagnostics.WebApi.Extensions;

/// <summary>
///     DI extensions for JWT validation and authorization (tokens from Authentication WebApi).
///     Supports JWT_MLDSA_JWK (env), JWT_ISSUER_URL (fetch from JWKS), or JWT_JWKS_URL (direct JWKS fetch).
///     Uses ML-DSA (post-quantum) for token validation.
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

        // 1. Try JWT_MLDSA_JWK or JWT_PUBLIC_KEY (ML-DSA JWK, base64 or JSON)
        var envMldsaJwk = configuration["JWT_MLDSA_JWK"] ?? Environment.GetEnvironmentVariable("JWT_MLDSA_JWK");
        var envPublicKey = configuration["JWT_PUBLIC_KEY"] ?? Environment.GetEnvironmentVariable("JWT_PUBLIC_KEY");
        var jwkInput = !string.IsNullOrWhiteSpace(envMldsaJwk) ? envMldsaJwk : envPublicKey;

        if (!string.IsNullOrWhiteSpace(jwkInput))
        {
            signingKey = TryParseMldsaJwk(jwkInput);
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
                signingKey = FetchMldsaKeyFromJwks(jwksUrl);
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

    private static SecurityKey? TryParseMldsaJwk(string input)
    {
        try
        {
            var json = input.Contains("{") ? input : Encoding.UTF8.GetString(Convert.FromBase64String(input));
            json = json.Replace("\\n", "\n").Replace("\r\n", "\n").Replace("\r", "\n");
            var jwk = new JsonWebKey(json);
            if (jwk.Kty == "AKP" || (jwk.Alg ?? "").StartsWith("ML-DSA"))
                return new MlDsaSecurityKey(jwk);
        }
        catch { /* ignore */ }
        return null;
    }

    /// <summary>
    ///     Fetches the ML-DSA signing key from the auth service JWKS endpoint.
    ///     Internal overload accepts optional HttpClient for unit testing.
    /// </summary>
    /// <param name="jwksUrl">URL of the JWKS endpoint (e.g. https://auth.example.com/.well-known/jwks.json)</param>
    /// <param name="httpClient">Optional HttpClient for testing; when null, creates a new client with 10s timeout</param>
    /// <returns>MlDsaSecurityKey if a valid ML-DSA key is found, otherwise null</returns>
    internal static SecurityKey? FetchMldsaKeyFromJwks(string jwksUrl, HttpClient? httpClient = null)
    {
        var client = httpClient ?? new HttpClient();
        var shouldDispose = httpClient == null;
        if (shouldDispose)
            client.Timeout = TimeSpan.FromSeconds(10);
        try
        {
            var json = client.GetStringAsync(jwksUrl).GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var keys = doc.RootElement.GetProperty("keys");
            foreach (var keyEl in keys.EnumerateArray())
            {
                var kty = keyEl.TryGetProperty("kty", out var k) ? k.GetString() : null;
                if (kty == "AKP" || (keyEl.TryGetProperty("alg", out var a) && (a.GetString() ?? "").StartsWith("ML-DSA")))
                {
                    var jwkJson = keyEl.GetRawText();
                    var jwk = new JsonWebKey(jwkJson);
                    return new MlDsaSecurityKey(jwk);
                }
            }
        }
        catch { /* ignore */ }
        finally
        {
            if (shouldDispose)
                client.Dispose();
        }
        return null;
    }

    /// <summary>
    ///     Adds JWT Bearer auth with a placeholder key when JWT is not configured.
    ///     Ensures DefaultChallengeScheme exists so [Authorize] doesn't throw; all requests get 401.
    /// </summary>
    private static void AddAuthenticationWithRejectAllScheme(IServiceCollection services)
    {
        // Use a minimal ML-DSA key as placeholder (validation will fail for any real token)
        var key = new MlDsaSecurityKey("ML-DSA-65");

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
}
