using System.Diagnostics;
using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Resources;

/// <summary>
///     Handles quota CRUD operations and basic usage reads.
/// </summary>
public class QuotaManagementService(
    IResourceQuotaRepository quotaRepository,
    IUsageRecordRepository usageRepository,
    IPublisher publisher,
    ILogger<QuotaManagementService> logger) : IQuotaManagementService
{
    /// <summary>OpenTelemetry activity source for quota management operations.</summary>
    public static readonly ActivitySource ActivitySource = new("GameGuild.Resources.QuotaManagement", "1.0.0");

    private const string SetQuotaOperation = "quota.set";
    private const string GetQuotaOperation = "quota.get";
    private const string DeleteQuotaOperation = "quota.delete";

    public async Task<ResourceQuota> SetQuotaAsync(
        Guid tenantId,
        ResourceUsageType type,
        long? softLimit,
        long? hardLimit,
        ResourceQuotaPeriod period = ResourceQuotaPeriod.Monthly,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity(SetQuotaOperation, ActivityKind.Internal);
        activity?.SetTag("tenant.id", tenantId.ToString());
        activity?.SetTag("resource.type", type.ToString());
        activity?.SetTag("quota.soft_limit", softLimit?.ToString() ?? "unlimited");
        activity?.SetTag("quota.hard_limit", hardLimit?.ToString() ?? "unlimited");
        activity?.SetTag("quota.period", period.ToString());

        var existingQuota = await quotaRepository.GetByTenantAndTypeAsync(tenantId, type, cancellationToken).ConfigureAwait(false);
        var isNew = existingQuota == null;
        var previousUsage = existingQuota?.CurrentUsage;

        activity?.SetTag("quota.is_new", isNew);

        if (existingQuota != null)
        {
            existingQuota.SoftLimit = softLimit;
            existingQuota.HardLimit = hardLimit;
            existingQuota.Period = period;
            existingQuota.Touch();
            existingQuota = await quotaRepository.UpdateAsync(existingQuota, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existingQuota = new ResourceQuota { Type = type, SoftLimit = softLimit, HardLimit = hardLimit, Period = period, CurrentUsage = 0, LastReset = SystemClock.UtcNow, IsActive = true };
            existingQuota.SetProperties(new Dictionary<string, object?> { ["TenantId"] = tenantId });
            existingQuota = await quotaRepository.CreateAsync(existingQuota, cancellationToken).ConfigureAwait(false);
        }

        await publisher.Publish(new QuotaChangedEvent(
            TenantId: tenantId,
            ResourceType: type,
            ChangeType: isNew ? QuotaChangeType.Created : QuotaChangeType.LimitsUpdated,
            PreviousUsage: previousUsage,
            CurrentUsage: existingQuota.CurrentUsage,
            SoftLimit: softLimit,
            HardLimit: hardLimit,
            Source: "SetQuotaAsync",
            ActorId: null,
            Timestamp: SystemClock.UtcNow), cancellationToken);

        logger.LogInformation("Set quota for tenant {TenantId}, type {Type}: Soft={SoftLimit}, Hard={HardLimit}", tenantId, type, softLimit, hardLimit);

        return existingQuota;
    }

    public async Task<ResourceQuota?> GetQuotaAsync(Guid tenantId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity(GetQuotaOperation, ActivityKind.Internal);
        activity?.SetTag("tenant.id", tenantId.ToString());
        activity?.SetTag("resource.type", type.ToString());

        var quota = await quotaRepository.GetByTenantAndTypeAsync(tenantId, type, cancellationToken).ConfigureAwait(false);
        activity?.SetTag("quota.found", quota != null);
        if (quota != null)
        {
            activity?.SetTag("quota.current_usage", quota.CurrentUsage);
            activity?.SetTag("quota.hard_limit", quota.HardLimit?.ToString() ?? "unlimited");
        }

        return quota;
    }

    public async Task<IEnumerable<ResourceQuota>> GetTenantQuotasAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await quotaRepository.GetByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteQuotaAsync(Guid tenantId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        var quota = await quotaRepository.GetByTenantAndTypeAsync(tenantId, type, cancellationToken).ConfigureAwait(false);

        if (quota == null)
        {
            return false;
        }

        var previousUsage = quota.CurrentUsage;
        var deleted = await quotaRepository.DeleteAsync(quota.Id, cancellationToken).ConfigureAwait(false);

        if (deleted)
        {
            await publisher.Publish(new QuotaChangedEvent(
                TenantId: tenantId,
                ResourceType: type,
                ChangeType: QuotaChangeType.Deleted,
                PreviousUsage: previousUsage,
                CurrentUsage: 0,
                SoftLimit: quota.SoftLimit,
                HardLimit: quota.HardLimit,
                Source: "DeleteQuotaAsync",
                ActorId: null,
                Timestamp: SystemClock.UtcNow), cancellationToken);

            logger.LogInformation("Deleted quota for tenant {TenantId}, type {Type}", tenantId, type);
        }

        return deleted;
    }

    public async Task<long> GetCurrentUsageAsync(Guid tenantId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        var quota = await GetQuotaAsync(tenantId, type, cancellationToken).ConfigureAwait(false);

        if (quota == null)
        {
            return 0;
        }

        return quota.ShouldReset() ? 0 : quota.CurrentUsage;
    }

    public async Task<IEnumerable<UsageRecord>> GetUsageHistoryAsync(Guid tenantId, ResourceUsageType type, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        return await usageRepository.GetByTenantAsync(tenantId, type, fromDate, toDate, cancellationToken).ConfigureAwait(false);
    }
}
