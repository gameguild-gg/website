using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using CanonicalResourcePermissionService = GameGuild.Identity.Authorization.IResourcePermissionService;
using CanonicalShareRequest = GameGuild.Identity.Authorization.ShareResourceRequest;

namespace GameGuild.Projects;

public sealed class ProjectPermissionResolver(IProjectAuthorizationService authorizationService) : IPermissionResolver
{
    public async Task<IEnumerable<EffectivePermission>> GetEffectivePermissionsAsync<T>(
        Guid userId,
        Guid? tenantId,
        Guid resourceId,
        string resourceType)
    {
        var granted = new List<PermissionType>();
        foreach (var permission in ProjectPermissionSet.All)
        {
            if (await authorizationService.HasPermissionAsync(resourceId, permission).ConfigureAwait(false))
            {
                granted.Add(permission);
            }
        }

        return granted.Count == 0
            ? []
            : [new EffectivePermission
            {
                ResourceId = resourceId,
                ResourceType = nameof(Project),
                Permissions = granted.ToArray(),
                IsOwner = granted.Count == ProjectPermissionSet.All.Length,
            }];
    }

    public async Task<bool> CanGrantPermissionsAsync(
        Guid userId,
        Guid? tenantId,
        PermissionType[] permissions,
        Guid? resourceId = null)
    {
        if (!resourceId.HasValue || permissions.Length == 0)
        {
            return false;
        }

        foreach (var permission in permissions.Distinct())
        {
            if (!ProjectPermissionSet.All.Contains(permission) ||
                !await authorizationService.HasPermissionAsync(resourceId.Value, permission).ConfigureAwait(false))
            {
                return false;
            }
        }

        return await authorizationService
            .HasPermissionAsync(resourceId.Value, PermissionType.Share)
            .ConfigureAwait(false);
    }
}

