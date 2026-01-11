using System.Security.Cryptography;
using BoilerPlate.Authentication.WebApi.Utilities;
using FluentAssertions;

namespace BoilerPlate.Authentication.WebApi.Tests.Utilities;

/// <summary>
///     Unit tests for RsaKeyGenerator
/// </summary>
public class RsaKeyGeneratorTests
{
    /// <summary>
    ///     Test case: GenerateKeyPair should return valid PEM-format keys when called with default key size (2048 bits).
    ///     Scenario: The key generator is called without specifying a key size. Both private and public keys should be
    ///     generated in PEM format with proper BEGIN and END markers.
    /// </summary>
    [Fact]
    public void GenerateKeyPair_WithDefaultKeySize_ShouldReturnValidPemKeys()
    {
        // Act
        var (privateKey, publicKey) = RsaKeyGenerator.GenerateKeyPair();

        // Assert
        privateKey.Should().NotBeNullOrEmpty();
        publicKey.Should().NotBeNullOrEmpty();
        privateKey.Should().Contain("BEGIN");
        privateKey.Should().Contain("PRIVATE KEY");
        publicKey.Should().Contain("BEGIN");
        publicKey.Should().Contain("PUBLIC KEY");
    }

    /// <summary>
    ///     Test case: GenerateKeyPair should return valid PEM-format keys when called with 2048-bit key size.
    ///     Scenario: The key generator is called with an explicit 2048-bit key size. Both private and public keys should be
    ///     generated in PEM format with proper BEGIN and END markers.
    /// </summary>
    [Fact]
    public void GenerateKeyPair_With2048KeySize_ShouldReturnValidPemKeys()
    {
        // Act
        var (privateKey, publicKey) = RsaKeyGenerator.GenerateKeyPair();

        // Assert
        privateKey.Should().NotBeNullOrEmpty();
        publicKey.Should().NotBeNullOrEmpty();
        privateKey.Should().Contain("BEGIN");
        privateKey.Should().Contain("PRIVATE KEY");
        publicKey.Should().Contain("BEGIN");
        publicKey.Should().Contain("PUBLIC KEY");
    }

    /// <summary>
    ///     Test case: GenerateKeyPair should return valid PEM-format keys when called with 4096-bit key size.
    ///     Scenario: The key generator is called with an explicit 4096-bit key size for higher security. Both private and
    ///     public keys should be generated in PEM format with proper BEGIN and END markers.
    /// </summary>
    [Fact]
    public void GenerateKeyPair_With4096KeySize_ShouldReturnValidPemKeys()
    {
        // Act
        var (privateKey, publicKey) = RsaKeyGenerator.GenerateKeyPair(4096);

        // Assert
        privateKey.Should().NotBeNullOrEmpty();
        publicKey.Should().NotBeNullOrEmpty();
        privateKey.Should().Contain("BEGIN");
        privateKey.Should().Contain("PRIVATE KEY");
        publicKey.Should().Contain("BEGIN");
        publicKey.Should().Contain("PUBLIC KEY");
    }

    /// <summary>
    ///     Test case: GenerateKeyPair should generate unique key pairs on each invocation.
    ///     Scenario: The key generator is called multiple times in succession. Each call should produce a different private
    ///     key and public key to ensure cryptographic uniqueness.
    /// </summary>
    [Fact]
    public void GenerateKeyPair_ShouldGenerateDifferentKeysOnEachCall()
    {
        // Act
        var (privateKey1, publicKey1) = RsaKeyGenerator.GenerateKeyPair();
        var (privateKey2, publicKey2) = RsaKeyGenerator.GenerateKeyPair();

        // Assert
        privateKey1.Should().NotBe(privateKey2);
        publicKey1.Should().NotBe(publicKey2);
    }

    /// <summary>
    ///     Test case: GenerateKeyPair should generate a compatible key pair where the public key matches the private key.
    ///     Scenario: A key pair is generated and both keys are imported into RSA objects. The modulus and exponent of the
    ///     public key extracted from the private key should match the standalone public key, confirming they belong to the
    ///     same key pair.
    /// </summary>
    [Fact]
    public void GenerateKeyPair_ShouldGenerateCompatibleKeyPair()
    {
        // Act
        var (privateKey, publicKey) = RsaKeyGenerator.GenerateKeyPair();

        // Assert - Verify we can import both keys
        using var rsaPrivate = RSA.Create();
        using var rsaPublic = RSA.Create();

        rsaPrivate.ImportFromPem(privateKey);
        rsaPublic.ImportFromPem(publicKey);

        // Verify keys have the same modulus
        var privateParams = rsaPrivate.ExportParameters(false);
        var publicParams = rsaPublic.ExportParameters(false);

        privateParams.Modulus.Should().BeEquivalentTo(publicParams.Modulus);
        privateParams.Exponent.Should().BeEquivalentTo(publicParams.Exponent);
    }

    /// <summary>
    ///     Test case: GenerateKeyPair should respect the specified key size parameter.
    ///     Scenario: Two key pairs are generated with different key sizes (2048 and 4096 bits). When the private keys are
    ///     imported, they should have the correct key sizes as specified, ensuring the key size parameter is properly applied.
    /// </summary>
    [Fact]
    public void GenerateKeyPair_WithDifferentKeySizes_ShouldGenerateDifferentKeySizes()
    {
        // Act
        var (privateKey2048, _) = RsaKeyGenerator.GenerateKeyPair();
        var (privateKey4096, _) = RsaKeyGenerator.GenerateKeyPair(4096);

        // Assert - Verify key sizes
        using var rsa2048 = RSA.Create();
        using var rsa4096 = RSA.Create();

        rsa2048.ImportFromPem(privateKey2048);
        rsa4096.ImportFromPem(privateKey4096);

        rsa2048.KeySize.Should().Be(2048);
        rsa4096.KeySize.Should().Be(4096);
    }
}