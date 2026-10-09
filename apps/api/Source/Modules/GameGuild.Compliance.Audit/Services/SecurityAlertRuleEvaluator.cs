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
