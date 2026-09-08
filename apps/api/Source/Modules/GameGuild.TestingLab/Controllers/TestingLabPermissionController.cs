using GameGuild.Identity.Authorization;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using Microsoft.AspNetCore.Mvc;


namespace GameGuild.TestingLab;

/// <summary> TestingLab-specific permission management controller Allows admins to create role templates for TestingLab resources: sessions, locations, feedbacks, etc. </summary>
[Route("api/testing-lab/permissions")]
[Authorize(Policy = "Users.Admin")]
public class TestingLabPermissionController : BaseApiController {
  private readonly ILogger<TestingLabPermissionController> _logger;

  private readonly ITestingLabPermissionService _permissionService;

  private readonly IActorContextAccessor _actorContextAccessor;
  private readonly ISender _sender;

  public TestingLabPermissionController(ITestingLabPermissionService permissionService, IActorContextAccessor actorContextAccessor, ISender sender, ILogger<TestingLabPermissionController> logger) {
    _permissionService = permissionService;
    _actorContextAccessor = actorContextAccessor;
    _sender = sender;
    _logger = logger;
  }

  private Guid GetCurrentUserId() {
    return _actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty;
  }

  private bool TryGetEffectiveTenantId(Guid? requestedTenantId, out Guid? effectiveTenantId) {
    var actor = _actorContextAccessor.ActorContext;
    var currentTenantId = actor.TenantId;
    effectiveTenantId = null;
    if (!currentTenantId.HasValue) return false;

    if (!requestedTenantId.HasValue || currentTenantId == requestedTenantId) {
      effectiveTenantId = currentTenantId;
      return true;
    }

    // A SystemAdmin remains a member of the protected base tenant. An explicit target
    // tenant scopes the administrative operation without moving or replacing that membership.
    if (!actor.IsSystemAdmin) return false;
    effectiveTenantId = requestedTenantId;
    return true;
  }

  // ===== TESTING LAB ROLE TEMPLATES =====

  /// <summary> Get all TestingLab role templates </summary>
  [HttpGet("role-templates")]
  public async Task<ActionResult<List<TestingLabRoleTemplate>>> GetRoleTemplates() {
    var roleTemplates = await _permissionService.GetRoleTemplatesAsync().ConfigureAwait(false);
    return Ok(roleTemplates.Select(MapToTestingLabRoleTemplate).ToList());
  }

  /// <summary> Create a new TestingLab role template </summary>
  [Authorize(Policy = Policies.SystemAdmin)]
  [HttpPost("role-templates")]
  public async Task<ActionResult<TestingLabRoleTemplate>> CreateTestingLabRoleTemplate([FromBody] CreateTestingLabRoleRequest request) {
    try {
      var permissionTemplates = BuildPermissionTemplates(request.Permissions);

      var template = await _sender.Send(new CreateTestingLabRoleTemplateEndpointCommand(request.Name, request.Description, permissionTemplates)).ConfigureAwait(false);

      _logger.LogInformation("Admin user {UserId} created TestingLab role template '{RoleName}'", GetCurrentUserId(), request.Name);

      return Ok(MapToTestingLabRoleTemplate(template));
    }
    catch (InvalidOperationException ex)
    {
      _logger.LogWarning(ex, "Conflict while creating role template '{RoleName}'", request.Name);
      return Conflict("A conflict occurred while creating the role template.");
    }
  }

  /// <summary> Update an existing TestingLab role template </summary>
  [Authorize(Policy = Policies.SystemAdmin)]
  [HttpPut("role-templates/{idOrName}")]
  public async Task<ActionResult<TestingLabRoleTemplate>> UpdateTestingLabRoleTemplate(string idOrName, [FromBody] UpdateTestingLabRoleRequest request) {
    try {
      var permissionTemplates = BuildPermissionTemplates(request.Permissions);
      var template = await _sender.Send(new UpdateTestingLabRoleTemplateEndpointCommand(idOrName, request.Name, request.Description, permissionTemplates)).ConfigureAwait(false);

      if (template == null) { return NotFound($"Role template '{idOrName}' not found"); }

      _logger.LogInformation("Admin user {UserId} updated TestingLab role template '{RoleName}'", GetCurrentUserId(), template.Name);

      return Ok(MapToTestingLabRoleTemplate(template));
    }
    catch (InvalidOperationException ex)
    {
      _logger.LogWarning(ex, "Conflict while updating role template '{RoleName}'", idOrName);
      return Conflict("A conflict occurred while updating the role template.");
    }
  }

