using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using PermissionType = GameGuild.Identity.Authorization.PermissionType;

namespace GameGuild.Projects;

/// <summary> Controller specifically for managing project permissions and collaboration Extends the base resource permission functionality with project-specific features </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/projects/{projectId}/permissions")]
[Authorize]
public class ProjectPermissionController : BaseApiController {
  private readonly ILogger<ProjectPermissionController> _logger;

  private readonly IPermissionResolver _permissionResolver;

  private readonly IResourcePermissionService _resourcePermissionService;

  private readonly IActorContextAccessor _actorContextAccessor;
  private readonly ISender _sender;

  public ProjectPermissionController(IPermissionResolver permissionResolver, IResourcePermissionService resourcePermissionService, IActorContextAccessor actorContextAccessor, ILogger<ProjectPermissionController> logger, ISender sender) {
    _permissionResolver = permissionResolver;
    _resourcePermissionService = resourcePermissionService;
    _actorContextAccessor = actorContextAccessor;
    _logger = logger;
    _sender = sender;
  }

  /// <summary> Get current user's permissions on the project </summary>
  [HttpGet("my-permissions")]
  [RequireProjectPermission(PermissionType.Read)]
  public async Task<ActionResult<IEnumerable<EffectivePermission>>> GetMyProjectPermissions(Guid projectId) {
    var userId = GetCurrentUserId();
    var tenantId = GetCurrentTenantId();

    var permissions = await _permissionResolver.GetEffectivePermissionsAsync<Project>(userId, tenantId, projectId, "Project").ConfigureAwait(false);

    return Ok(permissions);
  }

  /// <summary> Get all collaborators on the project </summary>
  [HttpGet("collaborators")]
  [RequireProjectPermission(PermissionType.Read)]
  public async Task<ActionResult<IEnumerable<ProjectCollaboratorDto>>> GetProjectCollaborators(Guid projectId) {
    var userId = GetCurrentUserId();

    var users = await _resourcePermissionService.GetResourceUsersAsync("projects", projectId, userId).ConfigureAwait(false);

    var collaborators = users.Select(u => new ProjectCollaboratorDto {
      UserId = u.UserId,
      UserName = u.UserName,
      Email = u.Email,
      ProfilePictureUrl = u.ProfilePictureUrl,
      Role = DetermineProjectRole(u.Permissions),
      Permissions = u.Permissions,
      JoinedAt = u.GrantedAt,
      InvitedBy = u.GrantedByUserName,
      IsOwner = u.IsOwner,
      ExpiresAt = u.ExpiresAt,
    }
    );

    return Ok(collaborators);
  }

  /// <summary> Add a collaborator to the project </summary>
  [HttpPost("collaborators")]
  [RequireProjectPermission(PermissionType.Edit)]
  public async Task<ActionResult<InvitationResult>> AddCollaborator(Guid projectId, [FromBody] AddCollaboratorRequest request) {
    var userId = GetCurrentUserId();

    // Validate that user can grant the requested permissions
    var canGrantPermissions = await _permissionResolver.CanGrantPermissionsAsync(userId, GetCurrentTenantId(), request.Permissions, projectId).ConfigureAwait(false);

    if (!canGrantPermissions) return Forbid("You don't have permission to grant some of the requested permissions");

    var inviteRequest = new InviteUserRequest { Email = request.Email, Permissions = request.Permissions, ExpiresAt = request.ExpiresAt, Message = request.Message, RequireAcceptance = request.RequireAcceptance };

    var result = await _sender.Send(new AddProjectPermissionCollaboratorCommand(projectId, inviteRequest, userId)).ConfigureAwait(false);

    if (!result.Success) return BadRequest(result.ErrorMessage);

    return Ok(result);
  }

