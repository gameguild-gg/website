using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository implementation for user MFA configuration data access operations
/// </summary>
public class UserMfaConfigurationRepository(IApplicationDbContext context) : IUserMfaConfigurationRepository
{
    private DbSet<UserMfaConfiguration> UserMfaConfigurations { get => context.Set<UserMfaConfiguration>(); }

    public async Task<UserMfaConfiguration?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await UserMfaConfigurations.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    public async Task<bool> IsMfaEnabledAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await UserMfaConfigurations.AnyAsync(c => c.UserId == userId && c.IsEnabled && c.IsSetupComplete, cancellationToken);
    }

    public async Task<UserMfaConfiguration> CreateAsync(UserMfaConfiguration configuration, CancellationToken cancellationToken = default)
    {
        configuration.Id = Guid.NewGuid();
        configuration.UpdatedAt = SystemClock.UtcNow;

        UserMfaConfigurations.Add(configuration);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return configuration;
    }

    public async Task<UserMfaConfiguration> UpdateAsync(UserMfaConfiguration configuration, CancellationToken cancellationToken = default)
    {
        configuration.UpdatedAt = SystemClock.UtcNow;

        UserMfaConfigurations.Update(configuration);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Discard stale mutations before a caller retries; never write consumed codes back.
            foreach (var entry in exception.Entries)
            {
                await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
            }
            throw;
        }

        return configuration;
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (configuration != null)
        {
            UserMfaConfigurations.Remove(configuration);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<MfaMethod?> GetPreferredMethodAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        return configuration?.PreferredMethod;
    }

    public async Task IncrementFailedAttemptsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (configuration != null)
        {
            configuration.FailedAttempts++;
            configuration.UpdatedAt = SystemClock.UtcNow;
            await UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ResetFailedAttemptsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (configuration != null)
        {
            configuration.FailedAttempts = 0;
            configuration.LastUsedAt = SystemClock.UtcNow;
            configuration.UpdatedAt = SystemClock.UtcNow;
            await UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SetLockoutAsync(Guid userId, DateTime lockoutUntil, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (configuration != null)
        {
            configuration.LockedOutUntil = lockoutUntil;
            configuration.UpdatedAt = SystemClock.UtcNow;
            await UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<UserMfaConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await UserMfaConfigurations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken); }

    public async Task<IReadOnlyList<UserMfaConfiguration>> GetEnabledConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        return await UserMfaConfigurations.Where(c => c.IsEnabled && c.IsSetupComplete).ToListAsync(cancellationToken);
    }

    public async Task<bool> EnableMfaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (configuration == null)
        {
            return false;
        }

        configuration.IsEnabled = true;
        configuration.EnabledAt = SystemClock.UtcNow;
        configuration.UpdatedAt = SystemClock.UtcNow;

        await UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> DisableMfaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (configuration == null)
        {
            return false;
        }

        configuration.IsEnabled = false;
        configuration.UpdatedAt = SystemClock.UtcNow;

        await UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<int> UpdateFailedAttemptsAsync(Guid userId, bool increment, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (configuration == null)
        {
            return 0;
        }

        if (increment) { configuration.FailedAttempts++; }
        else
        {
            configuration.FailedAttempts = 0;
            configuration.LastUsedAt = SystemClock.UtcNow;
        }

        configuration.UpdatedAt = SystemClock.UtcNow;
        await UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);

        return configuration.FailedAttempts;
    }

    public async Task<bool> LockoutUserAsync(Guid userId, DateTime lockoutUntil, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (configuration == null)
        {
            return false;
        }

        configuration.LockedOutUntil = lockoutUntil;
        configuration.UpdatedAt = SystemClock.UtcNow;

        await UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> ClearLockoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (configuration == null)
        {
            return false;
        }

        configuration.LockedOutUntil = null;
        configuration.FailedAttempts = 0;
        configuration.UpdatedAt = SystemClock.UtcNow;

        await UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (configuration == null)
        {
            return false;
        }

        UserMfaConfigurations.Remove(configuration);
        var changes = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return changes > 0;
    }
}