  /// <summary> Delete a TestingLab role template </summary>
  [Authorize(Policy = Policies.SystemAdmin)]
  [HttpDelete("role-templates/{idOrName}")]
  public async Task<ActionResult> DeleteTestingLabRoleTemplate(string idOrName) {
    try {
      var deleted = await _sender.Send(new DeleteTestingLabRoleTemplateEndpointCommand(idOrName)).ConfigureAwait(false);

      if (!deleted) { return NotFound($"Role template '{idOrName}' not found"); }

      _logger.LogInformation("Admin user {UserId} deleted TestingLab role template '{RoleName}'", GetCurrentUserId(), idOrName);

      return NoContent();
    }
    catch (InvalidOperationException ex)
    {
      _logger.LogWarning(ex, "Conflict while deleting role template '{RoleName}'", idOrName);
      return Conflict("A conflict occurred while deleting the role template.");
    }
  }

  /// <summary> Delete a TestingLab role template by name (legacy compatibility for clients that don't yet have Ids) </summary>
  [Authorize(Policy = Policies.SystemAdmin)]
  [HttpDelete("role-templates/by-name/{name}")]
  public async Task<ActionResult> DeleteTestingLabRoleTemplateByName(string name) {
    try {
      _logger.LogInformation("Attempting to delete TestingLab role template by name '{Name}'", name);
      var deleted = await _sender.Send(new DeleteTestingLabRoleTemplateEndpointCommand(name)).ConfigureAwait(false);

      if (!deleted) { return NotFound($"Role template with name '{name}' not found"); }

      _logger.LogInformation("Admin user {UserId} deleted TestingLab role template named '{Name}'", GetCurrentUserId(), name);

      return NoContent();
    }
    catch (InvalidOperationException ex)
    {
      _logger.LogWarning(ex, "Conflict while deleting role template '{Name}'", name);
      return Conflict("A conflict occurred while deleting the role template.");
    }
  }

  // ===== USER TESTING LAB PERMISSIONS =====

