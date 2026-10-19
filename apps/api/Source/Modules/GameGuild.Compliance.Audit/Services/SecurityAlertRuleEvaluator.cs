using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Compliance.Audit;

/// <summary>Alert rule identifiers raised by the security event pipeline.</summary>
public static class SecurityAlertRules
{
    public const string CriticalEvent = "security.critical-event";

    public const string FailedAuthenticationBurst = "security.failed-authentication-burst";

    public const string TenantIsolationBreach = "security.tenant-isolation-breach";

    public const string PermissionChangeBurst = "security.permission-change-burst";

    public const string BulkPermissionChange = "security.bulk-permission-change";
}

/// <summary>
///     Evaluates alert rules against a security event that is being persisted. Alerts are deduplicated
///     per (rule, tenant, subject): repeated hits merge into the open alert instead of creating new rows.
///     Alert rows are added to the same save as the security event so events and their alerts land atomically.
/// </summary>
public interface ISecurityAlertRuleEvaluator
{
    Task EvaluateAsync(
        IApplicationDbContext context,
        AuditLog securityEvent,
        ClassifiedSecurityEvent classification,
        CancellationToken cancellationToken = default);
}

public sealed class SecurityAlertRuleEvaluator(
    IOptions<SecurityEventPipelineOptions> options,
    ILogger<SecurityAlertRuleEvaluator> logger) : ISecurityAlertRuleEvaluator
{
    private readonly SecurityEventPipelineOptions options = options.Value;

    public async Task EvaluateAsync(
        IApplicationDbContext context,
        AuditLog securityEvent,
        ClassifiedSecurityEvent classification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(securityEvent);
        ArgumentNullException.ThrowIfNull(classification);

        try
        {
            var mutationCount = securityEvent.Success && IsPermissionMutation(securityEvent.ActionType)
                && securityEvent.ResourceType == "Permission"
                ? PermissionMutationEvidence.GetChangeCount(securityEvent.Metadata)
                : 0;
            if (mutationCount > 0)
            {
                await SerializePermissionEvaluationAsync(context, securityEvent, cancellationToken).ConfigureAwait(false);
            }

            if (classification.Kind == SecurityEventKind.TenantIsolation)
            {
                await RaiseOrMergeAsync(context, securityEvent, classification,
                    SecurityAlertRules.TenantIsolationBreach,
                    AuditRiskLevel.Critical,
                    "Tenant isolation breach recorded",
                    $"Action '{securityEvent.ActionType}' bypassed a tenant isolation boundary.",
                    cancellationToken).ConfigureAwait(false);
            }

            if (classification.Severity >= AuditRiskLevel.Critical)
            {
                await RaiseOrMergeAsync(context, securityEvent, classification,
                    SecurityAlertRules.CriticalEvent,
                    AuditRiskLevel.Critical,
                    "Critical security event recorded",
                    $"Action '{securityEvent.ActionType}' was classified with critical severity.",
                    cancellationToken).ConfigureAwait(false);
            }

            if (mutationCount > 0)
            {
                await EvaluatePermissionChangesAsync(context, securityEvent, classification, mutationCount, cancellationToken).ConfigureAwait(false);
            }

            if (classification.Kind == SecurityEventKind.Authentication && !securityEvent.Success)
            {
                await EvaluateFailedAuthenticationBurstAsync(context, securityEvent, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Alert evaluation must never block the durable capture of the security event itself.
            logger.LogError(
                exception,
                "Security alert evaluation failed for action {ActionType}; the security event is still being captured",
                securityEvent.ActionType);
        }
    }

    private static readonly string[] PermissionMutationActions =
    [
        AuditActionTypes.PermissionGranted,
        AuditActionTypes.PermissionRevoked,
        AuditActionTypes.PermissionChanged
    ];

    private static bool IsPermissionMutation(string actionType) =>
        PermissionMutationActions.Contains(actionType, StringComparer.Ordinal);

    private async Task EvaluatePermissionChangesAsync(
        IApplicationDbContext context,
        AuditLog securityEvent,
        ClassifiedSecurityEvent classification,
        int changeCount,
        CancellationToken cancellationToken)
    {
        var bulkThreshold = Math.Clamp(options.BulkPermissionChangeAlertThreshold, 2, 100000);
        if (changeCount >= bulkThreshold)
        {
            await RaiseOrMergeAsync(context, securityEvent, classification,
                SecurityAlertRules.BulkPermissionChange, AuditRiskLevel.High,
                "Bulk permission change detected",
                $"{changeCount} permission records changed in one committed operation (threshold {bulkThreshold}).",
                cancellationToken).ConfigureAwait(false);
        }

        var actorId = securityEvent.UserId.GetValueOrDefault();
        var matchingActor = actorId != Guid.Empty;
        var matchingIp = !string.IsNullOrWhiteSpace(securityEvent.IpAddress);
        if (!matchingActor && !matchingIp)
        {
            return;
        }

        var now = SystemClock.UtcNow;
        var windowMinutes = Math.Clamp(options.PermissionChangeWindowMinutes, 1, 1440);
        var windowStart = now.AddMinutes(-windowMinutes);
        var burstThreshold = Math.Clamp(options.PermissionChangeAlertThreshold, 2, 1000);
        var recentCount = 1; // The current event has not been saved yet.
        var candidates = context.Set<AuditLog>().AsNoTracking()
            .Where(log => log.Success && log.ResourceType == "Permission"
                && log.TenantId == securityEvent.TenantId
                && PermissionMutationActions.Contains(log.ActionType)
                && log.Metadata != null
                && log.CreatedAt >= windowStart && log.CreatedAt <= now
                && (matchingActor && log.UserId == actorId
                    || !matchingActor && matchingIp
                        && (log.UserId == null || log.UserId == Guid.Empty)
                        && log.IpAddress == securityEvent.IpAddress))
            .Select(log => log.Metadata).AsAsyncEnumerable();
        // Stream until the threshold is established. Never count authorization decisions or
        // apply a row limit before verifying snapshot evidence, which could hide real changes.
        await foreach (var metadata in candidates.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (PermissionMutationEvidence.GetChangeCount(metadata) > 0)
            {
                recentCount++;
                if (recentCount >= burstThreshold)
                {
                    break;
                }
            }
        }

        if (recentCount >= burstThreshold)
        {
            await RaiseOrMergeAsync(context, securityEvent, classification,
                SecurityAlertRules.PermissionChangeBurst, AuditRiskLevel.High,
                "Permission change burst detected",
                $"At least {recentCount} successful permission mutation events for one actor in this tenant within {windowMinutes} minutes (threshold {burstThreshold}).",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task SerializePermissionEvaluationAsync(
        IApplicationDbContext context, AuditLog securityEvent, CancellationToken cancellationToken)
    {
        if (context is not DbContext db
            || db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return;
        }

        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Permission alert evaluation requires the owning PostgreSQL transaction.");
        }

        var actor = securityEvent.UserId.GetValueOrDefault();
        var subject = actor != Guid.Empty ? actor.ToString("D") : securityEvent.IpAddress ?? "unknown";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"permission-alert|{securityEvent.TenantId}|{subject}"));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task EvaluateFailedAuthenticationBurstAsync(
        IApplicationDbContext context,
        AuditLog securityEvent,
        CancellationToken cancellationToken)
    {
        var windowStart = SystemClock.UtcNow.AddMinutes(-Math.Max(1, options.FailedAuthenticationWindowMinutes));
        var matchingUser = securityEvent.UserId.HasValue;
        var matchingIp = !string.IsNullOrEmpty(securityEvent.IpAddress);

        if (!matchingUser && !matchingIp)
        {
            return;
        }

        var failures = await context.Set<AuditLog>()
            .AsNoTracking()
            .CountAsync(log =>
                log.Category == AuditCategory.Authentication
                && !log.Success
                && log.CreatedAt >= windowStart
                && (matchingUser && log.UserId == securityEvent.UserId!.Value
                    || matchingIp && log.IpAddress == securityEvent.IpAddress),
                cancellationToken)
            .ConfigureAwait(false);

        // The current event is in the change tracker, not yet in the database, so it is counted here.
        failures++;

        if (failures < Math.Max(2, options.FailedAuthenticationAlertThreshold))
        {
            return;
        }

        var subject = matchingUser
            ? $"user {securityEvent.UserId!.Value}"
            : $"IP address {securityEvent.IpAddress}";

        await RaiseOrMergeAsync(context, securityEvent,
            SecurityEventTaxonomy.Classify(securityEvent.ActionType, securityEvent.Category, securityEvent.Success, securityEvent.RiskLevel),
            SecurityAlertRules.FailedAuthenticationBurst,
            AuditRiskLevel.High,
            "Failed authentication burst detected",
            $"{failures} failed authentication attempts for {subject} within {options.FailedAuthenticationWindowMinutes} minutes (threshold {options.FailedAuthenticationAlertThreshold}).",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task RaiseOrMergeAsync(
        IApplicationDbContext context,
        AuditLog securityEvent,
        ClassifiedSecurityEvent classification,
        string ruleId,
        AuditRiskLevel severity,
        string title,
        string description,
        CancellationToken cancellationToken)
    {
        var now = SystemClock.UtcNow;
        var deduplicationKey = SecurityAlert.BuildDeduplicationKey(ruleId, securityEvent.TenantId, securityEvent.UserId, securityEvent.IpAddress);

        var openAlert = await context.Set<SecurityAlert>()
            .FirstOrDefaultAsync(alert => alert.DeduplicationKey == deduplicationKey && alert.Status == SecurityAlertStatus.Open, cancellationToken)
            .ConfigureAwait(false);

        if (openAlert is not null)
        {
            openAlert.RecordOccurrence(now, securityEvent.Id);
            return;
        }

        context.Set<SecurityAlert>().Add(SecurityAlert.Raise(
            securityEvent.TenantId,
            ruleId,
            classification.Kind,
            severity,
            title,
            description,
            securityEvent.ActionType,
            securityEvent.Id,
            securityEvent.UserId,
            securityEvent.IpAddress,
            now));
    }
}
