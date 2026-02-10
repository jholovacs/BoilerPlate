using System.Diagnostics;
using BoilerPlate.Authentication.Abstractions;
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
///     Service implementation for tenant management
/// </summary>
public class TenantService : ITenantService
{
    private readonly BaseAuthDbContext _context;
    private readonly ILogger<TenantService>? _logger;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ITopicPublisher? _topicPublisher;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TenantService" /> class
    /// </summary>
    public TenantService(
        BaseAuthDbContext context,
        RoleManager<ApplicationRole> roleManager,
        ITopicPublisher? topicPublisher = null,
        ILogger<TenantService>? logger = null)
    {
        _context = context;
        _roleManager = roleManager;
        _topicPublisher = topicPublisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TenantDto?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant == null) return null;

        return MapToTenantDto(tenant);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TenantDto>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _context.Tenants.ToListAsync(cancellationToken);
        return tenants.Select(MapToTenantDto);
    }

    /// <inheritdoc />
    public async Task<TenantDto?> CreateTenantAsync(CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var existingTenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Name == request.Name, cancellationToken);

        if (existingTenant != null) return null;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync(cancellationToken);

                await CreateDefaultRolesForTenantAsync(tenant.Id, tenant.Name, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return MapToTenantDto(tenant);
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(cancellationToken); }
                catch (Exception rollbackEx) { _logger?.LogError(rollbackEx, "Rollback failed"); }
                _logger?.LogError(ex, "Failed to create tenant {Name}", request.Name);
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<TenantDto?> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant == null) return null;

        if (request.Name != null)
        {
            // Check if new name already exists
            var existingTenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Name == request.Name && t.Id != tenantId, cancellationToken);

            if (existingTenant != null) return null;

            tenant.Name = request.Name;
        }

        if (request.Description != null) tenant.Description = request.Description;

        if (request.IsActive.HasValue) tenant.IsActive = request.IsActive.Value;

        tenant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToTenantDto(tenant);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant == null) return false;

        // Check if tenant has users or roles
        var hasUsers = await _context.Users.AnyAsync(u => u.TenantId == tenantId, cancellationToken);
        var hasRoles = await _context.Roles.AnyAsync(r => r.TenantId == tenantId, cancellationToken);

        if (hasUsers || hasRoles)
            return false; // Cannot delete tenant with users or roles - use OffboardTenantAsync instead

        _context.Tenants.Remove(tenant);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<TenantDto?> OnboardTenantAsync(CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        // Use execution strategy to support retry logic with transactions
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Use a transaction to ensure atomicity
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Check if tenant name already exists
                var existingTenant = await _context.Tenants
                    .FirstOrDefaultAsync(t => t.Name == request.Name, cancellationToken);

                if (existingTenant != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return null;
                }

                // Create the tenant within the transaction
                var tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Tenants.Add(tenant);
                // Save tenant but don't commit transaction yet
                await _context.SaveChangesAsync(cancellationToken);

                var createdRoleNames = await CreateDefaultRolesForTenantAsync(tenant.Id, tenant.Name, cancellationToken);
                if (createdRoleNames == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return null;
                }

                // Map to DTO before committing
                var tenantDto = MapToTenantDto(tenant);

                // Commit transaction if everything succeeds
                await transaction.CommitAsync(cancellationToken);

                // Publish TenantOnboardedEvent (after successful commit)
                if (_topicPublisher != null)
                    try
                    {
                        var tenantOnboardedEvent = new TenantOnboardedEvent
                        {
                            TenantId = tenantDto.Id,
                            Name = tenantDto.Name,
                            Description = tenantDto.Description,
                            IsActive = tenantDto.IsActive,
                            CreatedAt = tenantDto.CreatedAt,
                            DefaultRoles = createdRoleNames,
                            TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                            ReferenceId = tenantDto.Id.ToString(),
                            CreatedTimestamp = DateTime.UtcNow,
                            FailureCount = 0
                        };

                        await _topicPublisher.PublishAsync(tenantOnboardedEvent, cancellationToken);
                        _logger?.LogDebug("Published TenantOnboardedEvent for tenant {TenantId}", tenantDto.Id);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail the onboarding if event publishing fails
                        _logger?.LogError(ex, "Failed to publish TenantOnboardedEvent for tenant {TenantId}",
                            tenantDto.Id);
                    }

                return tenantDto;
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    _logger?.LogError(rollbackEx,
                        "Error rolling back transaction during tenant onboarding for tenant name: {TenantName}",
                        request.Name);
                }

                _logger?.LogError(ex,
                    "Error during tenant onboarding for tenant name: {TenantName}. Exception: {Exception}",
                    request.Name, ex);
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<bool> OffboardTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant == null) return false;

        // Capture tenant info before deletion for event
        var tenantName = tenant.Name;
        var tenantDescription = tenant.Description;
        var tenantCreatedAt = tenant.CreatedAt;
        var offboardedAt = DateTime.UtcNow;

        // Use execution strategy to support retry logic with transactions
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Use a transaction to ensure atomicity
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Get all user IDs for this tenant (for efficient querying)
                var userIds = await _context.Users
                    .Where(u => u.TenantId == tenantId)
                    .Select(u => u.Id)
                    .ToListAsync(cancellationToken);

                // Get all role IDs for this tenant
                var roleIds = await _context.Roles
                    .Where(r => r.TenantId == tenantId)
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);

                // Capture counts before deletion for event
                var usersDeletedCount = userIds.Count;
                var rolesDeletedCount = roleIds.Count;

                // Delete all tenant-specific data in the correct order to respect foreign key constraints

                // 1. Delete user tokens
                if (userIds.Any())
                {
                    var userTokens = await _context.Set<IdentityUserToken<Guid>>()
                        .Where(ut => userIds.Contains(ut.UserId))
                        .ToListAsync(cancellationToken);
                    _context.Set<IdentityUserToken<Guid>>().RemoveRange(userTokens);
                }

                // 2. Delete user logins
                if (userIds.Any())
                {
                    var userLogins = await _context.Set<IdentityUserLogin<Guid>>()
                        .Where(ul => userIds.Contains(ul.UserId))
                        .ToListAsync(cancellationToken);
                    _context.Set<IdentityUserLogin<Guid>>().RemoveRange(userLogins);
                }

                // 3. Delete user claims
                if (userIds.Any())
                {
                    var userClaims = await _context.Set<IdentityUserClaim<Guid>>()
                        .Where(uc => userIds.Contains(uc.UserId))
                        .ToListAsync(cancellationToken);
                    _context.Set<IdentityUserClaim<Guid>>().RemoveRange(userClaims);
                }

                // 4. Delete role claims
                if (roleIds.Any())
                {
                    var roleClaims = await _context.Set<IdentityRoleClaim<Guid>>()
                        .Where(rc => roleIds.Contains(rc.RoleId))
                        .ToListAsync(cancellationToken);
                    _context.Set<IdentityRoleClaim<Guid>>().RemoveRange(roleClaims);
                }

                // 5. Delete user roles (many-to-many relationship)
                if (userIds.Any() || roleIds.Any())
                {
                    var userRoles = await _context.Set<IdentityUserRole<Guid>>()
                        .Where(ur => userIds.Contains(ur.UserId) || roleIds.Contains(ur.RoleId))
                        .ToListAsync(cancellationToken);
                    _context.Set<IdentityUserRole<Guid>>().RemoveRange(userRoles);
                }

                // 6. Delete users
                if (userIds.Any())
                {
                    var users = await _context.Users
                        .Where(u => u.TenantId == tenantId)
                        .ToListAsync(cancellationToken);
                    _context.Users.RemoveRange(users);
                }

                // 7. Delete roles
                if (roleIds.Any())
                {
                    var roles = await _context.Roles
                        .Where(r => r.TenantId == tenantId)
                        .ToListAsync(cancellationToken);
                    _context.Roles.RemoveRange(roles);
                }

                // 8. Delete the tenant itself
                _context.Tenants.Remove(tenant);

                // Save all changes in a single transaction
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // Publish TenantOffboardedEvent
                if (_topicPublisher != null)
                    try
                    {
                        var tenantOffboardedEvent = new TenantOffboardedEvent
                        {
                            TenantId = tenantId,
                            Name = tenantName,
                            Description = tenantDescription,
                            UsersDeletedCount = usersDeletedCount,
                            RolesDeletedCount = rolesDeletedCount,
                            TenantCreatedAt = tenantCreatedAt,
                            OffboardedAt = offboardedAt,
                            TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
                            ReferenceId = tenantId.ToString(),
                            CreatedTimestamp = DateTime.UtcNow,
                            FailureCount = 0
                        };

                        await _topicPublisher.PublishAsync(tenantOffboardedEvent, cancellationToken);
                        _logger?.LogDebug("Published TenantOffboardedEvent for tenant {TenantId}", tenantId);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail the offboarding if event publishing fails
                        _logger?.LogError(ex, "Failed to publish TenantOffboardedEvent for tenant {TenantId}",
                            tenantId);
                    }

                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    /// <summary>
    ///     Creates the predefined default roles for a tenant. System tenant (name "System" or "System Tenant") gets all 4 roles including Service Administrator; others get 3 roles.
    ///     Returns the list of created role names, or null if any role creation failed.
    /// </summary>
    private async Task<List<string>?> CreateDefaultRolesForTenantAsync(Guid tenantId, string tenantName,
        CancellationToken cancellationToken)
    {
        var definitions = GetDefaultRoleDefinitions(tenantName);
        var created = new List<string>();

        foreach (var (name, description) in definitions)
        {
            var role = new ApplicationRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                NormalizedName = _roleManager.NormalizeKey(name),
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                _logger?.LogError("Failed to create role {RoleName} for tenant {TenantId}. Errors: {Errors}",
                    name, tenantId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return null;
            }

            created.Add(name);
        }

        return created;
    }

    private static IEnumerable<(string Name, string Description)> GetDefaultRoleDefinitions(string tenantName)
    {
        if (PredefinedRoleNames.IsSystemTenant(tenantName))
        {
            yield return (PredefinedRoleNames.ServiceAdministrator, "Full access to all resources across all tenants");
        }

        yield return (PredefinedRoleNames.TenantAdministrator, "Full access to resources within the tenant");
        yield return (PredefinedRoleNames.UserAdministrator, "User management within the tenant");
        yield return (PredefinedRoleNames.RoleAdministrator, "Create and manage custom roles within the tenant");
    }

    private static TenantDto MapToTenantDto(Tenant tenant)
    {
        return new TenantDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Description = tenant.Description,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        };
    }
}