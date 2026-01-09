using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
/// Service implementation for tenant management
/// </summary>
public class TenantService : ITenantService
{
    private readonly BaseAuthDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantService"/> class
    /// </summary>
    public TenantService(
        BaseAuthDbContext context,
        RoleManager<ApplicationRole> roleManager)
    {
        _context = context;
        _roleManager = roleManager;
    }

    /// <inheritdoc />
    public async Task<TenantDto?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        return MapToTenantDto(tenant);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TenantDto>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _context.Tenants.ToListAsync(cancellationToken);
        return tenants.Select(MapToTenantDto);
    }

    /// <inheritdoc />
    public async Task<TenantDto?> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        // Check if tenant name already exists
        var existingTenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Name == request.Name, cancellationToken);
        
        if (existingTenant != null)
        {
            return null;
        }

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

        return MapToTenantDto(tenant);
    }

    /// <inheritdoc />
    public async Task<TenantDto?> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        if (request.Name != null)
        {
            // Check if new name already exists
            var existingTenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Name == request.Name && t.Id != tenantId, cancellationToken);
            
            if (existingTenant != null)
            {
                return null;
            }

            tenant.Name = request.Name;
        }

        if (request.Description != null)
        {
            tenant.Description = request.Description;
        }

        if (request.IsActive.HasValue)
        {
            tenant.IsActive = request.IsActive.Value;
        }

        tenant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToTenantDto(tenant);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant == null)
        {
            return false;
        }

        // Check if tenant has users or roles
        var hasUsers = await _context.Users.AnyAsync(u => u.TenantId == tenantId, cancellationToken);
        var hasRoles = await _context.Roles.AnyAsync(r => r.TenantId == tenantId, cancellationToken);

        if (hasUsers || hasRoles)
        {
            return false; // Cannot delete tenant with users or roles - use OffboardTenantAsync instead
        }

        _context.Tenants.Remove(tenant);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<TenantDto?> OnboardTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        // Use a transaction to ensure atomicity
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Create the tenant
            var tenantDto = await CreateTenantAsync(request, cancellationToken);
            if (tenantDto == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            // Create default roles for the tenant
            var defaultRoles = new[]
            {
                new { Name = "Tenant Administrator", Description = "Full administrative access to tenant settings and configuration" },
                new { Name = "User Administrator", Description = "Administrative access to manage users within the tenant" }
            };

            foreach (var roleInfo in defaultRoles)
            {
                var role = new ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantDto.Id,
                    Name = roleInfo.Name,
                    NormalizedName = _roleManager.NormalizeKey(roleInfo.Name),
                    Description = roleInfo.Description,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _roleManager.CreateAsync(role);
                if (!result.Succeeded)
                {
                    // Rollback transaction if role creation fails
                    await transaction.RollbackAsync(cancellationToken);
                    return null;
                }
            }

            // Commit transaction if everything succeeds
            await transaction.CommitAsync(cancellationToken);
            return tenantDto;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> OffboardTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
        if (tenant == null)
        {
            return false;
        }

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

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
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
