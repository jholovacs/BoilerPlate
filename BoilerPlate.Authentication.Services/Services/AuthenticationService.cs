using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using BoilerPlate.Authentication.Services.Events;
using BoilerPlate.ServiceBus.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
/// Service implementation for user authentication with multi-tenancy support
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly BaseAuthDbContext _context;
    private readonly ITopicPublisher? _topicPublisher;
    private readonly ILogger<AuthenticationService>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationService"/> class
    /// </summary>
    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        BaseAuthDbContext context,
        ITopicPublisher? topicPublisher = null,
        ILogger<AuthenticationService>? logger = null)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _topicPublisher = topicPublisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // Validate passwords match
        if (request.Password != request.ConfirmPassword)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "Password and confirm password do not match." }
            };
        }

        // Verify tenant exists
        var tenant = await _context.Tenants.FindAsync(new object[] { request.TenantId }, cancellationToken);
        if (tenant == null || !tenant.IsActive)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "Invalid or inactive tenant." }
            };
        }

        // Check if user already exists in this tenant
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == request.TenantId && u.Email == request.Email, cancellationToken);
        if (existingUser != null)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "A user with this email already exists in this tenant." }
            };
        }

        existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == request.TenantId && u.UserName == request.UserName, cancellationToken);
        if (existingUser != null)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "A user with this username already exists in this tenant." }
            };
        }

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
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = result.Errors.Select(e => e.Description)
            };
        }

        var userDto = await MapToUserDtoAsync(user, cancellationToken);

        // Publish UserCreatedEvent
        if (_topicPublisher != null)
        {
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

                await _topicPublisher.PublishAsync(userCreatedEvent, cancellationToken);
                _logger?.LogDebug("Published UserCreatedEvent for user {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                // Log but don't fail the registration if event publishing fails
                _logger?.LogError(ex, "Failed to publish UserCreatedEvent for user {UserId}", user.Id);
            }
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
        // Find user by email or username within the tenant
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == request.TenantId && 
                (u.Email == request.UserNameOrEmail || u.UserName == request.UserNameOrEmail), 
                cancellationToken);

        if (user == null)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "Invalid username or password." }
            };
        }

        // Check if user is active
        if (!user.IsActive)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new[] { "User account is inactive." }
            };
        }

        // Attempt sign in
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            var errors = new List<string>();
            if (result.IsLockedOut)
            {
                errors.Add("User account is locked out.");
            }
            else if (result.IsNotAllowed)
            {
                errors.Add("User is not allowed to sign in.");
            }
            else
            {
                errors.Add("Invalid username or password.");
            }

            return new AuthResult
            {
                Succeeded = false,
                Errors = errors
            };
        }

        var userDto = await MapToUserDtoAsync(user, cancellationToken);

        return new AuthResult
        {
            Succeeded = true,
            User = userDto
        };
    }

    /// <inheritdoc />
    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return false;
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == request.TenantId, cancellationToken);
        
        if (user == null)
        {
            return false;
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        return result.Succeeded;
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
