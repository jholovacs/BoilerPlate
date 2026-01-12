using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.WebSso;

namespace BoilerPlate.Authentication.WebApi.Controllers;

/// <summary>
///     SAML2 SSO controller for tenant-level SAML2 authentication
/// </summary>
[ApiController]
[Route("saml2")]
[Produces("application/json")]
public class Saml2Controller : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly BaseAuthDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<Saml2Controller> _logger;
    private readonly ISaml2Service _saml2Service;
    private readonly JwtTokenService _jwtTokenService;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Saml2Controller" /> class
    /// </summary>
    public Saml2Controller(
        ISaml2Service saml2Service,
        IAuthenticationService authenticationService,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        BaseAuthDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<Saml2Controller> logger)
    {
        _saml2Service = saml2Service;
        _authenticationService = authenticationService;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    ///     Initiates SAML2 SSO authentication for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="returnUrl">Optional return URL after successful authentication</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Redirects to Identity Provider SSO service</returns>
    /// <response code="302">Redirects to Identity Provider SSO service</response>
    /// <response code="400">SAML2 is not configured or enabled for this tenant</response>
    [HttpGet("sso/{tenantId}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateSso(
        [FromRoute] Guid tenantId,
        [FromQuery] string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _saml2Service.GetSaml2SettingsAsync(tenantId, cancellationToken);
        if (settings == null || !settings.IsEnabled)
        {
            _logger.LogWarning("SAML2 SSO attempted for tenant {TenantId} but SAML2 is not enabled", tenantId);
            return BadRequest(new { error = "SAML2 SSO is not configured or enabled for this tenant" });
        }

        // Validate required settings
        if (string.IsNullOrWhiteSpace(settings.IdpEntityId) ||
            string.IsNullOrWhiteSpace(settings.IdpSsoServiceUrl) ||
            string.IsNullOrWhiteSpace(settings.SpEntityId))
        {
            _logger.LogWarning("SAML2 SSO attempted for tenant {TenantId} but required settings are missing", tenantId);
            return BadRequest(new { error = "SAML2 configuration is incomplete" });
        }

        try
        {
            // Build ACS URL if not set
            var acsUrl = settings.SpAcsUrl;
            if (string.IsNullOrWhiteSpace(acsUrl))
            {
                var request = HttpContext.Request;
                var baseUrl = $"{request.Scheme}://{request.Host}";
                acsUrl = $"{baseUrl}/saml2/acs/{tenantId}";
            }

            // Store return URL in cookie for later use
            if (!string.IsNullOrWhiteSpace(returnUrl))
                Response.Cookies.Append($"saml2_return_{tenantId}", returnUrl, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(10)
                });

            // Create SAML2 options for this tenant
            var options = CreateSaml2Options(settings, tenantId, acsUrl);

            // Use CommandFactory to initiate SSO
            var idp = options.IdentityProviders.Default;
            if (idp == null)
            {
                _logger.LogError("No identity provider configured for tenant {TenantId}", tenantId);
                return BadRequest(new { error = "SAML2 identity provider not configured" });
            }

            // Create request data for sign-in command
            var requestData = new HttpRequestData(
                Request.Method,
                new Uri($"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}"),
                Request.PathBase,
                Request.Cookies.Select(c => new KeyValuePair<string, IEnumerable<string>>(c.Key, new[] { c.Value })),
                Request.Headers.SelectMany(h => h.Value.Select(v => new KeyValuePair<string, string>(h.Key, v))),
                bodyBytes => bodyBytes);

            // Execute sign-in command to get redirect URL
            var commandResult = CommandFactory.GetCommand(CommandFactory.SignInCommandName)
                .Run(requestData, options);

            if (commandResult.HttpStatusCode != System.Net.HttpStatusCode.SeeOther &&
                commandResult.HttpStatusCode != System.Net.HttpStatusCode.Found)
            {
                _logger.LogError("SAML2 SSO initiation failed for tenant {TenantId}: {Status}", tenantId,
                    commandResult.HttpStatusCode);
                return BadRequest(new { error = "Failed to initiate SAML2 SSO" });
            }

            var redirectUrl = commandResult.Location?.ToString();
            if (string.IsNullOrWhiteSpace(redirectUrl))
            {
                _logger.LogError("SAML2 SSO initiation did not return redirect URL for tenant {TenantId}", tenantId);
                return BadRequest(new { error = "Failed to generate SAML2 redirect URL" });
            }

            _logger.LogInformation("SAML2 SSO initiated for tenant {TenantId}", tenantId);

            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating SAML2 SSO for tenant {TenantId}", tenantId);
            return BadRequest(new { error = "Failed to initiate SAML2 SSO", message = ex.Message });
        }
    }

    /// <summary>
    ///     Handles SAML2 assertion consumer service (ACS) callback from Identity Provider
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT token response or redirect to return URL</returns>
    /// <response code="200">Returns JWT access token and refresh token</response>
    /// <response code="302">Redirects to return URL with token in query string or cookie</response>
    /// <response code="400">SAML2 response is invalid</response>
    [HttpPost("acs/{tenantId}")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Acs(
        [FromRoute] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _saml2Service.GetSaml2SettingsAsync(tenantId, cancellationToken);
        if (settings == null || !settings.IsEnabled)
        {
            _logger.LogWarning("SAML2 ACS callback received for tenant {TenantId} but SAML2 is not enabled", tenantId);
            return BadRequest(new { error = "SAML2 SSO is not configured or enabled for this tenant" });
        }

        try
        {
            // Build ACS URL
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            var acsUrl = settings.SpAcsUrl ?? $"{baseUrl}/saml2/acs/{tenantId}";

            // Create SAML2 options
            var options = CreateSaml2Options(settings, tenantId, acsUrl);

            // Read request body for SAML response
            Request.EnableBuffering();
            Request.Body.Position = 0;
            byte[] bodyBytes;
            using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                bodyBytes = ms.ToArray();
            }
            Request.Body.Position = 0;

            // Create request data from current HTTP request
            var requestData = new HttpRequestData(
                Request.Method,
                new Uri($"{Request.Scheme}://{Request.Host}{Request.Path}"),
                Request.PathBase,
                Request.Cookies.Select(c => new KeyValuePair<string, IEnumerable<string>>(c.Key, new[] { c.Value })),
                Request.Headers.SelectMany(h => h.Value.Select(v => new KeyValuePair<string, string>(h.Key, v))),
                bodyBytes => bodyBytes);

            // Process SAML response
            var commandResult = CommandFactory.GetCommand(CommandFactory.AcsCommandName)
                .Run(requestData, options);

            if (commandResult.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.LogWarning("SAML2 ACS processing failed for tenant {TenantId}: {Status}", tenantId,
                    commandResult.HttpStatusCode);
                return BadRequest(new { error = "Failed to process SAML2 response" });
            }

            // Extract claims from SAML response
            var principal = commandResult.Principal;
            if (principal == null || principal.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("SAML2 ACS callback did not produce authenticated principal for tenant {TenantId}",
                    tenantId);
                return BadRequest(new { error = "SAML2 authentication failed" });
            }

            // Map SAML claims to user properties
            var email = principal.FindFirst(ClaimTypes.Email)?.Value ??
                        principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
            var nameId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                         principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                             ?.Value;

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(nameId))
            {
                _logger.LogWarning(
                    "SAML2 ACS callback did not provide email or name identifier for tenant {TenantId}", tenantId);
                return BadRequest(new { error = "SAML2 response missing required claims (email or name identifier)" });
            }

            // Use email or nameId to find/create user
            var identifier = email ?? nameId!;
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.TenantId == tenantId && (u.Email == identifier || u.UserName == identifier),
                    cancellationToken);

            // Create user if doesn't exist (just-in-time provisioning)
            if (user == null)
            {
                var userName = email ?? nameId!;
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UserName = userName,
                    Email = email ?? userName,
                    EmailConfirmed = true, // Trust IdP email verification
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    _logger.LogError("Failed to create user from SAML2 response for tenant {TenantId}: {Errors}",
                        tenantId, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    return BadRequest(new { error = "Failed to create user account" });
                }

                _logger.LogInformation("Created user from SAML2 response. UserId: {UserId}, TenantId: {TenantId}",
                    user.Id, tenantId);
            }
            else if (!user.IsActive)
            {
                _logger.LogWarning("SAML2 authentication attempted for inactive user. UserId: {UserId}, TenantId: {TenantId}",
                    user.Id, tenantId);
                return BadRequest(new { error = "User account is inactive" });
            }

            // Get user roles
            var roles = await _userManager.GetRolesAsync(user);

            // Generate JWT token
            var accessToken = _jwtTokenService.GenerateToken(user, roles);

            // Get return URL from cookie
            var returnUrl = Request.Cookies[$"saml2_return_{tenantId}"];

            // Clear the cookie
            if (!string.IsNullOrWhiteSpace(returnUrl))
                Response.Cookies.Delete($"saml2_return_{tenantId}");

            _logger.LogInformation("SAML2 authentication successful. UserId: {UserId}, TenantId: {TenantId}", user.Id,
                tenantId);

            // If return URL is provided, redirect with token
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                var redirectUri = new UriBuilder(returnUrl);
                var query = new List<string>();
                if (redirectUri.Query != null && redirectUri.Query.Length > 1)
                    query.Add(redirectUri.Query.Substring(1)); // Remove leading '?'
                query.Add($"access_token={Uri.EscapeDataString(accessToken)}");
                redirectUri.Query = string.Join("&", query);
                return Redirect(redirectUri.ToString());
            }

            // Otherwise return token in JSON response
            return Ok(new
            {
                access_token = accessToken,
                token_type = "Bearer",
                tenant_id = tenantId,
                user_id = user.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SAML2 ACS callback for tenant {TenantId}", tenantId);
            return BadRequest(new { error = "Failed to process SAML2 response", message = ex.Message });
        }
    }

    /// <summary>
    ///     Gets SAML2 settings for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SAML2 settings</returns>
    [HttpGet("settings/{tenantId}")]
    [Authorize]
    [ProducesResponseType(typeof(Saml2SettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSettings(
        [FromRoute] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var settings = await _saml2Service.GetSaml2SettingsAsync(tenantId, cancellationToken);
        if (settings == null) return NotFound();

        // Don't return sensitive data (certificates, private keys)
        return Ok(new
        {
            settings.TenantId,
            settings.IsEnabled,
            settings.IdpEntityId,
            settings.IdpSsoServiceUrl,
            settings.SpEntityId,
            settings.SpAcsUrl,
            settings.NameIdFormat,
            settings.AttributeMapping,
            settings.SignAuthnRequest,
            settings.RequireSignedResponse,
            settings.RequireEncryptedAssertion,
            settings.ClockSkewMinutes
        });
    }

    /// <summary>
    ///     Creates or updates SAML2 settings for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="request">SAML2 settings request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated SAML2 settings (without sensitive data)</returns>
    [HttpPost("settings/{tenantId}")]
    [Authorize]
    [ProducesResponseType(typeof(Saml2SettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrUpdateSettings(
        [FromRoute] Guid tenantId,
        [FromBody] CreateOrUpdateSaml2SettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build ACS URL if not provided
            if (string.IsNullOrWhiteSpace(request.SpEntityId))
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                request.SpEntityId = $"{baseUrl}/saml2";
            }

            var settings = await _saml2Service.CreateOrUpdateSaml2SettingsAsync(tenantId, request, cancellationToken);

            // Don't return sensitive data
            return Ok(new
            {
                settings.TenantId,
                settings.IsEnabled,
                settings.IdpEntityId,
                settings.IdpSsoServiceUrl,
                settings.SpEntityId,
                settings.SpAcsUrl,
                settings.NameIdFormat,
                settings.AttributeMapping,
                settings.SignAuthnRequest,
                settings.RequireSignedResponse,
                settings.RequireEncryptedAssertion,
                settings.ClockSkewMinutes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating SAML2 settings for tenant {TenantId}", tenantId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    ///     Deletes SAML2 settings for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant ID (UUID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpDelete("settings/{tenantId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSettings(
        [FromRoute] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var result = await _saml2Service.DeleteSaml2SettingsAsync(tenantId, cancellationToken);
        if (!result) return NotFound();

        return Ok(new { message = "SAML2 settings deleted successfully" });
    }

    /// <summary>
    ///     Creates SAML2 options from tenant settings
    /// </summary>
    private Options CreateSaml2Options(Saml2SettingsDto settings, Guid tenantId, string acsUrl)
    {
        var spOptions = new SPOptions
        {
            EntityId = new EntityId(settings.SpEntityId ?? $"{Request.Scheme}://{Request.Host}/saml2"),
            ReturnUrl = new Uri(acsUrl)
        };

        // Load IdP certificate if provided
        X509Certificate2? idpCert = null;
        if (!string.IsNullOrWhiteSpace(settings.IdpCertificate))
            try
            {
                idpCert = LoadCertificate(settings.IdpCertificate, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load IdP certificate for tenant {TenantId}", tenantId);
            }

        var idp = new IdentityProvider(
            new EntityId(settings.IdpEntityId!),
            spOptions)
        {
            Binding = Saml2BindingType.HttpRedirect,
            SingleSignOnServiceUrl = new Uri(settings.IdpSsoServiceUrl!)
        };

        if (idpCert != null) idp.SigningKeys.AddConfiguredKey(idpCert);

        var options = new Options(spOptions);
        options.IdentityProviders.Add(idp);

        return options;
    }

    /// <summary>
    ///     Loads an X.509 certificate from base64 string
    /// </summary>
    private X509Certificate2? LoadCertificate(string? certificateBase64, string? privateKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(certificateBase64)) return null;

        try
        {
            var certBytes = Convert.FromBase64String(certificateBase64);

            // If private key is provided separately, combine them
            if (!string.IsNullOrWhiteSpace(privateKeyBase64))
            {
                var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
                // Note: This is a simplified approach. In production, you may need to properly combine
                // the certificate and private key using cryptographic APIs
                return new X509Certificate2(certBytes);
            }

            return new X509Certificate2(certBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load certificate from base64 string");
            return null;
        }
    }
}
