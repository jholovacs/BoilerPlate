using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Services;
using BoilerPlate.Authentication.WebApi.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.WebApi.Tests.Configuration;

/// <summary>
///     Unit tests for JwtBearerMlDsaPostConfigure, which post-configures JwtBearerOptions with the ML-DSA signing key from JwtTokenService.
/// </summary>
public class JwtBearerMlDsaPostConfigureTests
{
    /// <summary>
    ///     System under test: JwtBearerMlDsaPostConfigure.PostConfigure.
    ///     Test case: PostConfigure is invoked with JwtBearerOptions when JwtTokenService is initialized with a valid ML-DSA JWK.
    ///     Expected result: TokenValidationParameters.IssuerSigningKey is set to a non-null MlDsaSecurityKey with a valid KeyId.
    /// </summary>
    [Fact]
    public void PostConfigure_ShouldSetIssuerSigningKeyFromJwtTokenService()
    {
        var (fullJwk, _) = MlDsaKeyGenerator.GenerateKeyPair();
        var jwtSettings = new JwtSettings
        {
            Issuer = "test",
            Audience = "test",
            MldsaJwk = fullJwk
        };
        var jwtTokenService = new JwtTokenService(Options.Create(jwtSettings));
        var postConfigure = new JwtBearerMlDsaPostConfigure(jwtTokenService);

        var options = new JwtBearerOptions
        {
            TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters()
        };

        postConfigure.PostConfigure(null, options);

        options.TokenValidationParameters.IssuerSigningKey.Should().NotBeNull();
        options.TokenValidationParameters.IssuerSigningKey!.KeyId.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    ///     System under test: JwtBearerMlDsaPostConfigure.PostConfigure.
    ///     Test case: PostConfigure is invoked when JwtTokenService auto-generates keys (no MldsaJwk configured).
    ///     Expected result: TokenValidationParameters.IssuerSigningKey is set to a non-null key, enabling token validation.
    /// </summary>
    [Fact]
    public void PostConfigure_WithGeneratedKeys_ShouldSetValidKey()
    {
        var jwtSettings = new JwtSettings { Issuer = "test", Audience = "test" };
        var jwtTokenService = new JwtTokenService(Options.Create(jwtSettings));
        var postConfigure = new JwtBearerMlDsaPostConfigure(jwtTokenService);

        var options = new JwtBearerOptions
        {
            TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters()
        };

        postConfigure.PostConfigure("Bearer", options);

        options.TokenValidationParameters.IssuerSigningKey.Should().NotBeNull();
    }
}
