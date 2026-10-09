
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Rule that allows action if user is acting on themselves OR has a management permission.
///     Classic pattern: "edit self" OR "manage users"
///     Parameters:
///     - selfPermission: string - Permission required for self-action (e.g., "users:edit:self")
///     - anyPermission: string - Permission that allows action on anyone (e.g., "users:manage")
///     - resourceUserIdPath: string (optional) - Path to extract target user ID from resource (default: "UserId")
/// </summary>
public sealed class SelfOrPermissionRuleEvaluator : IRuleEvaluator
{
    private readonly IAuthorizationPermissionService _permissionService;
    private readonly IAuthorizationTenantContext _tenantContext;
    private readonly ITenantMembershipChecker _tenantMembershipChecker;

    public SelfOrPermissionRuleEvaluator(
        IAuthorizationPermissionService permissionService,
        IAuthorizationTenantContext tenantContext,
        ITenantMembershipChecker tenantMembershipChecker)
    {
        _permissionService = permissionService;
        _tenantContext = tenantContext;
        _tenantMembershipChecker = tenantMembershipChecker;
    }

    public string RuleType => RuleTypes.SelfOrPermission;

    public async Task<RuleEvaluationResult> EvaluateAsync(
        AuthorizationHandlerContext context,
        RuleParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var user = context.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            return RuleEvaluationResult.Fail("User is not authenticated");
        }

        // Extract user ID and tenant ID using centralized helpers
        var currentUserId = Utilities.ClaimsExtractor.GetUserIdAsGuid(user);
        if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
        {
            return RuleEvaluationResult.Fail("Could not determine current user ID");
        }

        // Platform administrators are the canonical "manage any user" actors. Their
        // authority is role-based rather than stored as a tenant permission, so a
        // tenant permission lookup would incorrectly reject them before the
        // controller can apply its resource-level scope.
        if (Utilities.ClaimsExtractor.GetRoles(user).Contains(Policies.SystemAdmin))
        {
            return RuleEvaluationResult.Success();
        }

        // Extract tenant ID from context (now Guid?) or claims
        Guid tenantId;
        if (_tenantContext.HasTenant && _tenantContext.TenantId.HasValue && _tenantContext.TenantId.Value != Guid.Empty)
        {
            tenantId = _tenantContext.TenantId.Value;
        }
        else
        {
            var tenantIdStr = Utilities.ClaimsExtractor.GetTenantId(user);
            if (!Guid.TryParse(tenantIdStr, out tenantId) || tenantId == Guid.Empty)
            {
                return RuleEvaluationResult.Fail("Could not determine tenant ID for permission check");
            }
        }

        var selfPermission = parameters.GetString("selfPermission");
        var anyPermission = parameters.GetString("anyPermission");
        var targetUserId = GetTargetUserIdFromResource(context.Resource, parameters);

        if (!targetUserId.HasValue || targetUserId.Value == Guid.Empty)
        {
            return RuleEvaluationResult.Fail("Cannot determine target user");
        }

        if (currentUserId.Value == targetUserId.Value)
        {
            if (string.IsNullOrEmpty(selfPermission))
            {
                return RuleEvaluationResult.Fail("Self-action permission is not configured");
            }

            var hasSelfPermission = await _permissionService.HasPermissionAsync(
                currentUserId.Value, tenantId, selfPermission, cancellationToken).ConfigureAwait(false);
            if (hasSelfPermission)
            {
                return RuleEvaluationResult.Success();
            }

            return RuleEvaluationResult.Fail(
                $"Self-action requires permission '{selfPermission}'");
        }

        var targetBelongsToTenant = await _tenantMembershipChecker.IsUserMemberOfTenantAsync(
            targetUserId.Value,
            tenantId,
            cancellationToken).ConfigureAwait(false);
        if (!targetBelongsToTenant)
        {
            return RuleEvaluationResult.Fail("Target user does not belong to the current tenant");
        }

        if (string.IsNullOrEmpty(anyPermission))
        {
            return RuleEvaluationResult.Fail("Permission for actions on other users is not configured");
        }

        var hasAnyPermission = await _permissionService.HasPermissionAsync(
            currentUserId.Value, tenantId, anyPermission, cancellationToken).ConfigureAwait(false);

        return hasAnyPermission
            ? RuleEvaluationResult.Success()
            : RuleEvaluationResult.Fail($"Action on other users requires permission '{anyPermission}'");
    }

    private static Guid? GetTargetUserIdFromResource(object? resource, RuleParameters parameters)
    {
        if (resource is null)
        {
            return null;
        }

        var userIdPath = parameters.GetString("resourceUserIdPath") ?? "UserId";

        if (resource is IUserIdResource userIdResource)
        {
            return userIdResource.UserId;
        }

        if (resource is HttpContext httpContext)
        {
            return ParseGuid(GetRouteValue(httpContext.Request.RouteValues, userIdPath));
        }

        if (resource is AuthorizationFilterContext filterContext)
        {
            return ParseGuid(GetRouteValue(filterContext.RouteData.Values, userIdPath));
        }

        var property = resource.GetType().GetProperty(userIdPath);
        if (property is not null)
        {
            return ParseGuid(property.GetValue(resource));
        }

        if (resource is IDictionary<string, object> dict && dict.TryGetValue(userIdPath, out var val))
        {
            return ParseGuid(val);
        }

        return null;
    }

    private static object? GetRouteValue(IEnumerable<KeyValuePair<string, object?>> routeValues, string key) =>
        routeValues.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    private static Guid? ParseGuid(object? value) =>
        value is Guid guid
            ? guid
            : Guid.TryParse(value?.ToString(), out var parsed) ? parsed : null;
}

/// <summary>
///     Interface for resources that have a user ID.
/// </summary>
public interface IUserIdResource
{
    /// <summary>
    ///     The user ID associated with this resource.
    /// </summary>
    Guid? UserId { get; }
}
