using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using cms.Modules.Auth.Attributes;
using System.Security.Claims;

namespace cms.Modules.Auth.Filters
{
    /// <summary>
    /// Role-based authorization filter
    /// </summary>
    public class RoleAuthorizationFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Check if the action/controller requires specific roles
            RequireRolesAttribute? requireRolesAttribute = context.ActionDescriptor.EndpointMetadata
                .OfType<RequireRolesAttribute>()
                .FirstOrDefault();

            if (requireRolesAttribute == null)
            {
                return; // No role requirement
            }

            // Check if the user is authenticated
            if (!context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                context.Result = new UnauthorizedResult();

                return;
            }

            // Check if a user has required roles
            var userRoles = context.HttpContext.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            bool hasRequiredRole = requireRolesAttribute.Roles
                .Any(requiredRole => userRoles.Contains(requiredRole));

            if (!hasRequiredRole)
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
