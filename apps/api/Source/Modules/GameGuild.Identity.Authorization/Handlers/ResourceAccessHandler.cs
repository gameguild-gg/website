using System.Security.Claims;
using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Handles resource access requirements using DAC (Discretionary Access Control).
///     Validates ownership and/or Access Control List-based access to resources.
///     Supports User, Role, Group, and Anonymous principals with deny-first evaluation.
/// </summary>
public sealed class ResourceAccessHandler : AuthorizationHandler<ResourceAccessRequirement>
{
    private readonly IAuthorizationTenantContext _tenantContext;
    private readonly IAccessControlListService _accessControlListService;
    private readonly AuthorizationTokenOptions _tokenOptions;
    private readonly ILogger<ResourceAccessHandler> _logger;

    /// <summary>
    ///     Initializes a new instance of <see cref="ResourceAccessHandler"/>.
    /// </summary>
    public ResourceAccessHandler(
        IAuthorizationTenantContext tenantContext,
        IAccessControlListService accessControlListService,
        IOptions<AuthorizationTokenOptions> tokenOptions,
        ILogger<ResourceAccessHandler> logger)
    {
        _tenantContext = tenantContext;
        _accessControlListService = accessControlListService;
        _tokenOptions = tokenOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceAccessRequirement requirement)
    {
        // Build ACL subject from claims
        var subject = BuildAclSubject(context.User);

        if (!TryGetTenantId(context.User, out var tenantId))
        {
            _logger.LogWarning("Cannot determine tenant ID for resource access check");
            context.Fail(new AuthorizationFailureReason(this, "Tenant context unavailable"));
            return;
        }

        // Check ownership if required
        if (requirement.RequireOwnership)
        {
            if (context.Resource is not IOwnedResource ownedResource)
            {
                _logger.LogWarning("Ownership required but resource is not IOwnedResource");
                context.Fail(new AuthorizationFailureReason(this, "Resource does not support ownership"));
                return;
            }

            if (subject.UserId.HasValue && ownedResource.OwnerId == subject.UserId.Value)
            {
                _logger.LogDebug("Resource access granted via ownership");
                context.Succeed(requirement);
                return;
            }

            if (!requirement.RequireAccessControlListAccess)
            {
                _logger.LogInformation("Resource access denied: not owner and no Access Control List fallback");
                context.Fail(new AuthorizationFailureReason(this, "Not resource owner"));
                return;
            }
        }

        // Check Access Control List access if required
        if (requirement.RequireAccessControlListAccess)
        {
            var (resourceType, resourceId) = GetResourceIdentifiers(context.Resource, requirement);

            if (string.IsNullOrEmpty(resourceType) || string.IsNullOrEmpty(resourceId))
            {
                _logger.LogWarning("Cannot determine resource identifiers for Access Control List check");
                context.Fail(new AuthorizationFailureReason(this, "Resource identifiers unavailable"));
                return;
            }

            try
            {
                // Use deny-first evaluation with all principals (user, roles, groups, anonymous)
                var hasAccess = await _accessControlListService.HasAccessAsync(
                    subject,
                    tenantId,
                    resourceType,
                    resourceId,
                    requirement.MinimumAccessLevel,
                    CancellationToken.None).ConfigureAwait(false);

                if (hasAccess)
                {
                    _logger.LogDebug(
                        "Resource access granted via Access Control List: {ResourceType}/{ResourceId}",
                        resourceType,
                        resourceId);
                    context.Succeed(requirement);
                    return;
                }

                _logger.LogInformation(
                    "Resource access denied: insufficient Access Control List permissions for {ResourceType}/{ResourceId}",
                    resourceType,
                    resourceId);
                context.Fail(new AuthorizationFailureReason(this, "Insufficient access level"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Access Control List access");
                context.Fail(new AuthorizationFailureReason(this, "Error checking resource access"));
                throw;
            }

            return;
        }

        // If we reach here without explicit requirements, succeed
        context.Succeed(requirement);
    }

    /// <summary>
    ///     Builds an ACL subject from the user's claims.
    /// </summary>
    private AclSubject BuildAclSubject(ClaimsPrincipal user)
    {
        var isAuthenticated = user.Identity?.IsAuthenticated == true;

        if (!isAuthenticated)
        {
            return AclSubject.Anonymous;
        }

        Guid? userId = null;
        var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedUserId))
        {
            userId = parsedUserId;
        }

        // Extract role IDs from claims
        var roleIds = user.FindAll(_tokenOptions.RoleIdClaimType)
            .Select(c => Guid.TryParse(c.Value, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        // Extract group IDs from claims
        var groupIds = user.FindAll(_tokenOptions.GroupIdClaimType)
            .Select(c => Guid.TryParse(c.Value, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        return new AclSubject
        {
            IsAuthenticated = true,
            UserId = userId,
            RoleIds = roleIds,
            GroupIds = groupIds
        };
    }

    private bool TryGetTenantId(ClaimsPrincipal user, out Guid tenantId)
    {
        tenantId = Guid.Empty;

        // TenantId is now Guid? - use directly if available
        // SECURITY: Reject Guid.Empty as valid tenant ID
        if (_tenantContext.HasTenant && _tenantContext.TenantId.HasValue)
        {
            tenantId = _tenantContext.TenantId.Value;
            return tenantId != Guid.Empty;
        }

        // Fall back to claims
        var tenantClaim = user.FindFirstValue(_tokenOptions.TenantClaimType);
        if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out tenantId))
        {
            return tenantId != Guid.Empty;
        }

        return false;
    }

    private static (string? resourceType, string? resourceId) GetResourceIdentifiers(
        object? resource,
        ResourceAccessRequirement requirement)
    {
        if (resource is IAccessControlListResource accessControlListResource)
        {
            return (accessControlListResource.ResourceType, accessControlListResource.ResourceId);
        }

        // Use requirement's resource type if available
        if (!string.IsNullOrEmpty(requirement.ResourceType))
        {
            // Try to get ID from resource if it's IOwnedResource
            if (resource is IOwnedResource ownedResource)
            {
                return (requirement.ResourceType, ownedResource.OwnerId.ToString());
            }
        }

        return (null, null);
    }
}
