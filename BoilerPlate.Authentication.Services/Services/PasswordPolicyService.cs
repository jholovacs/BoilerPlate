using System.Text.RegularExpressions;
using BoilerPlate.Authentication.Abstractions.Models;
using BoilerPlate.Authentication.Abstractions.Services;
using BoilerPlate.Authentication.Database;
using BoilerPlate.Authentication.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoilerPlate.Authentication.Services.Services;

/// <summary>
///     Service implementation for password policy management and validation
/// </summary>
public class PasswordPolicyService : IPasswordPolicyService
{
    private const string PasswordPolicyMinimumLengthKey = "PasswordPolicy.MinimumLength";
    private const string PasswordPolicyRequireDigitKey = "PasswordPolicy.RequireDigit";
    private const string PasswordPolicyRequireLowercaseKey = "PasswordPolicy.RequireLowercase";
    private const string PasswordPolicyRequireUppercaseKey = "PasswordPolicy.RequireUppercase";
    private const string PasswordPolicyRequireNonAlphanumericKey = "PasswordPolicy.RequireNonAlphanumeric";
    private const string PasswordPolicyMaximumLifetimeDaysKey = "PasswordPolicy.MaximumLifetimeDays";
    private const string PasswordPolicyEnableHistoryKey = "PasswordPolicy.EnableHistory";
    private const string PasswordPolicyHistoryCountKey = "PasswordPolicy.HistoryCount";

