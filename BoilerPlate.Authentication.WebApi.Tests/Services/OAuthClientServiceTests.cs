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
///     Unit tests for OAuthClientService
/// </summary>
public class OAuthClientServiceTests
{
    private readonly BaseAuthDbContext _context;
    private readonly Mock<ILogger<OAuthClientService>> _loggerMock;
    private readonly OAuthClientService _service;

    public OAuthClientServiceTests()
    {
        var options = new DbContextOptionsBuilder<BaseAuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestAuthDbContext(options);
        _loggerMock = new Mock<ILogger<OAuthClientService>>();
        _service = new OAuthClientService(_context, _loggerMock.Object);
    }

    /// <summary>
    ///     Tests that HashClientSecret creates a valid hash for a given secret
    /// </summary>
    [Fact]
    public async Task HashClientSecret_WithValidSecret_ShouldCreateHash()
    {
        // Arrange
        var secret = "test-secret-123";

        // Act
        var hash1 = _service.HashClientSecret(secret);
        var hash2 = _service.HashClientSecret(secret);

        // Assert
        hash1.Should().NotBeNullOrWhiteSpace();
        hash2.Should().NotBeNullOrWhiteSpace();
        // Hashes should be different (salted)
        hash1.Should().NotBe(hash2);
    }

    /// <summary>
    ///     Tests that HashClientSecret throws ArgumentException for null or empty secrets
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HashClientSecret_WithInvalidSecret_ShouldThrowArgumentException(string? secret)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.HashClientSecret(secret!));
    }

    /// <summary>
    ///     Tests that VerifyClientSecret returns true for a matching secret
    /// </summary>
    [Fact]
    public async Task VerifyClientSecret_WithMatchingSecret_ShouldReturnTrue()
    {
        // Arrange
        var secret = "test-secret-123";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "test-client",
            Name = "Test Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = true,
            ClientSecretHash = _service.HashClientSecret(secret)
        };

        // Act
        var result = _service.VerifyClientSecret(client, secret);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    ///     Tests that VerifyClientSecret returns false for a non-matching secret
    /// </summary>
    [Fact]
    public async Task VerifyClientSecret_WithNonMatchingSecret_ShouldReturnFalse()
    {
        // Arrange
        var secret = "test-secret-123";
        var wrongSecret = "wrong-secret-456";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "test-client",
            Name = "Test Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = true,
            ClientSecretHash = _service.HashClientSecret(secret)
        };

        // Act
        var result = _service.VerifyClientSecret(client, wrongSecret);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    ///     Tests that VerifyClientSecret throws ArgumentNullException for null client
    /// </summary>
    [Fact]
    public void VerifyClientSecret_WithNullClient_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.VerifyClientSecret(null!, "secret"));
    }

    /// <summary>
    ///     Tests that VerifyClientSecret returns false for null or empty secret
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void VerifyClientSecret_WithNullOrEmptySecret_ShouldReturnFalse(string? secret)
    {
        // Arrange
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "test-client",
            Name = "Test Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = true,
            ClientSecretHash = _service.HashClientSecret("valid-secret")
        };

        // Act
        var result = _service.VerifyClientSecret(client, secret!);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    ///     Tests that VerifyClientSecret returns false for public clients (no hash stored)
    /// </summary>
    [Fact]
    public void VerifyClientSecret_WithPublicClient_ShouldReturnFalse()
    {
        // Arrange
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "test-client",
            Name = "Test Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            ClientSecretHash = null
        };

        // Act
        var result = _service.VerifyClientSecret(client, "any-secret");

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    ///     Tests that CreateClientAsync creates a new confidential client with hashed secret
    /// </summary>
    [Fact]
    public async Task CreateClientAsync_WithValidConfidentialClient_ShouldCreateClient()
    {
        // Arrange
        var clientId = "test-client-1";
        var secret = "test-secret-123";
        var name = "Test Client";
        var description = "Test Description";
        var redirectUris = "https://example.com/callback";
        var tenantId = Guid.NewGuid();

        // Act
        var result = await _service.CreateClientAsync(
            clientId,
            secret,
            name,
            description,
            redirectUris,
            true,
            tenantId,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ClientId.Should().Be(clientId);
        result.Name.Should().Be(name);
        result.Description.Should().Be(description);
        result.RedirectUris.Should().Be(redirectUris);
        result.IsConfidential.Should().BeTrue();
        result.IsActive.Should().BeTrue();
        result.TenantId.Should().Be(tenantId);
        result.ClientSecretHash.Should().NotBeNullOrWhiteSpace();
        result.ClientSecretHash.Should().NotBe(secret); // Should be hashed

        // Verify secret can be verified
        var verifyResult = _service.VerifyClientSecret(result, secret);
        verifyResult.Should().BeTrue();
    }

    /// <summary>
    ///     Tests that CreateClientAsync creates a new public client without secret
    /// </summary>
    [Fact]
    public async Task CreateClientAsync_WithValidPublicClient_ShouldCreateClient()
    {
        // Arrange
        var clientId = "test-client-2";
        var name = "Public Client";
        var redirectUris = "https://example.com/callback";

        // Act
        var result = await _service.CreateClientAsync(
            clientId,
            null,
            name,
            null,
            redirectUris,
            false,
            null,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ClientId.Should().Be(clientId);
        result.Name.Should().Be(name);
        result.IsConfidential.Should().BeFalse();
        result.ClientSecretHash.Should().BeNull();
    }

    /// <summary>
    ///     Tests that CreateClientAsync returns null when client ID already exists
    /// </summary>
    [Fact]
    public async Task CreateClientAsync_WithExistingClientId_ShouldReturnNull()
    {
        // Arrange
        var clientId = "existing-client";
        var existingClient = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Existing Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(existingClient);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CreateClientAsync(
            clientId,
            null,
            "New Client",
            null,
            "https://example.com/callback",
            false,
            null,
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Tests that CreateClientAsync returns null when confidential client is missing secret
    /// </summary>
    [Fact]
    public async Task CreateClientAsync_WithConfidentialClientWithoutSecret_ShouldReturnNull()
    {
        // Arrange
        var clientId = "confidential-client-no-secret";

        // Act
        var result = await _service.CreateClientAsync(
            clientId,
            null,
            "Confidential Client",
            null,
            "https://example.com/callback",
            true,
            null,
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Tests that UpdateClientAsync updates client properties correctly
    /// </summary>
    [Fact]
    public async Task UpdateClientAsync_WithValidUpdates_ShouldUpdateClient()
    {
        // Arrange
        var clientId = "update-client";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Original Name",
            Description = "Original Description",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();

        var newName = "Updated Name";
        var newDescription = "Updated Description";
        var newRedirectUris = "https://new.example.com/callback";

        // Act
        var result = await _service.UpdateClientAsync(
            clientId,
            newName,
            newDescription,
            newRedirectUris,
            false,
            null,
            CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var updatedClient = await _context.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == clientId);
        updatedClient.Should().NotBeNull();
        updatedClient!.Name.Should().Be(newName);
        updatedClient.Description.Should().Be(newDescription);
        updatedClient.RedirectUris.Should().Be(newRedirectUris);
        updatedClient.IsActive.Should().BeFalse();
        updatedClient.UpdatedAt.Should().NotBeNull();
    }

    /// <summary>
    ///     Tests that UpdateClientAsync returns false when client is not found
    /// </summary>
    [Fact]
    public async Task UpdateClientAsync_WithNonExistentClient_ShouldReturnFalse()
    {
        // Act
        var result = await _service.UpdateClientAsync(
            "non-existent-client",
            "New Name",
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    ///     Tests that UpdateClientAsync updates client secret for confidential clients
    /// </summary>
    [Fact]
    public async Task UpdateClientAsync_WithNewSecretForConfidentialClient_ShouldUpdateSecret()
    {
        // Arrange
        var clientId = "confidential-update-client";
        var originalSecret = "original-secret";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Confidential Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = true,
            IsActive = true,
            ClientSecretHash = _service.HashClientSecret(originalSecret),
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();

        var newSecret = "new-secret-456";

        // Act
        var result = await _service.UpdateClientAsync(
            clientId,
            newClientSecret: newSecret,
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var updatedClient = await _context.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == clientId);
        updatedClient.Should().NotBeNull();
        _service.VerifyClientSecret(updatedClient!, newSecret).Should().BeTrue();
        _service.VerifyClientSecret(updatedClient!, originalSecret).Should().BeFalse();
    }

    /// <summary>
    ///     Tests that UpdateClientAsync returns false when trying to set secret for public client
    /// </summary>
    [Fact]
    public async Task UpdateClientAsync_WithNewSecretForPublicClient_ShouldReturnFalse()
    {
        // Arrange
        var clientId = "public-client";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Public Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UpdateClientAsync(
            clientId,
            newClientSecret: "new-secret",
            cancellationToken: CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    ///     Tests that DeleteClientAsync deletes a client successfully
    /// </summary>
    [Fact]
    public async Task DeleteClientAsync_WithExistingClient_ShouldDeleteClient()
    {
        // Arrange
        var clientId = "delete-client";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Client to Delete",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteClientAsync(clientId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var deletedClient = await _context.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == clientId);
        deletedClient.Should().BeNull();
    }

    /// <summary>
    ///     Tests that DeleteClientAsync returns false when client is not found
    /// </summary>
    [Fact]
    public async Task DeleteClientAsync_WithNonExistentClient_ShouldReturnFalse()
    {
        // Act
        var result = await _service.DeleteClientAsync("non-existent-client", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    ///     Tests that GetClientAsync returns the correct client by client ID
    /// </summary>
    [Fact]
    public async Task GetClientAsync_WithExistingClient_ShouldReturnClient()
    {
        // Arrange
        var clientId = "get-client";
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Tenants.Add(tenant);

        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Test Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetClientAsync(clientId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ClientId.Should().Be(clientId);
        result.Tenant.Should().NotBeNull();
        result.Tenant!.Id.Should().Be(tenantId);
    }

    /// <summary>
    ///     Tests that GetClientAsync returns null when client is not found
    /// </summary>
    [Fact]
    public async Task GetClientAsync_WithNonExistentClient_ShouldReturnNull()
    {
        // Act
        var result = await _service.GetClientAsync("non-existent-client", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    ///     Tests that GetClientsAsync returns all clients when no filters are applied
    /// </summary>
    [Fact]
    public async Task GetClientsAsync_WithoutFilters_ShouldReturnAllClients()
    {
        // Arrange
        var client1 = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "client-1",
            Name = "Client 1",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var client2 = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "client-2",
            Name = "Client 2",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.AddRange(client1, client2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetClientsAsync(includeInactive: true, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    /// <summary>
    ///     Tests that GetClientsAsync filters out inactive clients by default
    /// </summary>
    [Fact]
    public async Task GetClientsAsync_WithDefaultFilters_ShouldReturnOnlyActiveClients()
    {
        // Arrange
        var client1 = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "active-client",
            Name = "Active Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var client2 = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "inactive-client",
            Name = "Inactive Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.AddRange(client1, client2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetClientsAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().ClientId.Should().Be("active-client");
    }

    /// <summary>
    ///     Tests that GetClientsAsync filters by tenant ID when provided
    /// </summary>
    [Fact]
    public async Task GetClientsAsync_WithTenantFilter_ShouldReturnOnlyTenantClients()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var client1 = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "tenant1-client",
            Name = "Tenant 1 Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            TenantId = tenantId1,
            CreatedAt = DateTime.UtcNow
        };
        var client2 = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "tenant2-client",
            Name = "Tenant 2 Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            TenantId = tenantId2,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.AddRange(client1, client2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetClientsAsync(tenantId1, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().ClientId.Should().Be("tenant1-client");
        result.First().TenantId.Should().Be(tenantId1);
    }
}