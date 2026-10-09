namespace GameGuild.Identity.Authorization;

/// <summary>
///     Database-backed implementation of tenant security version store.
///     Uses the ITenantSecurityVersionRepository for persistence.
/// </summary>
public sealed class DatabaseTenantSecurityVersionStore(ITenantSecurityVersionRepository repository) : ITenantSecurityVersionStore
{
    public Task<(long TenantVersion, long GlobalVersion)> GetTenantAndGlobalVersionsAsync(Guid tenantId) =>
        GetTenantAndGlobalVersionsAsync(tenantId, CancellationToken.None);

    /// <inheritdoc />
    public async Task<(long TenantVersion, long GlobalVersion)> GetTenantAndGlobalVersionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var scopes = tenantId == Guid.Empty ? new[] { Guid.Empty } : new[] { tenantId, Guid.Empty };
        var versions = await repository.GetVersionsAsync(scopes, cancellationToken).ConfigureAwait(false);
        return (versions.GetValueOrDefault(tenantId), versions.GetValueOrDefault(Guid.Empty));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, (long TenantVersion, long GlobalVersion)>> GetTenantAndGlobalVersionsAsync(
        IReadOnlyCollection<Guid> tenantIds) =>
        GetTenantAndGlobalVersionsAsync(tenantIds, CancellationToken.None);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, (long TenantVersion, long GlobalVersion)>> GetTenantAndGlobalVersionsAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);

        var requestedTenantIds = tenantIds.Distinct().ToArray();
        if (requestedTenantIds.Length == 0)
        {
            return new Dictionary<Guid, (long TenantVersion, long GlobalVersion)>();
        }

        var scopeIds = requestedTenantIds.Append(Guid.Empty).Distinct().ToArray();
        var versions = await repository.GetVersionsAsync(scopeIds, cancellationToken).ConfigureAwait(false);
        var globalVersion = versions.GetValueOrDefault(Guid.Empty);
        var result = new Dictionary<Guid, (long TenantVersion, long GlobalVersion)>(requestedTenantIds.Length);

        foreach (var tenantId in requestedTenantIds)
        {
            var tenantVersion = versions.GetValueOrDefault(tenantId);
            result[tenantId] = tenantId == Guid.Empty
                ? (tenantVersion, tenantVersion)
                : (tenantVersion, globalVersion);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<long> GetVersionAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(tenantId, out var tenantGuid))
        {
            return 0;
        }

        var version = await repository.GetByTenantIdAsync(tenantGuid, cancellationToken).ConfigureAwait(false);
        return version?.SecurityVersion ?? 0;
    }

    /// <inheritdoc />
    public async Task<long> IncrementVersionAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(tenantId, out var tenantGuid))
        {
            return 0;
        }

        return await repository.IncrementVersionAsync(tenantGuid, reason: null, cancellationToken).ConfigureAwait(false);
    }
}
