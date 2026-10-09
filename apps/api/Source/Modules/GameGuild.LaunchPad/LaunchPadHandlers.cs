using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Authorization;
using GameGuild.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.LaunchPad;

public sealed class LaunchPadHandlers(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    IProjectChannelAvailabilityService availabilityService,
    IProjectAuthorizationService authorizationService,
    ILogger<LaunchPadHandlers> logger,
    IProjectLifecycleLock? lifecycleLock = null)
    : ICommandHandler<CreateLaunchPlanCommand, Result<LaunchPlan>>,
      ICommandHandler<CompleteLaunchChecklistItemCommand, Result<LaunchPlan>>,
      ICommandHandler<PublishLaunchCommand, Result<LaunchPlan>>,
      IQueryHandler<GetLaunchPlanQuery, Result<LaunchPlan?>>,
      IQueryHandler<GetLaunchPlanByProjectQuery, Result<LaunchPlan?>>,
      IQueryHandler<GetLaunchPadDashboardQuery, Result<IReadOnlyList<LaunchPlan>>>
{
    private readonly IProjectLifecycleLock _lifecycleLock = lifecycleLock ?? new ProjectLifecycleLock(context);

    public async Task<Result<LaunchPlan>> Handle(CreateLaunchPlanCommand request, CancellationToken cancellationToken)
    {
        await using var lockHandle = await _lifecycleLock.AcquireAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
        var actorError = await ValidateActorAsync(cancellationToken).ConfigureAwait(false);
        if (actorError != null)
        {
            return Result.Failure<LaunchPlan>(actorError);
        }

        var actor = actorContextAccessor.ActorContext;
        var availability = await availabilityService
            .GetAsync(request.ProjectId, ProjectChannel.LaunchPad, actor.TenantId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!availability.IsAvailable)
        {
            return Result.Failure<LaunchPlan>(AvailabilityError(availability));
        }

        if (!await authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<LaunchPlan>(Error.Forbidden("LaunchPad.ProjectForbidden", "Project Edit permission is required."));
        }

        var existing = await context.Set<LaunchPlan>()
            .Include(plan => plan.ChecklistItems)
            .FirstOrDefaultAsync(plan => plan.ProjectId == request.ProjectId && plan.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (existing != null)
        {
            return Result.Failure<LaunchPlan>(Error.Conflict("LaunchPad.PlanExists", "A launch plan already exists for this project."));
        }

        var plan = new LaunchPlan
        {
            ProjectId = request.ProjectId,
            Name = request.Name.Trim(),
            Positioning = request.Positioning?.Trim(),
            TargetLaunchAt = request.TargetLaunchAt,
            Channels = NormalizeChannels(request.Channels),
            TenantId = actor.TenantId
        };

        foreach (var item in request.ChecklistItems)
        {
            var checklistItem = new LaunchChecklistItem
            {
                Title = item.Title.Trim(),
                Category = string.IsNullOrWhiteSpace(item.Category) ? "Readiness" : item.Category.Trim(),
                IsRequired = item.IsRequired,
                IsComplete = item.IsComplete,
                CompletedAt = item.IsComplete ? SystemClock.UtcNow : null
            };
            plan.ChecklistItems.Add(checklistItem);
        }

        plan.RecalculateStatus();
        context.Set<LaunchPlan>().Add(plan);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await lockHandle.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Actor {ActorId} created launch plan {LaunchPlanId} for project {ProjectId}",
            actorContextAccessor.ActorContext.SubjectId,
            plan.Id,
            plan.ProjectId);

        return Result.Success(plan);
    }

    public async Task<Result<LaunchPlan>> Handle(CompleteLaunchChecklistItemCommand request, CancellationToken cancellationToken)
    {
        var actorError = await ValidateActorAsync(cancellationToken).ConfigureAwait(false);
        if (actorError != null)
        {
            return Result.Failure<LaunchPlan>(actorError);
        }

        var plan = await LoadPlan(request.LaunchPlanId, cancellationToken).ConfigureAwait(false);
        if (plan == null)
        {
            return Result.Failure<LaunchPlan>(Error.NotFound("LaunchPad.PlanNotFound", "Launch plan not found."));
        }

        var accessError = await AuthorizePlanProjectAsync(plan, PermissionType.Edit, cancellationToken).ConfigureAwait(false);
        if (accessError != null)
        {
            return Result.Failure<LaunchPlan>(accessError);
        }

        var item = plan.ChecklistItems.FirstOrDefault(candidate => candidate.Id == request.ChecklistItemId);
        if (item == null)
        {
            return Result.Failure<LaunchPlan>(Error.NotFound("LaunchPad.ChecklistItemNotFound", "Checklist item not found."));
        }

        item.Complete();
        plan.RecalculateStatus();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(plan);
    }

    public async Task<Result<LaunchPlan>> Handle(PublishLaunchCommand request, CancellationToken cancellationToken)
    {
        var actorError = await ValidateActorAsync(cancellationToken).ConfigureAwait(false);
        if (actorError != null)
        {
            return Result.Failure<LaunchPlan>(actorError);
        }

        var plan = await LoadPlan(request.LaunchPlanId, cancellationToken).ConfigureAwait(false);
        if (plan == null)
        {
            return Result.Failure<LaunchPlan>(Error.NotFound("LaunchPad.PlanNotFound", "Launch plan not found."));
        }

        var accessError = await AuthorizePlanProjectAsync(plan, PermissionType.Publish, cancellationToken).ConfigureAwait(false);
        if (accessError != null)
        {
            return Result.Failure<LaunchPlan>(accessError);
        }

        var actor = actorContextAccessor.ActorContext;
        var availability = await availabilityService
            .GetAsync(plan.ProjectId, ProjectChannel.LaunchPad, actor.TenantId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!availability.IsAvailable)
        {
            return Result.Failure<LaunchPlan>(AvailabilityError(availability));
        }

        try
        {
            plan.Publish();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<LaunchPlan>(Error.Validation("LaunchPad.NotReady", ex.Message));
        }

        var project = await context.Set<Project>()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == plan.ProjectId &&
                candidate.DeletedAt == null &&
                candidate.TenantId == actor.TenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (project == null)
        {
            return Result.Failure<LaunchPlan>(Error.Validation("LaunchPad.ProjectUnavailable", ProjectChannelReasonCodes.ProjectNotFound));
        }

        project.Status = ContentStatus.Published;
        project.Visibility = ContentVisibility.Public;
        project.PublishedAt ??= SystemClock.UtcNow;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(plan);
    }

    public async Task<Result<LaunchPlan?>> Handle(GetLaunchPlanQuery request, CancellationToken cancellationToken)
    {
        var actorError = await ValidateActorAsync(cancellationToken).ConfigureAwait(false);
        if (actorError != null)
        {
            return Result.Failure<LaunchPlan?>(actorError);
        }

        var plan = await LoadPlan(request.LaunchPlanId, cancellationToken).ConfigureAwait(false);
        if (plan == null)
        {
            return Result.Success<LaunchPlan?>(null);
        }

        var accessError = await AuthorizePlanProjectAsync(plan, PermissionType.Read, cancellationToken).ConfigureAwait(false);
        if (accessError != null)
        {
            return Result.Failure<LaunchPlan?>(accessError);
        }

        return Result.Success<LaunchPlan?>(plan);
    }

    public async Task<Result<LaunchPlan?>> Handle(GetLaunchPlanByProjectQuery request, CancellationToken cancellationToken)
    {
        var actorError = await ValidateActorAsync(cancellationToken).ConfigureAwait(false);
        if (actorError != null)
        {
            return Result.Failure<LaunchPlan?>(actorError);
        }

        var plan = await context.Set<LaunchPlan>()
            .AsNoTracking()
            .Include(plan => plan.Project)
            .Include(plan => plan.ChecklistItems)
            .FirstOrDefaultAsync(plan =>
                plan.ProjectId == request.ProjectId &&
                plan.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (plan == null)
        {
            return Result.Success<LaunchPlan?>(null);
        }

        var accessError = await AuthorizePlanProjectAsync(plan, PermissionType.Read, cancellationToken).ConfigureAwait(false);
        if (accessError != null)
        {
            return Result.Failure<LaunchPlan?>(accessError);
        }

        return Result.Success<LaunchPlan?>(plan);
    }

    public async Task<Result<IReadOnlyList<LaunchPlan>>> Handle(GetLaunchPadDashboardQuery request, CancellationToken cancellationToken)
    {
        var actorError = await ValidateActorAsync(cancellationToken).ConfigureAwait(false);
        if (actorError != null)
        {
            return Result.Failure<IReadOnlyList<LaunchPlan>>(actorError);
        }

        var actor = actorContextAccessor.ActorContext;
        var actorId = actor.SubjectIdAsGuid!.Value;
        var tenantId = actor.TenantId!.Value;
        var query = context.Set<LaunchPlan>()
            .AsNoTracking()
            .Include(plan => plan.Project)
            .Include(plan => plan.ChecklistItems)
            .Where(plan =>
                plan.DeletedAt == null &&
                plan.Project.DeletedAt == null &&
                plan.Project.TenantId == tenantId &&
                plan.Project.Status != ContentStatus.Archived &&
                plan.Project.Status != ContentStatus.Deleted);

        if (request.Status.HasValue)
        {
            query = query.Where(plan => plan.Status == request.Status.Value);
        }

        var plans = await query
            .OrderBy(plan => plan.TargetLaunchAt ?? DateTime.MaxValue)
            .ThenBy(plan => plan.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (plans.Count == 0)
        {
            return Result.Success<IReadOnlyList<LaunchPlan>>(plans);
        }

        var projectIds = plans.Select(plan => plan.ProjectId).Distinct().ToArray();
        var collaborators = await context.Set<ProjectCollaborator>()
            .AsNoTracking()
            .Where(collaborator =>
                projectIds.Contains(collaborator.ProjectId) &&
                collaborator.UserId == actorId &&
                collaborator.IsActive &&
                collaborator.DeletedAt == null)
            .Select(collaborator => new { collaborator.ProjectId, collaborator.Role, collaborator.Permissions })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var authorizedProjectIds = plans
            .Where(plan => plan.Project.CreatedById == actorId)
            .Select(plan => plan.ProjectId)
            .ToHashSet();
        foreach (var collaborator in collaborators)
        {
            if (string.Equals(collaborator.Role, ProjectRoles.Owner, StringComparison.OrdinalIgnoreCase) ||
                HasPermission(collaborator.Permissions, PermissionType.Read))
            {
                authorizedProjectIds.Add(collaborator.ProjectId);
            }
        }

        plans = plans.Where(plan => authorizedProjectIds.Contains(plan.ProjectId)).ToList();

        return Result.Success<IReadOnlyList<LaunchPlan>>(plans);
    }

    private async Task<LaunchPlan?> LoadPlan(Guid launchPlanId, CancellationToken cancellationToken)
        => await context.Set<LaunchPlan>()
            .Include(plan => plan.Project)
            .Include(plan => plan.ChecklistItems)
            .FirstOrDefaultAsync(plan => plan.Id == launchPlanId && plan.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);

    private async Task<Error?> ValidateActorAsync(CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid == null || actor.TenantId == null)
        {
            return Error.Unauthorized("LaunchPad.Unauthenticated", "An authenticated tenant actor is required.");
        }

        return await authorizationService.IsActorActiveTenantMemberAsync(cancellationToken).ConfigureAwait(false)
            ? null
            : Error.Unauthorized("LaunchPad.InactiveActor", "An active user and tenant membership are required.");
    }

    private async Task<Error?> AuthorizePlanProjectAsync(LaunchPlan plan, PermissionType permission, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (plan.Project.TenantId != actor.TenantId)
        {
            return Error.Forbidden("LaunchPad.ProjectTenantMismatch", "Launch plan is outside the current tenant.");
        }

        var availability = await availabilityService
            .GetAsync(plan.ProjectId, ProjectChannel.LaunchPad, actor.TenantId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!availability.IsAvailable)
        {
            return AvailabilityError(availability);
        }

        if (!await authorizationService.HasPermissionAsync(plan.ProjectId, permission, cancellationToken).ConfigureAwait(false))
        {
            return Error.Forbidden("LaunchPad.ProjectForbidden", $"Project {permission} permission is required.");
        }

        return null;
    }

    private static Error AvailabilityError(ProjectChannelAvailability availability)
        => availability.Reason == ProjectChannelReasonCodes.ProjectNotFound
            ? Error.NotFound("LaunchPad.ProjectNotFound", "Project not found.")
            : availability.Reason == ProjectChannelReasonCodes.TenantMismatch
                ? Error.Forbidden("LaunchPad.ProjectTenantMismatch", "Project is outside the current tenant.")
                : Error.Validation("LaunchPad.ProjectUnavailable", availability.Reason);

    private static string[] NormalizeChannels(IEnumerable<string> channels)
        => channels
            .Select(channel => channel.Trim().ToLowerInvariant())
            .Where(channel => channel.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(channel => channel, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool HasPermission(string permissions, PermissionType permission)
        => permissions
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(permission.ToString(), StringComparer.OrdinalIgnoreCase);
}
