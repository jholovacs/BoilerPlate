using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Configuration;
using BoilerPlate.Authentication.WebApi.Helpers;
using BoilerPlate.Authentication.WebApi.Models;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     RESTful API controller for OAuth2 client management
///     Allows Service Administrators and Tenant Administrators to manage OAuth clients
///     Service Administrators can manage all clients, Tenant Administrators can only manage clients for their tenant
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.OAuthClientManagement)]
public class OAuthClientsController : ControllerBase
{
    private readonly ILogger<OAuthClientsController> _logger;
    private readonly OAuthClientService _oauthClientService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OAuthClientsController" /> class
    /// </summary>
    public OAuthClientsController(
        OAuthClientService oauthClientService,
        ILogger<OAuthClientsController> logger)
    {
        _oauthClientService = oauthClientService;
        _logger = logger;
    }

    /// <summary>
    ///     Gets all OAuth clients, optionally filtered by tenant.
    ///     Service Administrators can see all clients, Tenant Administrators can only see clients for their tenant.
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive clients</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of OAuth clients</returns>
    /// <response code="200">Returns the list of OAuth clients</response>
    /// <response code="401">Unauthorized - OAuth Client Management policy required</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OAuthClientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<OAuthClientDto>>> GetAllClients(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetCurrentTenantIdIfNotServiceAdmin();
        var clients = await _oauthClientService.GetClientsAsync(tenantId, includeInactive, cancellationToken);

