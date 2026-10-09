using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Authorization;
using GameGuild.Projects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.TestingLab;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireTestingLabPermissionAttribute : TypeFilterAttribute
{
    public RequireTestingLabPermissionAttribute(
        string action,
        string resourceType,
        string? resourceIdParameterName = null)
        : base(typeof(TestingLabPermissionAuthorizationFilter))
    {
        Arguments = [action, resourceType, resourceIdParameterName ?? string.Empty];
    }
}

public sealed class TestingLabPermissionAuthorizationFilter(
    string action,
    string resourceType,
    string? resourceIdParameterName,
    IActorContextAccessor actorContextAccessor,
    ITestingLabPermissionService permissionService,
    IProjectAuthorizationService projectAuthorizationService,
    IApplicationDbContext dbContext) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (actor.TenantId == null)
        {
            context.Result = new ForbidResult();
            return;
        }

        if (!await TestingLabActorAccess.IsActiveTenantActorAsync(
                dbContext,
                actor,
                context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        Guid? resourceId = null;
        var isResourceOwner = false;
        if (!string.IsNullOrWhiteSpace(resourceIdParameterName))
        {
            resourceId = ResolveResourceId(context, resourceIdParameterName);
            if (!resourceId.HasValue)
            {
                context.Result = new ForbidResult();
                return;
            }

            var scope = await ResolveResourceScopeAsync(
                resourceType,
                resourceId.Value,
                actor.TenantId.Value,
                actor.SubjectIdAsGuid.Value,
                context.HttpContext.RequestAborted).ConfigureAwait(false);
            if (!scope.Exists)
            {
                context.Result = new ForbidResult();
                return;
            }
            isResourceOwner = scope.IsOwner;
        }
        else if (resourceType == TestingLabResourceTypes.Event && action == TestingLabActions.Read)
        {
            var actorId = actor.SubjectIdAsGuid.Value;
            var tenantId = actor.TenantId.Value;
            isResourceOwner = await dbContext.Set<TestingEvent>()
                .IgnoreQueryFilters()
                .AnyAsync(
                    item => item.TenantId == tenantId && item.ManagerUserId == actorId,
                    context.HttpContext.RequestAborted)
                .ConfigureAwait(false);
        }

        if (actor.IsSystemAdmin || actor.IsTenantAdmin || isResourceOwner)
        {
            return;
        }

        var allowed = await permissionService.HasPermissionAsync(
                actor.SubjectIdAsGuid.Value,
                actor.TenantId,
                action,
                resourceType,
                resourceId)
            .ConfigureAwait(false);
        if (!allowed)
        {
            context.Result = new ForbidResult();
        }
    }

    private async Task<ResourceScope> ResolveResourceScopeAsync(
        string type,
        Guid resourceId,
        Guid tenantId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (type == TestingLabResourceTypes.Event)
        {
            var resource = await dbContext.Set<TestingEvent>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == resourceId && item.TenantId == tenantId)
                .Select(item => new { item.ManagerUserId })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            return new(resource != null, resource?.ManagerUserId == actorId);
        }
        if (type == TestingLabResourceTypes.Application)
        {
            var resource = await dbContext.Set<TestingProjectApplication>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == resourceId && item.TenantId == tenantId)
                .Select(item => new { item.ProjectId })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (resource == null)
            {
                return new(false, false);
            }

            var requiredPermission = action == TestingLabActions.Read ? PermissionType.Read : PermissionType.Edit;
            var projectAccess = await projectAuthorizationService.HasPermissionAsync(
                resource.ProjectId,
                requiredPermission,
                cancellationToken).ConfigureAwait(false);
            return new(true, projectAccess);
        }
        if (type == TestingLabResourceTypes.Request)
        {
            var resource = await dbContext.Set<TestingRequest>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == resourceId && item.TenantId == tenantId)
                .Select(item => new { item.CreatedById })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            return new(resource != null, resource?.CreatedById == actorId);
        }
        if (type == TestingLabResourceTypes.Session)
        {
            var resource = await dbContext.Set<TestingSession>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == resourceId && item.TenantId == tenantId)
                .Select(item => new { item.ManagerId, item.CreatedById })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            return new(resource != null, resource?.ManagerId == actorId || resource?.CreatedById == actorId);
        }
        if (type == TestingLabResourceTypes.Location)
        {
            return new(await dbContext.Set<TestingLocation>().IgnoreQueryFilters().AnyAsync(
                item => item.Id == resourceId && item.TenantId == tenantId,
                cancellationToken).ConfigureAwait(false), false);
        }

        if (type == TestingLabResourceTypes.Feedback)
        {
            var resource = await dbContext.Set<TestingFeedback>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == resourceId && item.TenantId == tenantId)
                .Select(item => new { item.UserId })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            return new(resource != null, action == TestingLabActions.Read && resource?.UserId == actorId);
        }
        if (type == TestingLabResourceTypes.Participant)
        {
            var resource = await dbContext.Set<TestingParticipant>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == resourceId && item.TenantId == tenantId)
                .Select(item => new { item.UserId })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            return new(resource != null, action == TestingLabActions.Read && resource?.UserId == actorId);
        }

        return new(false, false);
    }

    private static Guid? ResolveResourceId(AuthorizationFilterContext context, string parameterName)
    {
        if (context.RouteData.Values.TryGetValue(parameterName, out var routeValue) &&
            Guid.TryParse(routeValue?.ToString(), out var routeId))
        {
            return routeId;
        }

        if (context.HttpContext.Request.Query.TryGetValue(parameterName, out var queryValue) &&
            Guid.TryParse(queryValue.FirstOrDefault(), out var queryId))
        {
            return queryId;
        }

        return null;
    }

    private sealed record ResourceScope(bool Exists, bool IsOwner);
}
