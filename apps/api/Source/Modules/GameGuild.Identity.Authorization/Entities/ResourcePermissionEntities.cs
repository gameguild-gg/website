using GameGuild.CQRS.Models;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Represents a user's direct permissions on a specific resource.
///     Tracks who granted the permissions, when they were granted, and when they expire.
///     Inherits the shared permission-grant contract from <see cref="PermissionBase"/> (#352):
///     expiration, grant metadata, validation, audit and tenant-isolation behaviors come
///     from the base; the required (non-nullable) subject and computed activity state hide
///     their optional base equivalents, mirroring the tenant-column pattern already used
///     by this entity.
/// </summary>
public class ResourceUserPermission : PermissionBase
{
    /// <summary>
    ///     Gets or sets the tenant ID this permission belongs to.
    /// </summary>
    public new required TenantId TenantId { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who has these permissions.
    ///     Hides the optional base property with a required (non-nullable) equivalent.
    /// </summary>
    public new required Guid UserId { get; set; }

    /// <summary>
    ///     Gets or sets the type of resource.
    ///     Example: "Project", "Post", "Document", "Dataset"
    /// </summary>
    public required string ResourceType { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the resource.
    /// </summary>
    public required string ResourceId { get; set; }

    /// <summary>
    ///     Gets or sets the array of permission strings granted to the user.
    ///     Example: ["read", "write", "delete", "admin"]
    /// </summary>
    public required string[] Permissions { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who granted these permissions.
    /// </summary>
    public required Guid GrantedByUserId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the user who granted these permissions (for display).
    /// </summary>
    public string? GrantedByUserName { get; set; }

    /// <summary>
    ///     Gets or sets the date and time the permissions were revoked.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who revoked these permissions.
    /// </summary>
    public Guid? RevokedByUserId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the user who revoked these permissions (for display).
    /// </summary>
    public string? RevokedByUserName { get; set; }

    /// <summary>
    ///     Gets or sets the reason for revoking these permissions.
    /// </summary>
    public string? RevocationReason { get; set; }

    /// <summary>
    ///     Gets or sets whether this permission was auto-granted as the resource owner/creator.
    ///     Owner permissions are granted automatically when a resource is created.
    /// </summary>
    public bool IsOwner { get; set; }

    /// <summary>
    ///     Gets or sets the date and time the user last accessed this resource.
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    ///     Gets whether these permissions are currently active.
    ///     Computed from the revocation state; hides the optional stored base property.
    /// </summary>
    public new bool IsActive => RevokedAt == null && !IsExpired;

    /// <summary>
    ///     Gets whether these permissions have expired.
    ///     Computed equivalent of the base expiration check (inclusive boundary); hides the
    ///     base method so existing property-style call sites keep compiling.
    /// </summary>
    public new bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= SystemClock.UtcNow;

    /// <summary>
    ///     Gets whether the user can access the resource.
    /// </summary>
    public bool CanAccess => IsActive;

    /// <summary>
    ///     Unified audit view: who granted this permission.
    /// </summary>
    public override Guid? CreatedBy => GrantedByUserId;

    /// <summary>
    ///     Unified audit view: tenant scope of this permission grant.
    /// </summary>
    public override Guid? PermissionTenantId => TenantId.Value;

    /// <summary>
    ///     Fail-closed mapping of the stored permission strings to defined permission types.
    ///     Strings that do not map to a defined <see cref="PermissionType"/> are dropped.
    /// </summary>
    public override IReadOnlyList<PermissionType> GetGrantedPermissionTypes() { return ParsePermissionTypes(Permissions).ToList(); }

    /// <summary>
    ///     Effective-activity contract override: because this entity computes activity from
    ///     its revocation state (hiding the optional stored base property), the shared base
    ///     logic must observe the computed value, not the hidden storage.
    /// </summary>
    public override bool IsEffective() { return IsActive && !IsExpired; }

    /// <summary>
    ///     Expiration/lifecycle override: expiring a direct resource permission revokes it
    ///     (there is no stored activation flag on this entity).
    /// </summary>
    public override void Expire()
    {
        if (RevokedAt.HasValue) { return; }

        RevokedAt = SystemClock.UtcNow;
        Touch();
    }

    /// <summary>
    ///     Common validation override: additionally rejects an empty required subject.
    /// </summary>
    public override bool IsValid() { return base.IsValid() && UserId != Guid.Empty; }

    /// <summary>
    ///     Revokes the permissions for the user.
    /// </summary>
    /// <param name="revokedByUserId">The ID of the user revoking the permissions.</param>
    /// <param name="reason">Optional reason for revocation.</param>
    /// <returns>True if revoked successfully, false if already revoked.</returns>
    public bool Revoke(Guid revokedByUserId, string? reason = null)
    {
        if (RevokedAt.HasValue) { return false; }

        RevokedAt = SystemClock.UtcNow;
        RevokedByUserId = revokedByUserId;
        RevocationReason = reason;

        return true;
    }

    /// <summary>
    ///     Updates the permissions granted to the user.
    /// </summary>
    /// <param name="newPermissions">The new set of permissions.</param>
    /// <param name="updatedByUserId">The ID of the user making the update.</param>
    /// <returns>True if updated successfully.</returns>
    public bool UpdatePermissions(string[] newPermissions, Guid updatedByUserId)
    {
        if (!IsActive) { return false; }

        Permissions = newPermissions;

        return true;
    }

    /// <summary>
    ///     Records that the user accessed the resource.
    /// </summary>
    public void RecordAccess() { LastAccessedAt = SystemClock.UtcNow; }

    /// <summary>
    ///     Checks if the user has a specific permission on this resource.
    /// </summary>
    /// <param name="permission">The permission to check.</param>
    /// <returns>True if the user has the permission and it's active.</returns>
    public bool HasPermission(string permission)
    {
        return IsActive && Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Checks if the user has any of the specified permissions.
    /// </summary>
    /// <param name="permissions">The permissions to check.</param>
    /// <returns>True if the user has at least one of the permissions and it's active.</returns>
    public bool HasAnyPermission(string[] permissions)
    {
        return IsActive && Permissions.Any(p => permissions.Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Checks if the user has all of the specified permissions.
    /// </summary>
    /// <param name="permissions">The permissions to check.</param>
    /// <returns>True if the user has all of the permissions and it's active.</returns>
    public bool HasAllPermissions(string[] permissions)
    {
        return IsActive && permissions.All(p => Permissions.Contains(p, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>
///     Represents the status of a resource invitation.
/// </summary>
public enum InvitationStatus
{
    /// <summary>
    ///     Invitation is pending a response.
    /// </summary>
    Pending = 0,

    /// <summary>
    ///     Invitation has been accepted by the invitee.
    /// </summary>
    Accepted = 1,

    /// <summary>
    ///     Invitation has been declined by the invitee.
    /// </summary>
    Declined = 2,

    /// <summary>
    ///     Invitation has been revoked by the inviter or administrator.
    /// </summary>
    Revoked = 3,

    /// <summary>
    ///     Invitation has expired before being accepted or declined.
    /// </summary>
    Expired = 4
}

/// <summary>
///     Represents an invitation to share a resource with a user via email.
///     Tracks invitation status, expiration, and response.
/// </summary>
public class ResourceInvitation : EntityBase<Guid>
{
    /// <summary>
    ///     Gets or sets the tenant ID this invitation belongs to.
    /// </summary>
    public new required TenantId TenantId { get; set; }

    /// <summary>
    ///     Gets or sets the email address of the invitee.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    ///     Gets or sets the type of resource being shared.
    /// </summary>
    public required string ResourceType { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the resource being shared.
    /// </summary>
    public required string ResourceId { get; set; }

    /// <summary>
    ///     Gets or sets the array of permission strings being granted.
    ///     Example: ["read", "write", "admin"]
    /// </summary>
    public required string[] Permissions { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who sent the invitation.
    /// </summary>
    public required Guid InvitedByUserId { get; set; }

    /// <summary>
    ///     Gets or sets the name of the user who sent the invitation (for display).
    /// </summary>
    public string? InvitedByUserName { get; set; }

    /// <summary>
    ///     Gets or sets an optional message to include with the invitation.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    ///     Gets or sets the date and time the invitation was sent.
    /// </summary>
    public DateTime InvitedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>
    ///     Gets or sets the date and time the invitation expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///     Gets or sets the current status of the invitation.
    /// </summary>
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    /// <summary>
    ///     Gets or sets the date and time the invitation was accepted.
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who accepted the invitation.
    /// </summary>
    public Guid? AcceptedByUserId { get; set; }

    /// <summary>
    ///     Gets or sets the date and time the invitation was declined.
    /// </summary>
    public DateTime? DeclinedAt { get; set; }

    /// <summary>
    ///     Gets or sets the reason for declining the invitation.
    /// </summary>
    public string? DeclineReason { get; set; }

    /// <summary>
    ///     Gets or sets the date and time the invitation was revoked.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the user who revoked the invitation.
    /// </summary>
    public Guid? RevokedByUserId { get; set; }

    /// <summary>
    ///     Gets whether the invitation is still pending.
    /// </summary>
    public bool IsPending => Status == InvitationStatus.Pending && !IsExpired;

    /// <summary>
    ///     Gets whether the invitation has expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= SystemClock.UtcNow;

    /// <summary>
    ///     Gets whether the invitation can be accepted.
    /// </summary>
    public bool CanBeAccepted => Status == InvitationStatus.Pending && !IsExpired;

    /// <summary>
    ///     Gets whether the invitation can be revoked.
    /// </summary>
    public bool CanBeRevoked => Status == InvitationStatus.Pending;

    /// <summary>
    ///     Accepts the invitation for the specified user.
    /// </summary>
    /// <param name="acceptingUserId">The ID of the user accepting the invitation.</param>
    /// <returns>True if accepted successfully, false if already processed or expired.</returns>
    public bool Accept(Guid acceptingUserId)
    {
        if (!CanBeAccepted) { return false; }

        Status = InvitationStatus.Accepted;
        AcceptedAt = SystemClock.UtcNow;
        AcceptedByUserId = acceptingUserId;

        return true;
    }

    /// <summary>
    ///     Declines the invitation with an optional reason.
    /// </summary>
    /// <param name="reason">The reason for declining.</param>
    /// <returns>True if declined successfully, false if already processed or expired.</returns>
    public bool Decline(string? reason = null)
    {
        if (Status != InvitationStatus.Pending) { return false; }

        Status = InvitationStatus.Declined;
        DeclinedAt = SystemClock.UtcNow;
        DeclineReason = reason;

        return true;
    }

    /// <summary>
    ///     Revokes the invitation.
    /// </summary>
    /// <param name="revokedByUserId">The ID of the user revoking the invitation.</param>
    /// <returns>True if revoked successfully, false if already processed.</returns>
    public bool Revoke(Guid revokedByUserId)
    {
        if (!CanBeRevoked) { return false; }

        Status = InvitationStatus.Revoked;
        RevokedAt = SystemClock.UtcNow;
        RevokedByUserId = revokedByUserId;

        return true;
    }
}
