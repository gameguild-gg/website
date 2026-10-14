using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Covers every learning-course mutation command with a central audit event. The courses module
/// persists through dozens of command handlers and write services on the shared context, so the
/// emission point is the shared save itself: each save that mutates an audited course entity also
/// adds the matching audit row, and both land in the same save batch. The batch is one transaction,
/// so an audit-row failure fails the whole save — the mutation cannot commit without its audit
/// event (fail closed). This is the entity-level counterpart of the module audit sinks used by the
/// smaller modules; it automatically covers commands added later.
/// </summary>
public sealed class LearningCoursesMutationAuditInterceptor(
    IActorContextAccessor actors,
    IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private const string ModuleAuditSource = "learning.courses";

    /// <summary>Course entities whose mutations are audited, mapped to their audit resource type.</summary>
    private static readonly IReadOnlyDictionary<Type, string> AuditedEntityTypes = new Dictionary<Type, string>
    {
        [typeof(Program)] = "Program",
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
        var actorUserId = actor.SubjectIdAsGuid;
        var actorTenantId = actor.TenantId;
        foreach (var entry in context.ChangeTracker.Entries())
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

            var entityTenantId = entry.Entity is ITenantScoped tenantScoped ? tenantScoped.TenantId : null;
            var resourceId = entry.Properties.FirstOrDefault(property => property.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();
            var request = new CreateAuditLogRequest
            {
                ActionType = $"Learning.Courses.{resourceType}{actionSuffix}",
                ResourceType = resourceType,
                ResourceId = resourceId,
                UserId = actorUserId,
                TenantId = entityTenantId ?? actorTenantId,
                Description = $"Course {resourceType.ToLowerInvariant()} {actionSuffix.ToLowerInvariant()} through the learning module",
                Metadata = new { State = actionSuffix },
                RiskLevel = AuditRiskLevel.Low,
                Category = AuditCategory.Data
            };
            // AuditLogEntryFactory.Create is the central mapping used by every audit sink; a
            // failure here throws and fails the owning save, rolling the mutation back.
            context.Set<AuditLog>().Add(AuditLogEntryFactory.Create(request, httpContextAccessor.HttpContext));
        }
    }
}