  /// <summary> Get TestingLab permissions for a specific user </summary>
  [HttpGet("users/{userId}")]
  public async Task<ActionResult<UserTestingLabPermissions>> GetUserTestingLabPermissions(Guid userId, [FromQuery] Guid? tenantId = null) {
    if (!TryGetEffectiveTenantId(tenantId, out var effectiveTenantId)) return Forbid();
    var userRoles = await _permissionService.GetUserRolesAsync(userId, effectiveTenantId).ConfigureAwait(false);
    var userPermissions = await _permissionService.GetUserPermissionsAsync(userId, effectiveTenantId).ConfigureAwait(false);

    var testingLabPermissions = userPermissions.Where(p => IsTestingLabResource(p.ResourceType)).ToList();

    var result = new UserTestingLabPermissions {
      UserId = userId,
      TenantId = effectiveTenantId,
      AssignedRoles = userRoles.Select(r => r.RoleName).ToList(),
      ResourcePermissions = testingLabPermissions
        .Where(permission => permission.ResourceId.HasValue)
        .Select(permission => new TestingLabResourcePermissionDto {
          Action = permission.Action,
          ResourceType = permission.ResourceType,
          ResourceId = permission.ResourceId!.Value,
          ExpiresAt = permission.ExpiresAt,
        })
        .ToList(),
      Permissions = new TestingLabPermissionsDto {
        CanCreateSessions = HasPermission(testingLabPermissions, TestingLabActions.Create, TestingLabResourceTypes.Session),
        CanEditSessions = HasPermission(testingLabPermissions, TestingLabActions.Edit, TestingLabResourceTypes.Session),
        CanDeleteSessions = HasPermission(testingLabPermissions, TestingLabActions.Delete, TestingLabResourceTypes.Session),
        CanViewSessions = HasPermission(testingLabPermissions, TestingLabActions.Read, TestingLabResourceTypes.Session),
        CanCreateLocations = HasPermission(testingLabPermissions, TestingLabActions.Create, TestingLabResourceTypes.Location),
        CanEditLocations = HasPermission(testingLabPermissions, TestingLabActions.Edit, TestingLabResourceTypes.Location),
        CanDeleteLocations = HasPermission(testingLabPermissions, TestingLabActions.Delete, TestingLabResourceTypes.Location),
        CanViewLocations = HasPermission(testingLabPermissions, TestingLabActions.Read, TestingLabResourceTypes.Location),
        CanCreateFeedback = HasPermission(testingLabPermissions, TestingLabActions.Create, TestingLabResourceTypes.Feedback),
        CanEditFeedback = HasPermission(testingLabPermissions, TestingLabActions.Edit, TestingLabResourceTypes.Feedback),
        CanDeleteFeedback = HasPermission(testingLabPermissions, TestingLabActions.Delete, TestingLabResourceTypes.Feedback),
        CanViewFeedback = HasPermission(testingLabPermissions, TestingLabActions.Read, TestingLabResourceTypes.Feedback),
        CanModerateFeedback = HasPermission(testingLabPermissions, TestingLabActions.Moderate, TestingLabResourceTypes.Feedback),
        CanCreateRequests = HasPermission(testingLabPermissions, TestingLabActions.Create, TestingLabResourceTypes.Request),
        CanEditRequests = HasPermission(testingLabPermissions, TestingLabActions.Edit, TestingLabResourceTypes.Request),
        CanDeleteRequests = HasPermission(testingLabPermissions, TestingLabActions.Delete, TestingLabResourceTypes.Request),
        CanViewRequests = HasPermission(testingLabPermissions, TestingLabActions.Read, TestingLabResourceTypes.Request),
        CanApproveRequests = HasPermission(testingLabPermissions, TestingLabActions.Approve, TestingLabResourceTypes.Request),
        CanManageParticipants = HasPermission(testingLabPermissions, TestingLabActions.Manage, TestingLabResourceTypes.Participant),
        CanViewParticipants = HasPermission(testingLabPermissions, TestingLabActions.Read, TestingLabResourceTypes.Participant),
        CanCreateEvents = HasPermission(testingLabPermissions, TestingLabActions.Create, TestingLabResourceTypes.Event),
        CanEditEvents = HasPermission(testingLabPermissions, TestingLabActions.Edit, TestingLabResourceTypes.Event),
        CanDeleteEvents = HasPermission(testingLabPermissions, TestingLabActions.Delete, TestingLabResourceTypes.Event),
        CanViewEvents = HasPermission(testingLabPermissions, TestingLabActions.Read, TestingLabResourceTypes.Event),
        CanViewApplications = HasPermission(testingLabPermissions, TestingLabActions.Read, TestingLabResourceTypes.Application),
        CanApproveApplications = HasPermission(testingLabPermissions, TestingLabActions.Approve, TestingLabResourceTypes.Application),
        CanManageApplications = HasPermission(testingLabPermissions, TestingLabActions.Manage, TestingLabResourceTypes.Application),
        CanViewAnalytics = HasPermission(testingLabPermissions, TestingLabActions.Read, TestingLabResourceTypes.Analytics),
      },
    };

    return Ok(result);
  }

  /// <summary> Assign a TestingLab role to a user </summary>
  [HttpPost("users/{userId}/roles")]
  public async Task<ActionResult> AssignTestingLabRole(Guid userId, [FromBody] AssignTestingLabRoleRequest request) {
    try {
      if (!TryGetEffectiveTenantId(request.TenantId, out var effectiveTenantId)) return Forbid();
      await _sender.Send(new AssignTestingLabRoleEndpointCommand(userId, effectiveTenantId, request.RoleName, request.ExpiresAt)).ConfigureAwait(false);

      _logger.LogInformation("Admin user {AdminUserId} assigned TestingLab role '{RoleName}' to user {UserId}", GetCurrentUserId(), request.RoleName, userId);

      return Ok();
    }
    catch (InvalidOperationException ex)
    {
      _logger.LogWarning(ex, "Failed to assign role '{RoleName}' to user {UserId}", request.RoleName, userId);
      return NotFound("The specified user or role was not found.");
    }
  }

