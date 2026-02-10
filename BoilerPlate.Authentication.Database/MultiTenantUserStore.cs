using BoilerPlate.Authentication.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoilerPlate.Authentication.Database;

/// <summary>
///     User store that scopes role lookups by the user's tenant so that Identity operations
///     (e.g. IsInRoleAsync, AddToRoleAsync, RemoveFromRoleAsync) resolve roles per tenant
///     instead of globally by normalized name, avoiding "Sequence contains more than one element"
///     when multiple tenants have roles with the same name.
/// </summary>
public class MultiTenantUserStore : UserStore<
    ApplicationUser,
    ApplicationRole,
    BaseAuthDbContext,
    Guid,
    IdentityUserClaim<Guid>,
    IdentityUserRole<Guid>,
    IdentityUserLogin<Guid>,
    IdentityUserToken<Guid>,
    IdentityRoleClaim<Guid>>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MultiTenantUserStore" /> class.
    /// </summary>
    public MultiTenantUserStore(BaseAuthDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Overridden to resolve the role by normalized name within the user's tenant,
    ///     so that multiple tenants can have roles with the same name without causing
    ///     "Sequence contains more than one element" from the base implementation.
    /// </remarks>
    public override async Task<bool> IsInRoleAsync(ApplicationUser user, string normalizedRoleName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRoleName);

        var role = await FindRoleByTenantAsync(user.TenantId, normalizedRoleName, cancellationToken);
        if (role == null)
            return false;

        var userRole = await FindUserRoleAsync(user.Id, role.Id, cancellationToken);
        return userRole != null;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Overridden to resolve the role by normalized name within the user's tenant.
    /// </remarks>
    public override async Task AddToRoleAsync(ApplicationUser user, string normalizedRoleName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRoleName);

        var role = await FindRoleByTenantAsync(user.TenantId, normalizedRoleName, cancellationToken);
        if (role == null)
            throw new InvalidOperationException($"Role '{normalizedRoleName}' was not found in the user's tenant.");

        Context.Set<IdentityUserRole<Guid>>().Add(CreateUserRole(user, role));
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Overridden to resolve the role by normalized name within the user's tenant.
    /// </remarks>
    public override async Task RemoveFromRoleAsync(ApplicationUser user, string normalizedRoleName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRoleName);

        var role = await FindRoleByTenantAsync(user.TenantId, normalizedRoleName, cancellationToken);
        if (role == null)
            return;

        var userRole = await FindUserRoleAsync(user.Id, role.Id, cancellationToken);
        if (userRole != null)
            Context.Set<IdentityUserRole<Guid>>().Remove(userRole);
    }

    /// <summary>
    ///     Finds a role by normalized name within a specific tenant.
    /// </summary>
    private Task<ApplicationRole?> FindRoleByTenantAsync(Guid tenantId, string normalizedRoleName,
        CancellationToken cancellationToken)
    {
        return Context.Set<ApplicationRole>()
            .SingleOrDefaultAsync(
                r => r.TenantId == tenantId && r.NormalizedName == normalizedRoleName,
                cancellationToken);
    }
}
