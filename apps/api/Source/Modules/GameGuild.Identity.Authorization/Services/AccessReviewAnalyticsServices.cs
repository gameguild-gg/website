using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Service for managing Access Review campaigns and items
/// </summary>
public class AccessReviewService(
    IAccessReviewCampaignRepository campaignRepository,
    IAccessReviewItemRepository itemRepository,
    ILogger<AccessReviewService> logger,
    IPublisher? publisher = null,
    IActorContextAccessor? actorContextAccessor = null
) : IAccessReviewService
{
    private const string TenantAdministratorRequiredMessage = "Tenant administrator access to this access review is required.";

    private readonly IAccessReviewCampaignRepository _campaignRepository =
        campaignRepository ?? throw new ArgumentNullException(nameof(campaignRepository));

    private readonly IAccessReviewItemRepository _itemRepository =
        itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));

    private readonly ILogger<AccessReviewService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IPublisher? _publisher = publisher;

    private readonly IActorContextAccessor _actorContextAccessor =
        actorContextAccessor ?? throw new ArgumentNullException(nameof(actorContextAccessor));

    private ActorContext Actor => _actorContextAccessor.ActorContext;

    public async Task<AccessReviewCampaign> CreateCampaignAsync(
        AccessReviewCampaign campaign,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(campaign);
        EnsureCanManageTenant(GetTenantId(campaign));
        EnsureReportedActor(campaign.CreatedBy);

        var result = await _campaignRepository.CreateAsync(campaign, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created access review campaign {CampaignId}: {Name}",
            result.Id,
            campaign.Name
        );

        return result;
    }

    public async Task<AccessReviewCampaign> UpdateCampaignAsync(
        AccessReviewCampaign campaign,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(campaign);
        var existing = await _campaignRepository.GetByIdAsync(campaign.Id, cancellationToken).ConfigureAwait(false);

        if (existing == null)
        {
            throw new InvalidOperationException($"Access review campaign {campaign.Id} not found");
        }

        EnsureCanManageTenant(GetTenantId(existing));

        if (GetTenantId(campaign) != GetTenantId(existing))
        {
            throw new UnauthorizedAccessException("An access review campaign cannot be moved between tenants.");
        }

        // Keep workflow and audit fields server-owned; this method only edits campaign configuration.
        existing.Name = campaign.Name;
        existing.Description = campaign.Description;
        existing.ReviewType = campaign.ReviewType;
        existing.Scope = campaign.Scope;
        existing.ScopeFilter = campaign.ScopeFilter;
        existing.StartDate = campaign.StartDate;
        existing.EndDate = campaign.EndDate;
        existing.AutoRevokeOnNoResponse = campaign.AutoRevokeOnNoResponse;
        existing.ReminderFrequencyDays = campaign.ReminderFrequencyDays;
        existing.NotificationTemplate = campaign.NotificationTemplate;

        await _campaignRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated access review campaign {CampaignId}", campaign.Id);

        return existing;
    }

    public async Task<bool> StartCampaignAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthenticated();
        var campaign = await _campaignRepository.GetByIdAsync(campaignId, cancellationToken).ConfigureAwait(false);

        if (campaign == null)
        {
            return false;
        }

        EnsureCanManageTenant(GetTenantId(campaign));
        campaign.Start();
        await _campaignRepository.UpdateAsync(campaign, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Started access review campaign {CampaignId}", campaignId);

        return true;
    }

    public async Task<bool> CompleteCampaignAsync(
        Guid campaignId,
        Guid completedBy,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthenticated();
        var campaign = await _campaignRepository.GetByIdAsync(campaignId, cancellationToken).ConfigureAwait(false);

        if (campaign == null)
        {
            return false;
        }

        EnsureCanManageTenant(GetTenantId(campaign));
        EnsureReportedActor(completedBy);
        campaign.Complete(completedBy);
        await _campaignRepository.UpdateAsync(campaign, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Completed access review campaign {CampaignId} by {CompletedBy}", campaignId, completedBy);

        return true;
    }

    public async Task<bool> CancelCampaignAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthenticated();
        var campaign = await _campaignRepository.GetByIdAsync(campaignId, cancellationToken).ConfigureAwait(false);

        if (campaign == null)
        {
            return false;
        }

        EnsureCanManageTenant(GetTenantId(campaign));
        campaign.Cancel();
        await _campaignRepository.UpdateAsync(campaign, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Cancelled access review campaign {CampaignId}", campaignId);

        return true;
    }

    public async Task<AccessReviewCampaign?> GetCampaignByIdAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthenticated();
        var campaign = await _campaignRepository.GetByIdAsync(campaignId, cancellationToken).ConfigureAwait(false);

        // Return the same result for missing and out-of-scope campaign IDs.
        return campaign != null && CanManageTenant(GetTenantId(campaign)) ? campaign : null;
    }

    public async Task<List<AccessReviewCampaign>> GetActiveCampaignsAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var scopedTenantId = ResolveManagerTenantScope(tenantId);
        return await _campaignRepository.GetActiveCampaignsAsync(scopedTenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AccessReviewItem>> GetPendingItemsForReviewerAsync(
        Guid reviewerId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthenticated();
        Guid? scopedTenantId;

        if (Actor.IsSystemAdmin)
        {
            scopedTenantId = tenantId;
        }
        else
        {
            var actorTenantId = Actor.TenantId
                ?? throw new UnauthorizedAccessException("Tenant context is required for access reviews.");
            var actorId = Actor.SubjectIdAsGuid
                ?? throw new UnauthorizedAccessException("A user identity is required for access reviews.");

            if (tenantId.HasValue && tenantId.Value != actorTenantId)
            {
                throw new UnauthorizedAccessException("Access reviews cannot cross tenant boundaries.");
            }

            scopedTenantId = actorTenantId;
            if (reviewerId != actorId)
            {
                EnsureCanManageTenant(scopedTenantId);
            }
        }

        return await _itemRepository
            .GetPendingByReviewerAsync(reviewerId, scopedTenantId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AccessReviewItem> ApproveItemAsync(
        Guid itemId,
        string? reason = null,
        string? notes = null,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthenticated();
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken).ConfigureAwait(false);

        if (item == null)
        {
            throw new InvalidOperationException($"Review item {itemId} not found");
        }

        EnsureCanReviewItem(item);
        item.Approve(reason, notes);
        await _itemRepository.UpdateAsync(item, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Approved review item {ItemId}", itemId);

        return item;
    }

    public async Task<AccessReviewItem> RevokeItemAsync(
        Guid itemId,
        string reason,
        string? notes = null,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthenticated();
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken).ConfigureAwait(false);

        if (item == null)
        {
            throw new InvalidOperationException($"Review item {itemId} not found");
        }

        EnsureCanReviewItem(item);
        item.Revoke(reason, notes);
        await _itemRepository.UpdateAsync(item, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Revoked review item {ItemId}", itemId);

        return item;
    }

    public async Task<int> SendRemindersAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuthenticated();
        var campaign = await _campaignRepository.GetByIdAsync(campaignId, cancellationToken).ConfigureAwait(false);

        if (campaign == null)
        {
            return 0;
        }

        EnsureCanManageTenant(GetTenantId(campaign));
        var items = await _itemRepository.GetByCampaignAsync(campaignId, cancellationToken).ConfigureAwait(false);
        var remindersSent = 0;

        foreach (var item in items.Where(i => i.NeedsReminder(campaign.ReminderFrequencyDays)))
        {
            item.RecordReminderSent();
            await _itemRepository.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
            remindersSent++;

            if (_publisher is not null)
            {
                await _publisher.Publish(
                    new AccessReviewReminderNotification(campaignId, item.Id, item.ReviewerId),
                    cancellationToken
                ).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Sent {Count} reminders for campaign {CampaignId}", remindersSent, campaignId);

        return remindersSent;
    }

    public async Task<int> ProcessExpiredCampaignsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var pendingCampaigns = await _campaignRepository.GetPendingCampaignsAsync(cancellationToken).ConfigureAwait(false);
        var expiredCount = 0;

        foreach (var campaign in pendingCampaigns.Where(c => c.IsExpired()))
        {
            campaign.MarkExpired();
            await _campaignRepository.UpdateAsync(campaign, cancellationToken).ConfigureAwait(false);
            expiredCount++;
        }

        _logger.LogInformation("Marked {Count} campaigns as expired", expiredCount);

        return expiredCount;
    }

    private void EnsureAuthenticated()
    {
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("An authenticated actor is required for access reviews.");
        }
    }

    private void EnsureReportedActor(Guid reportedActorId)
    {
        if (Actor.SubjectIdAsGuid is not { } actorId || actorId != reportedActorId)
        {
            throw new UnauthorizedAccessException("Access review actor IDs must match the authenticated user.");
        }
    }

    private void EnsureCanManageTenant(Guid? tenantId)
    {
        if (!CanManageTenant(tenantId))
        {
            throw new UnauthorizedAccessException(TenantAdministratorRequiredMessage);
        }
    }

    private bool CanManageTenant(Guid? tenantId) =>
        Actor.IsAuthenticated &&
        (Actor.IsSystemAdmin ||
         (tenantId.HasValue && Actor.TenantId == tenantId && Actor.IsTenantAdmin));

    private Guid? ResolveManagerTenantScope(Guid? requestedTenantId)
    {
        EnsureAuthenticated();

        if (Actor.IsSystemAdmin)
        {
            return requestedTenantId;
        }

        var actorTenantId = Actor.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required for access reviews.");

        if (requestedTenantId.HasValue && requestedTenantId.Value != actorTenantId)
        {
            throw new UnauthorizedAccessException("Access reviews cannot cross tenant boundaries.");
        }

        EnsureCanManageTenant(actorTenantId);
        return actorTenantId;
    }

    private void EnsureCanReviewItem(AccessReviewItem item)
    {
        if (item.Campaign == null)
        {
            throw new UnauthorizedAccessException("The review item has no tenant context.");
        }

        var tenantId = GetTenantId(item.Campaign);
        if (Actor.IsSystemAdmin)
        {
            return;
        }

        if (Actor.SubjectIdAsGuid == item.ReviewerId && Actor.TenantId == tenantId)
        {
            return;
        }

        EnsureCanManageTenant(tenantId);
    }

    private static Guid? GetTenantId(AccessReviewCampaign campaign) => campaign.TenantId?.Value;
}
/// <summary>
///     Service for permission analytics and reporting
/// </summary>
public class PermissionAnalyticsService(
    IPermissionAuditLogRepository auditLogRepository,
    ILogger<PermissionAnalyticsService> logger
) : IPermissionAnalyticsService
{
    private readonly IPermissionAuditLogRepository _auditLogRepository =
        auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));

    private readonly ILogger<PermissionAnalyticsService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<List<PermissionUsageMetrics>> GetPermissionUsageAsync(
        Guid? tenantId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var logs = await _auditLogRepository.GetByDateRangeAsync(
            fromDate ?? SystemClock.UtcNow.AddMonths(-1),
            toDate ?? SystemClock.UtcNow,
            tenantId,
            cancellationToken
        ).ConfigureAwait(false);

        return logs
            .Where(l => l.PermissionType != null)
            .GroupBy(l => l.PermissionType!)
            .Select(g => new PermissionUsageMetrics
            {
                Permission = g.Key,
                UsageCount = g.Count(),
                UniqueUsers = g.Select(l => l.UserId).Distinct().Count(),
                LastUsed = g.Max(l => l.Timestamp)
            })
            .OrderByDescending(m => m.UsageCount)
            .ToList();
    }

    public async Task<List<UserActivitySummary>> GetUserActivityAsync(
        Guid? tenantId,
        int top = 10,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var logs = await _auditLogRepository.GetByDateRangeAsync(
            fromDate ?? SystemClock.UtcNow.AddMonths(-1),
            toDate ?? SystemClock.UtcNow,
            tenantId,
            cancellationToken
        ).ConfigureAwait(false);

        return logs
            .Where(l => l.UserId.HasValue)
            .GroupBy(l => l.UserId!.Value)
            .Select(g => new UserActivitySummary
            {
                UserId = g.Key,
                TotalActions = g.Count(),
                PermissionChanges = g.Count(l => l.OperationType is PermissionOperationType.Grant or PermissionOperationType.Revoke),
                LastActivity = g.Max(l => l.Timestamp)
            })
            .OrderByDescending(s => s.TotalActions)
            .Take(top)
            .ToList();
    }

    public async Task<List<ResourceAccessPattern>> GetResourceAccessPatternsAsync(
        Guid? tenantId,
        int top = 10,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var logs = await _auditLogRepository.GetByDateRangeAsync(
            fromDate ?? SystemClock.UtcNow.AddMonths(-1),
            toDate ?? SystemClock.UtcNow,
            tenantId,
            cancellationToken
        ).ConfigureAwait(false);

        return logs
            .Where(l => l.ResourceId.HasValue && l.ResourceType != null)
            .GroupBy(l => new { l.ResourceId, l.ResourceType })
            .Select(g => new ResourceAccessPattern
            {
                ResourceId = g.Key.ResourceId!.Value,
                ResourceType = g.Key.ResourceType!,
                AccessCount = g.Count(),
                UniqueUsers = g.Select(l => l.UserId).Distinct().Count()
            })
            .OrderByDescending(p => p.AccessCount)
            .Take(top)
            .ToList();
    }

    public async Task<List<PermissionTrend>> GetPermissionTrendsAsync(
        Guid? tenantId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default
    )
    {
        var logs = await _auditLogRepository.GetByDateRangeAsync(
            fromDate,
            toDate,
            tenantId,
            cancellationToken
        ).ConfigureAwait(false);

        var dailyTrends = logs
            .GroupBy(l => l.Timestamp.Date)
            .Select(g => new PermissionTrend
            {
                Date = g.Key,
                Grants = g.Count(l => l.OperationType == PermissionOperationType.Grant),
                Revokes = g.Count(l => l.OperationType == PermissionOperationType.Revoke),
            })
            .OrderBy(t => t.Date)
            .ToList();

        var activePermissions = 0;
        foreach (var trend in dailyTrends)
        {
            activePermissions += trend.Grants - trend.Revokes;
            trend.ActivePermissions = activePermissions;
        }

        return dailyTrends;
    }

    public async Task<List<PermissionAnomaly>> DetectAnomaliesAsync(
        Guid? tenantId,
        DateTime? fromDate = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Detecting permission anomalies for tenant {TenantId} from {FromDate}",
            tenantId,
            fromDate
        );

        var logs = await _auditLogRepository.GetByDateRangeAsync(
            fromDate ?? SystemClock.UtcNow.AddDays(-7),
            SystemClock.UtcNow,
            tenantId,
            cancellationToken
        ).ConfigureAwait(false);

        var anomalies = new List<PermissionAnomaly>();

        // Detect unusual patterns: excessive grants/revokes
        var userGrantCounts = logs
            .Where(l => l.OperationType == PermissionOperationType.Grant)
            .GroupBy(l => l.PerformedBy)
            .Where(g => g.Count() > 50) // Threshold
            .ToList();

        foreach (var group in userGrantCounts)
        {
            anomalies.Add(new PermissionAnomaly
            {
                UserId = group.Key,
                AnomalyType = "ExcessiveGrants",
                Description = $"User performed {group.Count()} permission grants in the period",
                DetectedAt = SystemClock.UtcNow,
                Severity = ImpactSeverity.Medium
            });
        }

        return anomalies;
    }

    public async Task<PermissionAnalyticsReport> GenerateReportAsync(
        Guid? tenantId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Generating permission analytics report for tenant {TenantId} from {PeriodStart} to {PeriodEnd}",
            tenantId,
            periodStart,
            periodEnd
        );

        var topPermissions = await GetPermissionUsageAsync(tenantId, periodStart, periodEnd, cancellationToken).ConfigureAwait(false);
        var topUsers = await GetUserActivityAsync(tenantId, 10, periodStart, periodEnd, cancellationToken).ConfigureAwait(false);
        var anomalies = await DetectAnomaliesAsync(tenantId, periodStart, cancellationToken).ConfigureAwait(false);

        var logs = await _auditLogRepository.GetByDateRangeAsync(periodStart, periodEnd, tenantId, cancellationToken).ConfigureAwait(false);

        return new PermissionAnalyticsReport
        {
            TenantId = tenantId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            TopPermissions = topPermissions.Take(10).ToList(),
            TopUsers = topUsers,
            Anomalies = anomalies,
            TotalGrants = logs.Count(l => l.OperationType == PermissionOperationType.Grant),
            TotalRevokes = logs.Count(l => l.OperationType == PermissionOperationType.Revoke),
            ActiveUsers = logs.Select(l => l.UserId).Distinct().Count()
        };
    }
}
