using System.Reflection;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Pipeline behavior that performs authorization checks on CQRS commands and queries.
///     Uses <see cref="AuthorizeRequestAttribute"/> on request classes to determine
///     authorization requirements. The attribute is resolved by its concrete type — never
///     by attribute-name matching — so only the platform's own typed attribute is honored.
/// </summary>
/// <remarks>
///     <para>
///         This behavior supports two types of authorization checks:
///     </para>
///     <list type="bullet">
///         <item>
///             <term>Tenant-level permissions</term>
///             <description>
///                 Checks if the user has a capability in the tenant (e.g., "courses:create").
///                 Uses <see cref="ActorContext.HasPermission(string)"/> (populated by ActorContextMiddleware).
///             </description>
///         </item>
///         <item>
///             <term>Resource-level access</term>
///             <description>
///                 Checks if the user has access to a specific resource (e.g., Course #123).
///                 Uses <see cref="IAccessControlListService.HasAccessAsync(AclSubject, Guid, string, string, AccessLevel, CancellationToken)"/>.
///             </description>
///         </item>
///     </list>
///     <para>
///         <b>Fail-closed:</b> resource-level checks map the permission string to an
///         <see cref="AccessLevel"/>. A permission that does not match a known access-level
///         pattern is rejected with an explicit error — it is never silently upgraded to
///         <see cref="AccessLevel.Write"/>.
///     </para>
/// </remarks>
/// <typeparam name="TRequest">The request type (command or query)</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class AuthorizationBehavior<TRequest, TResponse>(
    IActorContextAccessor actorContextAccessor,
    IAccessControlListService aclService
) : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequestBase
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        // Typed attribute resolution: only the platform's AuthorizeRequestAttribute is honored.
        var authorizeAttributes = request.GetType()
            .GetCustomAttributes<AuthorizeRequestAttribute>(inherit: true)
            .ToList();

        if (authorizeAttributes.Count == 0)
        {
            return await next().ConfigureAwait(false);
        }

        // Check if user is authenticated
        if (!Actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }

        // System admin bypass
        if (Actor.IsSystemAdmin)
        {
            return await next().ConfigureAwait(false);
        }

        // Evaluate each attribute
        foreach (var attribute in authorizeAttributes)
        {
            // Check for explicit RequireSystemAdmin/RequireTenantAdmin flags
            if (attribute.RequireSystemAdmin)
            {
                if (!Actor.IsSystemAdmin)
                {
                    throw new UnauthorizedAccessException("System admin required");
                }

                continue;
            }

            if (attribute.RequireTenantAdmin)
            {
                if (!Actor.IsTenantAdmin)
                {
                    throw new UnauthorizedAccessException("Tenant admin required");
                }

                continue;
            }

            var permission = attribute.Permission;
            var resourceType = attribute.ResourceType;
            var resourceIdProperty = attribute.ResourceIdProperty ?? "ResourceId";

            if (string.IsNullOrEmpty(permission))
            {
                continue;
            }

            bool hasPerm;

            if (!string.IsNullOrEmpty(resourceType))
            {
                // RESOURCE-LEVEL CHECK: Use ACL service to check access to specific resource
                // This answers: "Does user have access to THIS specific resource?"
                var resourceId = GetResourceIdFromRequest(request, resourceIdProperty);

                if (resourceId == Guid.Empty)
                {
                    hasPerm = false;
                }
                else
                {
                    var userId = Actor.SubjectIdAsGuid ?? throw new UnauthorizedAccessException("User not authenticated");
                    var tenantId = Actor.TenantId ?? throw new UnauthorizedAccessException("Tenant context required");

                    // Map permission string to AccessLevel (e.g., "read" -> Read, "write" -> Write).
                    // Unknown permission patterns throw (fail closed) rather than defaulting.
                    var requiredLevel = MapPermissionToAccessLevel(permission);

                    // Build AclSubject from actor context
                    // Note: Role/Group IDs would require additional resolution from role names
                    // For now, we rely on user-based ACL lookup; the ACL service can resolve roles internally
                    var subject = AclSubject.ForUser(userId);

                    hasPerm = await aclService
                        .HasAccessAsync(subject, tenantId, resourceType, resourceId.ToString(), requiredLevel, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                // TENANT-LEVEL CHECK: Use permission service to check tenant capability
                // This answers: "Does user have this capability in the tenant?"
                hasPerm = Actor.HasPermission(permission);
            }

            if (!hasPerm)
            {
                throw new UnauthorizedAccessException(
                    $"User does not have required permission: {permission}");
            }
        }

        return await next().ConfigureAwait(false);
    }

    private static Guid GetResourceIdFromRequest(object request, string propertyName)
    {
        var property = request.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property == null)
        {
            return Guid.Empty;
        }

        var value = property.GetValue(request);

        if (value is Guid g)
        {
            return g;
        }

        if (value is string s && Guid.TryParse(s, out var parsed))
        {
            return parsed;
        }

        return Guid.Empty;
    }

    /// <summary>
    ///     Maps a permission string to an <see cref="AccessLevel"/> for ACL checks.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Permission strings like "read", "write", "delete", "manage" are mapped to
    ///         appropriate access levels for resource-level authorization.
    ///     </para>
    ///     <para>
    ///         <b>Fail-closed:</b> an unrecognized permission pattern throws an
    ///         <see cref="UnauthorizedAccessException"/> instead of defaulting to
    ///         <see cref="AccessLevel.Write"/>. Silently upgrading an unknown permission to
    ///         write access would grant more privilege than the caller declared.
    ///     </para>
    /// </remarks>
    /// <exception cref="UnauthorizedAccessException">
    ///     Thrown when the permission does not match any known access-level pattern.
    /// </exception>
    private static AccessLevel MapPermissionToAccessLevel(string permission)
    {
        var normalizedPermission = permission.ToLowerInvariant();

        // Check for common permission patterns
        // Admin = full access including delete and sharing
        if (normalizedPermission.Contains("manage") || normalizedPermission.Contains("admin") ||
            normalizedPermission.Contains("delete") || normalizedPermission.Contains("remove"))
        {
            return AccessLevel.Admin;
        }

        // Write = read and write access
        if (normalizedPermission.Contains("write") || normalizedPermission.Contains("edit") ||
            normalizedPermission.Contains("update") || normalizedPermission.Contains("create"))
        {
            return AccessLevel.Write;
        }

        // Read = read-only access
        if (normalizedPermission.Contains("read") || normalizedPermission.Contains("view") ||
            normalizedPermission.Contains("get") || normalizedPermission.Contains("list"))
        {
            return AccessLevel.Read;
        }

        // Fail closed: unknown permission patterns are denied with an explicit error,
        // never defaulted to Write.
        throw new UnauthorizedAccessException(
            $"Cannot determine required access level for unknown permission: '{permission}'");
    }
}

