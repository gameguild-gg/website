using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;

namespace GameGuild.TestingLab;

public interface ITestingLabPermissionService {
  Task<IReadOnlyList<RoleTemplate>> GetRoleTemplatesAsync();
  Task<RoleTemplate> CreateRoleTemplateAsync(string name, string description, IReadOnlyCollection<PermissionTemplate> permissionTemplates);
  Task<RoleTemplate?> UpdateRoleTemplateAsync(string idOrName, string? name, string description, IReadOnlyCollection<PermissionTemplate> permissionTemplates);
  Task<bool> DeleteRoleTemplateAsync(string idOrName);
  Task<IReadOnlyList<TestingLabAssignedRole>> GetUserRolesAsync(Guid userId, Guid? tenantId);
  Task<IReadOnlyList<TestingLabUserPermission>> GetUserPermissionsAsync(Guid userId, Guid? tenantId);
  Task AssignRoleToUserAsync(Guid userId, Guid? tenantId, string roleName, DateTime? expiresAt = null);
  Task RevokeRoleFromUserAsync(Guid userId, Guid? tenantId, string roleName);
  Task GrantPermissionAsync(Guid userId, Guid? tenantId, string action, string resourceType, Guid resourceId, string? reason = null, DateTime? expiresAt = null, Guid? grantedByUserId = null);
  Task RevokePermissionAsync(Guid userId, Guid? tenantId, string action, string resourceType, Guid resourceId, Guid? revokedByUserId = null);
  Task<bool> HasPermissionAsync(Guid userId, Guid? tenantId, string action, string resourceType, Guid? resourceId = null);
}

public sealed class TestingLabAssignedRole {
  public string RoleName { get; set; } = string.Empty;
}

public sealed class TestingLabUserPermission {
  public string Action { get; set; } = string.Empty;
  public string ResourceType { get; set; } = string.Empty;
  public Guid? ResourceId { get; set; }
  public DateTime? ExpiresAt { get; set; }
}

public sealed class TestingLabPermissionService(IApplicationDbContext context) : ITestingLabPermissionService {
  private const string TemplateCategory = "TestingLab";

  public async Task<IReadOnlyList<RoleTemplate>> GetRoleTemplatesAsync() {
    var templates = await context.Set<GameGuild.Identity.Authorization.PermissionTemplate>()
      .Where(template => template.IsActive && (template.Category == TemplateCategory || template.Name.StartsWith("TestingLab")))
      .OrderBy(template => template.Name)
      .ToListAsync()
      .ConfigureAwait(false);

    return templates.Select(MapTemplate).ToList();
  }

  public async Task<RoleTemplate> CreateRoleTemplateAsync(string name, string description, IReadOnlyCollection<PermissionTemplate> permissionTemplates) {
    ValidateTemplateName(name);
    var normalizedName = name.Trim();
    var exists = await context.Set<GameGuild.Identity.Authorization.PermissionTemplate>()
      .AnyAsync(template => template.Name == normalizedName)
      .ConfigureAwait(false);
    if (exists)
        {
            throw new InvalidOperationException($"Role template '{normalizedName}' already exists.");
        }

        var template = new GameGuild.Identity.Authorization.PermissionTemplate {
      Name = normalizedName,
      Description = description.Trim(),
      Category = TemplateCategory,
      Permissions = NormalizePermissionTemplates(permissionTemplates),
      IsSystemTemplate = false,
      IsActive = true,
    };

    context.Set<GameGuild.Identity.Authorization.PermissionTemplate>().Add(template);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return MapTemplate(template);
  }

  public async Task<RoleTemplate?> UpdateRoleTemplateAsync(string idOrName, string? name, string description, IReadOnlyCollection<PermissionTemplate> permissionTemplates) {
    var template = await FindRoleTemplateAsync(idOrName).ConfigureAwait(false);
    if (template == null)
        {
            return null;
        }

        if (template.IsSystemTemplate)
        {
            throw new InvalidOperationException($"System role template '{template.Name}' cannot be modified.");
        }

        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(template.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)) {
      var normalizedName = name.Trim();
      var duplicate = await context.Set<GameGuild.Identity.Authorization.PermissionTemplate>()
        .AnyAsync(candidate => candidate.Id != template.Id && candidate.Name == normalizedName)
        .ConfigureAwait(false);
      if (duplicate)
            {
                throw new InvalidOperationException($"Role template '{normalizedName}' already exists.");
            }

            template.Name = normalizedName;
    }

    template.Description = description.Trim();
    template.Category = TemplateCategory;
    template.Permissions = NormalizePermissionTemplates(permissionTemplates);

