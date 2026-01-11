using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Services;
using BoilerPlate.Authentication.WebApi.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BoilerPlate.Authentication.WebApi.Tests.Services;

/// <summary>
///     Unit tests for JwtTokenService
/// </summary>
public class JwtTokenServiceTests
{
    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenService _jwtTokenService;

    public JwtTokenServiceTests()
    {
        var (privateKey, publicKey) = RsaKeyGenerator.GenerateKeyPair();
        _jwtSettings = new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 60,
            PrivateKey = privateKey,
            PublicKey = publicKey
        };

        var options = Options.Create(_jwtSettings);
        _jwtTokenService = new JwtTokenService(options);
    }

    #region GenerateToken Tests

    /// <summary>
    ///     Test case: GenerateToken should return a valid JWT token string when provided with a valid user and roles.
    ///     Scenario: A valid ApplicationUser with ID, username, email, and tenant ID, along with a list of roles, is passed to
    ///     GenerateToken. The service should generate a JWT token string that can be successfully decoded and parsed by a
    ///     JwtSecurityTokenHandler, confirming the token is well-formed and valid.
    /// </summary>
    [Fact]
    public void GenerateToken_WithValidUserAndRoles_ShouldReturnValidJwtToken()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = Guid.NewGuid()
        };
        var roles = new[] { "Admin", "User" };

        // Act
        var token = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        token.Should().NotBeNullOrEmpty();

        // Verify token can be decoded
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);
        jsonToken.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: GenerateToken should include all user-related claims in the generated JWT token.
    ///     Scenario: A user with ID, username, email, and tenant ID is passed to GenerateToken. After decoding the token, it
    ///     should contain claims for the subject (sub), email, unique name (username), tenant_id, and user_id, ensuring all
    ///     user information is properly embedded in the token for authorization and identification purposes.
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldIncludeUserClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = tenantId
        };
        var roles = new[] { "Admin" };

        // Act
        var token = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString());
        jsonToken.Claims.Should()
            .Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "test@example.com");
        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == "testuser");
        jsonToken.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
        jsonToken.Claims.Should().Contain(c => c.Type == "user_id" && c.Value == userId.ToString());
    }

    /// <summary>
    ///     Test case: GenerateToken should include role claims both as individual Role claims and as a JSON array in the roles
    ///     claim.
    ///     Scenario: A user is passed to GenerateToken with multiple roles (Admin, User, Guest). After decoding the token, it
    ///     should contain individual ClaimTypes.Role claims for each role and a "roles" claim containing a JSON-serialized
    ///     array of all roles, supporting both role-based authorization checks and bulk role retrieval scenarios.
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldIncludeRoleClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = Guid.NewGuid()
        };
        var roles = new[] { "Admin", "User", "Guest" };

        // Act
        var token = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Guest");
        jsonToken.Claims.Should().Contain(c => c.Type == "roles");

        var rolesClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "roles");
        rolesClaim.Should().NotBeNull();
        rolesClaim!.Value.Should().Contain("Admin");
        rolesClaim.Value.Should().Contain("User");
        rolesClaim.Value.Should().Contain("Guest");
    }

    /// <summary>
    ///     Test case: GenerateToken should not include role claims when an empty roles collection is provided.
    ///     Scenario: A user is passed to GenerateToken with an empty array of roles. After decoding the token, it should not
    ///     contain any ClaimTypes.Role claims or a "roles" claim, confirming that the service correctly handles users without
    ///     assigned roles.
    /// </summary>
    [Fact]
    public void GenerateToken_WithEmptyRoles_ShouldNotIncludeRoleClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = Guid.NewGuid()
        };
        var roles = Array.Empty<string>();

        // Act
        var token = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        jsonToken.Claims.Should().NotContain(c => c.Type == ClaimTypes.Role);
        jsonToken.Claims.Should().NotContain(c => c.Type == "roles");
    }

    /// <summary>
    ///     Test case: GenerateToken should not include role claims when a null roles collection is provided.
    ///     Scenario: A user is passed to GenerateToken with a null roles parameter. After decoding the token, it should not
    ///     contain any ClaimTypes.Role claims or a "roles" claim, confirming that the service gracefully handles null role
    ///     collections without throwing exceptions.
    /// </summary>
    [Fact]
    public void GenerateToken_WithNullRoles_ShouldNotIncludeRoleClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = Guid.NewGuid()
        };

        // Act
        var token = _jwtTokenService.GenerateToken(user, null);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        jsonToken.Claims.Should().NotContain(c => c.Type == ClaimTypes.Role);
        jsonToken.Claims.Should().NotContain(c => c.Type == "roles");
    }

    /// <summary>
    ///     Test case: GenerateToken should include first name and last name as GivenName and Surname claims when provided.
    ///     Scenario: A user with FirstName ("John") and LastName ("Doe") is passed to GenerateToken. After decoding the token,
    ///     it should contain ClaimTypes.GivenName with "John" and ClaimTypes.Surname with "Doe", enabling personalized token
    ///     content and user identification from claims.
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldIncludeFirstNameAndLastName()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
            TenantId = Guid.NewGuid()
        };
        var roles = Array.Empty<string>();

        // Act
        var token = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.GivenName && c.Value == "John");
        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Surname && c.Value == "Doe");
    }

    /// <summary>
    ///     Test case: GenerateToken should not include GivenName or Surname claims when first name and last name are not
    ///     provided.
    ///     Scenario: A user without FirstName or LastName values (null or empty) is passed to GenerateToken. After decoding
    ///     the token, it should not contain ClaimTypes.GivenName or ClaimTypes.Surname claims, confirming that optional user
    ///     information is only included when available.
    /// </summary>
    [Fact]
    public void GenerateToken_WithoutFirstNameAndLastName_ShouldNotIncludeNameClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = Guid.NewGuid()
        };
        var roles = Array.Empty<string>();

        // Act
        var token = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        jsonToken.Claims.Should().NotContain(c => c.Type == ClaimTypes.GivenName);
        jsonToken.Claims.Should().NotContain(c => c.Type == ClaimTypes.Surname);
    }

    /// <summary>
    ///     Test case: GenerateToken should include an expiration time (exp claim) based on the configured ExpirationMinutes
    ///     setting.
    ///     Scenario: A user is passed to GenerateToken with JwtSettings configured with a specific ExpirationMinutes value.
    ///     After decoding the token, it should contain a JwtRegisteredClaimNames.Exp claim and the ValidTo property should be
    ///     approximately ExpirationMinutes minutes from the current time, ensuring tokens expire as configured for security
    ///     purposes.
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldIncludeExpiration()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = Guid.NewGuid()
        };
        var roles = Array.Empty<string>();

        // Act
        var token = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        jsonToken.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            TimeSpan.FromSeconds(5));
        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Exp);
    }

    /// <summary>
    ///     Test case: GenerateToken should include the configured issuer and audience in the generated JWT token.
    ///     Scenario: A user is passed to GenerateToken with JwtSettings configured with specific Issuer and Audience values.
    ///     After decoding the token, it should contain these values in the Issuer property and Audiences collection, enabling
    ///     proper token validation and ensuring tokens are only accepted by intended recipients.
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldIncludeIssuerAndAudience()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = Guid.NewGuid()
        };
        var roles = Array.Empty<string>();

        // Act
        var token = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        jsonToken.Issuer.Should().Be(_jwtSettings.Issuer);
        jsonToken.Audiences.Should().Contain(_jwtSettings.Audience);
    }

    /// <summary>
    ///     Test case: GenerateToken should include a unique JWT ID (JTI) claim in each generated token.
    ///     Scenario: GenerateToken is called twice with the same user. After decoding both tokens, each should contain a
    ///     unique JwtRegisteredClaimNames.Jti claim value, ensuring token uniqueness and supporting token revocation and
    ///     tracking scenarios where individual tokens need to be identified.
    /// </summary>
    [Fact]
    public void GenerateToken_ShouldIncludeJti()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = Guid.NewGuid()
        };
        var roles = Array.Empty<string>();

        // Act
        var token1 = _jwtTokenService.GenerateToken(user, roles);
        var token2 = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken1 = handler.ReadJwtToken(token1);
        var jsonToken2 = handler.ReadJwtToken(token2);

        jsonToken1.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jsonToken2.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);

        var jti1 = jsonToken1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jsonToken2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        jti1.Should().NotBe(jti2); // Each token should have a unique JTI
    }

    /// <summary>
    ///     Test case: GenerateToken should include an empty email claim when the user's email is null.
    ///     Scenario: A user with a null Email property (inherited from IdentityUser) is passed to GenerateToken. After
    ///     decoding the token, it should contain a JwtRegisteredClaimNames.Email claim with an empty string value, ensuring
    ///     the email claim is always present even when the user has no email address configured.
    /// </summary>
    [Fact]
    public void GenerateToken_WithNullEmail_ShouldIncludeEmptyEmailClaim()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            TenantId = Guid.NewGuid()
            // Email is inherited from IdentityUser<Guid> and defaults to null
        };
        var roles = Array.Empty<string>();

        // Act
        var token = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        jsonToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == string.Empty);
    }

    /// <summary>
    ///     Test case: GenerateToken should filter out whitespace and empty role names before including them as claims.
    ///     Scenario: A user is passed to GenerateToken with a roles collection containing valid role names ("Admin", "User")
    ///     mixed with whitespace (" ") and empty strings (""). After decoding the token, it should only contain
    ///     ClaimTypes.Role claims for the valid role names, confirming that invalid role values are properly filtered to
    ///     maintain token integrity.
    /// </summary>
    [Fact]
    public void GenerateToken_WithWhitespaceRoles_ShouldNotIncludeEmptyRoleClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@example.com",
            TenantId = Guid.NewGuid()
        };
        var roles = new[] { "Admin", " ", "", "User" };

        // Act
        var token = _jwtTokenService.GenerateToken(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadJwtToken(token);

        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        jsonToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
        jsonToken.Claims.Where(c => c.Type == ClaimTypes.Role).Should().HaveCount(2);
    }

    #endregion

    #region GenerateRefreshToken Tests

    /// <summary>
    ///     Test case: GenerateRefreshToken should return a valid Base64-encoded string.
    ///     Scenario: GenerateRefreshToken is called. It should return a non-empty string that matches the Base64 character
    ///     pattern (A-Z, a-z, 0-9, +, /, =), confirming that the refresh token is properly encoded as Base64 for safe
    ///     transmission and storage.
    /// </summary>
    [Fact]
    public void GenerateRefreshToken_ShouldReturnValidBase64String()
    {
        // Act
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        // Assert
        refreshToken.Should().NotBeNullOrEmpty();
        refreshToken.Should().MatchRegex("^[A-Za-z0-9+/=]+$"); // Base64 pattern
    }

    /// <summary>
    ///     Test case: GenerateRefreshToken should return a unique token on each invocation.
    ///     Scenario: GenerateRefreshToken is called three times in succession. Each call should return a different refresh
    ///     token string, ensuring cryptographic uniqueness and preventing token collisions that could lead to security
    ///     vulnerabilities.
    /// </summary>
    [Fact]
    public void GenerateRefreshToken_ShouldReturnDifferentTokensOnEachCall()
    {
        // Act
        var token1 = _jwtTokenService.GenerateRefreshToken();
        var token2 = _jwtTokenService.GenerateRefreshToken();
        var token3 = _jwtTokenService.GenerateRefreshToken();

        // Assert
        token1.Should().NotBe(token2);
        token2.Should().NotBe(token3);
        token1.Should().NotBe(token3);
    }

    /// <summary>
    ///     Test case: GenerateRefreshToken should return a Base64-encoded string representing exactly 64 bytes of random data.
    ///     Scenario: GenerateRefreshToken is called and the returned string is decoded from Base64. The decoded byte array
    ///     should have a length of exactly 64 bytes (512 bits), confirming that the refresh token has sufficient entropy for
    ///     security purposes and matches the expected cryptographic size.
    /// </summary>
    [Fact]
    public void GenerateRefreshToken_ShouldReturn64BytesEncoded()
    {
        // Act
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        // Assert
        var bytes = Convert.FromBase64String(refreshToken);
        bytes.Length.Should().Be(64); // 64 bytes = 512 bits
    }

    #endregion

    #region Key Export Tests

    /// <summary>
    ///     Test case: GetPublicKey should return valid RSA parameters containing only public key components (modulus and
    ///     exponent).
    ///     Scenario: GetPublicKey is called on a JwtTokenService instance initialized with RSA keys. The returned
    ///     RSAParameters should contain valid Modulus and Exponent values, but should not contain private key components (D,
    ///     P, Q, DP, DQ, InverseQ), ensuring that only public key information is exposed for token validation purposes.
    /// </summary>
    [Fact]
    public void GetPublicKey_ShouldReturnValidRsaParameters()
    {
        // Act
        var publicKey = _jwtTokenService.GetPublicKey();

        // Assert
        publicKey.Modulus.Should().NotBeNull();
        publicKey.Exponent.Should().NotBeNull();
        publicKey.D.Should().BeNull(); // Public key shouldn't have private exponent
        publicKey.P.Should().BeNull();
        publicKey.Q.Should().BeNull();
        publicKey.DP.Should().BeNull();
        publicKey.DQ.Should().BeNull();
        publicKey.InverseQ.Should().BeNull();
    }

    /// <summary>
    ///     Test case: GetPrivateKey should return valid RSA parameters containing both public and private key components.
    ///     Scenario: GetPrivateKey is called on a JwtTokenService instance initialized with RSA keys. The returned
    ///     RSAParameters should contain valid Modulus, Exponent, and private key components (D, P, Q), confirming that the
    ///     full key pair is accessible for token signing operations.
    /// </summary>
    [Fact]
    public void GetPrivateKey_ShouldReturnValidRsaParameters()
    {
        // Act
        var privateKey = _jwtTokenService.GetPrivateKey();

        // Assert
        privateKey.Modulus.Should().NotBeNull();
        privateKey.Exponent.Should().NotBeNull();
        privateKey.D.Should().NotBeNull(); // Private key should have private exponent
        privateKey.P.Should().NotBeNull();
        privateKey.Q.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: ExportPublicKeyPem should return a valid PEM-formatted public key string.
    ///     Scenario: ExportPublicKeyPem is called on a JwtTokenService instance. It should return a non-empty string
    ///     containing "BEGIN", "PUBLIC KEY", and "END" markers in the standard PEM format, enabling the public key to be
    ///     shared with clients for token validation purposes.
    /// </summary>
    [Fact]
    public void ExportPublicKeyPem_ShouldReturnValidPemFormat()
    {
        // Act
        var publicKeyPem = _jwtTokenService.ExportPublicKeyPem();

        // Assert
        publicKeyPem.Should().NotBeNullOrEmpty();
        publicKeyPem.Should().Contain("BEGIN");
        publicKeyPem.Should().Contain("PUBLIC KEY");
        publicKeyPem.Should().Contain("END");
    }

    /// <summary>
    ///     Test case: ExportPrivateKeyPem should return a valid PEM-formatted private key string.
    ///     Scenario: ExportPrivateKeyPem is called on a JwtTokenService instance. It should return a non-empty string
    ///     containing "BEGIN" and "END" markers in the standard PEM format, enabling the private key to be exported for backup
    ///     or configuration purposes (note: private keys should be kept secure).
    /// </summary>
    [Fact]
    public void ExportPrivateKeyPem_ShouldReturnValidPemFormat()
    {
        // Act
        var privateKeyPem = _jwtTokenService.ExportPrivateKeyPem();

        // Assert
        privateKeyPem.Should().NotBeNullOrEmpty();
        privateKeyPem.Should().Contain("BEGIN");
        privateKeyPem.Should().Contain("END");
    }

    #endregion

    #region Key Import Tests

    /// <summary>
    ///     Test case: JwtTokenService should initialize successfully when provided with a valid private key in PEM format.
    ///     Scenario: A JwtTokenService instance is created with JwtSettings containing a valid RSA private key in PEM format.
    ///     The constructor should complete without throwing an exception, confirming that valid private keys can be imported
    ///     and used for token signing.
    /// </summary>
    [Fact]
    public void JwtTokenService_WithValidPrivateKey_ShouldInitialize()
    {
        // Arrange
        var (privateKey, _) = RsaKeyGenerator.GenerateKeyPair();
        var settings = new JwtSettings
        {
            Issuer = "test",
            Audience = "test",
            PrivateKey = privateKey
        };
        var options = Options.Create(settings);

        // Act
        var act = () => new JwtTokenService(options);

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    ///     Test case: JwtTokenService should initialize successfully when provided with a valid public key in PEM format.
    ///     Scenario: A JwtTokenService instance is created with JwtSettings containing a valid RSA public key in PEM format.
    ///     The constructor should complete without throwing an exception, confirming that valid public keys can be imported
    ///     for token validation scenarios (note: token signing would not be possible with only a public key).
    /// </summary>
    [Fact]
    public void JwtTokenService_WithValidPublicKey_ShouldInitialize()
    {
        // Arrange
        var (_, publicKey) = RsaKeyGenerator.GenerateKeyPair();
        var settings = new JwtSettings
        {
            Issuer = "test",
            Audience = "test",
            PublicKey = publicKey
        };
        var options = Options.Create(settings);

        // Act
        var act = () => new JwtTokenService(options);

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    ///     Test case: JwtTokenService should generate new RSA keys automatically when no keys are provided in configuration.
    ///     Scenario: A JwtTokenService instance is created with JwtSettings that do not contain PrivateKey or PublicKey
    ///     values. The constructor should complete successfully and generate new RSA keys automatically, allowing the service
    ///     to function in development environments where keys may not be pre-configured.
    /// </summary>
    [Fact]
    public void JwtTokenService_WithNoKeys_ShouldGenerateNewKeys()
    {
        // Arrange
        var settings = new JwtSettings
        {
            Issuer = "test",
            Audience = "test"
        };
        var options = Options.Create(settings);

        // Act
        var service = new JwtTokenService(options);

        // Assert
        var publicKey = service.GetPublicKey();
        publicKey.Modulus.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: JwtTokenService should throw an exception when provided with an invalid private key format.
    ///     Scenario: A JwtTokenService instance is created with JwtSettings containing an invalid private key string (not in
    ///     valid PEM format). The constructor should throw an Exception (typically CryptographicException), confirming that
    ///     invalid key formats are properly rejected to prevent runtime errors during token operations.
    /// </summary>
    [Fact]
    public void JwtTokenService_WithInvalidPrivateKey_ShouldThrowException()
    {
        // Arrange
        var settings = new JwtSettings
        {
            Issuer = "test",
            Audience = "test",
            PrivateKey = "invalid-key"
        };
        var options = Options.Create(settings);

        // Act
        var act = () => new JwtTokenService(options);

        // Assert
        act.Should().Throw<Exception>();
    }

    #endregion
}