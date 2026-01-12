using System.Security.Cryptography;
using System.Text;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Services;
using BoilerPlate.Authentication.WebApi.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BoilerPlate.Authentication.WebApi.Tests.Services;

/// <summary>
///     Unit tests for AuthorizationCodeService
/// </summary>
public class AuthorizationCodeServiceTests : IDisposable
{
    private readonly BaseAuthDbContext _context;
    private readonly Mock<ILogger<AuthorizationCodeService>> _loggerMock;
    private readonly AuthorizationCodeService _service;

    public AuthorizationCodeServiceTests()
    {
        var options = new DbContextOptionsBuilder<BaseAuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestAuthDbContext(options);
        _loggerMock = new Mock<ILogger<AuthorizationCodeService>>();

        _service = new AuthorizationCodeService(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region CreateAuthorizationCodeAsync Tests

    [Fact]
    public async Task CreateAuthorizationCodeAsync_WithValidInput_ShouldCreateCode()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";
        var scope = "read write";
        var state = "test-state";

        // Act
        var result = await _service.CreateAuthorizationCodeAsync(
            userId, tenantId, clientId, redirectUri, scope, state, null, null, null, null);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.TenantId.Should().Be(tenantId);
        result.ClientId.Should().Be(clientId);
        result.RedirectUri.Should().Be(redirectUri);
        result.Scope.Should().Be(scope);
        result.State.Should().Be(state);
        result.Code.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAuthorizationCodeAsync_WithPkce_ShouldStoreCodeChallenge()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";
        var codeChallenge = "test-challenge";
        var codeChallengeMethod = "S256";

        // Act
        var result = await _service.CreateAuthorizationCodeAsync(
            userId, tenantId, clientId, redirectUri, null, null, codeChallenge, codeChallengeMethod, null, null);

        // Assert
        result.Should().NotBeNull();
        result.CodeChallenge.Should().Be(codeChallenge);
        result.CodeChallengeMethod.Should().Be(codeChallengeMethod);
    }

    [Fact]
    public async Task CreateAuthorizationCodeAsync_ShouldGenerateUniqueCodes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";

        // Act
        var code1 = await _service.CreateAuthorizationCodeAsync(userId, tenantId, clientId, redirectUri, null, null, null, null, null, null);
        var code2 = await _service.CreateAuthorizationCodeAsync(userId, tenantId, clientId, redirectUri, null, null, null, null, null, null);

        // Assert
        code1.Code.Should().NotBe(code2.Code); // Codes should be unique
    }

    #endregion

    #region ValidateAndConsumeAuthorizationCodeAsync Tests

    [Fact]
    public async Task ValidateAndConsumeAuthorizationCodeAsync_WithValidCode_ShouldReturnCodeAndMarkAsUsed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";

        var createdCode = await _service.CreateAuthorizationCodeAsync(userId, tenantId, clientId, redirectUri, null, null, null, null, null, null);

        // Act
        var result = await _service.ValidateAndConsumeAuthorizationCodeAsync(
            createdCode.Code, clientId, redirectUri, null);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(createdCode.Id);
        result.IsUsed.Should().BeTrue();
        result.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAuthorizationCodeAsync_WithAlreadyUsedCode_ShouldReturnNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";

        var createdCode = await _service.CreateAuthorizationCodeAsync(userId, tenantId, clientId, redirectUri, null, null, null, null, null, null);
        await _service.ValidateAndConsumeAuthorizationCodeAsync(createdCode.Code, clientId, redirectUri, null);