  /// <summary> Update collaborator permissions </summary>
  [HttpPut("collaborators/{collaboratorUserId}")]
  [RequireProjectPermission(PermissionType.Edit)]
  public async Task<ActionResult<PermissionUpdateResult>> UpdateCollaboratorPermissions(Guid projectId, Guid collaboratorUserId, [FromBody] UpdateCollaboratorRequest request) {
    var userId = GetCurrentUserId();

    // Validate that user can grant the requested permissions
    var canGrantPermissions = await _permissionResolver.CanGrantPermissionsAsync(userId, GetCurrentTenantId(), request.Permissions, projectId).ConfigureAwait(false);

    if (!canGrantPermissions) return Forbid("You don't have permission to grant some of the requested permissions");

    var result = await _sender.Send(new UpdateProjectPermissionCollaboratorCommand(
      projectId,
      collaboratorUserId,
      request.Permissions,
      userId,
      request.ExpiresAt)).ConfigureAwait(false);

    if (!result.Success) return BadRequest(result.ErrorMessage);

    return Ok(result);
  }

  /// <summary> Remove a collaborator from the project </summary>
  [HttpDelete("collaborators/{collaboratorUserId}")]
  [RequireProjectPermission(PermissionType.Edit)]
  public async Task<ActionResult<PermissionUpdateResult>> RemoveCollaborator(Guid projectId, Guid collaboratorUserId) {
    var userId = GetCurrentUserId();

    var result = await _sender.Send(new RemoveProjectPermissionCollaboratorCommand(projectId, collaboratorUserId, userId)).ConfigureAwait(false);

    if (!result.Success) return BadRequest(result.ErrorMessage);

    return Ok(result);
  }

  /// <summary> Get project permission templates for common roles </summary>
  [HttpGet("role-templates")]
  [RequireProjectPermission(PermissionType.Read)]
  public ActionResult<IEnumerable<ProjectRoleTemplate>> GetProjectRoleTemplates() {
    var templates = new[] {
      new ProjectRoleTemplate { Name = "Viewer", Description = "Can view project content", Permissions = [PermissionType.Read, PermissionType.Comment] },
      new ProjectRoleTemplate {
        Name = "Collaborator",
        Description = "Can edit project content and collaborate",
        Permissions = [PermissionType.Read, PermissionType.Edit, PermissionType.Comment, PermissionType.Reply, PermissionType.Share, PermissionType.Create],
      },
      new ProjectRoleTemplate {
        Name = "Editor",
        Description = "Can edit, review, and publish project content",
        Permissions = [PermissionType.Read, PermissionType.Edit, PermissionType.Create, PermissionType.Comment, PermissionType.Reply, PermissionType.Share, PermissionType.Review, PermissionType.Approve, PermissionType.Publish],
      },
      new ProjectRoleTemplate {
        Name = "Admin",
        Description = "Full access to project including user management",
        Permissions = [
          PermissionType.Read,
          PermissionType.Edit,
          PermissionType.Create,
          PermissionType.Delete,
          PermissionType.Comment,
          PermissionType.Reply,
          PermissionType.Share,
          PermissionType.Review,
          PermissionType.Approve,
          PermissionType.Publish,
          PermissionType.Archive,
          PermissionType.Restore,
        ],
      },
    };

    return Ok(templates);
  }

  /// <summary> Share project with multiple users using a role template </summary>
  [HttpPost(":share-with-role")]
  [RequireProjectPermission(PermissionType.Share)]
  public async Task<ActionResult<ShareResult>> ShareProjectWithRole(Guid projectId, [FromBody] ShareProjectWithRoleRequest request) {
    var userId = GetCurrentUserId();

    // Get permissions for the specified role
    var roleTemplate = GetRoleTemplate(request.RoleName);

    if (roleTemplate == null) return BadRequest($"Unknown role template: {request.RoleName}");

    // Validate that user can grant the role permissions
    var canGrantPermissions = await _permissionResolver.CanGrantPermissionsAsync(userId, GetCurrentTenantId(), roleTemplate.Permissions, projectId).ConfigureAwait(false);

    if (!canGrantPermissions) return Forbid($"You don't have permission to grant {request.RoleName} role");

    var shareRequest = new ShareResourceRequest {
      UserEmails = request.UserEmails,
      UserIds = request.UserIds,
      Permissions = roleTemplate.Permissions,
      ExpiresAt = request.ExpiresAt,
      Message = request.Message ?? $"You've been invited to collaborate on this project as a {request.RoleName}",
      RequireAcceptance = request.RequireAcceptance,
      NotifyUsers = request.NotifyUsers,
    };

    var result = await _sender.Send(new ShareProjectWithRoleCommand(projectId, shareRequest, userId)).ConfigureAwait(false);

    if (!result.Success) return BadRequest(result.ErrorMessage);

    return Ok(result);
  }