    await context.SaveChangesAsync().ConfigureAwait(false);

    return MapTemplate(template);
  }

  public async Task<bool> DeleteRoleTemplateAsync(string idOrName) {
    var template = await FindRoleTemplateAsync(idOrName).ConfigureAwait(false);
    if (template == null)
        {
            return false;
        }

        if (template.IsSystemTemplate)
        {
            throw new InvalidOperationException($"System role template '{template.Name}' cannot be deleted.");
        }

        var assignment = $"role:{template.Name}";
    var assignedRecords = await context.Set<TenantPermission>()
      .Where(permission => permission.DeletedAt == null && permission.IsActive)
      .ToListAsync()
      .ConfigureAwait(false);
    if (assignedRecords.Any(permission => permission.Permissions.Contains(assignment, StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Role template '{template.Name}' is assigned to one or more members and cannot be deleted.");
        }

        context.Set<GameGuild.Identity.Authorization.PermissionTemplate>().Remove(template);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return true;
  }

  public async Task<IReadOnlyList<TestingLabAssignedRole>> GetUserRolesAsync(Guid userId, Guid? tenantId) {
    var permissions = await FindTenantPermissionsAsync(userId, tenantId).ConfigureAwait(false);
    if (permissions == null || !permissions.IsActive || permissions.IsExpired())
        {
            return [];
        }

        return permissions.Permissions
      .Where(permission => permission.StartsWith("role:", StringComparison.OrdinalIgnoreCase))
      .Select(permission => new TestingLabAssignedRole { RoleName = permission["role:".Length..] })
      .ToList();
  }

  public async Task<IReadOnlyList<TestingLabUserPermission>> GetUserPermissionsAsync(Guid userId, Guid? tenantId) {
    var result = new List<TestingLabUserPermission>();
    var permissions = await FindTenantPermissionsAsync(userId, tenantId).ConfigureAwait(false);

    if (permissions is { IsActive: true } && !permissions.IsExpired()) {
      result.AddRange(permissions.Permissions
        .Select(ParsePermission)
        .Where(permission => permission != null)
        .Select(permission => permission!));

      var roleNames = permissions.Permissions
        .Where(permission => permission.StartsWith("role:", StringComparison.OrdinalIgnoreCase))
        .Select(permission => permission["role:".Length..])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

      if (roleNames.Length > 0) {
        var templates = await context.Set<GameGuild.Identity.Authorization.PermissionTemplate>()
          .Where(template => template.IsActive && (template.Category == TemplateCategory || template.Name.StartsWith("TestingLab")))
          .ToListAsync()
          .ConfigureAwait(false);

        result.AddRange(templates
          .Where(template => roleNames.Contains(template.Name, StringComparer.OrdinalIgnoreCase))
          .SelectMany(template => template.Permissions)
          .Select(ParsePermission)
          .Where(permission => permission != null)
          .Select(permission => permission!));
      }
    }

    if (tenantId.HasValue) {
      var resourceTenantId = new TenantId(tenantId.Value);
      var resourcePermissions = await context.Set<ResourceUserPermission>()
        .Where(permission =>
          permission.TenantId == resourceTenantId &&
          permission.UserId == userId &&
          permission.RevokedAt == null &&
          (permission.ResourceType == TestingLabResourceTypes.Session ||
           permission.ResourceType == TestingLabResourceTypes.Location ||
           permission.ResourceType == TestingLabResourceTypes.Feedback ||
           permission.ResourceType == TestingLabResourceTypes.Request ||
           permission.ResourceType == TestingLabResourceTypes.Participant ||
           permission.ResourceType == TestingLabResourceTypes.Event ||
           permission.ResourceType == TestingLabResourceTypes.Application ||
           permission.ResourceType == TestingLabResourceTypes.Analytics ||
           permission.ResourceType == TestingLabResourceTypes.Settings))
        .ToListAsync()
        .ConfigureAwait(false);

      result.AddRange(resourcePermissions
        .Where(permission => permission.IsActive)
        .SelectMany(permission => permission.Permissions.Select(action => new TestingLabUserPermission {
          Action = action,
          ResourceType = permission.ResourceType,
          ResourceId = Guid.TryParse(permission.ResourceId, out var resourceId) ? resourceId : null,
          ExpiresAt = permission.ExpiresAt,
        })));
    }

    return result
      .GroupBy(permission => new { permission.Action, permission.ResourceType, permission.ResourceId })
      .Select(group => group.OrderByDescending(permission => permission.ExpiresAt).First())
      .ToList();
  }

  public async Task AssignRoleToUserAsync(Guid userId, Guid? tenantId, string roleName, DateTime? expiresAt = null) {
    if (expiresAt.HasValue)
        {
            throw new InvalidOperationException("Role expiration is not supported. Use a temporary resource exception instead.");
        }

        await EnsureActiveTargetTenantMembershipAsync(userId, tenantId).ConfigureAwait(false);

    var template = await FindRoleTemplateAsync(roleName).ConfigureAwait(false);
    if (template == null)
        {
            throw new InvalidOperationException($"Role template '{roleName}' was not found.");
        }

        var permissions = await GetOrCreateTenantPermissionsAsync(userId, tenantId).ConfigureAwait(false);
    permissions.AddPermissions($"role:{template.Name}");
    await context.SaveChangesAsync().ConfigureAwait(false);
  }

  public async Task RevokeRoleFromUserAsync(Guid userId, Guid? tenantId, string roleName) {
    var permissions = await FindTenantPermissionsAsync(userId, tenantId).ConfigureAwait(false);
    if (permissions == null)
        {
            return;
        }

        permissions.Permissions = permissions.Permissions
      .Where(permission => !string.Equals(permission, $"role:{roleName}", StringComparison.OrdinalIgnoreCase))
      .ToArray();
    await context.SaveChangesAsync().ConfigureAwait(false);
  }

  public async Task GrantPermissionAsync(
    Guid userId,
    Guid? tenantId,
    string action,
    string resourceType,
    Guid resourceId,
    string? reason = null,
    DateTime? expiresAt = null,
    Guid? grantedByUserId = null) {
    var targetTenantId = await EnsureActiveTargetTenantMembershipAsync(userId, tenantId).ConfigureAwait(false);
    ValidateResourcePermission(action, resourceType);
    var resourceTenantId = new TenantId(targetTenantId);

    var permission = await context.Set<ResourceUserPermission>()
      .FirstOrDefaultAsync(candidate =>
        candidate.TenantId == resourceTenantId &&
        candidate.UserId == userId &&
        candidate.ResourceType == resourceType &&
        candidate.ResourceId == resourceId.ToString() &&
        candidate.RevokedAt == null)
      .ConfigureAwait(false);

    if (permission == null) {
      permission = new ResourceUserPermission {
        TenantId = targetTenantId,
        UserId = userId,
        ResourceType = resourceType,
        ResourceId = resourceId.ToString(),
        Permissions = [action],
        GrantedByUserId = grantedByUserId ?? userId,
        ExpiresAt = expiresAt,
      };
      context.Set<ResourceUserPermission>().Add(permission);
    }
    else {
      permission.Permissions = permission.Permissions
        .Append(action)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
      permission.ExpiresAt = expiresAt;
    }

    await context.SaveChangesAsync().ConfigureAwait(false);
  }

  public async Task RevokePermissionAsync(
    Guid userId,
    Guid? tenantId,
    string action,
    string resourceType,
    Guid resourceId,
    Guid? revokedByUserId = null) {
    if (!tenantId.HasValue)
        {
            return;
        }

        ValidateResourcePermission(action, resourceType);
    var resourceTenantId = new TenantId(tenantId.Value);

    var permission = await context.Set<ResourceUserPermission>()
      .FirstOrDefaultAsync(candidate =>
        candidate.TenantId == resourceTenantId &&
        candidate.UserId == userId &&
        candidate.ResourceType == resourceType &&
        candidate.ResourceId == resourceId.ToString() &&
        candidate.RevokedAt == null)
      .ConfigureAwait(false);
    if (permission == null)
        {
            return;
        }

        permission.Permissions = permission.Permissions
      .Where(candidate => !string.Equals(candidate, action, StringComparison.OrdinalIgnoreCase))
      .ToArray();
    if (permission.Permissions.Length == 0)
        {
            permission.Revoke(revokedByUserId ?? userId, "Testing Lab resource exception revoked.");
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
  }

  public async Task<bool> HasPermissionAsync(Guid userId, Guid? tenantId, string action, string resourceType, Guid? resourceId = null) {
    var permissions = await GetUserPermissionsAsync(userId, tenantId).ConfigureAwait(false);
    return permissions.Any(permission =>
      string.Equals(permission.Action, action, StringComparison.OrdinalIgnoreCase) &&
      string.Equals(permission.ResourceType, resourceType, StringComparison.Ordinal) &&
      (resourceId.HasValue
        ? !permission.ResourceId.HasValue || permission.ResourceId == resourceId
        : !permission.ResourceId.HasValue));
  }

  private async Task<TenantPermission> GetOrCreateTenantPermissionsAsync(Guid userId, Guid? tenantId) {
    var permission = await context.Set<TenantPermission>()
      .FirstOrDefaultAsync(tp => tp.UserId == userId && tp.TenantId == tenantId && tp.DeletedAt == null)
      .ConfigureAwait(false);

    if (permission != null)
        {
            return permission;
        }

        permission = new TenantPermission {
      UserId = userId,
      TenantId = tenantId,
      Permissions = [],
    };
    context.Set<TenantPermission>().Add(permission);

    return permission;
  }

  private async Task<Guid> EnsureActiveTargetTenantMembershipAsync(Guid userId, Guid? tenantId) {
    if (!tenantId.HasValue)
        {
            throw new InvalidOperationException("A target tenant is required to manage Testing Lab access.");
        }

        var isActiveMember = await context.Set<TenantMember>()
      .AsNoTracking()
      .AnyAsync(member =>
        member.UserId == userId &&
        member.TenantId == tenantId.Value &&
        member.IsActive &&
        member.DeletedAt == null)
      .ConfigureAwait(false);
    if (!isActiveMember)
        {
            throw new InvalidOperationException("The user must be an active member of the target tenant.");
        }

        return tenantId.Value;
  }

  private async Task<TenantPermission?> FindTenantPermissionsAsync(Guid userId, Guid? tenantId) {
    return await context.Set<TenantPermission>()
      .FirstOrDefaultAsync(permission =>
        permission.UserId == userId &&
        permission.TenantId == tenantId &&
        permission.DeletedAt == null)
      .ConfigureAwait(false);
  }

  private static void ValidateResourcePermission(string action, string resourceType) {
    if (!TestingLabResourceTypes.IsValid(resourceType))
        {
            throw new InvalidOperationException($"'{resourceType}' is not a valid Testing Lab resource type.");
        }

        if (!TestingLabActions.All.Contains(action, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"'{action}' is not a valid Testing Lab action.");
        }
    }

  private async Task<GameGuild.Identity.Authorization.PermissionTemplate?> FindRoleTemplateAsync(string idOrName) {
    if (Guid.TryParse(idOrName, out var id)) {
      return await context.Set<GameGuild.Identity.Authorization.PermissionTemplate>()
        .FirstOrDefaultAsync(template => template.Id == id && template.IsActive && (template.Category == TemplateCategory || template.Name.StartsWith("TestingLab")))
        .ConfigureAwait(false);
    }

    return await context.Set<GameGuild.Identity.Authorization.PermissionTemplate>()
      .FirstOrDefaultAsync(template => template.Name == idOrName && template.IsActive && (template.Category == TemplateCategory || template.Name.StartsWith("TestingLab")))
      .ConfigureAwait(false);
  }

  private static RoleTemplate MapTemplate(GameGuild.Identity.Authorization.PermissionTemplate template) {
    return new RoleTemplate {
      Id = template.Id,
      Name = template.Name,
      Description = template.Description,
      IsSystemRole = template.IsSystemTemplate,
      PermissionTemplates = template.Permissions.Select(ParseTemplatePermission).ToList(),
    };
  }

  private static PermissionTemplate ParseTemplatePermission(string permission) {
    var parts = permission.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return new PermissionTemplate {
      ResourceType = parts.Length > 0 ? parts[0] : string.Empty,
      Action = parts.Length > 1 ? parts[1] : string.Empty,
    };
  }

  private static string[] NormalizePermissionTemplates(IReadOnlyCollection<PermissionTemplate> templates) {
    return templates
      .Where(template => TestingLabResourceTypes.IsValid(template.ResourceType) && TestingLabActions.All.Contains(template.Action, StringComparer.OrdinalIgnoreCase))
      .Select(template => $"{template.ResourceType}:{template.Action}")
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
      .ToArray();
  }

  private static void ValidateTemplateName(string name) {
    if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Role template name is required.");
        }

        if (name.Trim().Length > 100)
        {
            throw new InvalidOperationException("Role template name cannot exceed 100 characters.");
        }
    }

  private static TestingLabUserPermission? ParsePermission(string permission) {
    if (permission.StartsWith("role:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = permission.Split(':', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 2)
        {
            return null;
        }

        return new TestingLabUserPermission {
      ResourceType = parts[0],
      Action = parts[1],
      ResourceId = parts.Length > 2 && Guid.TryParse(parts[2], out var resourceId) ? resourceId : null,
    };
  }
}
