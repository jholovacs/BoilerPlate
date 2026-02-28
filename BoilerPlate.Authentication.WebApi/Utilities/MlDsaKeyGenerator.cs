using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Strathweb.Dilithium.IdentityModel;

namespace BoilerPlate.Authentication.WebApi.Utilities;

/// <summary>
///     Utility class for generating ML-DSA key pairs for JWT signing (post-quantum cryptography).
/// </summary>
public static class MlDsaKeyGenerator
{
    private const string Algorithm = "ML-DSA-65";

    /// <summary>
    ///     Generates a new ML-DSA key pair and returns the full JWK (JSON Web Key) as JSON string.
    /// </summary>
    /// <returns>Full JWK JSON including private key (for signing) and public key</returns>
    public static string GenerateKeyPairJwk()
    {
        var key = new MlDsaSecurityKey(Algorithm);
        var jwk = key.ToJsonWebKey(includePrivateKey: true);
        return JsonSerializer.Serialize(jwk);
    }

    /// <summary>
    ///     Generates a new ML-DSA key pair and returns (full JWK, public-only JWK).
    /// </summary>
    public static (string FullJwk, string PublicJwk) GenerateKeyPair()
    {
        var key = new MlDsaSecurityKey(Algorithm);
        var fullJwk = key.ToJsonWebKey(includePrivateKey: true);
        var publicJwk = key.ToJsonWebKey(includePrivateKey: false);
        return (JsonSerializer.Serialize(fullJwk), JsonSerializer.Serialize(publicJwk));
    }

    /// <summary>
    ///     Generates a new ML-DSA key pair and writes the JWK to console (for development).
    /// </summary>
    public static void GenerateAndPrintKeyPair()
    {
        var (fullJwk, publicJwk) = GenerateKeyPair();

        Console.WriteLine("=== ML-DSA Full JWK (for JWT_MLDSA_JWK / signing) ===");
        Console.WriteLine(fullJwk);
        Console.WriteLine();
        Console.WriteLine("=== ML-DSA Public JWK (for validation only) ===");
        Console.WriteLine(publicJwk);
        Console.WriteLine();
        Console.WriteLine("=== Base64-encoded full JWK (for env vars) ===");
        Console.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(fullJwk)));
    }

    /// <summary>
    ///     Generates ML-DSA keys and writes to jwt-keys directory. Used by make setup-keys.
    /// </summary>
    /// <param name="jwtKeysDir">Directory for jwt-keys (default: jwt-keys in current dir)</param>
    /// <returns>Path to the generated full JWK file</returns>
    public static string GenerateAndWriteToDirectory(string? jwtKeysDir = null)
    {
        var dir = string.IsNullOrEmpty(jwtKeysDir) ? Path.Combine(Directory.GetCurrentDirectory(), "jwt-keys") : jwtKeysDir;
        Directory.CreateDirectory(dir);

        var (fullJwk, _) = GenerateKeyPair();
        var jwkPath = Path.Combine(dir, "mldsa_jwk.json");
        var base64Path = Path.Combine(dir, "mldsa_jwk_base64.txt");

        File.WriteAllText(jwkPath, fullJwk);
        File.WriteAllText(base64Path, Convert.ToBase64String(Encoding.UTF8.GetBytes(fullJwk)));

        return jwkPath;
    }
}
