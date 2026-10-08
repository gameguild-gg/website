using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Learning.Courses;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireCourseCapabilityAttribute(
    CourseCapability capability,
    string routeParameterName = "id") : Attribute, IAsyncAuthorizationFilter
{
    public CourseCapability Capability { get; } = capability;

    public string RouteParameterName { get; } = routeParameterName;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return;
        }

        if (!TryResolveCourseId(context.HttpContext.Request, out var courseId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var evaluator = context.HttpContext.RequestServices.GetRequiredService<ICourseAccessEvaluator>();
        if (!await evaluator
                .HasCapabilityAsync(courseId, Capability, context.HttpContext.RequestAborted)
                .ConfigureAwait(false))
        {
            context.Result = new ForbidResult();
        }
    }

    private bool TryResolveCourseId(HttpRequest request, out Guid courseId)
    {
        courseId = default;
        var hasRouteValue = request.RouteValues.TryGetValue(RouteParameterName, out var routeValue);
        var hasQueryValue = request.Query.TryGetValue(RouteParameterName, out var queryValue);

        Guid? routeCourseId = null;
        if (hasRouteValue)
        {
            if (!Guid.TryParse(routeValue?.ToString(), out var parsedRouteCourseId))
            {
                return false;
            }

            routeCourseId = parsedRouteCourseId;
        }

        Guid? queryCourseId = null;
        if (hasQueryValue)
        {
            if (queryValue.Count != 1 || !Guid.TryParse(queryValue[0], out var parsedQueryCourseId))
            {
                return false;
            }

            queryCourseId = parsedQueryCourseId;
        }

        if (routeCourseId.HasValue && queryCourseId.HasValue && routeCourseId != queryCourseId)
        {
            return false;
        }

        courseId = routeCourseId ?? queryCourseId ?? default;
        return courseId != Guid.Empty;
    }
}