  /// <summary> Revoke a TestingLab role from a user </summary>
  [HttpDelete("users/{userId}/roles/{roleName}")]
  public async Task<ActionResult> RevokeTestingLabRole(Guid userId, string roleName, [FromQuery] Guid? tenantId = null) {
    if (!TryGetEffectiveTenantId(tenantId, out var effectiveTenantId)) return Forbid();
    await _sender.Send(new RevokeTestingLabRoleEndpointCommand(userId, effectiveTenantId, roleName)).ConfigureAwait(false);

    _logger.LogInformation("Admin user {AdminUserId} revoked TestingLab role '{RoleName}' from user {UserId}", GetCurrentUserId(), roleName, userId);

    return NoContent();
  }

  // ===== INDIVIDUAL RESOURCE PERMISSIONS =====

  /// <summary> Grant permission to a specific TestingLab resource </summary>
  [HttpPost("users/{userId}/resources/{resourceType}/{resourceId}")]
  public async Task<ActionResult> GrantResourcePermission(Guid userId, string resourceType, Guid resourceId, [FromBody] GrantResourcePermissionRequest request) {
    if (!IsTestingLabResource(resourceType)) { return BadRequest($"'{resourceType}' is not a valid TestingLab resource type"); }

    if (!TryGetEffectiveTenantId(request.TenantId, out var effectiveTenantId)) return Forbid();
    await _sender.Send(new GrantTestingLabResourcePermissionEndpointCommand(userId, effectiveTenantId, request.Action, resourceType, resourceId, request.ExpiresAt, GetCurrentUserId())).ConfigureAwait(false);

    _logger.LogInformation("Admin user {AdminUserId} granted permission '{Action}' on {ResourceType} {ResourceId} to user {UserId}", GetCurrentUserId(), request.Action, resourceType, resourceId, userId);

    return Ok();
  }

  /// <summary> Revoke permission from a specific TestingLab resource </summary>
  [HttpDelete("users/{userId}/resources/{resourceType}/{resourceId}")]
  public async Task<ActionResult> RevokeResourcePermission(Guid userId, string resourceType, Guid resourceId, [FromQuery] string action, [FromQuery] Guid? tenantId = null) {
    if (!IsTestingLabResource(resourceType)) { return BadRequest($"'{resourceType}' is not a valid TestingLab resource type"); }

    if (!TryGetEffectiveTenantId(tenantId, out var effectiveTenantId)) return Forbid();
    await _sender.Send(new RevokeTestingLabResourcePermissionEndpointCommand(userId, effectiveTenantId, action, resourceType, resourceId, GetCurrentUserId())).ConfigureAwait(false);

    _logger.LogInformation("Admin user {AdminUserId} revoked permission '{Action}' on {ResourceType} {ResourceId} from user {UserId}", GetCurrentUserId(), action, resourceType, resourceId, userId);

    return NoContent();
  }

  // ===== PERMISSION CHECKING =====

  /// <summary> Check if a user can perform an action on a TestingLab resource </summary>
  [HttpGet("users/{userId}/check/{resourceType}")]
  public async Task<ActionResult<bool>> CheckTestingLabPermission(Guid userId, string resourceType, [FromQuery] string action, [FromQuery] Guid? resourceId = null, [FromQuery] Guid? tenantId = null) {
    if (!IsTestingLabResource(resourceType)) { return BadRequest($"'{resourceType}' is not a valid TestingLab resource type"); }

    if (!TryGetEffectiveTenantId(tenantId, out var effectiveTenantId)) return Forbid();
    var hasPermission = await _permissionService.HasPermissionAsync(userId, effectiveTenantId, action, resourceType, resourceId).ConfigureAwait(false);

    return Ok(hasPermission);
  }

  // ===== HELPER METHODS =====

