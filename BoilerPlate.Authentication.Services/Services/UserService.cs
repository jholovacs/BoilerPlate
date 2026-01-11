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
///     Service implementation for user management with multi-tenancy support
/// </summary>
public class UserService : IUserService
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<UserService>? _logger;
    private readonly IQueuePublisher? _queuePublisher;
    private readonly ITopicPublisher? _topicPublisher;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserService" /> class
    /// </summary>
    public UserService(
        UserManager<ApplicationUser> userManager,
        BaseAuthDbContext context,
        ITopicPublisher? topicPublisher = null,
        IQueuePublisher? queuePublisher = null,
        ILogger<UserService>? logger = null)
    {
        _userManager = userManager;
        _context = context;
        _topicPublisher = topicPublisher;
        _queuePublisher = queuePublisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByIdAsync(Guid tenantId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null) return null;

        return await MapToUserDtoAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByEmailAsync(Guid tenantId, string email,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, cancellationToken);

        if (user == null) return null;

        return await MapToUserDtoAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByUserNameAsync(Guid tenantId, string userName,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UserName == userName, cancellationToken);

        if (user == null) return null;

        return await MapToUserDtoAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserDto>> GetAllUsersAsync(Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var userDto = await MapToUserDtoAsync(user, cancellationToken);
            userDtos.Add(userDto);
        }

        return userDtos;
    }

    /// <inheritdoc />
    public async Task<UserDto?> UpdateUserAsync(Guid tenantId, Guid userId, UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null) return null;

        // Capture old values before updating
        var oldEmail = user.Email;
        var oldFirstName = user.FirstName;
        var oldLastName = user.LastName;
        var oldPhoneNumber = user.PhoneNumber;
        var oldIsActive = user.IsActive;

        // Update properties
        if (request.FirstName != null) user.FirstName = request.FirstName;

        if (request.LastName != null) user.LastName = request.LastName;

        if (request.Email != null && request.Email != user.Email)
        {
            // Check if email already exists in tenant
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == request.Email && u.Id != userId,
                    cancellationToken);

            if (existingUser != null) return null; // Email already exists

            user.Email = request.Email;
            user.NormalizedEmail = _userManager.NormalizeEmail(request.Email);
        }

        if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;

        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;

        // Track changed properties for event
        var changedProperties = new List<string>();
        if (request.FirstName != null && request.FirstName != oldFirstName)
            changedProperties.Add(nameof(user.FirstName));
        if (request.LastName != null && request.LastName != oldLastName) changedProperties.Add(nameof(user.LastName));
        if (request.Email != null && request.Email != oldEmail) changedProperties.Add(nameof(user.Email));
        if (request.PhoneNumber != null && request.PhoneNumber != oldPhoneNumber)
            changedProperties.Add(nameof(user.PhoneNumber));
        if (request.IsActive.HasValue && request.IsActive.Value != oldIsActive)
            changedProperties.Add(nameof(user.IsActive));

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return null;

        var userDto = await MapToUserDtoAsync(user, cancellationToken);

        // Publish UserModifiedEvent
        if (_topicPublisher != null && changedProperties.Any())
            try
            {
                var userModifiedEvent = new UserModifiedEvent
                {
                    UserId = user.Id,
                    TenantId = user.TenantId,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email,
                    OldEmail = oldEmail,
                    FirstName = user.FirstName,
                    OldFirstName = oldFirstName,
                    LastName = user.LastName,
                    OldLastName = oldLastName,
                    PhoneNumber = user.PhoneNumber,
                    OldPhoneNumber = oldPhoneNumber,
                    IsActive = user.IsActive,
                    OldIsActive = oldIsActive,
                    ChangedProperties = changedProperties,
                    TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                    ReferenceId = user.Id.ToString(),
                    CreatedTimestamp = DateTime.UtcNow,
                    FailureCount = 0
                };

                // Publish to topic (broadcast)
                await _topicPublisher.PublishAsync(userModifiedEvent, cancellationToken);
                _logger?.LogDebug("Published UserModifiedEvent to topic for user {UserId}", user.Id);

                // Publish to queue (durable for audit service)
                if (_queuePublisher != null)
                {
                    await _queuePublisher.PublishAsync(userModifiedEvent, cancellationToken);
                    _logger?.LogDebug("Published UserModifiedEvent to queue for user {UserId}", user.Id);
                }

                // If user was disabled (IsActive changed from true to false), also publish UserDisabledEvent
                if (oldIsActive && !user.IsActive)
                {
                    var userRoles = await _userManager.GetRolesAsync(user);
                    var userDisabledEvent = new UserDisabledEvent
                    {
                        UserId = user.Id,
                        TenantId = user.TenantId,
                        UserName = user.UserName ?? string.Empty,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        PhoneNumber = user.PhoneNumber,
                        UserCreatedAt = user.CreatedAt,
                        DisabledAt = DateTime.UtcNow,
                        Roles = userRoles.ToList(),
                        TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                        ReferenceId = user.Id.ToString(),
                        CreatedTimestamp = DateTime.UtcNow,
                        FailureCount = 0
                    };

                    // Publish to topic (broadcast)
                    await _topicPublisher.PublishAsync(userDisabledEvent, cancellationToken);
                    _logger?.LogDebug("Published UserDisabledEvent to topic for user {UserId}", user.Id);

                    // Publish to queue (durable for audit service)
                    if (_queuePublisher != null)
                    {
                        await _queuePublisher.PublishAsync(userDisabledEvent, cancellationToken);
                        _logger?.LogDebug("Published UserDisabledEvent to queue for user {UserId}", user.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail the update if event publishing fails
                _logger?.LogError(ex, "Failed to publish event for user {UserId}", user.Id);
            }

        return userDto;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null) return false;

        // Capture user info before deletion
        var userName = user.UserName;
        var email = user.Email;

        var result = await _userManager.DeleteAsync(user);

        if (result.Succeeded)
            // Publish UserDeletedEvent
            if (_topicPublisher != null)
                try
                {
                    var userDeletedEvent = new UserDeletedEvent
                    {
                        UserId = userId,
                        TenantId = tenantId,
                        UserName = userName,
                        Email = email,
                        TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                        ReferenceId = userId.ToString(),
                        CreatedTimestamp = DateTime.UtcNow,
                        FailureCount = 0
                    };

                    // Publish to topic (broadcast)
                    await _topicPublisher.PublishAsync(userDeletedEvent, cancellationToken);
                    _logger?.LogDebug("Published UserDeletedEvent to topic for user {UserId}", userId);

                    // Publish to queue (durable for audit service)
                    if (_queuePublisher != null)
                    {
                        await _queuePublisher.PublishAsync(userDeletedEvent, cancellationToken);
                        _logger?.LogDebug("Published UserDeletedEvent to queue for user {UserId}", userId);
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail the deletion if event publishing fails
                    _logger?.LogError(ex, "Failed to publish UserDeletedEvent for user {UserId}", userId);
                }

        return result.Succeeded;
    }

    /// <inheritdoc />
    public async Task<bool> ActivateUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null) return false;

        // Capture old value
        var oldIsActive = user.IsActive;

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
            // Publish UserModifiedEvent for activation
            if (_topicPublisher != null)
                try
                {
                    var userModifiedEvent = new UserModifiedEvent
                    {
                        UserId = user.Id,
                        TenantId = user.TenantId,
                        UserName = user.UserName ?? string.Empty,
                        Email = user.Email,
                        OldEmail = user.Email,
                        FirstName = user.FirstName,
                        OldFirstName = user.FirstName,
                        LastName = user.LastName,
                        OldLastName = user.LastName,
                        PhoneNumber = user.PhoneNumber,
                        OldPhoneNumber = user.PhoneNumber,
                        IsActive = user.IsActive,
                        OldIsActive = oldIsActive,
                        ChangedProperties = new List<string> { nameof(user.IsActive) },
                        TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                        ReferenceId = user.Id.ToString(),
                        CreatedTimestamp = DateTime.UtcNow,
                        FailureCount = 0
                    };

                    // Publish to topic (broadcast)
                    await _topicPublisher.PublishAsync(userModifiedEvent, cancellationToken);
                    _logger?.LogDebug("Published UserModifiedEvent to topic for user activation {UserId}", user.Id);

                    // Publish to queue (durable for audit service)
                    if (_queuePublisher != null)
                    {
                        await _queuePublisher.PublishAsync(userModifiedEvent, cancellationToken);
                        _logger?.LogDebug("Published UserModifiedEvent to queue for user activation {UserId}", user.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to publish UserModifiedEvent for user {UserId}", user.Id);
                }

        return result.Succeeded;
    }

    /// <inheritdoc />
    public async Task<bool> DeactivateUserAsync(Guid tenantId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null) return false;

        // Capture old value
        var oldIsActive = user.IsActive;

        // If user is already inactive, no need to do anything
        if (!oldIsActive) return true;

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
            // Publish UserModifiedEvent for deactivation
            if (_topicPublisher != null)
                try
                {
                    var userModifiedEvent = new UserModifiedEvent
                    {
                        UserId = user.Id,
                        TenantId = user.TenantId,
                        UserName = user.UserName ?? string.Empty,
                        Email = user.Email,
                        OldEmail = user.Email,
                        FirstName = user.FirstName,
                        OldFirstName = user.FirstName,
                        LastName = user.LastName,
                        OldLastName = user.LastName,
                        PhoneNumber = user.PhoneNumber,
                        OldPhoneNumber = user.PhoneNumber,
                        IsActive = user.IsActive,
                        OldIsActive = oldIsActive,
                        ChangedProperties = new List<string> { nameof(user.IsActive) },
                        TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                        ReferenceId = user.Id.ToString(),
                        CreatedTimestamp = DateTime.UtcNow,
                        FailureCount = 0
                    };

                    // Publish to topic (broadcast)
                    await _topicPublisher.PublishAsync(userModifiedEvent, cancellationToken);
                    _logger?.LogDebug("Published UserModifiedEvent to topic for user deactivation {UserId}", user.Id);

                    // Publish to queue (durable for audit service)
                    if (_queuePublisher != null)
                    {
                        await _queuePublisher.PublishAsync(userModifiedEvent, cancellationToken);
                        _logger?.LogDebug("Published UserModifiedEvent to queue for user deactivation {UserId}",
                            user.Id);
                    }

                    // Also publish UserDisabledEvent since user was active and is now disabled
                    var userRoles = await _userManager.GetRolesAsync(user);
                    var userDisabledEvent = new UserDisabledEvent
                    {
                        UserId = user.Id,
                        TenantId = user.TenantId,
                        UserName = user.UserName ?? string.Empty,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        PhoneNumber = user.PhoneNumber,
                        UserCreatedAt = user.CreatedAt,
                        DisabledAt = DateTime.UtcNow,
                        Roles = userRoles.ToList(),
                        TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                        ReferenceId = user.Id.ToString(),
                        CreatedTimestamp = DateTime.UtcNow,
                        FailureCount = 0
                    };

                    // Publish to topic (broadcast)
                    await _topicPublisher.PublishAsync(userDisabledEvent, cancellationToken);
                    _logger?.LogDebug("Published UserDisabledEvent to topic for user {UserId}", user.Id);

                    // Publish to queue (durable for audit service)
                    if (_queuePublisher != null)
                    {
                        await _queuePublisher.PublishAsync(userDisabledEvent, cancellationToken);
                        _logger?.LogDebug("Published UserDisabledEvent to queue for user {UserId}", user.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to publish event for user {UserId}", user.Id);
                }

        return result.Succeeded;
    }

    /// <inheritdoc />
    public async Task<bool> AssignRolesAsync(Guid tenantId, Guid userId, IEnumerable<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null) return false;

        // Verify all roles belong to the same tenant
        var roles = await _context.Roles
            .Where(r => r.TenantId == tenantId && roleNames.Contains(r.Name!))
            .ToListAsync(cancellationToken);

        if (roles.Count != roleNames.Count()) return false; // Not all roles found or belong to tenant

        // Get current roles before assignment
        var currentRoles = await _userManager.GetRolesAsync(user);
        var currentRolesList = currentRoles.ToList();

        var result = await _userManager.AddToRolesAsync(user, roleNames);

        if (result.Succeeded)
        {
            // Get roles after assignment
            var newRoles = await _userManager.GetRolesAsync(user);
            var newRolesList = newRoles.ToList();
            var assignedRoles = newRolesList.Except(currentRolesList).ToList();

            // Publish RoleAssignmentsChangedEvent
            if (_topicPublisher != null && assignedRoles.Any())
                try
                {
                    var roleAssignmentsChangedEvent = new RoleAssignmentsChangedEvent
                    {
                        UserId = user.Id,
                        TenantId = user.TenantId,
                        UserName = user.UserName ?? string.Empty,
                        AssignedRoles = assignedRoles,
                        RemovedRoles = new List<string>(),
                        CurrentRoles = newRolesList,
                        TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                        ReferenceId = user.Id.ToString(),
                        CreatedTimestamp = DateTime.UtcNow,
                        FailureCount = 0
                    };

                    await _topicPublisher.PublishAsync(roleAssignmentsChangedEvent, cancellationToken);
                    _logger?.LogDebug("Published RoleAssignmentsChangedEvent for user {UserId}", user.Id);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the assignment if event publishing fails
                    _logger?.LogError(ex, "Failed to publish RoleAssignmentsChangedEvent for user {UserId}", user.Id);
                }
        }

        return result.Succeeded;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveRolesAsync(Guid tenantId, Guid userId, IEnumerable<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null) return false;

        // Get current roles before removal
        var currentRoles = await _userManager.GetRolesAsync(user);
        var currentRolesList = currentRoles.ToList();
        var rolesToRemove = roleNames.ToList();

        var result = await _userManager.RemoveFromRolesAsync(user, roleNames);

        if (result.Succeeded)
        {
            // Get roles after removal
            var newRoles = await _userManager.GetRolesAsync(user);
            var newRolesList = newRoles.ToList();
            var removedRoles = currentRolesList.Except(newRolesList).ToList();

            // Publish RoleAssignmentsChangedEvent
            if (_topicPublisher != null && removedRoles.Any())
                try
                {
                    var roleAssignmentsChangedEvent = new RoleAssignmentsChangedEvent
                    {
                        UserId = user.Id,
                        TenantId = user.TenantId,
                        UserName = user.UserName ?? string.Empty,
                        AssignedRoles = new List<string>(),
                        RemovedRoles = removedRoles,
                        CurrentRoles = newRolesList,
                        TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                        ReferenceId = user.Id.ToString(),
                        CreatedTimestamp = DateTime.UtcNow,
                        FailureCount = 0
                    };

                    await _topicPublisher.PublishAsync(roleAssignmentsChangedEvent, cancellationToken);
                    _logger?.LogDebug("Published RoleAssignmentsChangedEvent for user {UserId}", user.Id);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the removal if event publishing fails
                    _logger?.LogError(ex, "Failed to publish RoleAssignmentsChangedEvent for user {UserId}", user.Id);
                }
        }

        return result.Succeeded;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetUserRolesAsync(Guid tenantId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user == null) return Enumerable.Empty<string>();

        var roles = await _userManager.GetRolesAsync(user);
        return roles;
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