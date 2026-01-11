using System.Security.Claims;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Controllers;
using BoilerPlate.Authentication.WebApi.Models;
using BoilerPlate.Authentication.WebApi.Services;
using BoilerPlate.Authentication.WebApi.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BoilerPlate.Authentication.WebApi.Tests.Controllers;

/// <summary>
///     Unit tests for OAuthClientsController
/// </summary>
public class OAuthClientsControllerTests
{
    private readonly BaseAuthDbContext _context;
    private readonly OAuthClientsController _controller;
    private readonly Mock<ILogger<OAuthClientsController>> _loggerMock;
    private readonly OAuthClientService _oauthClientService;

    public OAuthClientsControllerTests()
    {
        var options = new DbContextOptionsBuilder<BaseAuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestAuthDbContext(options);
        _loggerMock = new Mock<ILogger<OAuthClientsController>>();
        _oauthClientService = new OAuthClientService(_context, new Mock<ILogger<OAuthClientService>>().Object);
        _controller = new OAuthClientsController(_oauthClientService, _loggerMock.Object);
    }

    #region Authorization Attribute Tests

    /// <summary>
    ///     Test case: OAuthClientsController should have an AuthorizeAttribute with the OAuthClientManagement policy.
    ///     Scenario: The OAuthClientsController class is inspected for the AuthorizeAttribute. The controller should have the
    ///     attribute applied with the OAuthClientManagement policy, ensuring that only users with appropriate roles (Service
    ///     Administrator or Tenant Administrator) can access OAuth client management endpoints.
    /// </summary>
    [Fact]
    public void OAuthClientsController_ShouldHaveAuthorizeAttribute()
    {
        // Arrange & Act
        var authorizeAttribute = typeof(OAuthClientsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .FirstOrDefault() as AuthorizeAttribute;

        // Assert
        authorizeAttribute.Should().NotBeNull();
        authorizeAttribute!.Policy.Should().Be(AuthorizationPolicies.OAuthClientManagement);
    }

    /// <summary>
    ///     Test case: OAuthClientsController should have an ApiControllerAttribute applied.
    ///     Scenario: The OAuthClientsController class is inspected for the ApiControllerAttribute. The controller should have
    ///     the attribute applied, which enables automatic model validation, binding source parameter inference, and other
    ///     ASP.NET Core Web API conventions.
    /// </summary>
    [Fact]
    public void OAuthClientsController_ShouldHaveApiControllerAttribute()
    {
        // Arrange & Act
        var apiControllerAttribute = typeof(OAuthClientsController)
            .GetCustomAttributes(typeof(ApiControllerAttribute), true)
            .FirstOrDefault();

        // Assert
        apiControllerAttribute.Should().NotBeNull();
    }

    /// <summary>
    ///     Test case: OAuthClientsController should have a RouteAttribute with the template "api/[controller]".
    ///     Scenario: The OAuthClientsController class is inspected for the RouteAttribute. The controller should have the
    ///     attribute applied with the template "api/[controller]", which will resolve to "api/oauthclients" for routing
    ///     purposes, following RESTful API conventions.
    /// </summary>
    [Fact]
    public void OAuthClientsController_ShouldHaveRouteAttribute()
    {
        // Arrange & Act
        var routeAttribute = typeof(OAuthClientsController)
            .GetCustomAttributes(typeof(RouteAttribute), true)
            .FirstOrDefault() as RouteAttribute;

        // Assert
        routeAttribute.Should().NotBeNull();
        routeAttribute!.Template.Should().Be("api/[controller]");
    }

    #endregion

    #region GetAllClients Tests

    /// <summary>
    ///     Test case: GetAllClients should return all clients for Service Administrator.
    ///     Scenario: A Service Administrator calls GetAllClients. The endpoint should return all OAuth clients from all
    ///     tenants, as Service Administrators have access to all tenants.
    /// </summary>
    [Fact]
    public async Task GetAllClients_WithServiceAdministrator_ShouldReturnAllClients()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var client1 = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "client-1",
            Name = "Client 1",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            TenantId = tenantId1,
            CreatedAt = DateTime.UtcNow
        };
        var client2 = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "client-2",
            Name = "Client 2",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            TenantId = tenantId2,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.AddRange(client1, client2);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Service Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.GetAllClients(false, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var clients = okResult.Value.Should().BeAssignableTo<IEnumerable<OAuthClientDto>>().Subject;
        clients.Should().HaveCount(2);
    }

