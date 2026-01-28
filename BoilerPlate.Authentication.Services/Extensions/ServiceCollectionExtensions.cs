using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Services.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BoilerPlate.Authentication.Services.Extensions;

/// <summary>
///     Extension methods for registering authentication services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds authentication services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        // Register services
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ISaml2Service, Saml2Service>();
        services.AddScoped<ITenantEmailDomainService, TenantEmailDomainService>();
        services.AddScoped<ITenantVanityUrlService, TenantVanityUrlService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();

        return services;
    }
}