using System.IO;
using System.Text.Json;
using BoilerPlate.Authentication.WebApi.Utilities;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Strathweb.Dilithium.IdentityModel;

namespace BoilerPlate.Authentication.WebApi.Tests.Utilities;

/// <summary>
///     Unit tests for MlDsaKeyGenerator, the utility for generating ML-DSA (post-quantum) key pairs for JWT signing.
/// </summary>
public class MlDsaKeyGeneratorTests
{
    /// <summary>
    ///     System under test: MlDsaKeyGenerator.GenerateKeyPair.
    ///     Test case: GenerateKeyPair is called with default parameters.
    ///     Expected result: Returns a tuple of (full JWK, public JWK) containing valid JSON with kty "AKP", alg "ML-DSA",
    ///     and required key components (x for public, d for private in full JWK).
    /// </summary>
    [Fact]
    public void GenerateKeyPair_ShouldReturnValidJwkKeys()
    {
        var (fullJwk, publicJwk) = MlDsaKeyGenerator.GenerateKeyPair();

        fullJwk.Should().NotBeNullOrEmpty();
        publicJwk.Should().NotBeNullOrEmpty();
        fullJwk.Should().Contain("kty");
        fullJwk.Should().Contain("AKP");
        fullJwk.Should().Contain("ML-DSA");
    }

    /// <summary>
    ///     System under test: MlDsaKeyGenerator.GenerateKeyPair.
    ///     Test case: GenerateKeyPair is called multiple times in succession.
    ///     Expected result: Each call produces a cryptographically unique key pair; full JWKs from different calls differ.
    /// </summary>
    [Fact]
    public void GenerateKeyPair_ShouldGenerateDifferentKeysOnEachCall()
    {
        var (full1, _) = MlDsaKeyGenerator.GenerateKeyPair();
        var (full2, _) = MlDsaKeyGenerator.GenerateKeyPair();

        full1.Should().NotBe(full2);
    }

    /// <summary>
    ///     System under test: MlDsaKeyGenerator.GenerateKeyPair.
    ///     Test case: Full JWK and public-only JWK are loaded into MlDsaSecurityKey instances.
    ///     Expected result: Public key bytes match between full and public-only keys; full key has private key; public-only has null private key.
    /// </summary>
    [Fact]
    public void GenerateKeyPair_ShouldGenerateCompatibleKeyPair()
    {
        var (fullJwk, publicJwk) = MlDsaKeyGenerator.GenerateKeyPair();

        var fullKey = new MlDsaSecurityKey(new JsonWebKey(fullJwk));
        var publicKey = new MlDsaSecurityKey(new JsonWebKey(publicJwk));

        fullKey.PublicKey.Should().BeEquivalentTo(publicKey.PublicKey);
        fullKey.PrivateKey.Should().NotBeNull();
        publicKey.PrivateKey.Should().BeNull();
    }

    /// <summary>
    ///     System under test: MlDsaKeyGenerator.GenerateKeyPairJwk.
    ///     Test case: GenerateKeyPairJwk is called to produce a single full JWK string.
    ///     Expected result: Returns valid JSON containing kty, alg, x (public key), and d (private key) properties.
    /// </summary>
    [Fact]
    public void GenerateKeyPairJwk_ShouldReturnValidJson()
    {
        var jwk = MlDsaKeyGenerator.GenerateKeyPairJwk();

        jwk.Should().NotBeNullOrEmpty();
        var doc = JsonDocument.Parse(jwk);
        doc.RootElement.TryGetProperty("kty", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("alg", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("x", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("d", out _).Should().BeTrue();
    }

    /// <summary>
    ///     System under test: MlDsaKeyGenerator.GenerateAndWriteToDirectory.
    ///     Test case: GenerateAndWriteToDirectory is called with a temporary directory path.
    ///     Expected result: Creates mldsa_jwk.json and mldsa_jwk_base64.txt in the directory; JWK contains kty "AKP";
    ///     base64 file decodes to valid JWK JSON.
    /// </summary>
    [Fact]
    public void GenerateAndWriteToDirectory_ShouldCreateJwkAndBase64Files()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "mldsa-keys-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var path = MlDsaKeyGenerator.GenerateAndWriteToDirectory(tempDir);

            path.Should().EndWith("mldsa_jwk.json");
            File.Exists(path).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "mldsa_jwk_base64.txt")).Should().BeTrue();

            var jwkContent = File.ReadAllText(path);
            jwkContent.Should().Contain("kty");
            jwkContent.Should().Contain("AKP");

            var base64Content = File.ReadAllText(Path.Combine(tempDir, "mldsa_jwk_base64.txt"));
            base64Content.Should().NotBeNullOrEmpty();
            var decoded = Convert.FromBase64String(base64Content);
            var decodedJson = System.Text.Encoding.UTF8.GetString(decoded);
            decodedJson.Should().Contain("kty");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    ///     System under test: MlDsaKeyGenerator.GenerateAndWriteToDirectory.
    ///     Test case: GenerateAndWriteToDirectory is called with a custom output directory path.
    ///     Expected result: Returns a path that starts with the custom directory and ends with mldsa_jwk.json.
    /// </summary>
    [Fact]
    public void GenerateAndWriteToDirectory_WithCustomDir_ShouldReturnPathInThatDirectory()
    {
        var customDir = Path.Combine(Path.GetTempPath(), "mldsa-custom-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var path = MlDsaKeyGenerator.GenerateAndWriteToDirectory(customDir);

            path.Should().StartWith(customDir);
            path.Should().EndWith("mldsa_jwk.json");
        }
        finally
        {
            if (Directory.Exists(customDir))
                Directory.Delete(customDir, true);
        }
    }
}
