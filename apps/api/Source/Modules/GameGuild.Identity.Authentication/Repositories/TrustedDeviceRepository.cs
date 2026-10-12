using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository implementation for trusted device data access operations
/// </summary>
public class TrustedDeviceRepository(IApplicationDbContext context) : ITrustedDeviceRepository
{
    private DbSet<TrustedDevice> TrustedDevices { get => context.Set<TrustedDevice>(); }

    public async Task<TrustedDevice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) { return await TrustedDevices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken); }

    public async Task<TrustedDevice?> GetByUserAndFingerprintAsync(Guid userId, string deviceFingerprint, CancellationToken cancellationToken = default)
    {
        return await TrustedDevices.FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceFingerprint == deviceFingerprint, cancellationToken);
    }

    public async Task<List<TrustedDevice>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await TrustedDevices.Where(d => d.UserId == userId).OrderByDescending(d => d.LastUsedAt).ToListAsync(cancellationToken);
    }

    public async Task<List<TrustedDevice>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;

        return await TrustedDevices.Where(d => d.UserId == userId && d.IsActive && (!d.ExpiresAt.HasValue || d.ExpiresAt.Value > now)).OrderByDescending(d => d.LastUsedAt).ToListAsync(cancellationToken);
    }

    public async Task<TrustedDevice> CreateAsync(TrustedDevice device, CancellationToken cancellationToken = default)
    {
        device.Id = Guid.NewGuid();
        device.UpdatedAt = SystemClock.UtcNow;
        device.TrustedAt = SystemClock.UtcNow;
        device.LastUsedAt = SystemClock.UtcNow;

        TrustedDevices.Add(device);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return device;
    }

    public async Task<TrustedDevice> UpdateAsync(TrustedDevice device, CancellationToken cancellationToken = default)
    {
        device.UpdatedAt = SystemClock.UtcNow;

        TrustedDevices.Update(device);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return device;
    }

    public async Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await GetByIdAsync(deviceId, cancellationToken).ConfigureAwait(false);

        if (device == null)
        {
            return;
        }

        device.IsActive = false;
        device.UpdatedAt = SystemClock.UtcNow;

        await UpdateAsync(device, cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userDevices = await TrustedDevices.Where(d => d.UserId == userId && d.IsActive).ToListAsync(cancellationToken);

        if (userDevices.Count == 0)
        {
            return;
        }

        var now = SystemClock.UtcNow;

        foreach (var device in userDevices)
        {
            device.IsActive = false;
            device.UpdatedAt = now;
        }

        TrustedDevices.UpdateRange(userDevices);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var expiredDevices = await TrustedDevices.Where(d => d.ExpiresAt.HasValue && d.ExpiresAt.Value < now || !d.IsActive && d.UpdatedAt < now.AddDays(-90)).ToListAsync(cancellationToken);

        if (expiredDevices.Count == 0)
        {
            return;
        }

        TrustedDevices.RemoveRange(expiredDevices);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // Helper methods for backward compatibility and service layer
    public async Task<bool> IsDeviceTrustedAsync(Guid userId, string deviceFingerprint, CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;

        return await TrustedDevices.AnyAsync(d => d.UserId == userId && d.DeviceFingerprint == deviceFingerprint && d.IsActive && (!d.ExpiresAt.HasValue || d.ExpiresAt.Value > now), cancellationToken);
    }

    public async Task<bool> UpdateLastUsedAsync(Guid userId, string deviceFingerprint, CancellationToken cancellationToken = default)
    {
        var device = await GetByUserAndFingerprintAsync(userId, deviceFingerprint, cancellationToken).ConfigureAwait(false);

        if (device == null)
        {
            return false;
        }

        var now = SystemClock.UtcNow;

        if (!device.IsActive || device.ExpiresAt.HasValue && device.ExpiresAt.Value < now) { return false; }

        device.LastUsedAt = now;
        device.UpdatedAt = now;

        await UpdateAsync(device, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
