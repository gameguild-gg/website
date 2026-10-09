using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Service for managing resource-level permissions.
///     Handles sharing, access control, and invitations.
/// </summary>
public class ResourcePermissionService : IResourcePermissionService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<ResourcePermissionService> _logger;
    private readonly IPermissionAnalyticsService? _analyticsService;

    public ResourcePermissionService(
        IApplicationDbContext dbContext,
        ILogger<ResourcePermissionService> logger,
        IPermissionAnalyticsService? analyticsService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _analyticsService = analyticsService;
    }

    public async Task<ShareResult> ShareResourceAsync(
        TenantId tenantId,
        string resourceType,
        string resourceId,
        ShareResourceRequest request,
        Guid sharedByUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if user exists by email (would need to be implemented via user service)
            // For now, create an invitation
            var invitation = new ResourceInvitation
            {
                TenantId = tenantId,
                Email = request.Email,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Permissions = request.Permissions,
                InvitedByUserId = sharedByUserId,
                Message = request.Message,
                ExpiresAt = request.ExpiresAt ?? SystemClock.UtcNow.AddDays(7)
            };

            _dbContext.Set<ResourceInvitation>().Add(invitation);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Created resource invitation for {Email} to access {ResourceType}/{ResourceId}",
                LogRedaction.MaskEmail(request.Email), LogRedaction.Sanitize(resourceType), LogRedaction.Sanitize(resourceId));

            return ShareResult.SuccessWithInvitation(
                invitation.Id,
                request.Email,
                $"/invitations/{invitation.Id}/accept");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to share resource {ResourceType}/{ResourceId}", resourceType, resourceId);
            return ShareResult.Failure($"Failed to share resource: {ex.Message}");
        }
    }

    public async Task<PermissionUpdateResult> UpdateUserPermissionsAsync(
        TenantId tenantId,
        string resourceType,
        string resourceId,
        UpdatePermissionsRequest request,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var permission = await _dbContext.Set<ResourceUserPermission>()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId &&
                    p.ResourceType == resourceType &&
                    p.ResourceId == resourceId &&
                    p.UserId == request.UserId &&
                    p.RevokedAt == null,
                    cancellationToken);

            if (permission == null)
            {
                return PermissionUpdateResult.Failure("Permission record not found");
            }

            permission.Permissions = request.Permissions;
            if (request.ExpiresAt.HasValue)
            {
                permission.ExpiresAt = request.ExpiresAt.Value;
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Updated permissions for user {UserId} on {ResourceType}/{ResourceId}",
                request.UserId, resourceType, resourceId);

            return PermissionUpdateResult.SuccessResult(request.UserId, request.Permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update permissions for user {UserId}", request.UserId);
            return PermissionUpdateResult.Failure($"Failed to update permissions: {ex.Message}");
        }
    }

    public async Task<bool> RemoveUserAccessAsync(
        TenantId tenantId,
        string resourceType,
        string resourceId,
        Guid userId,
        Guid removedByUserId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var permission = await _dbContext.Set<ResourceUserPermission>()
                .FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId &&
                    p.ResourceType == resourceType &&
                    p.ResourceId == resourceId &&
                    p.UserId == userId &&
                    p.RevokedAt == null,
                    cancellationToken);

            if (permission == null)
            {
                return false;
            }

            permission.Revoke(removedByUserId, reason);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Removed access for user {UserId} from {ResourceType}/{ResourceId}",
                userId, resourceType, resourceId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove access for user {UserId}", userId);
            return false;
        }
    }

    public async Task<ResourceUsersResponse> GetResourceUsersAsync(
        TenantId tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var permissions = await _dbContext.Set<ResourceUserPermission>()
            .Where(p =>
                p.TenantId == tenantId &&
                p.ResourceType == resourceType &&
                p.ResourceId == resourceId &&
                p.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var invitations = await _dbContext.Set<ResourceInvitation>()
            .Where(i =>
                i.TenantId == tenantId &&
                i.ResourceType == resourceType &&
                i.ResourceId == resourceId &&
                i.Status == InvitationStatus.Pending)
            .ToListAsync(cancellationToken);

        return new ResourceUsersResponse
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            Users = permissions
                .Where(p => p.IsActive)
                .Select(p => new ResourceAccessDto(
                    p.UserId,
                    p.GrantedByUserName ?? "Unknown",
                    string.Empty, // Would need user lookup
                    p.Permissions,
                    p.GrantedAt,
                    p.ExpiresAt,
                    false))
                .ToList(),
            PendingInvitations = invitations
                .Where(i => i.IsPending)
                .Select(i => new PendingInvitationDto(
                    i.Id,
                    i.Email,
                    i.Permissions,
                    i.InvitedAt,
                    i.ExpiresAt,
                    i.Status.ToString()))
                .ToList(),
            TotalCount = permissions.Count(p => p.IsActive) + invitations.Count(i => i.IsPending)
        };
    }

    public async Task<string[]> GetEffectivePermissionsAsync(
        TenantId tenantId,
        Guid userId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var permission = await _dbContext.Set<ResourceUserPermission>()
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.UserId == userId &&
                p.ResourceType == resourceType &&
                p.ResourceId == resourceId &&
                p.RevokedAt == null,
                cancellationToken);

        if (permission == null || !permission.IsActive)
        {
            return [];
        }

        return permission.Permissions;
    }

    public async Task<bool> HasPermissionAsync(
        TenantId tenantId,
        Guid userId,
        string resourceType,
        string resourceId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var userPermission = await _dbContext.Set<ResourceUserPermission>()
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.UserId == userId &&
                p.ResourceType == resourceType &&
                p.ResourceId == resourceId &&
                p.RevokedAt == null,
                cancellationToken);

        return userPermission?.HasPermission(permission) ?? false;
    }

    public async Task<bool> AcceptInvitationAsync(
        Guid invitationId,
        Guid acceptingUserId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _dbContext.Set<ResourceInvitation>()
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken).ConfigureAwait(false);

        if (invitation == null || !invitation.Accept(acceptingUserId))
        {
            return false;
        }

        // Create the actual permission
        var permission = new ResourceUserPermission
        {
            TenantId = invitation.TenantId,
            UserId = acceptingUserId,
            ResourceType = invitation.ResourceType,
            ResourceId = invitation.ResourceId,
            Permissions = invitation.Permissions,
            GrantedByUserId = invitation.InvitedByUserId,
            GrantedByUserName = invitation.InvitedByUserName,
            ExpiresAt = invitation.ExpiresAt
        };

        _dbContext.Set<ResourceUserPermission>().Add(permission);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Invitation {InvitationId} accepted by user {UserId}",
            invitationId, acceptingUserId);

        return true;
    }

    public async Task<bool> DeclineInvitationAsync(
        Guid invitationId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _dbContext.Set<ResourceInvitation>()
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken).ConfigureAwait(false);

        if (invitation == null || !invitation.Decline(reason))
        {
            return false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RevokeInvitationAsync(
        Guid invitationId,
        Guid revokedByUserId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _dbContext.Set<ResourceInvitation>()
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken).ConfigureAwait(false);

        if (invitation == null || !invitation.Revoke(revokedByUserId))
        {
            return false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<ResourceInvitation>> GetPendingInvitationsAsync(
        TenantId tenantId,
        string email,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<ResourceInvitation>()
            .Where(i =>
                i.TenantId == tenantId &&
                i.Email == email &&
                i.Status == InvitationStatus.Pending &&
                (i.ExpiresAt == null || i.ExpiresAt > SystemClock.UtcNow))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceUserPermission>> GetUserResourcesAsync(
        TenantId tenantId,
        Guid userId,
        string? resourceType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<ResourceUserPermission>()
            .Where(p =>
                p.TenantId == tenantId &&
                p.UserId == userId &&
                p.RevokedAt == null);

        if (!string.IsNullOrEmpty(resourceType))
        {
            query = query.Where(p => p.ResourceType == resourceType);
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<BulkShareResult> BulkShareResourceAsync(
        TenantId tenantId,
        string resourceType,
        string resourceId,
        IEnumerable<ShareResourceRequest> requests,
        Guid sharedByUserId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ShareResult>();
        var successCount = 0;
        var failureCount = 0;

        foreach (var request in requests)
        {
            var result = await ShareResourceAsync(
                tenantId, resourceType, resourceId, request, sharedByUserId, cancellationToken).ConfigureAwait(false);

            results.Add(result);
            if (result.Success)
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }
        }

        return new BulkShareResult
        {
            Success = failureCount == 0,
            SuccessCount = successCount,
            FailureCount = failureCount,
            Results = results
        };
    }

    public async Task RecordAccessAsync(
        TenantId tenantId,
        Guid userId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var permission = await _dbContext.Set<ResourceUserPermission>()
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.UserId == userId &&
                p.ResourceType == resourceType &&
                p.ResourceId == resourceId &&
                p.RevokedAt == null,
                cancellationToken);

        if (permission != null)
        {
            permission.RecordAccess();
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Automatically grants owner permissions when a resource is created.
    ///     This implements the auto-grant on creation pattern.
    /// </summary>
    public async Task<bool> GrantOwnerPermissionsOnCreationAsync(
        TenantId tenantId,
        Guid ownerId,
        string resourceType,
        string resourceId,
        string[]? ownerPermissions = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Default owner permissions - full access
            var permissions = ownerPermissions ?? new[]
            {
                "read", "write", "delete", "share", "admin", "manage_permissions"
            };

            // Check if permission already exists
            var existingPermission = await _dbContext.Set<ResourceUserPermission>()
                .AnyAsync(p =>
                    p.TenantId == tenantId &&
                    p.UserId == ownerId &&
                    p.ResourceType == resourceType &&
                    p.ResourceId == resourceId &&
                    p.RevokedAt == null,
                    cancellationToken);

            if (existingPermission)
            {
                _logger.LogDebug(
                    "Owner permissions already exist for user {UserId} on {ResourceType}/{ResourceId}",
                    ownerId, resourceType, resourceId);
                return true;
            }

            // Create owner permission record
            var permission = new ResourceUserPermission
            {
                TenantId = tenantId,
                UserId = ownerId,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Permissions = permissions,
                GrantedAt = SystemClock.UtcNow,
                GrantedByUserId = ownerId, // Self-granted as owner/creator
                GrantedByUserName = "System (Owner)",
                IsOwner = true // Mark as owner permission
            };

            _dbContext.Set<ResourceUserPermission>().Add(permission);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Auto-granted owner permissions for user {UserId} on {ResourceType}/{ResourceId}: [{Permissions}]",
                ownerId, resourceType, resourceId, string.Join(", ", permissions));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to auto-grant owner permissions for user {UserId} on {ResourceType}/{ResourceId}",
                ownerId, resourceType, resourceId);
            return false;
        }
    }
}
