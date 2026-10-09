using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Compliance.Audit;

/// <summary>Outcome of capturing one security event through the durable pipeline.</summary>
public enum SecurityEventCaptureOutcome
{
    /// <summary>The event (and any alerts it raised) was persisted to the audit database.</summary>
    PersistedToDatabase = 0,

    /// <summary>The database was unavailable; the event was appended to the durable local spool for replay.</summary>
    SpooledLocally = 1,

    /// <summary>The database was unavailable and spooling is disabled; the event could not be captured durably.</summary>
    SpoolingDisabled = 2,

    /// <summary>The event is not security-relevant; it was delegated to the standard audit log path.</summary>
    NotSecurityRelevant = 3
}

/// <summary>Result of one security event capture.</summary>
public sealed record SecurityEventCaptureResult(
    SecurityEventCaptureOutcome Outcome,
    ClassifiedSecurityEvent Classification,
    Guid EventId,
    string? CaptureError = null);

/// <summary>
///     Durable capture path for security events. Security events (per <see cref="SecurityEventTaxonomy"/>)
///     are persisted with bounded retries; when the database is unavailable they are appended to a durable
///     local spool and replayed by a background drainer, so a database outage never silently drops a
///     security event. Alert rules are evaluated on the same context as the persisted event.
/// </summary>
public interface ISecurityEventLogger
{
    Task<SecurityEventCaptureResult> RecordAsync(CreateAuditLogRequest request, CancellationToken cancellationToken = default);

    /// <summary>Replays spooled security events. Returns the number of events delivered to the database.</summary>
    Task<int> ReplaySpooledEventsAsync(CancellationToken cancellationToken = default);
}

