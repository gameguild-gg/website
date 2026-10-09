using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using GameGuild.Identity.Context.Actors;
using CommerceSubscription = GameGuild.Commerce.Subscriptions.Subscription;
using CommerceSubscriptionPlan = GameGuild.Commerce.Subscriptions.SubscriptionPlan;
using CommerceSubscriptionStatus = GameGuild.Commerce.Subscriptions.SubscriptionStatus;

namespace GameGuild.Features;

/// <summary>
/// Implementation of capability service with fail-closed behavior and audit logging.
/// Integrates subscription plan entitlements with explicit tenant overrides.
/// </summary>
public class CapabilityService : ICapabilityService
{
    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CapabilityService> _logger;
    private readonly IActorContextAccessor _actorContextAccessor;

    private const string CapabilitiesCacheKeyPrefix = "TenantCapabilities:";
    [ExcludeFromCodeCoverage]
    private static TimeSpan CacheDuration { get; } = TimeSpan.FromMinutes(5);

    // Known capabilities with their plan mappings
    [ExcludeFromCodeCoverage]
    private static Dictionary<string, HashSet<string>> PlanCapabilities { get; } = new()
    {
        ["free"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "lms.courses.basic",
            "lms.enrollments"
        },
        ["starter"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "lms.courses.basic",
            "lms.enrollments",
            "lms.certificates",
            "lxp.discovery",
            "lxp.socialProof"
        },
        ["pro"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "lms.courses.basic",
            "lms.enrollments",
            "lms.certificates",
            "lms.assessments",
            "lms.cohorts",
            "lxp.discovery",
            "lxp.learningPaths",
            "lxp.recommendations.basic",
            "lxp.skills",
            "lxp.social",
            "lxp.bookmarks",
            "lxp.socialProof",
            "lxp.personalizedFeed",
            "analytics.advanced"
        },
        ["enterprise"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "lms.courses.basic",
            "lms.enrollments",
            "lms.certificates",
            "lms.assessments",
            "lms.cohorts",
            "lxp.discovery",
            "lxp.learningPaths",
            "lxp.recommendations.basic",
            "lxp.recommendations.ai",
            "lxp.skills",
            "lxp.social",
            "lxp.bookmarks",
            "lxp.socialProof",
            "lxp.personalizedFeed",
            "analytics.advanced",
            "branding.custom"
        }
    };

    // All known capability keys for returning the full matrix
    [ExcludeFromCodeCoverage]
    private static HashSet<string> AllCapabilities { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "lms.courses.basic",
        "lms.enrollments",
        "lms.certificates",
        "lms.assessments",
        "lms.cohorts",
        "lxp.discovery",
        "lxp.learningPaths",
        "lxp.recommendations.basic",
        "lxp.recommendations.ai",
        "lxp.skills",
        "lxp.social",
        "lxp.bookmarks",
        "lxp.socialProof",
        "lxp.personalizedFeed",
        "analytics.advanced",
        "branding.custom"
    };