        var dtos = clients.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    ///     Gets an OAuth client by client ID
    /// </summary>
    /// <param name="clientId">The client identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OAuth client information</returns>
    /// <response code="200">Returns the OAuth client</response>
    /// <response code="404">OAuth client not found</response>
    /// <response code="403">Forbidden - Tenant Administrators cannot access clients from other tenants</response>
    /// <response code="401">Unauthorized - OAuth Client Management policy required</response>
    [HttpGet("{clientId}")]
    [ProducesResponseType(typeof(OAuthClientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OAuthClientDto>> GetClient(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var client = await _oauthClientService.GetClientAsync(clientId, cancellationToken);

        if (client == null) return NotFound(new { error = "OAuth client not found", clientId });

        // Check tenant access for Tenant Administrators
        var currentTenantId = GetCurrentTenantIdIfNotServiceAdmin();
        if (currentTenantId.HasValue && client.TenantId != currentTenantId.Value) return Forbid();

        return Ok(MapToDto(client));
    }

    /// <summary>
    ///     Creates a new OAuth client
    ///     Service Administrators can create system-wide or tenant-specific clients.
    ///     Tenant Administrators can only create clients for their tenant.
    /// </summary>
    /// <param name="request">Create OAuth client request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created OAuth client (excluding client secret)</returns>
    /// <response code="201">OAuth client created successfully</response>
    /// <response code="400">Invalid request - client ID may already exist or validation failed</response>
    /// <response code="401">Unauthorized - OAuth Client Management policy required</response>
    [HttpPost]
    [ProducesResponseType(typeof(OAuthClientDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OAuthClientDto>> CreateClient(
        [FromBody] CreateOAuthClientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Tenant Administrators can only create clients for their tenant
        var tenantId = GetCurrentTenantIdIfNotServiceAdmin();

        var client = await _oauthClientService.CreateClientAsync(
            request.ClientId,
            request.ClientSecret,
            request.Name,
            request.Description,
            request.RedirectUris,
            request.IsConfidential,
            tenantId,
            cancellationToken);

        if (client == null)
            return BadRequest(new
                { error = "Failed to create OAuth client. Client ID may already exist or validation failed." });

        _logger.LogInformation(
            "OAuth client created: {ClientId} - {ClientName} (Confidential: {IsConfidential}, TenantId: {TenantId})",
            client.ClientId, client.Name, client.IsConfidential, client.TenantId);

        return CreatedAtAction(
            nameof(GetClient),
            new { clientId = client.ClientId },
            MapToDto(client));
    }

    /// <summary>
    ///     Updates an existing OAuth client
    ///     Service Administrators can update any client.
    ///     Tenant Administrators can only update clients for their tenant.
    /// </summary>
    /// <param name="clientId">The client identifier</param>
    /// <param name="request">Update OAuth client request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    /// <response code="204">OAuth client updated successfully</response>
    /// <response code="400">Invalid request - validation failed</response>
    /// <response code="404">OAuth client not found</response>
    /// <response code="403">Forbidden - Tenant Administrators cannot update clients from other tenants</response>
    /// <response code="401">Unauthorized - OAuth Client Management policy required</response>
    [HttpPut("{clientId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateClient(
        string clientId,
        [FromBody] UpdateOAuthClientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Check tenant access for Tenant Administrators
        var client = await _oauthClientService.GetClientAsync(clientId, cancellationToken);
        if (client == null) return NotFound(new { error = "OAuth client not found", clientId });

        var currentTenantId = GetCurrentTenantIdIfNotServiceAdmin();
        if (currentTenantId.HasValue && client.TenantId != currentTenantId.Value) return Forbid();

        var result = await _oauthClientService.UpdateClientAsync(
            clientId,
            request.Name,
            request.Description,
            request.RedirectUris,
            request.IsActive,
            request.NewClientSecret,
            cancellationToken);

        if (!result) return BadRequest(new { error = "Failed to update OAuth client. Validation may have failed." });

        _logger.LogInformation("OAuth client updated: {ClientId}", clientId);

        return NoContent();
    }

    /// <summary>
    ///     Deletes an OAuth client
    ///     Service Administrators can delete any client.
    ///     Tenant Administrators can only delete clients for their tenant.
    /// </summary>
    /// <param name="clientId">The client identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    /// <response code="204">OAuth client deleted successfully</response>
    /// <response code="404">OAuth client not found</response>
    /// <response code="403">Forbidden - Tenant Administrators cannot delete clients from other tenants</response>
    /// <response code="401">Unauthorized - OAuth Client Management policy required</response>
    [HttpDelete("{clientId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteClient(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        // Check tenant access for Tenant Administrators
        var client = await _oauthClientService.GetClientAsync(clientId, cancellationToken);
        if (client == null) return NotFound(new { error = "OAuth client not found", clientId });

        var currentTenantId = GetCurrentTenantIdIfNotServiceAdmin();
        if (currentTenantId.HasValue && client.TenantId != currentTenantId.Value) return Forbid();

        var result = await _oauthClientService.DeleteClientAsync(clientId, cancellationToken);

        if (!result) return NotFound(new { error = "OAuth client not found", clientId });

        _logger.LogInformation("OAuth client deleted: {ClientId}", clientId);

        return NoContent();
    }

    /// <summary>
    ///     Gets the current tenant ID if the user is not a Service Administrator.
    ///     Service Administrators return null (can access all tenants).
    /// </summary>
    /// <returns>The current tenant ID, or null if Service Administrator</returns>
    private Guid? GetCurrentTenantIdIfNotServiceAdmin()
    {
        if (User.IsInRole("Service Administrator")) return null; // Service Administrators can access all tenants

        return ClaimsHelper.GetTenantId(User);
    }

    /// <summary>
    ///     Maps an OAuthClient entity to an OAuthClientDto (excluding sensitive data like client secret hash)
    /// </summary>
    private static OAuthClientDto MapToDto(OAuthClient client)
    {
        return new OAuthClientDto
        {
            Id = client.Id,
            ClientId = client.ClientId,
            Name = client.Name,
            Description = client.Description,
            RedirectUris = client.RedirectUris,
            IsConfidential = client.IsConfidential,
            IsActive = client.IsActive,
            CreatedAt = client.CreatedAt,
            UpdatedAt = client.UpdatedAt,
            TenantId = client.TenantId
        };
    }
}