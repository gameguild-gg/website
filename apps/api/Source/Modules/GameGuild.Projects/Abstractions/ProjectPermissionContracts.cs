using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GameGuild.Projects;

/// <summary>Effective permissions returned by the project compatibility API.</summary>
public class EffectivePermission {
    public Guid ResourceId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public PermissionType[] Permissions { get; set; } = [];
    public bool IsOwner { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Result of an invitation operation.</summary>
public class InvitationResult {
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? InvitationId { get; set; }
}

/// <summary>Result of a permission update operation.</summary>
public class PermissionUpdateResult {
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Result of a bulk share operation.</summary>
public class ShareResult {
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

/// <summary>Request to invite a user to a project.</summary>
public class InviteUserRequest {
    public string Email { get; set; } = string.Empty;
    public PermissionType[] Permissions { get; set; } = [];
    public DateTime? ExpiresAt { get; set; }
    public string? Message { get; set; }
    public bool RequireAcceptance { get; set; } = true;
}

/// <summary>Request to share a project with multiple users.</summary>
public class ShareResourceRequest {
    public string[] UserEmails { get; set; } = [];
    public Guid[] UserIds { get; set; } = [];
    public PermissionType[] Permissions { get; set; } = [];
    public DateTime? ExpiresAt { get; set; }
    public string? Message { get; set; }
    public bool RequireAcceptance { get; set; } = true;
    public bool NotifyUsers { get; set; } = true;
}

/// <summary>Compatibility contract backed by the central project authorization service.</summary>
public interface IPermissionResolver {
    Task<IEnumerable<EffectivePermission>> GetEffectivePermissionsAsync(Guid userId, Guid? tenantId, Guid resourceId, string resourceType);
    Task<bool> CanGrantPermissionsAsync(Guid userId, Guid? tenantId, PermissionType[] permissions, Guid? resourceId = null);
}

/// <summary>Project access entry returned by the compatibility API.</summary>
public class ResourceUserInfo {
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public PermissionType[] Permissions { get; set; } = [];
    public DateTime GrantedAt { get; set; }
    public string GrantedByUserName { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Compatibility contract backed by the canonical collaborator and typed grant stores.</summary>
public interface IResourcePermissionService {
    Task<IEnumerable<ResourceUserInfo>> GetResourceUsersAsync(string resourceType, Guid resourceId, Guid requestingUserId);
    Task<InvitationResult> InviteUserToResourceAsync(string resourceType, Guid resourceId, InviteUserRequest request, Guid invitingUserId);
    Task<PermissionUpdateResult> UpdateUserPermissionsAsync(string resourceType, Guid resourceId, Guid userId, PermissionType[] permissions, Guid updatingUserId, DateTime? expiresAt = null);
    Task<PermissionUpdateResult> RemoveUserAccessAsync(string resourceType, Guid resourceId, Guid userId, Guid removingUserId);
    Task<ShareResult> ShareResourceAsync(string resourceType, Guid resourceId, ShareResourceRequest request, Guid sharingUserId);
}

/// <summary>Requires an exact project permission and hides unauthorized private projects.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequireProjectPermissionAttribute : TypeFilterAttribute {
  public PermissionType Permission { get; }

  public RequireProjectPermissionAttribute(PermissionType permission)
      : base(typeof(RequireProjectPermissionFilter)) {
    Permission = permission;
    Arguments = [permission];
  }
}

public sealed class RequireProjectPermissionFilter(
    PermissionType permission,
    IProjectAuthorizationService authorizationService) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!context.RouteData.Values.TryGetValue("projectId", out var rawProjectId) ||
            !Guid.TryParse(Convert.ToString(rawProjectId), out var projectId))
        {
            context.Result = new NotFoundResult();
            return;
        }

        if (!await authorizationService.HasPermissionAsync(projectId, permission, context.HttpContext.RequestAborted)
                .ConfigureAwait(false))
            context.Result = new NotFoundResult();
    }
}
