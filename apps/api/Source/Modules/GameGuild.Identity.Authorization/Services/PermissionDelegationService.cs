using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Service for managing permission delegations
/// </summary>
public class PermissionDelegationService(
    IPermissionDelegationRepository repository,
    ILogger<PermissionDelegationService> logger
) : IPermissionDelegationService
{
    private readonly ILogger<PermissionDelegationService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IPermissionDelegationRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<PermissionDelegation> DelegatePermissionsAsync(
        Guid delegatorUserId,
        Guid delegateUserId,
        string[] permissions,
        Guid? tenantId,
        Guid? resourceId = null,
        DateTime? expiresAt = null,
        bool canSubDelegate = false,
        string? reason = null,
        int? usageLimit = null,
        CancellationToken cancellationToken = default
    )
    {
        var delegation = new PermissionDelegation
        {
            DelegatorUserId = delegatorUserId,
            DelegateUserId = delegateUserId,
            DelegatedPermissions = permissions,
            TenantId = tenantId,
            ResourceId = resourceId,
            ExpiresAt = expiresAt,
            CanSubDelegate = canSubDelegate,
            Reason = reason,
            UsageLimit = usageLimit,
            IsActive = true
        };

        return await _repository.CreateAsync(delegation, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RevokeDelegationAsync(
        Guid delegationId,
        CancellationToken cancellationToken = default
    )
    {
        var delegation = await _repository.GetByIdAsync(delegationId, cancellationToken).ConfigureAwait(false);

        if (delegation == null)
        {
            return false;
        }

        delegation.Deactivate();
        await _repository.UpdateAsync(delegation, cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<PermissionDelegation?> GetDelegationByIdAsync(
        Guid delegationId,
        CancellationToken cancellationToken = default
    ) => await _repository.GetByIdAsync(delegationId, cancellationToken);

    public async Task<List<PermissionDelegation>> GetActiveDelegationsAsync(
        Guid delegateUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => await _repository.GetActiveByDelegateAsync(delegateUserId, tenantId, cancellationToken);

    public async Task<List<PermissionDelegation>> GetDelegationsByDelegatorAsync(
        Guid delegatorUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => await _repository.GetByDelegatorAsync(delegatorUserId, tenantId, cancellationToken);

    public async Task<bool> CheckDelegatedPermissionAsync(
        Guid delegateUserId,
        string permission,
        Guid? tenantId,
        Guid? resourceId = null,
        CancellationToken cancellationToken = default
    )
    {
        var delegations = await _repository.GetActiveByDelegateAsync(delegateUserId, tenantId, cancellationToken).ConfigureAwait(false);

        return delegations.Any(d =>
            d.DelegatedPermissions.Contains(permission) &&
            d.IsValidNow() &&
            (d.ResourceId == null || d.ResourceId == resourceId)
        );
    }

    public async Task<bool> RecordDelegationUsageAsync(
        Guid delegationId,
        CancellationToken cancellationToken = default
    )
    {
        var delegation = await _repository.GetByIdAsync(delegationId, cancellationToken).ConfigureAwait(false);

        if (delegation == null)
        {
            return false;
        }

        delegation.RecordUsage();
        await _repository.UpdateAsync(delegation, cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<int> CleanupExpiredDelegationsAsync(CancellationToken cancellationToken = default)
    {
        var expiredDelegations = await _repository.GetExpiredDelegationsAsync(cancellationToken).ConfigureAwait(false);

        foreach (var delegation in expiredDelegations)
        {
            delegation.Deactivate();
            await _repository.UpdateAsync(delegation, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Cleaned up {Count} expired delegations", expiredDelegations.Count);

        return expiredDelegations.Count;
    }
}
