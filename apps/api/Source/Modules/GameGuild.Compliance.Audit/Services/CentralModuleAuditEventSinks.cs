using GameGuild.Content.Pages;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning;
using GameGuild.Teams;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Persists a required module audit event through the caller's command context. The audit row is
/// added to the same <see cref="IApplicationDbContext"/> the mutation used and saved inside the
/// caller's transaction, so a persistence failure propagates and rolls the mutation back. This is
/// the module-mutation counterpart of <see cref="SecurityEventLogger.RecordInCommandAsync"/>: the
/// event is not required to be a classified security event, but it is required to be durable.
/// </summary>
internal static class RequiredModuleAuditRecorder
{
    public static async Task RecordAsync(
        IApplicationDbContext context,
        IActorContextAccessor actors,
        HttpContext? httpContext,
        CreateAuditLogRequest request,
        string moduleAuditSource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        // Acting identity and tenant come from the ambient actor context, never from the payload.
        var actor = actors.ActorContext;
        request.UserId ??= actor.SubjectIdAsGuid;
        request.TenantId ??= actor.TenantId;

        // Fail closed: without the owning transaction the mutation and the audit row could commit
        // independently, and an audit failure would no longer roll the mutation back.
        if (context is DbContext relational && relational.Database.IsRelational() && relational.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                $"Required {moduleAuditSource} audit event needs the owning command transaction; refusing to record it independently.");
        }

        var auditLog = AuditLogEntryFactory.Create(request, httpContext);
        context.Set<AuditLog>().Add(auditLog);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Bridges content-page mutation events to the central audit log. Content mutations are required
/// audit events: they are recorded in the owning command's transaction and fail closed.
/// </summary>
public sealed class CentralContentPagesAuditEventSink(
    IApplicationDbContext context,
    IActorContextAccessor actors,
    IHttpContextAccessor httpContextAccessor) : IContentPagesAuditEventSink
{
    public Task RecordAsync(ContentPagesAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return RequiredModuleAuditRecorder.RecordAsync(
            context,
            actors,
            httpContextAccessor.HttpContext,
            new CreateAuditLogRequest
            {
                ActionType = auditEvent.ActionType,
                ResourceType = auditEvent.ResourceType,
                ResourceId = auditEvent.ResourceId,
                UserId = auditEvent.ActorUserId,
                TenantId = auditEvent.TenantId,
                Description = auditEvent.Description,
                Metadata = auditEvent.Metadata,
                Success = auditEvent.Success,
                ErrorMessage = auditEvent.ErrorMessage,
                RiskLevel = auditEvent.Success ? AuditRiskLevel.Low : AuditRiskLevel.Medium,
                Category = AuditCategory.Data
            },
            "content.pages",
            cancellationToken);
    }
}

/// <summary>
/// Bridges learning mutation events (courses, enrollments, certificates) to the central audit
/// log. Learning mutations are required audit events: they are recorded in the owning command's
/// transaction and fail closed.
/// </summary>
public sealed class CentralLearningAuditEventSink(
    IApplicationDbContext context,
    IActorContextAccessor actors,
    IHttpContextAccessor httpContextAccessor) : ILearningAuditEventSink
{
    public Task RecordAsync(LearningAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return RequiredModuleAuditRecorder.RecordAsync(
            context,
            actors,
            httpContextAccessor.HttpContext,
            new CreateAuditLogRequest
            {
                ActionType = auditEvent.ActionType,
                ResourceType = auditEvent.ResourceType,
                ResourceId = auditEvent.ResourceId,
                UserId = auditEvent.ActorUserId,
                TenantId = auditEvent.TenantId,
                Description = auditEvent.Description,
                Metadata = auditEvent.Metadata,
                Success = auditEvent.Success,
                ErrorMessage = auditEvent.ErrorMessage,
                RiskLevel = auditEvent.Success ? AuditRiskLevel.Low : AuditRiskLevel.Medium,
                Category = AuditCategory.Data
            },
            "learning",
            cancellationToken);
    }
}

/// <summary>
/// Bridges team mutation events to the central audit log. Team mutations are required audit
/// events: they are recorded in the owning command's transaction and fail closed.
/// </summary>
public sealed class CentralTeamsAuditEventSink(
    IApplicationDbContext context,
    IActorContextAccessor actors,
    IHttpContextAccessor httpContextAccessor) : ITeamsAuditEventSink
{
    public Task RecordAsync(TeamsAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return RequiredModuleAuditRecorder.RecordAsync(
            context,
            actors,
            httpContextAccessor.HttpContext,
            new CreateAuditLogRequest
            {
                ActionType = auditEvent.ActionType,
                ResourceType = auditEvent.ResourceType,
                ResourceId = auditEvent.ResourceId,
                UserId = auditEvent.ActorUserId,
                TenantId = auditEvent.TenantId,
                Description = auditEvent.Description,
                Metadata = auditEvent.Metadata,
                Success = auditEvent.Success,
                ErrorMessage = auditEvent.ErrorMessage,
                RiskLevel = auditEvent.Success ? AuditRiskLevel.Low : AuditRiskLevel.Medium,
                Category = AuditCategory.Data
            },
            "teams",
            cancellationToken);
    }
}
