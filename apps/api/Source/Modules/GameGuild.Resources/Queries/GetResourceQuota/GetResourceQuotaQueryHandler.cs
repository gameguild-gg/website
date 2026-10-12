using GameGuild.CQRS;

namespace GameGuild.Resources;

/// <summary>
///     Handles getting resource quota information
/// </summary>
public sealed class GetResourceQuotaQueryHandler(IResourceQuotaRepository resourceQuotaRepository) : IQueryHandler<GetResourceQuotaQuery, ResourceQuotaResponse?>
{
    public async Task<ResourceQuotaResponse?> Handle(GetResourceQuotaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quota = await resourceQuotaRepository.GetByTenantAndTypeAsync(request.TenantId, request.Type, cancellationToken).ConfigureAwait(false);

        if (quota == null)
        {
            return null;
        }

        return new ResourceQuotaResponse
        {
            Id = quota.Id,
            TenantId = quota.TenantId ?? Guid.Empty,
            Type = quota.Type,
            Limit = quota.HardLimit ?? quota.SoftLimit ?? 0,
            CurrentUsage = quota.CurrentUsage,
            RemainingQuota = Math.Max(0, (quota.HardLimit ?? quota.SoftLimit ?? 0) - quota.CurrentUsage),
            UsagePercentage = (decimal) quota.GetUsagePercentage(),
            SoftLimitPercentage = quota.SoftLimit.HasValue && quota.HardLimit.HasValue ? (decimal) quota.SoftLimit.Value / quota.HardLimit.Value * 100 : 0,
            IsActive = quota.IsActive,
            Period = quota.Period,
            LastResetDate = quota.LastReset ?? DateTime.MinValue,
            NextResetDate = quota.GetNextResetTime() ?? DateTime.MaxValue,
            Description = null,
            IsSoftLimitExceeded = quota.IsSoftLimitExceeded(),
            IsHardLimitExceeded = quota.IsHardLimitExceeded(),
            ShouldReset = quota.ShouldReset()
        };
    }
}
