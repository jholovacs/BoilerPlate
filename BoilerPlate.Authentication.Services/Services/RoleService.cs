using BoilerPlate.Authentication.Abstractions;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
///     Service implementation for role management with multi-tenancy support
/// </summary>
public class RoleService : IRoleService
{
    private readonly BaseAuthDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RoleService" /> class
    /// </summary>
    public RoleService(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        BaseAuthDbContext context)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _context = context;
    }

    /// <inheritdoc />
    public async Task<RoleDto?> GetRoleByIdAsync(Guid tenantId, Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null) return null;

        return MapToRoleDto(role);
    }

    /// <inheritdoc />
    public async Task<RoleDto?> GetRoleByNameAsync(Guid tenantId, string roleName,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == roleName, cancellationToken);

        if (role == null) return null;

        return MapToRoleDto(role);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RoleDto>> GetAllRolesAsync(Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var roles = await _context.Roles
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return roles.Select(MapToRoleDto);
    }

    /// <inheritdoc />
    public async Task<RoleDto?> CreateRoleAsync(CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        // Verify tenant exists
        var tenant = await _context.Tenants.FindAsync(new object[] { request.TenantId }, cancellationToken);
        if (tenant == null || !tenant.IsActive) return null;

        // Check if role already exists in tenant
        var existingRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.TenantId == request.TenantId && r.Name == request.Name, cancellationToken);

        if (existingRole != null) return null;

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Name = request.Name,
            NormalizedName = _roleManager.NormalizeKey(request.Name),
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded) return null;

        return MapToRoleDto(role);
    }

    /// <inheritdoc />
    public async Task<RoleDto?> UpdateRoleAsync(Guid tenantId, Guid roleId, UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null) return null;

        if (PredefinedRoleNames.IsProtected(role.Name ?? string.Empty)) return null;
        if (PredefinedRoleNames.IsProtected(request.Name) && request.Name != role.Name) return null;

        // Check if new name already exists in tenant (if different from current)
        if (request.Name != role.Name)
        {
            var existingRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == request.Name && r.Id != roleId,
                    cancellationToken);

            if (existingRole != null) return null;
        }

        role.Name = request.Name;
        role.NormalizedName = _roleManager.NormalizeKey(request.Name);

        if (request.Description != null) role.Description = request.Description;

        role.UpdatedAt = DateTime.UtcNow;

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded) return null;

        return MapToRoleDto(role);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

        if (role == null) return false;

        if (PredefinedRoleNames.IsProtected(role.Name ?? string.Empty)) return false;

        var result = await _roleManager.DeleteAsync(role);
        return result.Succeeded;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserDto>> GetUsersInRoleAsync(Guid tenantId, string roleName,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == roleName, cancellationToken);

        if (role == null) return Enumerable.Empty<UserDto>();

        // Resolve users by role Id (tenant-scoped), not by role name. GetUsersInRoleAsync(roleName)
        // would look up the role by name only and can return multiple roles across tenants.
        var userIdsInRole = await _context.Set<IdentityUserRole<Guid>>()
            .Where(ur => ur.RoleId == role.Id)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        if (userIdsInRole.Count == 0) return Enumerable.Empty<UserDto>();

        var users = await _context.Users
            .Where(u => userIdsInRole.Contains(u.Id))
            .ToListAsync(cancellationToken);

        var userDtos = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto
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
            });
        }

        return userDtos;
    }

    private static RoleDto MapToRoleDto(ApplicationRole role)
    {
        return new RoleDto
        {
            Id = role.Id,
            TenantId = role.TenantId,
            Name = role.Name ?? string.Empty,
            NormalizedName = role.NormalizedName,
            Description = role.Description,
            IsSystemRole = PredefinedRoleNames.IsProtected(role.Name ?? string.Empty)
        };
    }
}