public sealed class SecurityEventLogger(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor,
    ISecurityEventSpool spool,
    ISecurityAlertRuleEvaluator alertEvaluator,
    IOptions<SecurityEventPipelineOptions> options,
    IAuditService fallbackAuditService,
    ILogger<SecurityEventLogger> logger) : ISecurityEventLogger
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SecurityEventPipelineOptions options = options.Value;

    public async Task<SecurityEventCaptureResult> RecordAsync(CreateAuditLogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var classification = SecurityEventTaxonomy.Classify(request.ActionType, request.Category, request.Success, request.RiskLevel);
        if (!classification.IsSecurityRelevant)
        {
            await fallbackAuditService.LogAsync(request).ConfigureAwait(false);
            return new SecurityEventCaptureResult(SecurityEventCaptureOutcome.NotSecurityRelevant, classification, Guid.NewGuid());
        }

        var eventId = Guid.NewGuid();
        var outcome = await PersistWithRetriesAsync(request, classification, eventId, cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    public async Task<int> ReplaySpooledEventsAsync(CancellationToken cancellationToken = default)
    {
        var records = spool.ReadAll();
        if (records.Count == 0)
        {
            return 0;
        }

        var delivered = new List<Guid>(records.Count);
        foreach (var record in records)
        {
            try
            {
                var request = JsonSerializer.Deserialize<CreateAuditLogRequest>(record.PayloadJson, PayloadJsonOptions);
                if (request is null)
                {
                    // Unparseable payloads cannot be replayed; keep them in the spool for operator inspection.
                    continue;
                }

                if (await AlreadyPersistedAsync(record.EventId, cancellationToken).ConfigureAwait(false))
                {
                    delivered.Add(record.EventId);
                    continue;
                }

                var classification = SecurityEventTaxonomy.Classify(request.ActionType, request.Category, request.Success, request.RiskLevel);
                var result = await PersistWithRetriesAsync(request, classification, record.EventId, cancellationToken).ConfigureAwait(false);
                if (result.Outcome == SecurityEventCaptureOutcome.PersistedToDatabase)
                {
                    delivered.Add(record.EventId);
                }
                else
                {
                    // The database is still unavailable; stop draining and retry on the next cycle.
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        if (delivered.Count > 0)
        {
            spool.Remove(delivered);
        }

        return delivered.Count;
    }

    private async Task<SecurityEventCaptureResult> PersistWithRetriesAsync(
        CreateAuditLogRequest request,
        ClassifiedSecurityEvent classification,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= Math.Max(1, options.DatabaseWriteAttempts); attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var auditLog = AuditLogEntryFactory.Create(request, httpContextAccessor.HttpContext, classification, eventId);

                context.Set<AuditLog>().Add(auditLog);
                await alertEvaluator.EvaluateAsync(context, auditLog, classification, cancellationToken).ConfigureAwait(false);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                logger.Log(
                    classification.Severity >= AuditRiskLevel.Critical ? LogLevel.Critical : LogLevel.Warning,
                    "Security event {ActionType} ({Kind}, severity {Severity}) captured for tenant {TenantId}",
                    request.ActionType,
                    classification.Kind,
                    classification.Severity,
                    request.TenantId);

                return new SecurityEventCaptureResult(SecurityEventCaptureOutcome.PersistedToDatabase, classification, eventId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException && attempt < Math.Max(1, options.DatabaseWriteAttempts))
            {
                lastError = exception;
                logger.LogWarning(
                    exception,
                    "Security event {ActionType} persistence attempt {Attempt} failed; retrying",
                    request.ActionType,
                    attempt);
                await DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastError = exception;
            }
        }

        if (options.SpoolingEnabled)
        {
            try
            {
                var payload = JsonSerializer.Serialize(request, PayloadJsonOptions);
                spool.Append(new SecurityEventSpoolRecord(eventId, SystemClock.UtcNow, payload));
                logger.LogError(
                    lastError,
                    "Security event {ActionType} could not be persisted after {Attempts} attempts; event {EventId} spooled locally for replay",
                    request.ActionType,
                    options.DatabaseWriteAttempts,
                    eventId);
                return new SecurityEventCaptureResult(SecurityEventCaptureOutcome.SpooledLocally, classification, eventId, lastError?.Message);
            }
            catch (Exception spoolException)
            {
                logger.LogCritical(
                    spoolException,
                    "Security event {ActionType} was NOT captured durably: persistence failed and spooling failed. Event payload: {Payload}",
                    request.ActionType,
                    JsonSerializer.Serialize(request, PayloadJsonOptions));
                return new SecurityEventCaptureResult(SecurityEventCaptureOutcome.SpoolingDisabled, classification, eventId, spoolException.Message);
            }
        }

        logger.LogCritical(
            lastError,
            "Security event {ActionType} was NOT captured durably: persistence failed and spooling is disabled. Event payload: {Payload}",
            request.ActionType,
            JsonSerializer.Serialize(request, PayloadJsonOptions));
        return new SecurityEventCaptureResult(SecurityEventCaptureOutcome.SpoolingDisabled, classification, eventId, lastError?.Message);
    }

    private async Task<bool> AlreadyPersistedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var identity = eventId;
        return await context.Set<AuditLog>().AsNoTracking().AnyAsync(log => log.Id == identity, cancellationToken).ConfigureAwait(false);
    }

    private async Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromMilliseconds(Math.Max(0, options.RetryDelayMilliseconds) * attempt);
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>Maps a <see cref="CreateAuditLogRequest"/> to a persisted <see cref="AuditLog"/> entry.</summary>
internal static class AuditLogEntryFactory
{
    internal static AuditLog Create(
        CreateAuditLogRequest request,
        HttpContext? httpContext,
        ClassifiedSecurityEvent? classification = null,
        Guid? eventId = null)
    {
        return new AuditLog
        {
            Id = eventId ?? Guid.NewGuid(),
            ActionType = request.ActionType,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            UserId = request.UserId,
            TenantId = request.TenantId,
            IpAddress = request.IpAddress ?? GetClientIpAddress(httpContext),
            UserAgent = request.UserAgent ?? httpContext?.Request.Headers.UserAgent.ToString(),
            SessionId = request.SessionId ?? GetSessionId(httpContext),
            Description = request.Description,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
            Success = request.Success,
            ErrorMessage = request.ErrorMessage,
            RiskLevel = classification?.Severity ?? request.RiskLevel,
            Category = request.Category,
            CorrelationId = request.CorrelationId ?? GetCorrelationId(httpContext)
        };
    }

    private static string? GetClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null) return null;

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor)) { return forwardedFor.Split(',')[0].Trim(); }

        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp)) { return realIp; }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static Guid? GetSessionId(HttpContext? httpContext)
    {
        if (httpContext == null) { return null; }
        var sessionIdValue = httpContext.User.FindFirst("session_id")?.Value;
        return Guid.TryParse(sessionIdValue, out var sessionId) ? sessionId : null;
    }

    private static string? GetCorrelationId(HttpContext? httpContext) => httpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault();
}