    /// <summary>
    ///     Test case: GetAllClients should return only tenant clients for Tenant Administrator.
    ///     Scenario: A Tenant Administrator calls GetAllClients. The endpoint should return only OAuth clients belonging to
    ///     their tenant, as Tenant Administrators have access only to their own tenant.
    /// </summary>
    [Fact]
    public async Task GetAllClients_WithTenantAdministrator_ShouldReturnOnlyTenantClients()
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

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Tenant Administrator"),
            new("tenant_id", tenantId1.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.GetAllClients(false, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var clients = okResult.Value.Should().BeAssignableTo<IEnumerable<OAuthClientDto>>().Subject;
        clients.Should().HaveCount(1);
        clients.First().ClientId.Should().Be("tenant1-client");
    }

    #endregion

    #region GetClient Tests

    /// <summary>
    ///     Test case: GetClient should return client for Service Administrator.
    ///     Scenario: A Service Administrator calls GetClient with a valid client ID. The endpoint should return the OAuth
    ///     client, as Service Administrators have access to all tenants.
    /// </summary>
    [Fact]
    public async Task GetClient_WithServiceAdministrator_ShouldReturnClient()
    {
        // Arrange
        var clientId = "test-client";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Test Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Service Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.GetClient(clientId, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var clientDto = okResult.Value.Should().BeOfType<OAuthClientDto>().Subject;
        clientDto.ClientId.Should().Be(clientId);
    }

    /// <summary>
    ///     Test case: GetClient should return Forbid for Tenant Administrator accessing another tenant's client.
    ///     Scenario: A Tenant Administrator calls GetClient with a client ID belonging to another tenant. The endpoint should
    ///     return Forbid (403), as Tenant Administrators can only access clients from their own tenant.
    /// </summary>
    [Fact]
    public async Task GetClient_WithTenantAdministratorAccessingOtherTenant_ShouldReturnForbid()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var clientId = "other-tenant-client";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Other Tenant Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            TenantId = tenantId2,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Tenant Administrator"),
            new("tenant_id", tenantId1.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.GetClient(clientId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    /// <summary>
    ///     Test case: GetClient should return NotFound for non-existent client.
    ///     Scenario: A Service Administrator calls GetClient with a non-existent client ID. The endpoint should return
    ///     NotFound (404).
    /// </summary>
    [Fact]
    public async Task GetClient_WithNonExistentClient_ShouldReturnNotFound()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Service Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.GetClient("non-existent-client", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region CreateClient Tests

    /// <summary>
    ///     Test case: CreateClient should create a new OAuth client successfully.
    ///     Scenario: A Service Administrator calls CreateClient with valid client data. The endpoint should create the OAuth
    ///     client and return CreatedAtAction (201) with the created client DTO.
    /// </summary>
    [Fact]
    public async Task CreateClient_WithValidRequest_ShouldCreateClient()
    {
        // Arrange
        var request = new CreateOAuthClientRequest
        {
            ClientId = "new-client",
            Name = "New Client",
            Description = "Test Description",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Service Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.CreateClient(request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var clientDto = createdResult.Value.Should().BeOfType<OAuthClientDto>().Subject;
        clientDto.ClientId.Should().Be("new-client");
        clientDto.Name.Should().Be("New Client");
    }

    /// <summary>
    ///     Test case: CreateClient should return BadRequest when client ID already exists.
    ///     Scenario: A Service Administrator calls CreateClient with a client ID that already exists. The endpoint should
    ///     return BadRequest (400).
    /// </summary>
    [Fact]
    public async Task CreateClient_WithExistingClientId_ShouldReturnBadRequest()
    {
        // Arrange
        var existingClient = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = "existing-client",
            Name = "Existing Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(existingClient);
        await _context.SaveChangesAsync();

        var request = new CreateOAuthClientRequest
        {
            ClientId = "existing-client",
            Name = "New Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Service Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.CreateClient(request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
    }

    /// <summary>
    ///     Test case: CreateClient should assign tenant ID for Tenant Administrator.
    ///     Scenario: A Tenant Administrator calls CreateClient with valid client data. The endpoint should create the OAuth
    ///     client with the tenant ID from the user's claims and return CreatedAtAction (201).
    /// </summary>
    [Fact]
    public async Task CreateClient_WithTenantAdministrator_ShouldAssignTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var request = new CreateOAuthClientRequest
        {
            ClientId = "tenant-client",
            Name = "Tenant Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Tenant Administrator"),
            new("tenant_id", tenantId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.CreateClient(request, CancellationToken.None);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var clientDto = createdResult.Value.Should().BeOfType<OAuthClientDto>().Subject;
        clientDto.ClientId.Should().Be("tenant-client");
        clientDto.TenantId.Should().Be(tenantId);
    }

    #endregion

    #region UpdateClient Tests

    /// <summary>
    ///     Test case: UpdateClient should update an existing OAuth client successfully.
    ///     Scenario: A Service Administrator calls UpdateClient with valid update data. The endpoint should update the OAuth
    ///     client and return NoContent (204).
    /// </summary>
    [Fact]
    public async Task UpdateClient_WithValidRequest_ShouldUpdateClient()
    {
        // Arrange
        var clientId = "update-client";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Original Name",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();

        var request = new UpdateOAuthClientRequest
        {
            Name = "Updated Name",
            Description = "Updated Description",
            IsActive = false
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Service Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.UpdateClient(clientId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        var updatedClient = await _context.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == clientId);
        updatedClient.Should().NotBeNull();
        updatedClient!.Name.Should().Be("Updated Name");
        updatedClient.IsActive.Should().BeFalse();
    }

    /// <summary>
    ///     Test case: UpdateClient should return Forbid for Tenant Administrator updating another tenant's client.
    ///     Scenario: A Tenant Administrator calls UpdateClient with a client ID belonging to another tenant. The endpoint
    ///     should return Forbid (403), as Tenant Administrators can only update clients from their own tenant.
    /// </summary>
    [Fact]
    public async Task UpdateClient_WithTenantAdministratorAccessingOtherTenant_ShouldReturnForbid()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var clientId = "other-tenant-client";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Other Tenant Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            TenantId = tenantId2,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();

        var request = new UpdateOAuthClientRequest
        {
            Name = "Updated Name"
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Tenant Administrator"),
            new("tenant_id", tenantId1.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.UpdateClient(clientId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    /// <summary>
    ///     Test case: UpdateClient should return NotFound for non-existent client.
    ///     Scenario: A Service Administrator calls UpdateClient with a non-existent client ID. The endpoint should return
    ///     NotFound (404).
    /// </summary>
    [Fact]
    public async Task UpdateClient_WithNonExistentClient_ShouldReturnNotFound()
    {
        // Arrange
        var request = new UpdateOAuthClientRequest
        {
            Name = "Updated Name"
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Service Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.UpdateClient("non-existent-client", request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteClient Tests

    /// <summary>
    ///     Test case: DeleteClient should delete an existing OAuth client successfully.
    ///     Scenario: A Service Administrator calls DeleteClient with a valid client ID. The endpoint should delete the OAuth
    ///     client and return NoContent (204).
    /// </summary>
    [Fact]
    public async Task DeleteClient_WithValidClient_ShouldDeleteClient()
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

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Service Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.DeleteClient(clientId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        var deletedClient = await _context.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == clientId);
        deletedClient.Should().BeNull();
    }

    /// <summary>
    ///     Test case: DeleteClient should return Forbid for Tenant Administrator deleting another tenant's client.
    ///     Scenario: A Tenant Administrator calls DeleteClient with a client ID belonging to another tenant. The endpoint
    ///     should return Forbid (403), as Tenant Administrators can only delete clients from their own tenant.
    /// </summary>
    [Fact]
    public async Task DeleteClient_WithTenantAdministratorAccessingOtherTenant_ShouldReturnForbid()
    {
        // Arrange
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        var clientId = "other-tenant-client";
        var client = new OAuthClient
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = "Other Tenant Client",
            RedirectUris = "https://example.com/callback",
            IsConfidential = false,
            IsActive = true,
            TenantId = tenantId2,
            CreatedAt = DateTime.UtcNow
        };
        _context.OAuthClients.Add(client);
        await _context.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Tenant Administrator"),
            new("tenant_id", tenantId1.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.DeleteClient(clientId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    /// <summary>
    ///     Test case: DeleteClient should return NotFound for non-existent client.
    ///     Scenario: A Service Administrator calls DeleteClient with a non-existent client ID. The endpoint should return
    ///     NotFound (404).
    /// </summary>
    [Fact]
    public async Task DeleteClient_WithNonExistentClient_ShouldReturnNotFound()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Service Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        // Act
        var result = await _controller.DeleteClient("non-existent-client", CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion
}