using GameGuild.CQRS.Models;
using Microsoft.Extensions.Logging;

namespace GameGuild.Resources;

/// <summary>
///     Implementation of usage data retention and archival
/// </summary>
public class UsageRetentionService(IUsageRetentionPolicyRepository policyRepository, IUsageRecordRepository usageRepository, ILogger<UsageRetentionService> logger) : IUsageRetentionService
{
    public async Task<UsageRetentionPolicy> SetPolicyAsync(
        Guid? tenantId,
        ResourceUsageType? resourceType,
        int retentionDays,
        int archiveAfterDays,
        bool enableCompaction = true,
        CancellationToken cancellationToken = default
    )
    {
        var existingPolicy = await policyRepository.GetByTenantAndTypeAsync(tenantId, resourceType, cancellationToken).ConfigureAwait(false);

        if (existingPolicy != null)
        {
            existingPolicy.RetentionDays = retentionDays;
            existingPolicy.ArchiveAfterDays = archiveAfterDays;
            existingPolicy.EnableCompaction = enableCompaction;
            existingPolicy.Touch();
            existingPolicy = await policyRepository.UpdateAsync(existingPolicy, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existingPolicy = new UsageRetentionPolicy
            {
                Name = $"Retention Policy - {(tenantId.HasValue ? $"Tenant {tenantId}" : "Global")} - {(resourceType.HasValue ? resourceType.ToString() : "All Types")}",
                ResourceType = resourceType,
                RetentionDays = retentionDays,
                ArchiveAfterDays = archiveAfterDays,
                EnableCompaction = enableCompaction,
                IsActive = true,
                NextExecutionAt = SystemClock.UtcNow.AddDays(1)
            };

            // Set TenantId using SetProperties (EntityBase has protected setter)
            if (tenantId.HasValue) { existingPolicy.SetTenantId(tenantId.Value); }

            existingPolicy = await policyRepository.CreateAsync(existingPolicy, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Set retention policy: TenantId={TenantId}, Type={Type}, Retention={Retention}days, Archive={Archive}days", tenantId, resourceType, retentionDays, archiveAfterDays);

        return existingPolicy;
    }

    public async Task<UsageRetentionPolicy?> GetPolicyAsync(Guid? tenantId, ResourceUsageType? resourceType, CancellationToken cancellationToken = default)
    {
        return await policyRepository.GetByTenantAndTypeAsync(tenantId, resourceType, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<UsageRetentionPolicy>> GetActivePoliciesAsync(CancellationToken cancellationToken = default) { return await policyRepository.GetActivePoliciesAsync(cancellationToken).ConfigureAwait(false); }

    public async Task<RetentionExecutionResult> ExecuteRetentionAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        var policy = await policyRepository.GetByIdAsync(policyId, cancellationToken).ConfigureAwait(false);

        if (policy is not { IsActive: true })
        {
            logger.LogWarning("Cannot execute retention for policy {PolicyId} - not found or inactive", policyId);

            return new RetentionExecutionResult();
        }

        logger.LogInformation("Executing retention policy {PolicyId}: {Name}", policyId, policy.Name);

        var result = new RetentionExecutionResult();

        try
        {
            // Archive old records
            var archiveThreshold = policy.GetArchiveThresholdDate();
            result.RecordsArchived = await usageRepository.ArchiveOlderThanAsync(archiveThreshold, cancellationToken).ConfigureAwait(false);

            // Delete very old records
            var deleteThreshold = policy.GetDeletionThresholdDate();
            var deleted = await usageRepository.DeleteOlderThanAsync(deleteThreshold, cancellationToken).ConfigureAwait(false);
            result.RecordsDeleted = deleted ? 1 : 0;

            // Compact records if enabled
            if (policy.EnableCompaction) { result.RecordsCompacted = await CompactUsageRecordsAsync(policy.TenantId ?? Guid.Empty, policy.ResourceType, archiveThreshold, cancellationToken).ConfigureAwait(false); }

            // Update policy execution time
            policy.LastExecutedAt = SystemClock.UtcNow;
            policy.NextExecutionAt = policy.CalculateNextCompaction();
            await policyRepository.UpdateAsync(policy, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Retention policy {PolicyId} executed: Archived={Archived}, Deleted={Deleted}, Compacted={Compacted}", policyId, result.RecordsArchived, result.RecordsDeleted, result.RecordsCompacted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing retention policy {PolicyId}", policyId);

            throw;
        }

        return result;
    }

    public async Task<int> CompactUsageRecordsAsync(Guid tenantId, ResourceUsageType? type = null, DateTime? olderThan = null, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            logger.LogWarning("Cannot compact records without tenant ID");

            return 0;
        }

        var threshold = olderThan ?? SystemClock.UtcNow.AddDays(-30);

        // If type is not specified, get all types for the tenant
        IEnumerable<UsageRecord> records;

        if (type.HasValue) { records = await usageRepository.GetByTenantAsync(tenantId, type.Value, null, threshold, cancellationToken).ConfigureAwait(false); }
        else
        {
            records = await usageRepository.GetByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
            records = records.Where(r => r.PeriodStart <= threshold);
        }

        var recordsList = records.ToList();

        if (recordsList.Count == 0)
        {
            return 0;
        }

        // Group by month and create aggregated records
        var monthlyGroups = recordsList.GroupBy(r => new { r.Type, r.PeriodStart.Year, r.PeriodStart.Month }).ToList();

        var compactedCount = 0;

        foreach (var group in monthlyGroups)
        {
            var periodStart = new DateTime(group.Key.Year, group.Key.Month, 1);
            var totalUsage = group.Sum(r => r.UsageAmount);

            // Create monthly aggregated record
            var monthlyRecord = UsageRecord.CreateMonthly(group.Key.Type, tenantId, totalUsage, periodStart);

            await usageRepository.AddAsync(monthlyRecord, cancellationToken).ConfigureAwait(false);
            compactedCount++;
        }

        // Delete the original detailed records
        await usageRepository.DeleteOlderThanAsync(threshold, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Compacted {Count} usage records into {MonthlyCount} monthly records for tenant {TenantId}", recordsList.Count, compactedCount, tenantId);

        return compactedCount;
    }

    public async Task<int> ArchiveUsageRecordsAsync(Guid tenantId, ResourceUsageType? type = null, DateTime? olderThan = null, CancellationToken cancellationToken = default)
    {
        var threshold = olderThan ?? SystemClock.UtcNow.AddDays(-90);

        return await usageRepository.ArchiveOlderThanAsync(threshold, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteArchivedRecordsAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        var deleted = await usageRepository.DeleteOlderThanAsync(olderThan, cancellationToken).ConfigureAwait(false);

        return deleted ? 1 : 0;
    }

    public async Task<RetentionStats> GetRetentionStatsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var totalRecords = await usageRepository.GetTotalRecordCountAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var archivedRecords = await usageRepository.GetArchivedRecordCountAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var oldestDate = await usageRepository.GetOldestRecordDateAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var totalStorageBytes = await usageRepository.GetEstimatedStorageBytesAsync(tenantId, archivedOnly: false, cancellationToken).ConfigureAwait(false);
        var archivedStorageBytes = await usageRepository.GetEstimatedStorageBytesAsync(tenantId, archivedOnly: true, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Retrieved retention stats: TenantId={TenantId}, Total={Total}, Archived={Archived}, OldestDate={OldestDate}",
            tenantId, totalRecords, archivedRecords, oldestDate);

        return new RetentionStats
        {
            TotalRecords = totalRecords,
            ArchivedRecords = archivedRecords,
            ActiveRecords = totalRecords - archivedRecords,
            TotalStorageBytes = totalStorageBytes,
            ArchivedStorageBytes = archivedStorageBytes,
            OldestRecordDate = oldestDate
        };
    }
}
