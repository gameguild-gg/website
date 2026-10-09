using Microsoft.Extensions.Logging;

namespace GameGuild.Resources;

/// <summary>
///     Implementation of resource throttling policy management
/// </summary>
public class ResourceThrottlingService(IResourceThrottlingPolicyRepository policyRepository, IResourceQuotaRepository quotaRepository, ILogger<ResourceThrottlingService> logger) : IResourceThrottlingService
{
    public async Task<ResourceThrottlingPolicy> SetPolicyAsync(
        Guid tenantId,
        ResourceUsageType type,
        ThrottlingStrategy strategy,
        long threshold,
        string? configuration = null,
        CancellationToken cancellationToken = default
    )
    {
        var existingPolicy = await policyRepository.GetByTenantAndTypeAsync(tenantId, type, cancellationToken).ConfigureAwait(false);

        if (existingPolicy != null)
        {
            existingPolicy.Strategy = strategy;
            existingPolicy.ThrottlingThresholdPercent = (int) threshold;
            existingPolicy.Configuration = configuration;
            existingPolicy.Touch();
            existingPolicy = await policyRepository.UpdateAsync(existingPolicy, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existingPolicy = new ResourceThrottlingPolicy { ResourceType = type, Strategy = strategy, ThrottlingThresholdPercent = (int) threshold, Configuration = configuration, IsActive = true };
            existingPolicy.SetProperties(new Dictionary<string, object?> { ["TenantId"] = tenantId });
            existingPolicy = await policyRepository.CreateAsync(existingPolicy, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Set throttling policy for tenant {TenantId}, type {Type}: Strategy={Strategy}, Threshold={Threshold}", tenantId, type, strategy, threshold);

        return existingPolicy;
    }

    public async Task<ResourceThrottlingPolicy?> GetPolicyAsync(Guid tenantId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        return await policyRepository.GetByTenantAndTypeAsync(tenantId, type, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ResourceThrottlingPolicy>> GetTenantPoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await policyRepository.GetByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeletePolicyAsync(Guid tenantId, ResourceUsageType type, CancellationToken cancellationToken = default)
    {
        var policy = await policyRepository.GetByTenantAndTypeAsync(tenantId, type, cancellationToken).ConfigureAwait(false);

        if (policy == null)
        {
            return false;
        }

        var deleted = await policyRepository.DeleteAsync(policy.Id, cancellationToken).ConfigureAwait(false);

        if (deleted) { logger.LogInformation("Deleted throttling policy for tenant {TenantId}, type {Type}", tenantId, type); }

        return deleted;
    }

    public async Task<(bool ShouldBlock, int DelayMs)> ShouldThrottleAsync(Guid tenantId, ResourceUsageType type, long currentUsage, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(tenantId, type, cancellationToken).ConfigureAwait(false);

        if (policy is not { IsActive: true })
        {
            return (false, 0);
        }

        var shouldBlock = policy.ShouldBlock(currentUsage);
        var delayMs = policy.CalculateDelayMs(currentUsage);

        return (shouldBlock, delayMs);
    }

    public async Task<ThrottlingResult> ApplyThrottlingAsync(Guid tenantId, ResourceUsageType type, long requestedAmount = 1, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(tenantId, type, cancellationToken).ConfigureAwait(false);

        if (policy is not { IsActive: true }) { return new ThrottlingResult { IsAllowed = true, DelayMs = 0, Reason = "No throttling policy active", AppliedStrategy = ThrottlingStrategy.None }; }

        // Get current usage from quota
        var quota = await quotaRepository.GetByTenantAndTypeAsync(tenantId, type, cancellationToken).ConfigureAwait(false);
        var currentUsage = quota?.CurrentUsage ?? 0;

        var shouldBlock = policy.ShouldBlock(currentUsage);
        var delayMs = policy.CalculateDelayMs(currentUsage);

        var result = new ThrottlingResult { IsAllowed = !shouldBlock, DelayMs = delayMs, AppliedStrategy = policy.Strategy };

        if (shouldBlock)
        {
            result.Reason = $"Resource usage ({currentUsage}) exceeds throttling threshold ({policy.ThrottlingThresholdPercent})";

            logger.LogWarning("Throttled request for tenant {TenantId}, type {Type}: {Reason}", tenantId, type, result.Reason);
        }
        else if (delayMs > 0)
        {
            result.Reason = $"Request delayed by {delayMs}ms due to approaching threshold";

            logger.LogDebug("Applied throttling delay for tenant {TenantId}, type {Type}: {DelayMs}ms", tenantId, type, delayMs);
        }
        else { result.Reason = "Within normal operating limits"; }

        return result;
    }

    public async Task<IEnumerable<ResourceThrottlingPolicy>> GetActivePoliciesAsync(ResourceUsageType? type = null, CancellationToken cancellationToken = default)
    {
        return await policyRepository.GetActivePoliciesAsync(type, cancellationToken).ConfigureAwait(false);
    }

    // PLANNED: Integration with API Gateway for rate limiting enforcement (depends on API Gateway module)
    // PLANNED: Integration with Monitoring module for throttling metrics (depends on GameGuild.Monitoring)
}
