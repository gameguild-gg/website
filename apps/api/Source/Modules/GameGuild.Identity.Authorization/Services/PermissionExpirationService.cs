using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Implementation of <see cref="IPermissionExpirationService"/> (issue #331):
///     automatic expiration of tenant permission grants, upcoming-expiration
///     notifications, and bulk administrative expiration management.
/// </summary>
/// <remarks>
///     <para>
///         <b>SECURITY - Cache invalidation:</b> every mutation increments the tenant
///         security version (<see cref="ITenantSecurityVersionStore"/>) so cached
///         permission data is invalidated immediately.
///     </para>
///     <para>
///         <b>SECURITY - Audit:</b> every mutation writes a
///         <see cref="PermissionAuditLog"/> entry; automatic expirations use
///         <see cref="PermissionOperationType.Expire"/> with a system actor,
///         administrative set/extend use the authenticated actor from
///         <see cref="IActorContextAccessor"/>.
///     </para>
///     <para>
///         Expired grants already contribute nothing to permission evaluation
///         (<c>EffectivePermissionResolverService</c> and <c>PermissionQueryService</c>
///         filter them fail-closed); the durable deactivation performed here exists so
///         revocation survives re-activation paths and produces the audit/notification
///         trail required for temporal access control.
///     </para>
/// </remarks>
public sealed class PermissionExpirationService(
    ITenantPermissionRepository repository,
    IPermissionAuditService auditService,
    ITenantSecurityVersionStore securityVersionStore,
    IOptions<PermissionExpirationOptions> expirationOptions,
    ILogger<PermissionExpirationService> logger,
    IPublisher? publisher = null,
    IActorContextAccessor? actorContextAccessor = null
) : IPermissionExpirationService
{
    private const string ReminderStampKey = "expirationReminderAt";
    private const string ProcessedStampKey = "expirationProcessedAt";

    private PermissionExpirationOptions Options => expirationOptions.Value;

    private ActorContext Actor => actorContextAccessor?.ActorContext ?? ActorContext.Anonymous;

    public async Task<int> ProcessExpiredAsync(CancellationToken cancellationToken = default)
    {
        var expired = await repository.GetExpiredPermissionsAsync(cancellationToken).ConfigureAwait(false);
        var processed = 0;
        var affectedTenants = new HashSet<Guid>();

        foreach (var grant in expired.Take(Options.BatchSize))
        {
            if (HasStamp(grant, ProcessedStampKey))
            {
                // Already deactivated by a previous cycle; skip so processing stays idempotent.
                continue;
            }

            var originalExpiresAt = grant.ExpiresAt;

            try
            {
                grant.Expire();
                SetStamp(grant, ProcessedStampKey);
                await repository.UpdateAsync(grant, cancellationToken).ConfigureAwait(false);

                if (grant.TenantId is { } tenantId)
                {
                    affectedTenants.Add(tenantId);
                }

                await auditService.LogPermissionChangeAsync(
                    PermissionOperationType.Expire,
                    grant.UserId,
                    Guid.Empty,
                    grant.TenantId,
                    permissionType: "Tenant",
                    resourceType: "TenantPermission",
                    resourceId: grant.Id,
                    oldValue: originalExpiresAt?.ToString("O"),
                    newValue: "inactive",
                    reason: "Automatic permission expiration",
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                await PublishAsync(
                    new PermissionExpirationNotification(
                        grant.Id,
                        grant.UserId,
                        grant.TenantId,
                        grant.Permissions.ToArray(),
                        originalExpiresAt ?? SystemClock.UtcNow,
                        PermissionExpirationKind.Expired),
                    cancellationToken).ConfigureAwait(false);

                processed++;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Failed to process expired permission grant {PermissionId}; retrying on the next cycle.",
                    grant.Id);
            }
        }

        foreach (var tenantId in affectedTenants)
        {
            await IncrementTenantVersionAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Processed {Count} expired permission grants.", processed);

        return processed;
    }

    public async Task<int> SendUpcomingExpirationRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        var cutoff = now + Options.UpcomingNotificationWindow;
        var expiring = await repository.GetExpiringBeforeAsync(cutoff, cancellationToken).ConfigureAwait(false);
        var published = 0;

        foreach (var grant in expiring.Take(Options.BatchSize))
        {
            if (TryGetStamp(grant, ReminderStampKey, out var lastReminder) &&
                now - lastReminder < Options.MinimumReminderInterval)
            {
                continue;
            }

            try
            {
                SetStamp(grant, ReminderStampKey);
                await repository.UpdateAsync(grant, cancellationToken).ConfigureAwait(false);

                await PublishAsync(
                    new PermissionExpirationNotification(
                        grant.Id,
                        grant.UserId,
                        grant.TenantId,
                        grant.Permissions.ToArray(),
                        grant.ExpiresAt!.Value,
                        PermissionExpirationKind.Upcoming),
                    cancellationToken).ConfigureAwait(false);

                published++;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Failed to send expiration reminder for permission grant {PermissionId}.",
                    grant.Id);
            }
        }

        if (published > 0)
        {
            logger.LogInformation("Published {Count} upcoming-expiration reminders.", published);
        }

        return published;
    }

    public async Task<List<TenantPermission>> SetExpirationAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> permissionIds,
        DateTime? expiresAt,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissionIds);

        if (expiresAt.HasValue && expiresAt.Value <= SystemClock.UtcNow)
        {
            throw new ArgumentException("Expiration must be in the future. Use ProcessExpiredAsync to expire grants now.", nameof(expiresAt));
        }

        return await MutateExpirationAsync(
            tenantId,
            permissionIds,
            grant =>
            {
                grant.ExpiresAt = expiresAt;
                grant.IsActive = expiresAt is null || expiresAt.Value > SystemClock.UtcNow;
                ClearStamp(grant, ReminderStampKey);
                ClearStamp(grant, ProcessedStampKey);
            },
            expiresAt is null ? "Expiration cleared (grant made permanent)" : $"Expiration set to {expiresAt.Value:O}",
            reason,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<TenantPermission>> ExtendExpirationAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> permissionIds,
        TimeSpan extension,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissionIds);

        if (extension <= TimeSpan.Zero)
        {
            throw new ArgumentException("Extension must be positive.", nameof(extension));
        }

        var now = SystemClock.UtcNow;

        return await MutateExpirationAsync(
            tenantId,
            permissionIds,
            grant =>
            {
                var baseline = grant.ExpiresAt is { } current && current > now ? current : now;
                var newExpiresAt = baseline + extension;
                grant.ExpiresAt = newExpiresAt;
                grant.IsActive = true;
                ClearStamp(grant, ReminderStampKey);
                ClearStamp(grant, ProcessedStampKey);
            },
            $"Expiration extended by {extension}",
            reason,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<TenantPermission>> GetExpiringAsync(
        Guid tenantId,
        DateTime? cutoff = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveCutoff = cutoff ?? SystemClock.UtcNow + Options.UpcomingNotificationWindow;
        var expiring = await repository.GetExpiringBeforeAsync(effectiveCutoff, cancellationToken).ConfigureAwait(false);

        return expiring.Where(p => p.TenantId == tenantId).ToList();
    }

    private async Task<List<TenantPermission>> MutateExpirationAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> permissionIds,
        Action<TenantPermission> mutate,
        string operationDescription,
        string? reason,
        CancellationToken cancellationToken)
    {
        var grants = await repository.GetByIdsInTenantAsync(tenantId, permissionIds, cancellationToken).ConfigureAwait(false);
        var performedBy = Actor.SubjectIdAsGuid ?? Guid.Empty;
        var updated = new List<TenantPermission>(grants.Count);

        foreach (var grant in grants)
        {
            var previousExpiresAt = grant.ExpiresAt;
            mutate(grant);
            await repository.UpdateAsync(grant, cancellationToken).ConfigureAwait(false);
            updated.Add(grant);

            await auditService.LogPermissionChangeAsync(
                PermissionOperationType.Update,
                grant.UserId,
                performedBy,
                grant.TenantId,
                permissionType: "Tenant",
                resourceType: "TenantPermission",
                resourceId: grant.Id,
                oldValue: previousExpiresAt?.ToString("O") ?? "permanent",
                newValue: grant.ExpiresAt?.ToString("O") ?? "permanent",
                reason: string.IsNullOrWhiteSpace(reason) ? operationDescription : $"{operationDescription}. {reason}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        if (updated.Count > 0)
        {
            // SECURITY: Invalidate cached permissions for the mutated tenant.
            await IncrementTenantVersionAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation(
            "Bulk expiration mutation '{Operation}' applied to {Count} grants in tenant {TenantId}.",
            operationDescription,
            updated.Count,
            tenantId);

        return updated;
    }

    private async Task IncrementTenantVersionAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var newVersion = await securityVersionStore
                .IncrementVersionAsync(tenantId.ToString(), cancellationToken)
                .ConfigureAwait(false);
            logger.LogDebug("Incremented security version for tenant {TenantId} to {Version}.", tenantId, newVersion);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Failed to increment security version for tenant {TenantId}. Cache may be stale.",
                tenantId);
            throw;
        }
    }

    private async Task PublishAsync(PermissionExpirationNotification notification, CancellationToken cancellationToken)
    {
        if (publisher is null)
        {
            return;
        }

        await publisher.Publish(notification, cancellationToken).ConfigureAwait(false);
    }

    private static bool HasStamp(TenantPermission grant, string key)
    {
        return TryGetStamp(grant, key, out _);
    }

    private static bool TryGetStamp(TenantPermission grant, string key, out DateTime stamp)
    {
        stamp = default;

        if (grant.Metadata is null || !grant.Metadata.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        string? raw = value switch
        {
            DateTime dateTime => dateTime.ToString("O"),
            string text => text,
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } element => element.GetString(),
            _ => null
        };

        return raw is not null && DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out stamp);
    }

    private static void SetStamp(TenantPermission grant, string key)
    {
        grant.Metadata ??= new Dictionary<string, object>();
        grant.Metadata[key] = SystemClock.UtcNow.ToString("O");
    }

    private static void ClearStamp(TenantPermission grant, string key)
    {
        grant.Metadata?.Remove(key);
    }
}
