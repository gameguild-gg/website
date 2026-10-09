using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository implementation for API-key credential data access operations.
/// </summary>
public class ApiKeyRepository(IApplicationDbContext context) : IApiKeyRepository
{
    private DbSet<ApiKey> ApiKeys => context.Set<ApiKey>();

    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return await ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ApiKey?> GetByIdForUserAsync(Guid keyId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<ApiKey>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId && k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ApiKey> AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        ApiKeys.Add(apiKey);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return apiKey;
    }

    public async Task<ApiKey?> RevokeAsync(Guid keyId, Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        var apiKey = await ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (apiKey == null) return null;

        apiKey.Revoke(reason);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return apiKey;
    }

    public async Task RecordUsageAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        apiKey.RecordUsage();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RotateAsync(ApiKey oldKey, ApiKey newKey, DateTime? graceEndsAt, CancellationToken cancellationToken = default)
    {
        newKey.ReplacesKeyId = oldKey.Id;
        if (graceEndsAt.HasValue)
        {
            oldKey.BeginRotationGrace(graceEndsAt.Value);
        }
        else
        {
            oldKey.Revoke($"Rotated: superseded by {newKey.Id}");
        }

        ApiKeys.Add(newKey);
        // Single save: new-key issuance and old-key transition are atomic.
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task FinalizeRotationRevocationAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        if (apiKey.FinalizeRotationRevocation())
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