public sealed class ProjectResourcePermissionService(
    IApplicationDbContext context,
    IProjectAuthorizationService authorizationService,
    CanonicalResourcePermissionService canonicalPermissionService) : IResourcePermissionService
{
    public async Task<IEnumerable<ResourceUserInfo>> GetResourceUsersAsync(
        string resourceType,
        Guid resourceId,
        Guid requestingUserId)
    {
        var project = await GetAuthorizedProjectAsync(resourceId, PermissionType.Read).ConfigureAwait(false);
        if (project == null)
        {
            return [];
        }

        var collaborators = await context.Set<ProjectCollaborator>()
            .AsNoTracking()
            .Where(candidate =>
                candidate.ProjectId == resourceId &&
                candidate.IsActive &&
                candidate.DeletedAt == null &&
                candidate.LeftAt == null)
            .Include(candidate => candidate.User)
            .ToListAsync()
            .ConfigureAwait(false);
        var tenantId = new TenantId(project.TenantId!.Value);
        var directGrants = await context.Set<ResourceUserPermission>()
            .AsNoTracking()
            .Where(grant =>
                grant.TenantId == tenantId &&
                (grant.ResourceType == nameof(Project) || grant.ResourceType == "projects") &&
                grant.ResourceId == resourceId.ToString() &&
                grant.RevokedAt == null &&
                (!grant.ExpiresAt.HasValue || grant.ExpiresAt > SystemClock.UtcNow))
            .ToListAsync()
            .ConfigureAwait(false);
        var userIds = collaborators.Select(item => item.UserId)
            .Concat(directGrants.Select(item => item.UserId))
            .Concat(project.CreatedById.HasValue ? [project.CreatedById.Value] : [])
            .Distinct()
            .ToArray();
        var users = await context.Set<User>()
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id)
            .ConfigureAwait(false);
        var entries = new Dictionary<Guid, ResourceUserInfo>();

        foreach (var collaborator in collaborators)
        {
            users.TryGetValue(collaborator.UserId, out var user);
            entries[collaborator.UserId] = new ResourceUserInfo
            {
                UserId = collaborator.UserId,
                UserName = user?.Name ?? "Unknown",
                Email = user?.Email ?? string.Empty,
                Permissions = ProjectPermissionSet.Parse(collaborator.Permissions),
                GrantedAt = collaborator.JoinedAt,
                IsOwner = collaborator.UserId == project.CreatedById ||
                          string.Equals(collaborator.Role, ProjectRoles.Owner, StringComparison.OrdinalIgnoreCase),
            };
        }

        foreach (var grant in directGrants)
        {
            users.TryGetValue(grant.UserId, out var user);
            if (!entries.TryGetValue(grant.UserId, out var entry))
            {
                entry = new ResourceUserInfo
                {
                    UserId = grant.UserId,
                    UserName = user?.Name ?? "Unknown",
                    Email = user?.Email ?? string.Empty,
                    GrantedAt = grant.GrantedAt,
                    GrantedByUserName = grant.GrantedByUserName ?? string.Empty,
                    IsOwner = grant.IsOwner || grant.UserId == project.CreatedById,
                    ExpiresAt = grant.ExpiresAt,
                };
                entries.Add(grant.UserId, entry);
            }

            entry.Permissions = entry.Permissions
                .Concat(ProjectPermissionSet.Parse(grant.Permissions))
                .Distinct()
                .ToArray();
        }

        if (project.CreatedById.HasValue && !entries.ContainsKey(project.CreatedById.Value))
        {
            var ownerId = project.CreatedById.Value;
            users.TryGetValue(ownerId, out var owner);
            entries[ownerId] = new ResourceUserInfo
            {
                UserId = ownerId,
                UserName = owner?.Name ?? "Unknown",
                Email = owner?.Email ?? string.Empty,
                Permissions = ProjectPermissionSet.All,
                GrantedAt = project.CreatedAt,
                IsOwner = true,
            };
        }

        return entries.Values.OrderByDescending(entry => entry.IsOwner).ThenBy(entry => entry.GrantedAt).ToArray();
    }

    public async Task<InvitationResult> InviteUserToResourceAsync(
        string resourceType,
        Guid resourceId,
        InviteUserRequest request,
        Guid invitingUserId)
    {
        var project = await GetAuthorizedProjectAsync(resourceId, PermissionType.Edit).ConfigureAwait(false);
        if (project == null)
        {
            return new InvitationResult { ErrorMessage = "Project not found." };
        }

        if (request.Permissions.Length == 0 || request.Permissions.Any(permission => !ProjectPermissionSet.All.Contains(permission)))
        {
            return new InvitationResult { ErrorMessage = "At least one valid project permission is required." };
        }

        var email = request.Email.Trim();
        var user = await context.Set<User>()
            .FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == email.ToLower() && candidate.DeletedAt == null)
            .ConfigureAwait(false);
        if (!request.RequireAcceptance && user != null)
        {
            await UpsertDirectGrantAsync(project, user.Id, request.Permissions, request.ExpiresAt, invitingUserId)
                .ConfigureAwait(false);
            return new InvitationResult { Success = true };
        }

        var result = await canonicalPermissionService.ShareResourceAsync(
                new TenantId(project.TenantId!.Value),
                nameof(Project),
                project.Id.ToString(),
                new CanonicalShareRequest(email, ProjectPermissionSet.Names(request.Permissions), request.ExpiresAt, request.Message),
                invitingUserId)
            .ConfigureAwait(false);
        return new InvitationResult
        {
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            InvitationId = result.InvitationId,
        };
    }

    public async Task<PermissionUpdateResult> UpdateUserPermissionsAsync(
        string resourceType,
        Guid resourceId,
        Guid userId,
        PermissionType[] permissions,
        Guid updatingUserId,
        DateTime? expiresAt = null)
    {
        var project = await GetAuthorizedProjectAsync(resourceId, PermissionType.Edit).ConfigureAwait(false);
        if (project == null)
        {
            return new PermissionUpdateResult { ErrorMessage = "Project not found." };
        }

        if (userId == project.CreatedById)
        {
            return new PermissionUpdateResult { ErrorMessage = "Project owner access cannot be reduced." };
        }

        var collaborator = await context.Set<ProjectCollaborator>()
            .FirstOrDefaultAsync(candidate =>
                candidate.ProjectId == resourceId &&
                candidate.UserId == userId &&
                candidate.IsActive &&
                candidate.DeletedAt == null &&
                candidate.LeftAt == null)
            .ConfigureAwait(false);
        if (collaborator != null)
        {
            collaborator.Permissions = string.Join(',', ProjectPermissionSet.Names(permissions));
            await context.SaveChangesAsync().ConfigureAwait(false);
            return new PermissionUpdateResult { Success = true };
        }

        var grant = await FindDirectGrantAsync(project, userId).ConfigureAwait(false);
        if (grant == null)
        {
            return new PermissionUpdateResult { ErrorMessage = "Permission record not found." };
        }

        grant.Permissions = ProjectPermissionSet.Names(permissions);
        grant.ExpiresAt = expiresAt;
        await context.SaveChangesAsync().ConfigureAwait(false);
        return new PermissionUpdateResult { Success = true };
    }

    public async Task<PermissionUpdateResult> RemoveUserAccessAsync(
        string resourceType,
        Guid resourceId,
        Guid userId,
        Guid removingUserId)
    {
        var project = await GetAuthorizedProjectAsync(resourceId, PermissionType.Edit).ConfigureAwait(false);
        if (project == null)
        {
            return new PermissionUpdateResult { ErrorMessage = "Project not found." };
        }

        if (userId == project.CreatedById)
        {
            return new PermissionUpdateResult { ErrorMessage = "Project owner access cannot be removed." };
        }

        var changed = false;
        var collaborators = await context.Set<ProjectCollaborator>()
            .Where(candidate => candidate.ProjectId == resourceId && candidate.UserId == userId && candidate.IsActive)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var collaborator in collaborators)
        {
            collaborator.IsActive = false;
            collaborator.LeftAt = SystemClock.UtcNow;
            changed = true;
        }

        var tenantId = new TenantId(project.TenantId!.Value);
        var grants = await context.Set<ResourceUserPermission>()
            .Where(grant =>
                grant.TenantId == tenantId &&
                grant.UserId == userId &&
                (grant.ResourceType == nameof(Project) || grant.ResourceType == "projects") &&
                grant.ResourceId == resourceId.ToString() &&
                grant.RevokedAt == null)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var grant in grants)
        {
            changed |= grant.Revoke(removingUserId, "Project access removed");
        }

        if (changed)
        {
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        return new PermissionUpdateResult
        {
            Success = changed,
            ErrorMessage = changed ? null : "Permission record not found.",
        };
    }

    public async Task<ShareResult> ShareResourceAsync(
        string resourceType,
        Guid resourceId,
        ShareResourceRequest request,
        Guid sharingUserId)
    {
        var emails = request.UserEmails.Where(email => !string.IsNullOrWhiteSpace(email)).Select(email => email.Trim()).ToList();
        if (request.UserIds.Length > 0)
        {
            emails.AddRange(await context.Set<User>()
                .Where(user => request.UserIds.Contains(user.Id) && user.DeletedAt == null)
                .Select(user => user.Email)
                .ToListAsync()
                .ConfigureAwait(false));
        }

        var successCount = 0;
        var failureCount = 0;
        foreach (var email in emails.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var result = await InviteUserToResourceAsync(resourceType, resourceId, new InviteUserRequest
            {
                Email = email,
                Permissions = request.Permissions,
                ExpiresAt = request.ExpiresAt,
                Message = request.Message,
                RequireAcceptance = request.RequireAcceptance,
            }, sharingUserId).ConfigureAwait(false);
            if (result.Success)
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }
        }

        return new ShareResult
        {
            Success = successCount > 0 && failureCount == 0,
            SuccessCount = successCount,
            FailureCount = failureCount,
            ErrorMessage = failureCount == 0 ? null : "One or more project shares failed.",
        };
    }

    private async Task<Project?> GetAuthorizedProjectAsync(Guid projectId, PermissionType permission)
    {
        if (!await authorizationService.HasPermissionAsync(projectId, permission).ConfigureAwait(false))
        {
            return null;
        }

        return await context.Set<Project>()
            .FirstOrDefaultAsync(project => project.Id == projectId && project.DeletedAt == null)
            .ConfigureAwait(false);
    }

    private async Task<ResourceUserPermission?> FindDirectGrantAsync(Project project, Guid userId)
    {
        var tenantId = new TenantId(project.TenantId!.Value);
        return await context.Set<ResourceUserPermission>()
            .FirstOrDefaultAsync(grant =>
                grant.TenantId == tenantId &&
                grant.UserId == userId &&
                (grant.ResourceType == nameof(Project) || grant.ResourceType == "projects") &&
                grant.ResourceId == project.Id.ToString() &&
                grant.RevokedAt == null)
            .ConfigureAwait(false);
    }

    private async Task UpsertDirectGrantAsync(
        Project project,
        Guid userId,
        PermissionType[] permissions,
        DateTime? expiresAt,
        Guid grantedByUserId)
    {
        var existingCollaborator = await context.Set<ProjectCollaborator>()
            .FirstOrDefaultAsync(candidate =>
                candidate.ProjectId == project.Id &&
                candidate.UserId == userId &&
                candidate.IsActive &&
                candidate.DeletedAt == null &&
                candidate.LeftAt == null)
            .ConfigureAwait(false);
        if (existingCollaborator != null)
        {
            existingCollaborator.Permissions = string.Join(',', ProjectPermissionSet.Names(permissions));
            await context.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        var grant = await FindDirectGrantAsync(project, userId).ConfigureAwait(false);
        if (grant == null)
        {
            grant = new ResourceUserPermission
            {
                TenantId = new TenantId(project.TenantId!.Value),
                UserId = userId,
                ResourceType = nameof(Project),
                ResourceId = project.Id.ToString(),
                Permissions = ProjectPermissionSet.Names(permissions),
                GrantedByUserId = grantedByUserId,
                ExpiresAt = expiresAt,
            };
            context.Set<ResourceUserPermission>().Add(grant);
        }
        else
        {
            grant.Permissions = ProjectPermissionSet.Names(permissions);
            grant.ExpiresAt = expiresAt;
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}

internal static class ProjectPermissionSet
{
    internal static readonly PermissionType[] All =
    [
        PermissionType.Read,
        PermissionType.Edit,
        PermissionType.Delete,
        PermissionType.Create,
        PermissionType.Share,
        PermissionType.Comment,
        PermissionType.Reply,
        PermissionType.Review,
        PermissionType.Approve,
        PermissionType.Publish,
        PermissionType.Archive,
        PermissionType.Restore,
    ];

    internal static string[] Names(IEnumerable<PermissionType> permissions)
        => permissions.Distinct().Select(permission => permission.ToString()).ToArray();

    internal static PermissionType[] Parse(string? permissions)
        => Parse(string.IsNullOrWhiteSpace(permissions)
            ? []
            : permissions.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    internal static PermissionType[] Parse(IEnumerable<string> permissions)
        => permissions
            .Select(permission => Enum.TryParse<PermissionType>(permission, true, out var parsed) ? parsed : (PermissionType?)null)
            .Where(permission => permission.HasValue && All.Contains(permission.Value))
            .Select(permission => permission!.Value)
            .Distinct()
            .ToArray();
}
