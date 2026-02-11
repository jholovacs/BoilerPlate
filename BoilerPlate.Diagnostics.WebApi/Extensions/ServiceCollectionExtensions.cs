using System.Security.Cryptography;
using System.Text;
using BoilerPlate.Diagnostics.WebApi.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace BoilerPlate.Diagnostics.WebApi.Extensions;

/// <summary>
///     DI extensions for JWT validation and authorization (tokens from Authentication WebApi).
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
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
        if (jwtSettings == null || string.IsNullOrWhiteSpace(jwtSettings.PublicKey))
        {
            // Allow running without JWT for local dev; endpoints will return 401 if [Authorize] is applied
            return services;
        }

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
            jwtSettings.PublicKey = envPublicKey;
        }

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(jwtSettings.PublicKey);
        }
        catch
        {
            return services;
        }

        var key = new RsaSecurityKey(rsa) { KeyId = "auth-key-1" };

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
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
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.DiagnosticsODataAccess, policy =>
                policy.RequireRole("Service Administrator", "Tenant Administrator"));
        });

        return services;
    }
}