  #region Private Helper Methods

  private Guid GetCurrentUserId() {
    return _actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty;
  }

  private Guid? GetCurrentTenantId() {
    return _actorContextAccessor.ActorContext.TenantId;
  }

  private static string DetermineProjectRole(PermissionType[] permissions) {
    var permissionSet = permissions.ToHashSet();

    if (permissionSet.Contains(PermissionType.Delete) && permissionSet.Contains(PermissionType.Archive)) return "Admin";

    if (permissionSet.Contains(PermissionType.Publish) && permissionSet.Contains(PermissionType.Review)) return "Editor";

    if (permissionSet.Contains(PermissionType.Edit) && permissionSet.Contains(PermissionType.Create)) return "Collaborator";

    if (permissionSet.Contains(PermissionType.Read)) return "Viewer";

    return "Custom";
  }

  private static ProjectRoleTemplate? GetRoleTemplate(string roleName) {
    return roleName.ToLower() switch {
      "viewer" => new ProjectRoleTemplate { Name = "Viewer", Description = "Can view project content", Permissions = [PermissionType.Read, PermissionType.Comment] },
      "collaborator" => new ProjectRoleTemplate {
        Name = "Collaborator",
        Description = "Can edit project content and collaborate",
        Permissions = [PermissionType.Read, PermissionType.Edit, PermissionType.Comment, PermissionType.Reply, PermissionType.Share, PermissionType.Create],
      },
      "editor" => new ProjectRoleTemplate {
        Name = "Editor",
        Description = "Can edit, review, and publish project content",
        Permissions = [PermissionType.Read, PermissionType.Edit, PermissionType.Create, PermissionType.Comment, PermissionType.Reply, PermissionType.Share, PermissionType.Review, PermissionType.Approve, PermissionType.Publish],
      },
      "admin" => new ProjectRoleTemplate {
        Name = "Admin",
        Description = "Full access to project including user management",
        Permissions = [
          PermissionType.Read,
          PermissionType.Edit,
          PermissionType.Create,
          PermissionType.Delete,
          PermissionType.Comment,
          PermissionType.Reply,
          PermissionType.Share,
          PermissionType.Review,
          PermissionType.Approve,
          PermissionType.Publish,
          PermissionType.Archive,
          PermissionType.Restore,
        ],
      },
      _ => null,
    };
  }

  #endregion
}

/// <summary> Project collaborator information for API responses </summary>
public class ProjectCollaboratorDto {
  public Guid UserId { get; set; }

  public string UserName { get; set; } = string.Empty;

  public string Email { get; set; } = string.Empty;

  public string? ProfilePictureUrl { get; set; }

  public string Role { get; set; } = string.Empty;

  public PermissionType[] Permissions { get; set; } = [];

  public DateTime JoinedAt { get; set; }

  public string InvitedBy { get; set; } = string.Empty;

  public bool IsOwner { get; set; }

  public DateTime? ExpiresAt { get; set; }
}

/// <summary> Request to add a collaborator to a project </summary>
public class AddCollaboratorRequest {
  public string Email { get; set; } = string.Empty;

  public PermissionType[] Permissions { get; set; } = [];

  public DateTime? ExpiresAt { get; set; }

  public string? Message { get; set; }

  public bool RequireAcceptance { get; set; } = true;
}

/// <summary> Request to update collaborator permissions </summary>
public class UpdateCollaboratorRequest {
  public PermissionType[] Permissions { get; set; } = [];

  public DateTime? ExpiresAt { get; set; }
}

/// <summary> Project role template </summary>
public class ProjectRoleTemplate {
  public string Name { get; set; } = string.Empty;

  public string Description { get; set; } = string.Empty;

  public PermissionType[] Permissions { get; set; } = [];
}

/// <summary> Request to share project with specific role </summary>
public class ShareProjectWithRoleRequest {
  public string RoleName { get; set; } = string.Empty;

  public string[] UserEmails { get; set; } = [];

  public Guid[] UserIds { get; set; } = [];

  public DateTime? ExpiresAt { get; set; }

  public string? Message { get; set; }

  public bool RequireAcceptance { get; set; } = true;

  public bool NotifyUsers { get; set; } = true;
}