    public CapabilityService(
        IApplicationDbContext context,
        IMemoryCache cache,
        ILogger<CapabilityService> logger,
        IActorContextAccessor actorContextAccessor)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _actorContextAccessor = actorContextAccessor;
    }

    /// <inheritdoc />
    public async Task<bool> IsCapabilityEnabledAsync(
        Guid tenantId,
        string capability,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Check explicit tenant override (highest priority)
            var tenantOverride = await _context.Set<TenantCapability>()
                .Where(tc => tc.TenantId == tenantId && tc.CapabilityKey == capability)
                .OrderByDescending(tc => tc.Priority)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (tenantOverride != null)
            {
                // Check if override has expired
                if (tenantOverride.ExpiresAt.HasValue && tenantOverride.ExpiresAt.Value < DateTimeOffset.UtcNow)
                {
                    _logger.LogInformation(
                        "Capability {Capability} override for tenant {TenantId} has expired, falling back to plan",
                        LogRedaction.Sanitize(capability), LogRedaction.Sanitize(tenantId.ToString()));
                }
                else
                {
                    return tenantOverride.IsEnabled;
                }
            }

            // 2. Check subscription plan entitlements
            var subscription = await _context.Set<CommerceSubscription>()
                .Include(s => s.Plan)
                .Where(s => s.TenantId == tenantId && s.Status == CommerceSubscriptionStatus.Active)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (subscription == null)
            {
                _logger.LogWarning(
                    "No active subscription found for tenant {TenantId}, capability {Capability} denied (fail-closed)",
                    LogRedaction.Sanitize(tenantId.ToString()), LogRedaction.Sanitize(capability));
                return false; // Fail-closed
            }

            // 3. Check plan capabilities
            var planSlug = subscription.Plan?.Slug?.ToLowerInvariant() ?? "free";
            return GetPlanCapability(planSlug, capability);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Capability check failed for {TenantId}/{Capability}, defaulting to false (fail-closed)",
                LogRedaction.Sanitize(tenantId.ToString()), LogRedaction.Sanitize(capability));
            return false; // Fail-closed
        }
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, bool>> GetTenantCapabilitiesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CapabilitiesCacheKeyPrefix}{tenantId}";

        if (_cache.TryGetValue(cacheKey, out IDictionary<string, bool>? cachedCapabilities) && cachedCapabilities != null)
        {
            return cachedCapabilities;
        }

        try
        {
            var capabilities = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            // Get all explicit overrides for this tenant
            var overrides = await _context.Set<TenantCapability>()
                .Where(tc => tc.TenantId == tenantId)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            // Get subscription plan
            var subscription = await _context.Set<CommerceSubscription>()
                .Include(s => s.Plan)
                .Where(s => s.TenantId == tenantId && s.Status == CommerceSubscriptionStatus.Active)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            var planSlug = subscription?.Plan?.Slug?.ToLowerInvariant() ?? "free";

            // Build capability matrix for all known capabilities
            foreach (var capability in AllCapabilities)
            {
                // Check for override first
                var tenantOverride = overrides
                    .Where(o => o.CapabilityKey.Equals(capability, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(o => o.Priority)
                    .FirstOrDefault();

                if (tenantOverride != null &&
                    (!tenantOverride.ExpiresAt.HasValue || tenantOverride.ExpiresAt.Value >= DateTimeOffset.UtcNow))
                {
                    capabilities[capability] = tenantOverride.IsEnabled;
                }
                else
                {
                    // Fall back to plan capability
                    capabilities[capability] = GetPlanCapability(planSlug, capability);
                }
            }

            _cache.Set(cacheKey, capabilities, CacheDuration);

            return capabilities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to get capabilities for tenant {TenantId}, returning empty dictionary (fail-closed)",
                tenantId);

            // Return all capabilities as false (fail-closed)
            return AllCapabilities.ToDictionary(c => c, _ => false, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc />
    public async Task SetCapabilityOverrideAsync(
        Guid tenantId,
        string capability,
        bool isEnabled,
        string source,
        Guid? userId,
        string? reason,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        // The acting administrator always comes from the resolved actor context
        // (AGENTS.md invariant 8), never from the caller-supplied parameter.
        var actingUserId = CapabilityAccessGuard.RequireAdministrator(_actorContextAccessor.ActorContext, tenantId);
        var existing = await _context.Set<TenantCapability>()
            .FirstOrDefaultAsync(tc => tc.TenantId == tenantId && tc.CapabilityKey == capability, cancellationToken).ConfigureAwait(false);

        bool? oldValue = existing?.IsEnabled;
        var oldSource = existing?.Source;

        if (existing != null)
        {
            existing.IsEnabled = isEnabled;
            existing.Source = source;
            existing.ExpiresAt = expiresAt;
            existing.ModifiedByUserId = actingUserId;
            existing.ModificationReason = reason;
            existing.Priority = source.StartsWith("override:", StringComparison.OrdinalIgnoreCase) ? 1000 : 0;
        }
        else
        {
            var newCapability = new TenantCapability
            {
                TenantId = tenantId,
                CapabilityKey = capability,
                IsEnabled = isEnabled,
                Source = source,
                ExpiresAt = expiresAt,
                ModifiedByUserId = actingUserId,
                ModificationReason = reason,
                Priority = source.StartsWith("override:", StringComparison.OrdinalIgnoreCase) ? 1000 : 0
            };
            _context.Set<TenantCapability>().Add(newCapability);
        }

        // Create audit log
        var auditLog = new CapabilityAuditLog
        {
            TenantId = tenantId,
            CapabilityKey = capability,
            OldValue = oldValue,
            NewValue = isEnabled,
            OldSource = oldSource,
            NewSource = source,
            ChangedByUserId = actingUserId,
            ChangeReason = reason,
            ChangeType = oldValue == null ? CapabilityChangeType.Granted :
                         (isEnabled && !oldValue.Value) ? CapabilityChangeType.Restored :
                         (!isEnabled && oldValue.Value) ? CapabilityChangeType.Revoked :
                         CapabilityChangeType.Modified,
            ChangedAt = DateTimeOffset.UtcNow
        };
        _context.Set<CapabilityAuditLog>().Add(auditLog);

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Invalidate cache
        InvalidateCache(tenantId);

        _logger.LogInformation(
            "Capability {Capability} for tenant {TenantId} changed from {OldValue} to {NewValue} (source: {Source}, reason: {Reason})",
            capability, tenantId, oldValue, isEnabled, source, reason);
    }

    /// <inheritdoc />
    public async Task RemoveCapabilityOverrideAsync(
        Guid tenantId,
        string capability,
        Guid? userId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        userId = CapabilityAccessGuard.RequireAdministrator(_actorContextAccessor.ActorContext, tenantId);
        var existing = await _context.Set<TenantCapability>()
            .FirstOrDefaultAsync(tc => tc.TenantId == tenantId && tc.CapabilityKey == capability, cancellationToken).ConfigureAwait(false);

        if (existing == null)
        {
            _logger.LogWarning(
                "Attempted to remove non-existent capability override for {TenantId}/{Capability}",
                tenantId, capability);
            return;
        }

        // Create audit log before removing
        var auditLog = new CapabilityAuditLog
        {
            TenantId = tenantId,
            CapabilityKey = capability,
            OldValue = existing.IsEnabled,
            NewValue = false, // Will fall back to plan
            OldSource = existing.Source,
            NewSource = "removed",
            ChangedByUserId = userId,
            ChangeReason = reason ?? "Override removed",
            ChangeType = CapabilityChangeType.Modified,
            ChangedAt = DateTimeOffset.UtcNow
        };
        _context.Set<CapabilityAuditLog>().Add(auditLog);

        _context.Set<TenantCapability>().Remove(existing);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Invalidate cache
        InvalidateCache(tenantId);

        _logger.LogInformation(
            "Capability override {Capability} removed for tenant {TenantId} (reason: {Reason})",
            capability, tenantId, reason);
    }

    /// <inheritdoc />
    public async Task SyncCapabilitiesFromPlanAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var userId = CapabilityAccessGuard.RequireAdministrator(_actorContextAccessor.ActorContext, tenantId);
        var subscription = await _context.Set<CommerceSubscription>()
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId && s.Status == CommerceSubscriptionStatus.Active)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (subscription?.Plan == null)
        {
            _logger.LogWarning("No active subscription found for tenant {TenantId} during capability sync", tenantId);
            return;
        }

        var planSlug = subscription.Plan.Slug?.ToLowerInvariant() ?? "free";
        var planSource = $"plan:{planSlug}";
        var planCapabilities = GetPlanCapabilities(planSlug);

        // One row owns each tenant/key. Preserve explicit decisions, including expired
        // overrides whose effective value falls back to the current subscription plan.
        var existingCapabilities = await _context.Set<TenantCapability>()
            .Where(tc => tc.TenantId == tenantId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Update capabilities based on plan
        foreach (var capability in AllCapabilities)
        {
            var shouldBeEnabled = planCapabilities.Contains(capability);
            var existing = existingCapabilities.FirstOrDefault(c =>
                c.CapabilityKey.Equals(capability, StringComparison.OrdinalIgnoreCase));

            if (existing != null &&
                existing.Source?.StartsWith("plan:", StringComparison.OrdinalIgnoreCase) != true)
            {
                continue;
            }

            if (existing != null && existing.IsEnabled == shouldBeEnabled && existing.Source == planSource)
            {
                continue;
            }

            var oldValue = existing?.IsEnabled;
            var oldSource = existing?.Source;
            var reason = $"Plan sync from {oldSource ?? "none"} to {planSource}";

            if (existing == null)
            {
                existing = new TenantCapability
                {
                    TenantId = tenantId,
                    CapabilityKey = capability,
                    Priority = 0
                };
                _context.Set<TenantCapability>().Add(existing);
            }

            existing.IsEnabled = shouldBeEnabled;
            existing.Source = planSource;
            existing.ModifiedByUserId = userId;
            existing.ModificationReason = reason;
            _context.Set<CapabilityAuditLog>().Add(new CapabilityAuditLog
            {
                TenantId = tenantId,
                CapabilityKey = capability,
                OldValue = oldValue,
                NewValue = shouldBeEnabled,
                OldSource = oldSource,
                NewSource = planSource,
                ChangedByUserId = userId,
                ChangeReason = reason,
                ChangeType = CapabilityChangeType.PlanChange,
                ChangedAt = DateTimeOffset.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        InvalidateCache(tenantId);

        _logger.LogInformation(
            "Synced capabilities for tenant {TenantId} from plan {PlanSlug}",
            tenantId, planSlug);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CapabilityAuditLog>> GetAuditLogAsync(
        Guid tenantId,
        string? capability = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        CapabilityAccessGuard.RequireAdministrator(_actorContextAccessor.ActorContext, tenantId);
        var query = _context.Set<CapabilityAuditLog>()
            .Where(log => log.TenantId == tenantId);

        if (!string.IsNullOrEmpty(capability))
        {
            query = query.Where(log => log.CapabilityKey == capability);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(log => log.ChangedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(log => log.ChangedAt <= toDate.Value);
        }

        return await query
            .OrderByDescending(log => log.ChangedAt)
            .Take(100)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool GetPlanCapability(string planSlug, string capability)
    {
        if (PlanCapabilities.TryGetValue(planSlug, out var capabilities))
        {
            return capabilities.Contains(capability);
        }

        // Unknown plan, default to free tier
        return PlanCapabilities["free"].Contains(capability);
    }

    private HashSet<string> GetPlanCapabilities(string planSlug)
    {
        return PlanCapabilities.TryGetValue(planSlug, out var capabilities)
            ? capabilities
            : PlanCapabilities["free"];
    }

    private void InvalidateCache(Guid tenantId)
    {
        var cacheKey = $"{CapabilitiesCacheKeyPrefix}{tenantId}";
        _cache.Remove(cacheKey);
    }
}
