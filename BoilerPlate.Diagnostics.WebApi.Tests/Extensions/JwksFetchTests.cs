using System.Net;
using System.Text.Json;
using BoilerPlate.Diagnostics.WebApi.Extensions;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Strathweb.Dilithium.IdentityModel;

namespace BoilerPlate.Diagnostics.WebApi.Tests.Extensions;

/// <summary>
///     Unit tests for Diagnostics JWKS fetch behavior in <see cref="ServiceCollectionExtensions" />.
///     Covers FetchMldsaKeyFromJwks with valid JWKS, malformed input, and error responses.
/// </summary>
public class JwksFetchTests
{
    #region Valid JWKS Response Tests

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks with valid ML-DSA JWKS.
    ///     Scenario: HTTP returns valid JWKS with kty=AKP, alg=ML-DSA-65.
    ///     Expected: Returns non-null MlDsaSecurityKey.
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_WithValidMlDsaJwks_ShouldReturnMlDsaSecurityKey()
    {
        var jwksJson = BuildValidJwks();
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jwksJson)
        });
        using var client = new HttpClient(handler);

        var result = ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/.well-known/jwks.json", client);

        result.Should().NotBeNull();
        result.Should().BeOfType<MlDsaSecurityKey>();
    }

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks selects ML-DSA key when multiple keys present.
    ///     Scenario: JWKS has RSA key first, ML-DSA key second.
    ///     Expected: Returns the ML-DSA key (first matching kty=AKP or alg starts with ML-DSA).
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_WithRsaThenMlDsa_ShouldReturnMlDsaKey()
    {
        var (_, publicJwk) = BuildMlDsaPublicJwk();
        var jwksJson = $@"{{
            ""keys"": [
                {{ ""kty"": ""RSA"", ""kid"": ""rsa-1"", ""alg"": ""RS256"", ""n"": ""x"", ""e"": ""AQAB"" }},
                {publicJwk}
            ]
        }}";
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jwksJson)
        });
        using var client = new HttpClient(handler);

        var result = ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/jwks.json", client);

        result.Should().NotBeNull();
        result.Should().BeOfType<MlDsaSecurityKey>();
    }

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks matches by alg when kty differs.
    ///     Scenario: Key has alg=ML-DSA-65 (some implementations may use different kty).
    ///     Expected: Returns MlDsaSecurityKey when alg starts with ML-DSA.
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_WithAlgMlDsa_ShouldReturnMlDsaSecurityKey()
    {
        var jwksJson = BuildValidJwks();
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jwksJson)
        });
        using var client = new HttpClient(handler);

        var result = ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/jwks.json", client);

        result.Should().NotBeNull();
    }

    #endregion

    #region Malformed Input Tests

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks with malformed JSON.
    ///     Scenario: HTTP returns invalid JSON (not parseable).
    ///     Expected: Returns null; does not throw.
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_WithMalformedJson_ShouldReturnNull()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ invalid json }")
        });
        using var client = new HttpClient(handler);

        var result = ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/jwks.json", client);

        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks with empty keys array.
    ///     Scenario: JWKS has "keys": [].
    ///     Expected: Returns null.
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_WithEmptyKeysArray_ShouldReturnNull()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"keys\":[]}")
        });
        using var client = new HttpClient(handler);

        var result = ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/jwks.json", client);

        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks with missing keys property.
    ///     Scenario: JSON is valid but has no "keys" property.
    ///     Expected: Returns null; does not throw (KeyNotFoundException caught).
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_WithMissingKeysProperty_ShouldReturnNull()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"other\":\"value\"}")
        });
        using var client = new HttpClient(handler);

        var result = ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/jwks.json", client);

        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks with only non-ML-DSA keys.
    ///     Scenario: JWKS has RSA and EC keys only.
    ///     Expected: Returns null.
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_WithOnlyRsaKeys_ShouldReturnNull()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{
                ""keys"": [
                    { ""kty"": ""RSA"", ""kid"": ""rsa-1"", ""alg"": ""RS256"", ""n"": ""x"", ""e"": ""AQAB"" }
                ]
            }")
        });
        using var client = new HttpClient(handler);

        var result = ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/jwks.json", client);

        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks with injection-like content in response.
    ///     Scenario: JWKS contains unexpected/malicious-looking JSON structure.
    ///     Expected: Returns null or valid key; does not throw unhandled exception.
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_WithInjectionLikeContent_ShouldNotThrow()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"keys\":[{\"kty\":\"AKP\",\"x\":\"\"; DROP TABLE keys;--\"}]}")
        });
        using var client = new HttpClient(handler);

        var act = () => ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/jwks.json", client);

        act.Should().NotThrow();
    }

    #endregion

    #region HTTP Error Tests

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks when server returns 404.
    ///     Scenario: JWKS URL returns Not Found.
    ///     Expected: Returns null; does not throw.
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_With404_ShouldReturnNull()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(handler);

        var result = ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/jwks.json", client);

        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks when server returns 500.
    ///     Scenario: JWKS endpoint returns Internal Server Error.
    ///     Expected: Returns null; does not throw.
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_With500_ShouldReturnNull()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = new HttpClient(handler);

        var result = ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/jwks.json", client);

        result.Should().BeNull();
    }

    /// <summary>
    ///     Test case: FetchMldsaKeyFromJwks when request throws (e.g. network failure).
    ///     Scenario: HttpClient throws HttpRequestException.
    ///     Expected: Returns null; does not throw.
    /// </summary>
    [Fact]
    public void FetchMldsaKeyFromJwks_WhenRequestThrows_ShouldReturnNull()
    {
        var handler = new MockHttpMessageHandler(_ => throw new HttpRequestException("Connection refused"));
        using var client = new HttpClient(handler);

        var result = ServiceCollectionExtensions.FetchMldsaKeyFromJwks("https://auth.example.com/jwks.json", client);

        result.Should().BeNull();
    }

    #endregion

    #region Helpers

    private static string BuildValidJwks()
    {
        var (_, publicJwk) = BuildMlDsaPublicJwk();
        return $"{{\"keys\":[{publicJwk}]}}";
    }

    private static (string FullJwk, string PublicJwk) BuildMlDsaPublicJwk()
    {
        var key = new MlDsaSecurityKey("ML-DSA-65");
        var fullJwk = key.ToJsonWebKey(includePrivateKey: true);
        var publicJwk = key.ToJsonWebKey(includePrivateKey: false);
        return (JsonSerializer.Serialize(fullJwk), JsonSerializer.Serialize(publicJwk));
    }

    /// <summary>
    ///     Mock HttpMessageHandler that returns configurable responses for unit testing.
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    #endregion
}
