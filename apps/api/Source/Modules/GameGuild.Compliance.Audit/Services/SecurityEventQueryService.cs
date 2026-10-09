using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Compliance.Audit;

public interface ISecurityEventQueryService
{
    SecurityEventTaxonomyResponse GetTaxonomy();

    Task<SecurityEventDeliveryStatusResponse> GetDeliveryStatusAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityAlertResponse>> GetAlertsAsync(SecurityAlertListRequest request, CancellationToken cancellationToken = default);

    Task<SecurityAlertResponse?> AcknowledgeAlertAsync(Guid alertId, string? notes, CancellationToken cancellationToken = default);
}

/// <summary>
///     Read side of the security event pipeline: the published taxonomy, the durable delivery status,
///     and the security alert queue with acknowledgement.
/// </summary>
public sealed class SecurityEventQueryService(
    IActorContextAccessor actors,
    ISecurityEventSpool spool,
    ISecurityEventPipelineStatusTracker statusTracker,
    IServiceScopeFactory scopeFactory,
    IOptions<SecurityEventPipelineOptions> options,
    ILogger<SecurityEventQueryService> logger) : ISecurityEventQueryService
{
    private readonly SecurityEventPipelineOptions pipelineOptions = options.Value;

    public SecurityEventTaxonomyResponse GetTaxonomy() =>
        new()
        {
            Entries = SecurityEventTaxonomy.GetTaxonomy(),
            TotalEntries = SecurityEventTaxonomy.GetTaxonomy().Count,
            Kinds = Enum.GetNames<SecurityEventKind>()
        };

    public Task<SecurityEventDeliveryStatusResponse> GetDeliveryStatusAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var stats = spool.GetStats();
        return Task.FromResult(new SecurityEventDeliveryStatusResponse
        {
            SpooledEventCount = stats.PendingCount,
            OldestSpooledEventUtc = stats.OldestOccurredAtUtc,
            LastDrainAttemptedAtUtc = statusTracker.LastDrainAttemptedAtUtc,
            LastDrainSucceededAtUtc = statusTracker.LastDrainSucceededAtUtc,
            LastDrainError = statusTracker.LastDrainError,
            DatabaseWriteAttemptsBeforeSpool = pipelineOptions.DatabaseWriteAttempts,
            SpoolingEnabled = pipelineOptions.SpoolingEnabled
        });
    }

    public async Task<IReadOnlyList<SecurityAlertResponse>> GetAlertsAsync(SecurityAlertListRequest request, CancellationToken cancellationToken = default)
    {
        var (tenant, _) = await RequireAdministratorAsync().ConfigureAwait(false);
        if (request.Skip < 0 || request.Take is < 1 or > 100)
        {
            throw new SecurityLogRetentionValidationException(new Dictionary<string, string[]> { ["Pagination"] = ["Use skip >= 0 and 1 <= take <= 100."] });
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        // The alert queue is tenant-scoped: the tenant comes from the actor context, never the route.
        var query = context.Set<SecurityAlert>().AsNoTracking()
            .Where(alert => alert.TenantId == tenant);
        if (request.Status.HasValue)
        {
            query = query.Where(alert => alert.Status == request.Status.Value);
        }

        if (request.MinimumSeverity.HasValue)
        {
            query = query.Where(alert => alert.Severity >= request.MinimumSeverity.Value);
        }

        if (request.Kind.HasValue)
        {
            query = query.Where(alert => alert.Kind == request.Kind.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.RuleId))
        {
            query = query.Where(alert => alert.RuleId == request.RuleId);
        }

        if (request.SubjectUserId.HasValue)
        {
            query = query.Where(alert => alert.SubjectUserId == request.SubjectUserId.Value);
        }

        var alerts = await query
            .OrderByDescending(alert => alert.Severity)
            .ThenByDescending(alert => alert.LastSeenAtUtc)
            .ThenByDescending(alert => alert.Id)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return alerts.Select(MapAlert).ToArray();
    }

    public async Task<SecurityAlertResponse?> AcknowledgeAlertAsync(Guid alertId, string? notes, CancellationToken cancellationToken = default)
    {
        var (tenant, user) = await RequireAdministratorAsync().ConfigureAwait(false);

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var alert = await context.Set<SecurityAlert>()
            .FirstOrDefaultAsync(item => item.Id == alertId && item.TenantId == tenant, cancellationToken)
            .ConfigureAwait(false);
        if (alert is null)
        {
            return null;
        }

        alert.Acknowledge(user, notes, SystemClock.UtcNow);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Security alert {AlertId} acknowledged by user {UserId}", alertId, user);
        return MapAlert(alert);
    }

    private async Task<(Guid Tenant, Guid User)> RequireAdministratorAsync()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is null || actor.TenantId == Guid.Empty ||
            actor.SubjectIdAsGuid is null || actor.SubjectIdAsGuid == Guid.Empty)
        {
            logger.LogWarning("Security event query access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return (actor.TenantId.Value, actor.SubjectIdAsGuid.Value);
    }

    private static SecurityAlertResponse MapAlert(SecurityAlert alert) =>
        new(alert.Id, alert.TenantId, alert.RuleId, alert.Kind, alert.Severity, alert.Title, alert.Description,
            alert.SourceActionType, alert.SourceAuditLogId, alert.SubjectUserId, alert.IpAddress, alert.Status,
            alert.OccurrenceCount, alert.FirstSeenAtUtc, alert.LastSeenAtUtc, alert.AcknowledgedByUserId,
            alert.AcknowledgedAtUtc, alert.AcknowledgementNotes);
}