/// <summary>
///     Attribute to mark CQRS requests (commands/queries) with authorization requirements.
/// </summary>
/// <remarks>
///     <para>
///         Use this attribute to declare authorization requirements on command/query classes.
///     </para>
///     <para>
///         <b>Tenant-level permissions</b> (when ResourceType is null):
///         Checks if the user has the specified capability in the current tenant.
///         Example: <c>[AuthorizeRequest("courses:create")]</c>
///     </para>
///     <para>
///         <b>Resource-level access</b> (when ResourceType is set):
///         Checks if the user has access to the specific resource via ACL.
///         Example: <c>[AuthorizeRequest("edit", ResourceType = "course", ResourceIdProperty = "CourseId")]</c>
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AuthorizeRequestAttribute(string permission) : Attribute
{
    /// <summary>
    ///     The permission required (e.g., "courses:create" for tenant-level, "edit" for resource-level).
    /// </summary>
    public string Permission { get; } = permission;

    /// <summary>
    ///     The layer at which this permission is evaluated.
    /// </summary>
    public PermissionLayer Layer { get; set; } = PermissionLayer.Tenant;

    /// <summary>
    ///     If set, triggers resource-level ACL check instead of tenant-level permission check.
    ///     The value specifies the resource type (e.g., "course", "project").
    /// </summary>
    public string? ResourceType { get; set; }

    /// <summary>
    ///     The name of the property on the request that contains the resource ID.
    ///     Defaults to "ResourceId" if not specified.
    /// </summary>
    public string? ResourceIdProperty { get; set; }

    /// <summary>
    ///     If true, requires the user to be a system administrator. Bypasses other checks.
    /// </summary>
    public bool RequireSystemAdmin { get; set; }

    /// <summary>
    ///     If true, requires the user to be a tenant administrator.
    /// </summary>
    public bool RequireTenantAdmin { get; set; }
}
