using System.Text.Json;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Controllers;
using BoilerPlate.Authentication.WebApi.Services;
using BoilerPlate.Authentication.WebApi.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.WebApi.Tests.Controllers;

/// <summary>
///     Unit tests for WellKnownController, which exposes OpenID Connect discovery and JSON Web Key Set (JWKS) endpoints for ML-DSA.
/// </summary>
public class WellKnownControllerTests
{
    private readonly WellKnownController _controller;

    public WellKnownControllerTests()
    {
        var (fullJwk, _) = MlDsaKeyGenerator.GenerateKeyPair();
        var jwtSettings = new JwtSettings
        {
            Issuer = "https://auth.example.com",
            Audience = "test-audience",
            MldsaJwk = fullJwk
        };
        var jwtTokenService = new JwtTokenService(Options.Create(jwtSettings));
        _controller = new WellKnownController(Options.Create(jwtSettings), jwtTokenService);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
                {
                    Scheme = "https",
                    Host = new HostString("auth.example.com")
                }
            }
        };
    }

    /// <summary>
    ///     System under test: WellKnownController.GetJwks.
    ///     Test case: GetJwks is invoked to retrieve the public key set for JWT validation.
    ///     Expected result: Returns OkObjectResult with keys array containing one key; key has kty "AKP", alg "ML-DSA-65",
    ///     use "sig", and base64url-encoded public key in "x" property.
    /// </summary>
    [Fact]
    public void GetJwks_ShouldReturnMlDsaFormat()
    {
        var result = _controller.GetJwks();

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();

        var json = JsonSerializer.Serialize(okResult.Value);
        var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.GetProperty("keys");
        keys.GetArrayLength().Should().BeGreaterThan(0);

        var key0 = keys[0];
        key0.GetProperty("kty").GetString().Should().Be("AKP");
        key0.GetProperty("alg").GetString().Should().Be("ML-DSA-65");
        key0.GetProperty("use").GetString().Should().Be("sig");
        key0.TryGetProperty("x", out _).Should().BeTrue();
    }

    /// <summary>
    ///     System under test: WellKnownController.GetOpenIdConfiguration.
    ///     Test case: GetOpenIdConfiguration is invoked with request context providing scheme and host.
    ///     Expected result: Returns OkObjectResult with OpenIdConfiguration containing Issuer, AuthorizationEndpoint,
    ///     TokenEndpoint, and JwksUri populated from the request origin.
    /// </summary>
    [Fact]
    public void GetOpenIdConfiguration_ShouldReturnDiscoveryDocument()
    {
        var result = _controller.GetOpenIdConfiguration();

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var config = okResult.Value as WellKnownController.OpenIdConfiguration;
        config.Should().NotBeNull();
        config!.Issuer.Should().Be("https://auth.example.com");
        config.AuthorizationEndpoint.Should().Contain("/oauth/authorize");
        config.TokenEndpoint.Should().Contain("/oauth/token");
        config.JwksUri.Should().Contain("/jwks.json");
    }

    /// <summary>
    ///     System under test: WellKnownController.GetOpenIdConfiguration.
    ///     Test case: GetOpenIdConfiguration is invoked when OAuth2IssuerUrl is configured in JwtSettings.
    ///     Expected result: Issuer in the discovery document is the configured OAuth2IssuerUrl, not the request origin.
    /// </summary>
    [Fact]
    public void GetOpenIdConfiguration_WithOAuth2IssuerUrl_ShouldUseConfiguredIssuer()
    {
        var (fullJwk, _) = MlDsaKeyGenerator.GenerateKeyPair();
        var jwtSettings = new JwtSettings
        {
            Issuer = "https://auth.example.com",
            Audience = "test",
            OAuth2IssuerUrl = "https://custom.issuer.com",
            MldsaJwk = fullJwk
        };
        var jwtTokenService = new JwtTokenService(Options.Create(jwtSettings));
        var controller = new WellKnownController(Options.Create(jwtSettings), jwtTokenService);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = controller.GetOpenIdConfiguration();

        var okResult = (OkObjectResult)result;
        var config = okResult.Value as WellKnownController.OpenIdConfiguration;
        config!.Issuer.Should().Be("https://custom.issuer.com");
    }
}
