using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.WebApi.Services;

/// <summary>
/// Hosted service that initializes the admin user at startup
/// </summary>
public class AdminUserInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdminUserInitializationService> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminUserInitializationService"/> class
    /// </summary>
    public AdminUserInitializationService(
        IServiceProvider serviceProvider,
        ILogger<AdminUserInitializationService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var adminUsername = _configuration["ADMIN_USERNAME"];
        var adminPassword = _configuration["ADMIN_PASSWORD"];

        // Only proceed if both environment variables are set
        if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogInformation("ADMIN_USERNAME or ADMIN_PASSWORD not set. Skipping admin user initialization.");
            return;
        }

        _logger.LogInformation("Initializing admin user: {Username}", adminUsername);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BaseAuthDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();

        try
        {
            // Get or create system tenant
            var systemTenantId = await GetOrCreateSystemTenantAsync(tenantService, cancellationToken);
            if (systemTenantId == null)
            {
                _logger.LogError("Failed to get or create system tenant for admin user");
                return;
            }

            // Look up the user by username (search across all tenants)
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.UserName == adminUsername, cancellationToken);

            Guid userTenantId;
            if (user == null)
            {
                // User doesn't exist - create it in system tenant
                _logger.LogInformation("Admin user does not exist. Creating new admin user in system tenant.");
                userTenantId = systemTenantId.Value;
                
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    TenantId = userTenantId,
                    UserName = adminUsername,
                    Email = adminUsername.Contains('@') ? adminUsername : $"{adminUsername}@system.local",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(user, adminPassword);
                if (!createResult.Succeeded)
                {
                    _logger.LogError("Failed to create admin user: {Errors}", 
                        string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    return;
                }

                _logger.LogInformation("Admin user created successfully");
            }
            else
            {
                // User exists - update it
                _logger.LogInformation("Admin user exists in tenant {TenantId}. Updating password and ensuring correct configuration.", user.TenantId);
                userTenantId = user.TenantId;

                // Reset password
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await userManager.ResetPasswordAsync(user, token, adminPassword);
                if (!resetResult.Succeeded)
                {
                    _logger.LogError("Failed to reset admin user password: {Errors}", 
                        string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                    return;
                }

                // Ensure account is enabled and active
                user.EmailConfirmed = true;
                user.IsActive = true;
                user.UpdatedAt = DateTime.UtcNow;

                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogError("Failed to update admin user: {Errors}", 
                        string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                    return;
                }

                _logger.LogInformation("Admin user updated successfully");
            }

            // Ensure "Service Administrator" role exists in the user's tenant
            var serviceAdminRoleName = "Service Administrator";
            var serviceAdminRole = await context.Roles
                .FirstOrDefaultAsync(r => r.TenantId == userTenantId && r.Name == serviceAdminRoleName, cancellationToken);
            
            if (serviceAdminRole == null)
            {
                _logger.LogInformation("Creating Service Administrator role in tenant {TenantId}", userTenantId);
                serviceAdminRole = new ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    TenantId = userTenantId,
                    Name = serviceAdminRoleName,
                    NormalizedName = roleManager.NormalizeKey(serviceAdminRoleName),
                    Description = "System-level administrator with access to all tenants and system configuration",
                    CreatedAt = DateTime.UtcNow
                };

                var roleResult = await roleManager.CreateAsync(serviceAdminRole);
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Failed to create Service Administrator role: {Errors}", 
                        string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    return;
                }

                _logger.LogInformation("Service Administrator role created successfully");
            }

            // Ensure user has "Service Administrator" role
            var userRoles = await userManager.GetRolesAsync(user);
            if (!userRoles.Contains(serviceAdminRoleName))
            {
                _logger.LogInformation("Assigning Service Administrator role to admin user");
                var addRoleResult = await userManager.AddToRoleAsync(user, serviceAdminRoleName);
                if (!addRoleResult.Succeeded)
                {
                    _logger.LogError("Failed to assign Service Administrator role: {Errors}", 
                        string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
                    return;
                }

                _logger.LogInformation("Service Administrator role assigned successfully");
            }
            else
            {
                _logger.LogInformation("Admin user already has Service Administrator role");
            }

            _logger.LogInformation("Admin user initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during admin user initialization");
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets or creates a system tenant for service administrators
    /// </summary>
    private async Task<Guid?> GetOrCreateSystemTenantAsync(ITenantService tenantService, CancellationToken cancellationToken)
    {
        // Check if ADMIN_TENANT_ID is specified
        var adminTenantIdStr = _configuration["ADMIN_TENANT_ID"];
        if (!string.IsNullOrWhiteSpace(adminTenantIdStr) && Guid.TryParse(adminTenantIdStr, out var adminTenantId))
        {
            var tenant = await tenantService.GetTenantByIdAsync(adminTenantId, cancellationToken);
            if (tenant != null)
            {
                _logger.LogInformation("Using specified tenant for admin user: {TenantId}", adminTenantId);
                return adminTenantId;
            }
            else
            {
                _logger.LogWarning("Specified ADMIN_TENANT_ID not found: {TenantId}. Creating system tenant instead.", adminTenantId);
            }
        }

        // Try to find an existing system tenant
        var allTenants = await tenantService.GetAllTenantsAsync(cancellationToken);
        var systemTenant = allTenants.FirstOrDefault(t => t.Name == "System" || t.Name == "System Tenant");

        if (systemTenant != null)
        {
            _logger.LogInformation("Using existing system tenant: {TenantId}", systemTenant.Id);
            return systemTenant.Id;
        }

        // Create system tenant if it doesn't exist
        _logger.LogInformation("Creating system tenant for service administrators");
        var createRequest = new CreateTenantRequest
        {
            Name = "System",
            Description = "System tenant for service administrators and system-level operations"
        };

        var newTenant = await tenantService.CreateTenantAsync(createRequest, cancellationToken);
        if (newTenant == null)
        {
            _logger.LogError("Failed to create system tenant");
            return null;
        }

        _logger.LogInformation("System tenant created: {TenantId}", newTenant.Id);
        return newTenant.Id;
    }
}