  private static bool HasPermission(IEnumerable<TestingLabUserPermission> permissions, string action, string resourceType) {
    return permissions.Any(permission =>
      string.Equals(permission.Action, action, StringComparison.OrdinalIgnoreCase) &&
      string.Equals(permission.ResourceType, resourceType, StringComparison.Ordinal));
  }

  private static bool IsTestingLabResource(string resourceType) {
    return TestingLabResourceTypes.IsValid(resourceType);
  }

  private static List<PermissionTemplate> BuildPermissionTemplates(TestingLabPermissionsDto permissions) {
    var templates = new List<PermissionTemplate>();

    // Sessions
    if (permissions.CanCreateSessions) templates.Add(new PermissionTemplate { Action = TestingLabActions.Create, ResourceType = TestingLabResourceTypes.Session });
    if (permissions.CanEditSessions) templates.Add(new PermissionTemplate { Action = TestingLabActions.Edit, ResourceType = TestingLabResourceTypes.Session });
    if (permissions.CanDeleteSessions) templates.Add(new PermissionTemplate { Action = TestingLabActions.Delete, ResourceType = TestingLabResourceTypes.Session });
    if (permissions.CanViewSessions) templates.Add(new PermissionTemplate { Action = TestingLabActions.Read, ResourceType = TestingLabResourceTypes.Session });

    // Locations
    if (permissions.CanCreateLocations) templates.Add(new PermissionTemplate { Action = TestingLabActions.Create, ResourceType = TestingLabResourceTypes.Location });
    if (permissions.CanEditLocations) templates.Add(new PermissionTemplate { Action = TestingLabActions.Edit, ResourceType = TestingLabResourceTypes.Location });
    if (permissions.CanDeleteLocations) templates.Add(new PermissionTemplate { Action = TestingLabActions.Delete, ResourceType = TestingLabResourceTypes.Location });
    if (permissions.CanViewLocations) templates.Add(new PermissionTemplate { Action = TestingLabActions.Read, ResourceType = TestingLabResourceTypes.Location });

    // Feedback
    if (permissions.CanCreateFeedback) templates.Add(new PermissionTemplate { Action = TestingLabActions.Create, ResourceType = TestingLabResourceTypes.Feedback });
    if (permissions.CanEditFeedback) templates.Add(new PermissionTemplate { Action = TestingLabActions.Edit, ResourceType = TestingLabResourceTypes.Feedback });
    if (permissions.CanDeleteFeedback) templates.Add(new PermissionTemplate { Action = TestingLabActions.Delete, ResourceType = TestingLabResourceTypes.Feedback });
    if (permissions.CanViewFeedback) templates.Add(new PermissionTemplate { Action = TestingLabActions.Read, ResourceType = TestingLabResourceTypes.Feedback });
    if (permissions.CanModerateFeedback) templates.Add(new PermissionTemplate { Action = TestingLabActions.Moderate, ResourceType = TestingLabResourceTypes.Feedback });

    // Requests
    if (permissions.CanCreateRequests) templates.Add(new PermissionTemplate { Action = TestingLabActions.Create, ResourceType = TestingLabResourceTypes.Request });
    if (permissions.CanEditRequests) templates.Add(new PermissionTemplate { Action = TestingLabActions.Edit, ResourceType = TestingLabResourceTypes.Request });
    if (permissions.CanDeleteRequests) templates.Add(new PermissionTemplate { Action = TestingLabActions.Delete, ResourceType = TestingLabResourceTypes.Request });
    if (permissions.CanViewRequests) templates.Add(new PermissionTemplate { Action = TestingLabActions.Read, ResourceType = TestingLabResourceTypes.Request });
    if (permissions.CanApproveRequests) templates.Add(new PermissionTemplate { Action = TestingLabActions.Approve, ResourceType = TestingLabResourceTypes.Request });

    // Participants
    if (permissions.CanManageParticipants) templates.Add(new PermissionTemplate { Action = TestingLabActions.Manage, ResourceType = TestingLabResourceTypes.Participant });
    if (permissions.CanViewParticipants) templates.Add(new PermissionTemplate { Action = TestingLabActions.Read, ResourceType = TestingLabResourceTypes.Participant });

    if (permissions.CanCreateEvents) templates.Add(new PermissionTemplate { Action = TestingLabActions.Create, ResourceType = TestingLabResourceTypes.Event });
    if (permissions.CanEditEvents) templates.Add(new PermissionTemplate { Action = TestingLabActions.Edit, ResourceType = TestingLabResourceTypes.Event });
    if (permissions.CanDeleteEvents) templates.Add(new PermissionTemplate { Action = TestingLabActions.Delete, ResourceType = TestingLabResourceTypes.Event });
    if (permissions.CanViewEvents) templates.Add(new PermissionTemplate { Action = TestingLabActions.Read, ResourceType = TestingLabResourceTypes.Event });
    if (permissions.CanViewApplications) templates.Add(new PermissionTemplate { Action = TestingLabActions.Read, ResourceType = TestingLabResourceTypes.Application });
    if (permissions.CanApproveApplications) templates.Add(new PermissionTemplate { Action = TestingLabActions.Approve, ResourceType = TestingLabResourceTypes.Application });
    if (permissions.CanManageApplications) templates.Add(new PermissionTemplate { Action = TestingLabActions.Manage, ResourceType = TestingLabResourceTypes.Application });
    if (permissions.CanViewAnalytics) templates.Add(new PermissionTemplate { Action = TestingLabActions.Read, ResourceType = TestingLabResourceTypes.Analytics });

    return templates;
  }

