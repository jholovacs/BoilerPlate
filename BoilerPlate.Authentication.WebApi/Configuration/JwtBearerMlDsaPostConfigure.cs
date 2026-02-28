using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Strathweb.Dilithium.IdentityModel;

namespace BoilerPlate.Authentication.WebApi.Configuration;

/// <summary>
///     Post-configures JwtBearerOptions with the ML-DSA signing key from JwtTokenService.
///     Required because the key is created by JwtTokenService which depends on configured JwtSettings.
/// </summary>
public class JwtBearerMlDsaPostConfigure : IPostConfigureOptions<JwtBearerOptions>
{
    private readonly JwtTokenService _jwtTokenService;

    public JwtBearerMlDsaPostConfigure(JwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        var jwk = _jwtTokenService.GetPublicKeyJwk();
        options.TokenValidationParameters.IssuerSigningKey = new MlDsaSecurityKey(jwk);
    }
}