    private readonly BaseAuthDbContext _context;
    private readonly ILogger<PasswordPolicyService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PasswordPolicyService" /> class
    /// </summary>
    public PasswordPolicyService(BaseAuthDbContext context, ILogger<PasswordPolicyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PasswordPolicyConfiguration> GetPasswordPolicyAsync(Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var config = new PasswordPolicyConfiguration();

        try
        {
            // Get all password policy settings for the tenant
            var settings = await _context.TenantSettings
                .Where(ts => ts.TenantId == tenantId && ts.Key.StartsWith("PasswordPolicy."))
                .ToListAsync(cancellationToken);

            var settingsDict = settings.ToDictionary(s => s.Key, s => s.Value);

            // Minimum length
            if (settingsDict.TryGetValue(PasswordPolicyMinimumLengthKey, out var minLengthValue) &&
                !string.IsNullOrWhiteSpace(minLengthValue) &&
                int.TryParse(minLengthValue, out var minLength) && minLength > 0)
            {
                config.MinimumLength = minLength;
            }

            // Require digit
            if (settingsDict.TryGetValue(PasswordPolicyRequireDigitKey, out var requireDigitValue) &&
                !string.IsNullOrWhiteSpace(requireDigitValue) &&
                bool.TryParse(requireDigitValue, out var requireDigit))
            {
                config.RequireDigit = requireDigit;
            }

            // Require lowercase
            if (settingsDict.TryGetValue(PasswordPolicyRequireLowercaseKey, out var requireLowercaseValue) &&
                !string.IsNullOrWhiteSpace(requireLowercaseValue) &&
                bool.TryParse(requireLowercaseValue, out var requireLowercase))
            {
                config.RequireLowercase = requireLowercase;
            }

            // Require uppercase
            if (settingsDict.TryGetValue(PasswordPolicyRequireUppercaseKey, out var requireUppercaseValue) &&
                !string.IsNullOrWhiteSpace(requireUppercaseValue) &&
                bool.TryParse(requireUppercaseValue, out var requireUppercase))
            {
                config.RequireUppercase = requireUppercase;
            }

            // Require non-alphanumeric
            if (settingsDict.TryGetValue(PasswordPolicyRequireNonAlphanumericKey, out var requireNonAlphaValue) &&
                !string.IsNullOrWhiteSpace(requireNonAlphaValue) &&
                bool.TryParse(requireNonAlphaValue, out var requireNonAlpha))
            {
                config.RequireNonAlphanumeric = requireNonAlpha;
            }

            // Maximum lifetime
            if (settingsDict.TryGetValue(PasswordPolicyMaximumLifetimeDaysKey, out var maxLifetimeValue) &&
                !string.IsNullOrWhiteSpace(maxLifetimeValue) &&
                int.TryParse(maxLifetimeValue, out var maxLifetime) && maxLifetime >= 0)
            {
                config.MaximumLifetimeDays = maxLifetime;
            }

            // Enable history
            if (settingsDict.TryGetValue(PasswordPolicyEnableHistoryKey, out var enableHistoryValue) &&
                !string.IsNullOrWhiteSpace(enableHistoryValue) &&
                bool.TryParse(enableHistoryValue, out var enableHistory))
            {
                config.EnablePasswordHistory = enableHistory;
            }

            // History count
            if (settingsDict.TryGetValue(PasswordPolicyHistoryCountKey, out var historyCountValue) &&
                !string.IsNullOrWhiteSpace(historyCountValue) &&
                int.TryParse(historyCountValue, out var historyCount) && historyCount > 0)
            {
                config.PasswordHistoryCount = historyCount;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve password policy settings for tenant {TenantId}, using defaults",
                tenantId);
        }

        return config;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> ValidatePasswordComplexityAsync(string password, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var policy = await GetPasswordPolicyAsync(tenantId, cancellationToken);

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("Password is required.");
            return errors;
        }

        // Check minimum length
        if (password.Length < policy.MinimumLength)
            errors.Add($"Password must be at least {policy.MinimumLength} characters long.");

        // Check for digit
        if (policy.RequireDigit && !Regex.IsMatch(password, @"\d"))
            errors.Add("Password must contain at least one digit (0-9).");

        // Check for lowercase
        if (policy.RequireLowercase && !Regex.IsMatch(password, @"[a-z]"))
            errors.Add("Password must contain at least one lowercase letter (a-z).");

        // Check for uppercase
        if (policy.RequireUppercase && !Regex.IsMatch(password, @"[A-Z]"))
            errors.Add("Password must contain at least one uppercase letter (A-Z).");

        // Check for non-alphanumeric
        if (policy.RequireNonAlphanumeric && !Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
            errors.Add("Password must contain at least one special character.");

        return errors;
    }

    /// <inheritdoc />
    public async Task<bool> IsPasswordExpiredAsync(Guid userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var policy = await GetPasswordPolicyAsync(tenantId, cancellationToken);

        // If maximum lifetime is 0, passwords never expire
        if (policy.MaximumLifetimeDays == 0) return false;

        // Note: ASP.NET Core Identity doesn't track password set date directly.
        // We use password history to determine when the current password was set.
        // If password history is enabled and the user has changed their password,
        // the most recent history entry's ChangedAt date represents when the current password was set.
        // If no history exists, we can't determine expiration, so we return false (not expired).

        // Get the most recent password history entry (which represents when current password was set)
        var mostRecentHistory = await _context.UserPasswordHistories
            .Where(h => h.UserId == userId && h.TenantId == tenantId)
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (mostRecentHistory == null)
        {
            // No password history - can't determine expiration
            // This happens for:
            // 1. New users who haven't changed their password yet
            // 2. Users who haven't changed password since password history was enabled
            // In these cases, we can't determine expiration, so we assume password is not expired
            // TODO: For complete password expiration tracking, add PasswordSetAt field to ApplicationUser
            return false;
        }

        // Check if password has expired
        // The ChangedAt date of the most recent history entry is when the current password was set
        var passwordSetDate = mostRecentHistory.ChangedAt;
        var expirationDate = passwordSetDate.AddDays(policy.MaximumLifetimeDays);
        return DateTime.UtcNow > expirationDate;
    }

    /// <inheritdoc />
    public async Task<bool> IsPasswordInHistoryAsync(Guid userId, string passwordHash, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var policy = await GetPasswordPolicyAsync(tenantId, cancellationToken);

        // If password history is not enabled, always return false
        if (!policy.EnablePasswordHistory) return false;

        // Check if the password hash exists in history
        var existsInHistory = await _context.UserPasswordHistories
            .AnyAsync(h => h.UserId == userId && h.TenantId == tenantId && h.PasswordHash == passwordHash,
                cancellationToken);

        return existsInHistory;
    }

    /// <inheritdoc />
    public async Task SavePasswordToHistoryAsync(Guid userId, string oldPasswordHash, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var policy = await GetPasswordPolicyAsync(tenantId, cancellationToken);

        // If password history is not enabled, don't save
        if (!policy.EnablePasswordHistory) return;

        // Save current password to history
        var passwordHistory = new UserPasswordHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            PasswordHash = oldPasswordHash,
            SetAt = DateTime.UtcNow,
            ChangedAt = DateTime.UtcNow
        };

        _context.UserPasswordHistories.Add(passwordHistory);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Saved password to history for user {UserId} in tenant {TenantId}", userId, tenantId);

        // Cleanup old password history entries
        await CleanupPasswordHistoryAsync(userId, tenantId, policy.PasswordHistoryCount, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CleanupPasswordHistoryAsync(Guid userId, Guid tenantId, int keepCount,
        CancellationToken cancellationToken = default)
    {
        // Get all password history entries for the user, ordered by most recent first
        var allHistory = await _context.UserPasswordHistories
            .Where(h => h.UserId == userId && h.TenantId == tenantId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync(cancellationToken);

        // If we have more than keepCount entries, delete the oldest ones
        if (allHistory.Count > keepCount)
        {
            var toDelete = allHistory.Skip(keepCount).ToList();
            _context.UserPasswordHistories.RemoveRange(toDelete);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Cleaned up {Count} old password history entries for user {UserId} in tenant {TenantId}, keeping {KeepCount} most recent",
                toDelete.Count, userId, tenantId, keepCount);
        }
    }
}
