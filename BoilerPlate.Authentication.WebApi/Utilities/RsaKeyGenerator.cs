using System.Security.Cryptography;

namespace BoilerPlate.Authentication.WebApi.Utilities;

/// <summary>
/// Utility class for generating RSA key pairs for JWT signing
/// </summary>
public static class RsaKeyGenerator
{
    /// <summary>
    /// Generates a new RSA key pair and returns them in PEM format
    /// </summary>
    /// <param name="keySize">Key size in bits (2048 or 4096 recommended)</param>
    /// <returns>Tuple containing private key and public key in PEM format</returns>
    public static (string PrivateKey, string PublicKey) GenerateKeyPair(int keySize = 2048)
    {
        using var rsa = RSA.Create();
        rsa.KeySize = keySize;

        var privateKey = rsa.ExportRSAPrivateKeyPem();
        var publicKey = rsa.ExportRSAPublicKeyPem();

        return (privateKey, publicKey);
    }

    /// <summary>
    /// Generates a new RSA key pair and writes them to console (for development)
    /// </summary>
    /// <param name="keySize">Key size in bits</param>
    public static void GenerateAndPrintKeyPair(int keySize = 2048)
    {
        var (privateKey, publicKey) = GenerateKeyPair(keySize);

        Console.WriteLine("=== RSA Private Key (PEM) ===");
        Console.WriteLine(privateKey);
        Console.WriteLine();
        Console.WriteLine("=== RSA Public Key (PEM) ===");
        Console.WriteLine(publicKey);
    }
}
