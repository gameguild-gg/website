using GameGuild.CQRS;

namespace GameGuild.Resources;

/// <summary>
///     Handler for getting a specific user resource quota
/// </summary>
public sealed class GetUserResourceQuotaQueryHandler(IResourceQuotaRepository quotaRepository) : IQueryHandler<GetUserResourceQuotaQuery, ResourceQuotaResponse?>
{
    public async Task<ResourceQuotaResponse?> Handle(GetUserResourceQuotaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var quota = await quotaRepository.GetByUserAndTypeAsync(request.UserId, request.Type, cancellationToken).ConfigureAwait(false);

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
            SoftLimit = quota.SoftLimit,
            HardLimit = quota.HardLimit,
            CurrentUsage = quota.CurrentUsage,
            RemainingQuota = Math.Max(0, (quota.HardLimit ?? quota.SoftLimit ?? 0) - quota.CurrentUsage),
            UsagePercentage = (decimal)quota.GetUsagePercentage(),
            SoftLimitPercentage = quota.SoftLimit.HasValue && quota.HardLimit.HasValue ? (decimal)quota.SoftLimit.Value / quota.HardLimit.Value * 100 : 0,
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
