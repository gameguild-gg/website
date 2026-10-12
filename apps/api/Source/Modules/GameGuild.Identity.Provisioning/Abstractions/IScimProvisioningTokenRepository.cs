namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Persistence for <see cref="ScimProvisioningToken"/> credentials.
/// </summary>
public interface IScimProvisioningTokenRepository
{
    Task<ScimProvisioningToken?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);

    Task<ScimProvisioningToken?> GetByIdAsync(Guid tokenId, CancellationToken cancellationToken = default);

    Task<ScimProvisioningToken?> GetByIdForTenantAsync(Guid tokenId, Guid tenantId, CancellationToken cancellationToken = default);

    Task<List<ScimProvisioningToken>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<ScimProvisioningToken> AddAsync(ScimProvisioningToken token, CancellationToken cancellationToken = default);

    Task<ScimProvisioningToken?> RevokeAsync(Guid tokenId, Guid tenantId, string reason, CancellationToken cancellationToken = default);

    Task RecordUsageAsync(ScimProvisioningToken token, CancellationToken cancellationToken = default);

    Task RotateAsync(
        ScimProvisioningToken oldToken,
        ScimProvisioningToken newToken,
        DateTime? graceEndsAt,
        CancellationToken cancellationToken = default);

    Task FinalizeRotationRevocationAsync(ScimProvisioningToken token, CancellationToken cancellationToken = default);
}
