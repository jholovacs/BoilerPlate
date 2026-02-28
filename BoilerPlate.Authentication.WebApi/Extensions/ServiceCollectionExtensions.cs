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
    ///     Adds JWT authentication with ML-DSA (post-quantum digital signatures).
    ///     Uses ML-DSA-65 (NIST FIPS 204) for PQC resistance.
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

        // Override ML-DSA JWK from environment variable if provided
        var envMldsaJwk = configuration["JWT_MLDSA_JWK"] ?? Environment.GetEnvironmentVariable("JWT_MLDSA_JWK");
        if (!string.IsNullOrWhiteSpace(envMldsaJwk))
        {
            // May be base64-encoded (common in Docker) or raw JSON
            if (!envMldsaJwk.Contains("{"))
                try
                {
                    var keyBytes = Convert.FromBase64String(envMldsaJwk);
                    envMldsaJwk = Encoding.UTF8.GetString(keyBytes);
                }
                catch (FormatException)
                {
                    // Treat as raw JSON
                }
            envMldsaJwk = envMldsaJwk.Replace("\\n", "\n").Replace("\r\n", "\n").Replace("\r", "\n");
            jwtSettings.MldsaJwk = envMldsaJwk;
        }

        // Override issuer URL from environment
        var envIssuerUrl = configuration["JWT_ISSUER_URL"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER_URL");
        if (!string.IsNullOrWhiteSpace(envIssuerUrl)) jwtSettings.Issuer = envIssuerUrl.TrimEnd('/');

        services.Configure<JwtSettings>(options =>
        {
            options.Issuer = jwtSettings.Issuer;
            options.Audience = jwtSettings.Audience;
            options.ExpirationMinutes = jwtSettings.ExpirationMinutes;
            options.RefreshTokenExpirationDays = jwtSettings.RefreshTokenExpirationDays;
            options.MldsaJwk = jwtSettings.MldsaJwk;
        });
        services.AddSingleton<JwtTokenService>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<JwtSettings>>();
            return new JwtTokenService(settings);
        });

        // Post-configure JwtBearer with ML-DSA key from JwtTokenService (resolved at runtime)
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerMlDsaPostConfigure>();

        // Register refresh token service with Data Protection for encryption
        services.AddScoped<RefreshTokenService>();

        // Register MFA challenge token service with Data Protection for encryption
        services.AddScoped<MfaChallengeTokenService>();

        // Register authorization code service for OAuth2 Authorization Code Grant flow
        services.AddScoped<AuthorizationCodeService>();
        services.AddScoped<OAuthClientService>();

        var jwtSettingsForAuth = jwtSettings;

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
                    ValidIssuer = jwtSettingsForAuth.Issuer,
                    ValidAudience = jwtSettingsForAuth.Audience,
                    // IssuerSigningKey set by JwtBearerMlDsaPostConfigure
                    ClockSkew = TimeSpan.Zero
                };

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
        services.AddAuthenticationServices();
        return services;
    }
}
