using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Repository implementation for SCIM provisioning-token credential data access.
/// </summary>
public class ScimProvisioningTokenRepository(IApplicationDbContext context) : IScimProvisioningTokenRepository
{
    private DbSet<ScimProvisioningToken> Tokens => context.Set<ScimProvisioningToken>();

    public async Task<ScimProvisioningToken?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return await Tokens
            .FirstOrDefaultAsync(token => token.KeyHash == keyHash, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScimProvisioningToken?> GetByIdAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        return await Tokens
            .FirstOrDefaultAsync(token => token.Id == tokenId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScimProvisioningToken?> GetByIdForTenantAsync(Guid tokenId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Tokens
            .FirstOrDefaultAsync(token => token.Id == tokenId && token.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<ScimProvisioningToken>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Tokens
            .AsNoTracking()
            .Where(token => token.TenantId == tenantId)
            .OrderByDescending(token => token.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScimProvisioningToken> AddAsync(ScimProvisioningToken token, CancellationToken cancellationToken = default)
    {
        Tokens.Add(token);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return token;
    }

    public async Task<ScimProvisioningToken?> RevokeAsync(Guid tokenId, Guid tenantId, string reason, CancellationToken cancellationToken = default)
    {
        var token = await Tokens
            .FirstOrDefaultAsync(candidate => candidate.Id == tokenId && candidate.TenantId == tenantId, cancellationToken).ConfigureAwait(false);
        if (token == null)
        {
            return null;
        }

        token.Revoke(reason);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return token;
    }

    public async Task RecordUsageAsync(ScimProvisioningToken token, CancellationToken cancellationToken = default)
    {
        token.RecordUsage();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RotateAsync(
        ScimProvisioningToken oldToken,
        ScimProvisioningToken newToken,
        DateTime? graceEndsAt,
        CancellationToken cancellationToken = default)
    {
        newToken.ReplacesTokenId = oldToken.Id;
        if (graceEndsAt.HasValue)
        {
            oldToken.BeginRotationGrace(graceEndsAt.Value);
        }
        else
        {
            oldToken.Revoke($"Rotated: superseded by {newToken.Id}");
        }

        Tokens.Add(newToken);
        // Single save: new-token issuance and old-token transition are atomic.
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task FinalizeRotationRevocationAsync(ScimProvisioningToken token, CancellationToken cancellationToken = default)
    {
        if (token.FinalizeRotationRevocation())
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