        // Act - Try to use again
        var result = await _service.ValidateAndConsumeAuthorizationCodeAsync(
            createdCode.Code, clientId, redirectUri, null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAuthorizationCodeAsync_WithExpiredCode_ShouldReturnNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";

        var code = new AuthorizationCode
        {
            Id = Guid.NewGuid(),
            Code = "expired-code",
            UserId = userId,
            TenantId = tenantId,
            ClientId = clientId,
            RedirectUri = redirectUri,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // Expired
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuthorizationCodes.Add(code);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ValidateAndConsumeAuthorizationCodeAsync(
            "expired-code", clientId, redirectUri, null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAuthorizationCodeAsync_WithWrongClientId_ShouldReturnNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";

        var createdCode = await _service.CreateAuthorizationCodeAsync(userId, tenantId, clientId, redirectUri, null, null, null, null, null, null);

        // Act - Use wrong client ID
        var result = await _service.ValidateAndConsumeAuthorizationCodeAsync(
            createdCode.Code, "wrong-client", redirectUri, null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAuthorizationCodeAsync_WithWrongRedirectUri_ShouldReturnNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";

        var createdCode = await _service.CreateAuthorizationCodeAsync(userId, tenantId, clientId, redirectUri, null, null, null, null, null, null);

        // Act - Use wrong redirect URI
        var result = await _service.ValidateAndConsumeAuthorizationCodeAsync(
            createdCode.Code, clientId, "https://wrong.com/callback", null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAuthorizationCodeAsync_WithPkce_ShouldValidateCodeVerifier()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";
        var codeChallenge = "test-challenge";
        var codeChallengeMethod = "S256";
        var codeVerifier = "test-verifier";

        // Create code challenge from verifier (simplified - in real scenario use proper PKCE)
        var codeVerifierBytes = Encoding.UTF8.GetBytes(codeVerifier);
        var challengeBytes = SHA256.HashData(codeVerifierBytes);
        var computedChallenge = Convert.ToBase64String(challengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var createdCode = await _service.CreateAuthorizationCodeAsync(
            userId, tenantId, clientId, redirectUri, null, null, computedChallenge, codeChallengeMethod, null, null);

        // Act - Validate with correct verifier
        var result = await _service.ValidateAndConsumeAuthorizationCodeAsync(
            createdCode.Code, clientId, redirectUri, codeVerifier);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAuthorizationCodeAsync_WithWrongCodeVerifier_ShouldReturnNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";
        var codeChallenge = "test-challenge";
        var codeChallengeMethod = "S256";

        var createdCode = await _service.CreateAuthorizationCodeAsync(
            userId, tenantId, clientId, redirectUri, null, null, codeChallenge, codeChallengeMethod, null, null);

        // Act - Use wrong verifier
        var result = await _service.ValidateAndConsumeAuthorizationCodeAsync(
            createdCode.Code, clientId, redirectUri, "wrong-verifier");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAuthorizationCodeAsync_WithMissingCodeVerifier_ShouldReturnNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";
        var codeChallenge = "test-challenge";
        var codeChallengeMethod = "S256";

        var createdCode = await _service.CreateAuthorizationCodeAsync(
            userId, tenantId, clientId, redirectUri, null, null, codeChallenge, codeChallengeMethod, null, null);

        // Act - Don't provide verifier when challenge was provided
        var result = await _service.ValidateAndConsumeAuthorizationCodeAsync(
            createdCode.Code, clientId, redirectUri, null);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region CleanupExpiredCodesAsync Tests

    [Fact]
    public async Task CleanupExpiredCodesAsync_ShouldDeleteExpiredCodes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clientId = "test-client";
        var redirectUri = "https://example.com/callback";

        // Create an expired code
        var expiredCode = new AuthorizationCode
        {
            Id = Guid.NewGuid(),
            Code = "expired-code",
            UserId = userId,
            TenantId = tenantId,
            ClientId = clientId,
            RedirectUri = redirectUri,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // Expired
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuthorizationCodes.Add(expiredCode);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CleanupExpiredCodesAsync();

        // Assert
        result.Should().Be(1);
        var codeInDb = await _context.AuthorizationCodes.FindAsync(expiredCode.Id);
        codeInDb.Should().BeNull();
    }

    #endregion
}
