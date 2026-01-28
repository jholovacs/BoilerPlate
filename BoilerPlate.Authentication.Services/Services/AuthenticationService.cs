using System.Diagnostics;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.Services.Events;
using BoilerPlate.ServiceBus.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
///     Service implementation for user authentication with multi-tenancy support
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<AuthenticationService>? _logger;
    private readonly IPasswordPolicyService? _passwordPolicyService;
    private readonly IQueuePublisher? _queuePublisher;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITenantEmailDomainService? _tenantEmailDomainService;
    private readonly ITenantVanityUrlService? _tenantVanityUrlService;
    private readonly ITopicPublisher? _topicPublisher;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AuthenticationService" /> class
    /// </summary>
    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        BaseAuthDbContext context,
        ITopicPublisher? topicPublisher = null,
        IQueuePublisher? queuePublisher = null,
        ITenantEmailDomainService? tenantEmailDomainService = null,
        ITenantVanityUrlService? tenantVanityUrlService = null,
        IPasswordPolicyService? passwordPolicyService = null,
        ILogger<AuthenticationService>? logger = null)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _topicPublisher = topicPublisher;
        _queuePublisher = queuePublisher;
        _tenantEmailDomainService = tenantEmailDomainService;
        _tenantVanityUrlService = tenantVanityUrlService;
        _passwordPolicyService = passwordPolicyService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // Validate passwords match
        if (request.Password != request.ConfirmPassword)
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "Password and confirm password do not match." }
            };

        // Verify tenant exists
        var tenant = await _context.Tenants.FindAsync(new object[] { request.TenantId }, cancellationToken);
        if (tenant == null || !tenant.IsActive)
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "Invalid or inactive tenant." }
            };

        // Validate password complexity using tenant-specific policy
        if (_passwordPolicyService != null)
        {
            var complexityErrors = await _passwordPolicyService.ValidatePasswordComplexityAsync(
                request.Password,
                request.TenantId,
                cancellationToken);

            if (complexityErrors.Any())
            {
                return new AuthResult
                {
                    Succeeded = false,
                    Errors = complexityErrors
                };
            }
        }

        // Check if user already exists in this tenant
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == request.TenantId && u.Email == request.Email, cancellationToken);
        if (existingUser != null)
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "A user with this email already exists in this tenant." }
            };

        existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == request.TenantId && u.UserName == request.UserName,
                cancellationToken);
        if (existingUser != null)
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "A user with this username already exists in this tenant." }
            };

        // Create new user
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            UserName = request.UserName,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return new AuthResult
            {
                Succeeded = false,
                Errors = result.Errors.Select(e => e.Description)
            };

        var userDto = await MapToUserDtoAsync(user, cancellationToken);

        // Publish UserCreatedEvent
        if (_topicPublisher != null)
            try
            {
                var userCreatedEvent = new UserCreatedEvent
                {
                    UserId = user.Id,
                    TenantId = user.TenantId,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    IsActive = user.IsActive,
                    TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                    ReferenceId = user.Id.ToString(),
                    CreatedTimestamp = DateTime.UtcNow,
                    FailureCount = 0
                };

                // Publish to topic (broadcast)
                await _topicPublisher.PublishAsync(userCreatedEvent, cancellationToken);
                _logger?.LogDebug("Published UserCreatedEvent to topic for user {UserId}", user.Id);

                // Publish to queue (durable for audit service)
                if (_queuePublisher != null)
                {
                    await _queuePublisher.PublishAsync(userCreatedEvent, cancellationToken);
                    _logger?.LogDebug("Published UserCreatedEvent to queue for user {UserId}", user.Id);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the registration if event publishing fails
                _logger?.LogError(ex, "Failed to publish UserCreatedEvent for user {UserId}", user.Id);
            }

        return new AuthResult
        {
            Succeeded = true,
            User = userDto
        };
    }

    /// <inheritdoc />
    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Resolve tenant ID if not provided
        var tenantId = request.TenantId;
        if (!tenantId.HasValue)
        {
            // First, try to resolve tenant from vanity URL hostname if Host is provided
            if (!string.IsNullOrWhiteSpace(request.Host) && _tenantVanityUrlService != null)
            {
                var resolvedTenantId = await _tenantVanityUrlService.ResolveTenantIdFromHostnameAsync(
                    request.Host,
                    cancellationToken);

                if (resolvedTenantId.HasValue)
                {
                    tenantId = resolvedTenantId;
                    _logger?.LogDebug("Resolved tenant {TenantId} from vanity URL hostname {Hostname}", tenantId.Value,
                        request.Host);
                }
            }

            // If still not resolved, try to resolve tenant from email domain if UserNameOrEmail looks like an email
            if (!tenantId.HasValue && request.UserNameOrEmail.Contains('@') && _tenantEmailDomainService != null)
            {
                var resolvedTenantId = await _tenantEmailDomainService.ResolveTenantIdFromEmailAsync(
                    request.UserNameOrEmail,
                    cancellationToken);

                if (resolvedTenantId.HasValue)
                {
                    tenantId = resolvedTenantId;
                    _logger?.LogDebug("Resolved tenant {TenantId} from email domain for user {Email}", tenantId.Value,
                        request.UserNameOrEmail);
                }
            }

            // If still not resolved, return error
            if (!tenantId.HasValue)
            {
                var errorMessages = new List<string>();
                
                if (!string.IsNullOrWhiteSpace(request.Host))
                {
                    errorMessages.Add("Unable to resolve tenant from vanity URL hostname. Please specify tenant_id or ensure your vanity URL is configured.");
                }
                
                if (request.UserNameOrEmail.Contains('@'))
                {
                    errorMessages.Add("Unable to resolve tenant from email domain. Please specify tenant_id or ensure your email domain is configured.");
                }
                
                if (!request.UserNameOrEmail.Contains('@') && string.IsNullOrWhiteSpace(request.Host))
                {
                    errorMessages.Add("Tenant ID is required when using username without vanity URL. Please specify tenant_id.");
                }

                return new AuthResult
                {
                    Succeeded = false,
                    Errors = errorMessages
                };
            }
        }

        // Find user by email or username within the tenant
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId.Value &&
                                      (u.Email == request.UserNameOrEmail || u.UserName == request.UserNameOrEmail),
                cancellationToken);

        if (user == null)
        {
            _logger?.LogWarning("Login failed: User not found. UsernameOrEmail: {UserNameOrEmail}, TenantId: {TenantId}",
                request.UserNameOrEmail, tenantId.Value);
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "Invalid username or password." }
            };
        }

        // Check if user is active
        if (!user.IsActive)
        {
            _logger?.LogWarning("Login failed: User account is inactive. UserId: {UserId}, UsernameOrEmail: {UserNameOrEmail}, TenantId: {TenantId}",
                user.Id, request.UserNameOrEmail, tenantId.Value);
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "User account is inactive." }
            };
        }

        // Attempt sign in
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);

        if (!result.Succeeded)
        {
            var errors = new List<string>();
            if (result.IsLockedOut)
            {
                _logger?.LogWarning("Login failed: User account is locked out. UserId: {UserId}, UsernameOrEmail: {UserNameOrEmail}, TenantId: {TenantId}",
                    user.Id, request.UserNameOrEmail, tenantId.Value);
                errors.Add("User account is locked out.");
            }
            else if (result.IsNotAllowed)
            {
                _logger?.LogWarning("Login failed: User is not allowed to sign in. UserId: {UserId}, UsernameOrEmail: {UserNameOrEmail}, TenantId: {TenantId}",
                    user.Id, request.UserNameOrEmail, tenantId.Value);
                errors.Add("User is not allowed to sign in.");
            }
            else
            {
                _logger?.LogWarning("Login failed: Invalid password. UserId: {UserId}, UsernameOrEmail: {UserNameOrEmail}, TenantId: {TenantId}",
                    user.Id, request.UserNameOrEmail, tenantId.Value);
                errors.Add("Invalid username or password.");
            }

            return new AuthResult
            {
                Succeeded = false,
                Errors = errors
            };
        }

        // Check if password has expired
        if (_passwordPolicyService != null)
        {
            var isPasswordExpired = await _passwordPolicyService.IsPasswordExpiredAsync(
                user.Id,
                tenantId.Value,
                cancellationToken);

            if (isPasswordExpired)
            {
                _logger?.LogWarning("Login failed for user {UserId}: Password has expired", user.Id);
                return new AuthResult
                {
                    Succeeded = false,
                    Errors = new[] { "Your password has expired. Please change your password." }
                };
            }
        }

        var userDto = await MapToUserDtoAsync(user, cancellationToken);

        return new AuthResult
        {
            Succeeded = true,
            User = userDto
        };
    }

    /// <inheritdoc />
    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.NewPassword != request.ConfirmNewPassword) return false;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == request.TenantId, cancellationToken);

        if (user == null) return false;

        // Validate password complexity using tenant-specific policy
        if (_passwordPolicyService != null)
        {
            var complexityErrors = await _passwordPolicyService.ValidatePasswordComplexityAsync(
                request.NewPassword,
                request.TenantId,
                cancellationToken);

            if (complexityErrors.Any())
            {
                _logger?.LogWarning(
                    "Password change failed for user {UserId}: Password does not meet complexity requirements. Errors: {Errors}",
                    userId, string.Join(", ", complexityErrors));
                return false;
            }

            // Check if new password is in history
            // Hash the new password using Identity's password hasher to check against history
            var passwordHasher = _userManager.PasswordHasher;
            var newPasswordHash = passwordHasher.HashPassword(user, request.NewPassword);

            var isInHistory = await _passwordPolicyService.IsPasswordInHistoryAsync(
                userId,
                newPasswordHash,
                request.TenantId,
                cancellationToken);

            if (isInHistory)
            {
                _logger?.LogWarning(
                    "Password change failed for user {UserId}: New password matches a previously used password",
                    userId);
                return false;
            }
        }

        // Get old password hash before changing
        var oldPasswordHash = user.PasswordHash;

        // Change password using Identity
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            _logger?.LogWarning("Password change failed for user {UserId}: {Errors}", userId,
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        // Save old password to history (if password history is enabled)
        if (_passwordPolicyService != null && !string.IsNullOrEmpty(oldPasswordHash))
        {
            await _passwordPolicyService.SavePasswordToHistoryAsync(
                userId,
                oldPasswordHash,
                request.TenantId,
                cancellationToken);
        }

        _logger?.LogInformation("Password changed successfully for user {UserId}", userId);
        return true;
    }

    private async Task<UserDto> MapToUserDtoAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserDto
        {
            Id = user.Id,
            TenantId = user.TenantId,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Roles = roles
        };
    }
}