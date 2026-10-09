using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.TestingLab;

public sealed class SessionProjectHandlers(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    IProjectChannelAvailabilityService availabilityService,
    IProjectAuthorizationService authorizationService,
    ILogger<SessionProjectHandlers> logger,
    IProjectLifecycleLock? lifecycleLock = null)
    : ICommandHandler<LinkSessionProjectCommand, Result<SessionProjectProjection>>,
      ICommandHandler<UnlinkSessionProjectCommand, Result<bool>>,
      IQueryHandler<GetSessionProjectLinksQuery, Result<IReadOnlyList<SessionProjectProjection>>>
{
    private readonly IProjectLifecycleLock _lifecycleLock = lifecycleLock ?? new ProjectLifecycleLock(context);

    public async Task<Result<SessionProjectProjection>> Handle(LinkSessionProjectCommand request, CancellationToken cancellationToken)
    {
        await using var lockHandle = await _lifecycleLock.AcquireAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
        var authorization = await AuthorizeSessionAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null)
        {
            return Result.Failure<SessionProjectProjection>(authorization.Error);
        }

        var actor = actorContextAccessor.ActorContext;
        if (!await authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<SessionProjectProjection>(Error.NotFound("TestingLab.ProjectNotFound", "Project not found."));
        }

        var availability = await availabilityService
            .GetAsync(request.ProjectId, ProjectChannel.TestingLab, actor.TenantId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!availability.IsAvailable)
        {
            return Result.Failure<SessionProjectProjection>(Error.Validation("TestingLab.ProjectUnavailable", availability.Reason));
        }

        if (request.ProjectVersionId.HasValue)
        {
            var validVersion = await context.Set<ProjectVersion>()
                .AnyAsync(version =>
                    version.Id == request.ProjectVersionId.Value &&
                    version.ProjectId == request.ProjectId &&
                    version.TenantId == actor.TenantId &&
                    version.DeletedAt == null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!validVersion)
            {
                return Result.Failure<SessionProjectProjection>(Error.Validation("TestingLab.ProjectVersionMismatch", "Project version must be active and belong to the linked project."));
            }
        }

        var duplicate = await context.Set<SessionProject>()
            .AnyAsync(link =>
                link.SessionId == request.SessionId &&
                link.ProjectId == request.ProjectId &&
                link.IsActive &&
                link.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            return Result.Failure<SessionProjectProjection>(Error.Conflict("TestingLab.SessionProjectExists", "An active session-project link already exists."));
        }

        var link = new SessionProject
        {
            SessionId = request.SessionId,
            ProjectId = request.ProjectId,
            ProjectVersionId = request.ProjectVersionId,
            RegisteredById = actor.SubjectIdAsGuid!.Value,
            RegisteredAt = SystemClock.UtcNow,
            Notes = request.Notes?.Trim(),
            IsActive = true,
            TenantId = actor.TenantId
        };
        context.Set<SessionProject>().Add(link);
        authorization.Session!.RegisteredProjectCount++;
        authorization.Session.Touch();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await lockHandle.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Actor {ActorId} linked project {ProjectId} to testing session {SessionId}", actor.SubjectId, request.ProjectId, request.SessionId);
        return Result.Success(ToProjection(link));
    }

    public async Task<Result<bool>> Handle(UnlinkSessionProjectCommand request, CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeSessionAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null)
        {
            return Result.Failure<bool>(authorization.Error);
        }

        if (!await authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(Error.NotFound("TestingLab.ProjectNotFound", "Project not found."));
        }

        var link = await context.Set<SessionProject>()
            .FirstOrDefaultAsync(candidate =>
                candidate.SessionId == request.SessionId &&
                candidate.ProjectId == request.ProjectId &&
                candidate.IsActive &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (link == null)
        {
            return Result.Failure<bool>(Error.NotFound("TestingLab.SessionProjectNotFound", "Active session-project link not found."));
        }

        link.IsActive = false;
        link.DeletedAt = SystemClock.UtcNow;
        link.Touch();
        authorization.Session!.RegisteredProjectCount = Math.Max(0, authorization.Session.RegisteredProjectCount - 1);
        authorization.Session.Touch();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<IReadOnlyList<SessionProjectProjection>>> Handle(GetSessionProjectLinksQuery request, CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeSessionAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (authorization.Error != null)
        {
            return Result.Failure<IReadOnlyList<SessionProjectProjection>>(authorization.Error);
        }

        var tenantId = actorContextAccessor.ActorContext.TenantId!.Value;
        var query = context.Set<SessionProject>()
            .AsNoTracking()
            .Where(link =>
                link.SessionId == request.SessionId &&
                link.TenantId == tenantId &&
                (!link.IsActive ||
                 link.DeletedAt != null ||
                 (link.Project.DeletedAt == null &&
                  link.Project.TenantId == tenantId &&
                  link.Project.Status != ContentStatus.Archived &&
                  link.Project.Status != ContentStatus.Deleted &&
                  (!link.ProjectVersionId.HasValue ||
                   (link.ProjectVersion != null &&
                    link.ProjectVersion.ProjectId == link.ProjectId &&
                    link.ProjectVersion.TenantId == tenantId &&
                    link.ProjectVersion.DeletedAt == null)))));
        if (!request.IncludeInactive)
        {
            query = query.Where(link => link.IsActive && link.DeletedAt == null);
        }

        var links = await query
            .OrderBy(link => link.RegisteredAt)
            .Select(link => new SessionProjectProjection(link.Id, link.SessionId, link.ProjectId, link.ProjectVersionId, link.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<IReadOnlyList<SessionProjectProjection>>(links);
    }

    private async Task<SessionAuthorization> AuthorizeSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var actorId = actor.SubjectIdAsGuid;
        if (!actor.IsAuthenticated || actorId == null || actor.TenantId == null)
        {
            return new(null, Error.Unauthorized("TestingLab.Unauthenticated", "An authenticated tenant actor is required."));
        }

        if (!await authorizationService.IsActorActiveTenantMemberAsync(cancellationToken).ConfigureAwait(false))
        {
            return new(null, Error.Unauthorized("TestingLab.InactiveActor", "An active user and tenant membership are required."));
        }

        var session = await context.Set<TestingSession>()
            .FirstOrDefaultAsync(candidate => candidate.Id == sessionId && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (session == null)
        {
            return new(null, Error.NotFound("TestingLab.SessionNotFound", "Testing session not found."));
        }

        if (session.TenantId != actor.TenantId)
        {
            return new(null, Error.Forbidden("TestingLab.SessionTenantMismatch", "Testing session is outside the current tenant."));
        }

        var tenantRole = await context.Set<TenantMember>()
            .AsNoTracking()
            .Where(member =>
                member.UserId == actorId.Value &&
                member.TenantId == session.TenantId &&
                member.IsActive &&
                member.DeletedAt == null)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var isSessionTenantAdmin = tenantRole != null && TenantRole.FromString(tenantRole).IsAdmin;
        if (session.ManagerId != actorId && session.CreatedById != actorId &&
            !isSessionTenantAdmin && !actor.IsSystemAdmin)
        {
            return new(null, Error.Forbidden("TestingLab.SessionForbidden", "Session manager or creator access is required."));
        }

        return new(session, null);
    }

    private static SessionProjectProjection ToProjection(SessionProject link)
        => new(link.Id, link.SessionId, link.ProjectId, link.ProjectVersionId, link.IsActive);

    private sealed record SessionAuthorization(TestingSession? Session, Error? Error);
}
