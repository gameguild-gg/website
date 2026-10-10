using GameGuild.Assets;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Authorizes assets inherited from courses and individual learning content.
/// The authoritative tenant and ownership are always loaded from the learning aggregate.
/// </summary>
public sealed class LearningAssetParentAuthorizationResolver(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    IPermissionQueryService permissionQueryService) : IAssetParentAuthorizationResolver
{
    public bool Supports(string resourceType) =>
        resourceType.Equals(nameof(Program), StringComparison.OrdinalIgnoreCase) ||
        resourceType.Equals("Course", StringComparison.OrdinalIgnoreCase) ||
        resourceType.Equals("programs", StringComparison.OrdinalIgnoreCase) ||
        resourceType.Equals("courses", StringComparison.OrdinalIgnoreCase) ||
        resourceType.Equals(nameof(ProgramContent), StringComparison.OrdinalIgnoreCase) ||
        resourceType.Equals("lessons", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> CanReadAsync(
        Guid parentResourceId,
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!HasAuthoritativeActor(userId, tenantId))
        {
            return false;
        }

        var program = await ResolveProgramAsync(parentResourceId, cancellationToken).ConfigureAwait(false);
        if (program is null || program.TenantId != tenantId)
        {
            return false;
        }

        if (program.CreatorId == userId)
        {
            return true;
        }

        var enrolled = await context.Set<ProgramEnrollment>()
            .AsNoTracking()
            .AnyAsync(
                enrollment => enrollment.ProgramId == program.Id &&
                              enrollment.DeletedAt == null &&
                              enrollment.UserId == userId &&
                              enrollment.TenantId == tenantId &&
                              (enrollment.EnrollmentStatus == EnrollmentStatus.Active ||
                               enrollment.EnrollmentStatus == EnrollmentStatus.Completed),
                cancellationToken)
            .ConfigureAwait(false);
        if (enrolled)
        {
            return true;
        }

        return await HasAnyPermissionAsync(program.Id, userId, tenantId!.Value, cancellationToken,
            "Read", "Edit", "Review", "Publish").ConfigureAwait(false);
    }

    public async Task<bool> CanManageAsync(
        Guid parentResourceId,
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!HasAuthoritativeActor(userId, tenantId))
        {
            return false;
        }

        var program = await ResolveProgramAsync(parentResourceId, cancellationToken).ConfigureAwait(false);
        if (program is null || program.TenantId != tenantId)
        {
            return false;
        }

        if (program.CreatorId == userId)
        {
            return true;
        }

        return await HasAnyPermissionAsync(program.Id, userId, tenantId!.Value, cancellationToken,
            "Edit", "Publish").ConfigureAwait(false);
    }

    private bool HasAuthoritativeActor(Guid userId, Guid? tenantId)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated)
        {
            return false;
        }

        var actorUserId = actor.SubjectIdAsGuid;
        if (!actorUserId.HasValue)
        {
            return false;
        }

        if (actorUserId.Value != userId)
        {
            return false;
        }

        if (!tenantId.HasValue)
        {
            return false;
        }

        if (!actor.TenantId.HasValue)
        {
            return false;
        }

        return actor.TenantId.Value == tenantId.Value;
    }

    private async Task<Program?> ResolveProgramAsync(Guid parentResourceId, CancellationToken cancellationToken)
    {
        var programId = await context.Set<ProgramContent>()
            .AsNoTracking()
            .Where(content => content.Id == parentResourceId && content.DeletedAt == null)
            .Select(content => (Guid?)content.ProgramId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.Set<Program>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                program => program.Id == (programId ?? parentResourceId) && program.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> HasAnyPermissionAsync(
        Guid programId,
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken,
        params string[] permissions)
    {
        foreach (var permission in permissions)
        {
            if (await permissionQueryService.HasTenantPermissionAsync(
                    userId,
                    tenantId,
                    $"Program.{programId}.{permission}",
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }
}