  private static TestingLabRoleTemplate MapToTestingLabRoleTemplate(RoleTemplate template) {
    return new TestingLabRoleTemplate {
      Id = template.Id,
      Name = template.Name,
      Description = template.Description,
      IsSystemRole = template.IsSystemRole,
      Permissions = new TestingLabPermissionsDto {
        // Sessions
        CanCreateSessions = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Create && p.ResourceType == TestingLabResourceTypes.Session) == true,
        CanEditSessions = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Edit && p.ResourceType == TestingLabResourceTypes.Session) == true,
        CanDeleteSessions = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Delete && p.ResourceType == TestingLabResourceTypes.Session) == true,
        CanViewSessions = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Read && p.ResourceType == TestingLabResourceTypes.Session) == true,

        // Locations
        CanCreateLocations = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Create && p.ResourceType == TestingLabResourceTypes.Location) == true,
        CanEditLocations = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Edit && p.ResourceType == TestingLabResourceTypes.Location) == true,
        CanDeleteLocations = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Delete && p.ResourceType == TestingLabResourceTypes.Location) == true,
        CanViewLocations = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Read && p.ResourceType == TestingLabResourceTypes.Location) == true,

        // Feedback
        CanCreateFeedback = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Create && p.ResourceType == TestingLabResourceTypes.Feedback) == true,
        CanEditFeedback = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Edit && p.ResourceType == TestingLabResourceTypes.Feedback) == true,
        CanDeleteFeedback = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Delete && p.ResourceType == TestingLabResourceTypes.Feedback) == true,
        CanViewFeedback = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Read && p.ResourceType == TestingLabResourceTypes.Feedback) == true,
        CanModerateFeedback = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Moderate && p.ResourceType == TestingLabResourceTypes.Feedback) == true,

        // Requests
        CanCreateRequests = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Create && p.ResourceType == TestingLabResourceTypes.Request) == true,
        CanEditRequests = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Edit && p.ResourceType == TestingLabResourceTypes.Request) == true,
        CanDeleteRequests = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Delete && p.ResourceType == TestingLabResourceTypes.Request) == true,
        CanViewRequests = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Read && p.ResourceType == TestingLabResourceTypes.Request) == true,
        CanApproveRequests = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Approve && p.ResourceType == TestingLabResourceTypes.Request) == true,

        // Participants
        CanManageParticipants = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Manage && p.ResourceType == TestingLabResourceTypes.Participant) == true,
        CanViewParticipants = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Read && p.ResourceType == TestingLabResourceTypes.Participant) == true,
        CanCreateEvents = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Create && p.ResourceType == TestingLabResourceTypes.Event) == true,
        CanEditEvents = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Edit && p.ResourceType == TestingLabResourceTypes.Event) == true,
        CanDeleteEvents = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Delete && p.ResourceType == TestingLabResourceTypes.Event) == true,
        CanViewEvents = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Read && p.ResourceType == TestingLabResourceTypes.Event) == true,
        CanViewApplications = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Read && p.ResourceType == TestingLabResourceTypes.Application) == true,
        CanApproveApplications = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Approve && p.ResourceType == TestingLabResourceTypes.Application) == true,
        CanManageApplications = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Manage && p.ResourceType == TestingLabResourceTypes.Application) == true,
        CanViewAnalytics = template.PermissionTemplates?.Any(p => p.Action == TestingLabActions.Read && p.ResourceType == TestingLabResourceTypes.Analytics) == true,
      },
    };
  }
}

