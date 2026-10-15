using System.Text.Json;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;

// The host's own GameGuild.API.Program shadows the course entity inside this namespace.
using CourseProgram = GameGuild.Learning.Courses.Program;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GameGuild.API.Core.Compliance;

/// <summary>
/// Covers every learning-course mutation command with a central audit event. The courses module
/// persists through dozens of command handlers and write services on the shared context, so the
/// emission point is the shared save itself: each save that mutates an audited course entity also
/// adds the matching central <see cref="AuditLog"/> row, and both land in the same save batch. The
/// batch is one transaction, so an audit-row failure fails the whole save — the mutation cannot
/// commit without its audit event (fail closed). Commands added later are covered automatically.
/// This bridge lives in the host composition root because the audit module cannot reference
/// Learning.Courses (that would close a project-reference cycle through Commerce.Products).
/// </summary>
public sealed class LearningCoursesMutationAuditInterceptor(
    IActorContextAccessor actors,
    IHttpContextAccessor? httpContextAccessor = null) : SaveChangesInterceptor
{
    /// <summary>Course entities whose mutations are audited, mapped to their audit resource type.</summary>
    private static readonly IReadOnlyDictionary<Type, string> AuditedEntityTypes = new Dictionary<Type, string>
    {
        [typeof(CourseProgram)] = "Program",
        [typeof(ProgramContent)] = "ProgramContent",
        [typeof(ProgramUser)] = "ProgramUser",
        [typeof(ProgramEnrollment)] = "ProgramEnrollment",
        [typeof(ProgramRating)] = "ProgramRating",
        [typeof(ProgramWishlist)] = "ProgramWishlist",
        [typeof(ProgramTag)] = "ProgramTag",
        [typeof(CoursePrerequisite)] = "CoursePrerequisite",
        [typeof(ProductProgram)] = "ProductProgram",
        [typeof(ContentInteraction)] = "ContentInteraction",
        [typeof(ActivityGrade)] = "ActivityGrade"
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        EmitAuditRows(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EmitAuditRows(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void EmitAuditRows(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Acting identity and tenant come from the ambient actor context, never from the payload.
        var actor = actors.ActorContext;
        var httpContext = httpContextAccessor?.HttpContext;
        // Materialize first: adding AuditLog rows below mutates the change tracker,
        // which would otherwise invalidate the enumeration mid-loop.
        var entries = context.ChangeTracker.Entries().ToList();
        foreach (var entry in entries)
        {
            if (entry.Entity is AuditLog)
            {
                continue;
            }

            if (!AuditedEntityTypes.TryGetValue(entry.Entity.GetType(), out var resourceType))
            {
                continue;
            }

            var actionSuffix = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted => "Deleted",
                _ => null
            };
            if (actionSuffix is null)
            {
                continue;
            }

            var entityTenantId = entry.Entity is GameGuild.CQRS.ITenantScoped tenantScoped ? tenantScoped.TenantId : null;
            var resourceId = entry.Properties.FirstOrDefault(property => property.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();
            // A failure while building or adding the audit row throws and fails the owning save,
            // rolling the mutation back with it.
            context.Set<AuditLog>().Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActionType = $"Learning.Courses.{resourceType}{actionSuffix}",
                ResourceType = resourceType,
                ResourceId = resourceId,
                UserId = actor.SubjectIdAsGuid,
                TenantId = entityTenantId ?? actor.TenantId,
                IpAddress = ClientIpAddress(httpContext),
                UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
                SessionId = SessionId(httpContext),
                Description = $"Course {resourceType.ToLowerInvariant()} {actionSuffix.ToLowerInvariant()} through the learning module",
                Metadata = JsonSerializer.Serialize(new { State = actionSuffix }),
                Success = true,
                RiskLevel = AuditRiskLevel.Low,
                Category = AuditCategory.Data,
                CorrelationId = httpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            });
        }
    }

    private static string? ClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static Guid? SessionId(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        var sessionIdValue = httpContext.User.FindFirst("session_id")?.Value;
        return Guid.TryParse(sessionIdValue, out var sessionId) ? sessionId : null;
    }
}