// ===== TESTING LAB SPECIFIC MODELS =====

public class TestingLabPermissionsDto {
  // Sessions
  public bool CanCreateSessions { get; set; }

  public bool CanEditSessions { get; set; }

  public bool CanDeleteSessions { get; set; }

  public bool CanViewSessions { get; set; }

  // Locations
  public bool CanCreateLocations { get; set; }

  public bool CanEditLocations { get; set; }

  public bool CanDeleteLocations { get; set; }

  public bool CanViewLocations { get; set; }

  // Feedback
  public bool CanCreateFeedback { get; set; }

  public bool CanEditFeedback { get; set; }

  public bool CanDeleteFeedback { get; set; }

  public bool CanViewFeedback { get; set; }

  public bool CanModerateFeedback { get; set; }

  // Requests
  public bool CanCreateRequests { get; set; }

  public bool CanEditRequests { get; set; }

  public bool CanDeleteRequests { get; set; }

  public bool CanViewRequests { get; set; }

  public bool CanApproveRequests { get; set; }

  // Participants
  public bool CanManageParticipants { get; set; }

  public bool CanViewParticipants { get; set; }

  public bool CanCreateEvents { get; set; }

  public bool CanEditEvents { get; set; }

  public bool CanDeleteEvents { get; set; }

  public bool CanViewEvents { get; set; }

  public bool CanViewApplications { get; set; }

  public bool CanApproveApplications { get; set; }

  public bool CanManageApplications { get; set; }

  public bool CanViewAnalytics { get; set; }
}

public class TestingLabRoleTemplate {
  public Guid Id { get; set; } // Added so clients can perform update/delete operations

  public string Name { get; set; } = string.Empty;

  public string Description { get; set; } = string.Empty;

  public bool IsSystemRole { get; set; }

  public TestingLabPermissionsDto Permissions { get; set; } = new TestingLabPermissionsDto();
}

public class UserTestingLabPermissions {
  public Guid UserId { get; set; }

  public Guid? TenantId { get; set; }

  public List<string> AssignedRoles { get; set; } = new List<string>();

  public TestingLabPermissionsDto Permissions { get; set; } = new TestingLabPermissionsDto();

  public List<TestingLabResourcePermissionDto> ResourcePermissions { get; set; } = new();
}

public class TestingLabResourcePermissionDto {
  public string Action { get; set; } = string.Empty;
  public string ResourceType { get; set; } = string.Empty;
  public Guid ResourceId { get; set; }
  public DateTime? ExpiresAt { get; set; }
}

public class CreateTestingLabRoleRequest {
  public string Name { get; set; } = string.Empty;

  public string Description { get; set; } = string.Empty;

  public TestingLabPermissionsDto Permissions { get; set; } = new TestingLabPermissionsDto();
}

public class UpdateTestingLabRoleRequest {
  public string? Name { get; set; } // Optional new name

  public string Description { get; set; } = string.Empty;

  public TestingLabPermissionsDto Permissions { get; set; } = new TestingLabPermissionsDto();
}

public class AssignTestingLabRoleRequest {
  public Guid? TenantId { get; set; }

  public string RoleName { get; set; } = string.Empty;

  public DateTime? ExpiresAt { get; set; }
}

public class GrantResourcePermissionRequest {
  public Guid? TenantId { get; set; }

  public string Action { get; set; } = string.Empty;

  public DateTime? ExpiresAt { get; set; }
}
