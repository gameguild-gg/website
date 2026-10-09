using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Represents an Access Control List entry for a resource.
///     Supports User, Role, Group, and Anonymous principals with deny-first evaluation.
/// </summary>
[Table("AccessControlListEntries")]
[Index(nameof(TenantId), nameof(ResourceType), nameof(ResourceId), nameof(PrincipalType), nameof(PrincipalId), IsUnique = true)]
[Index(nameof(TenantId), nameof(PrincipalType), nameof(PrincipalId))]
[Index(nameof(ResourceType), nameof(ResourceId))]
public class AccessControlListEntry : EntityBase
{
    /// <summary>
    ///     Gets or sets the tenant ID this Access Control List entry belongs to.
    /// </summary>
    [Required]
    public new Guid TenantId { get; set; }

    /// <summary>
    ///     Gets or sets the type of principal (User, Role, Group, Anonymous).
    /// </summary>
    [Required]
    public AclPrincipalType PrincipalType { get; set; } = AclPrincipalType.User;

    /// <summary>
    ///     Gets or sets the principal ID (User ID, Role ID, or Group ID).
    ///     For Anonymous principal type, this is null.
    /// </summary>
    public Guid? PrincipalId { get; set; }

    /// <summary>
    ///     Gets or sets the user ID who has access.
    ///     This is a computed property for backward compatibility.
    ///     Use PrincipalType and PrincipalId for new code.
    /// </summary>
    [NotMapped]
    [Obsolete("Use PrincipalType and PrincipalId instead.")]
    public Guid UserId
    {
        get => PrincipalType == AclPrincipalType.User && PrincipalId.HasValue ? PrincipalId.Value : Guid.Empty;
        set
        {
            PrincipalType = AclPrincipalType.User;
            PrincipalId = value;
        }
    }

    /// <summary>
    ///     Gets or sets the resource type (e.g., "Course", "Project", "Document").
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the resource ID.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the access level granted (or denied).
    /// </summary>
    [Required]
    public AccessLevel AccessLevel { get; set; } = AccessLevel.None;

    /// <summary>
    ///     Gets or sets whether this is a deny entry.
    ///     Deny entries take precedence over allow entries in evaluation.
    /// </summary>
    public bool IsDenied { get; set; }

    /// <summary>
    ///     Gets or sets the user ID who granted this access.
    /// </summary>
    [Required]
    public Guid GrantedBy { get; set; }

    /// <summary>
    ///     Gets or sets when this access was granted.
    /// </summary>
    [Required]
    public DateTime GrantedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>
    ///     Gets or sets optional expiration date for the access.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///     Gets or sets whether the access is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Gets or sets optional notes about the access grant.
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    ///     Returns true if this entry has not expired and is active.
    /// </summary>
    public bool IsEffective
    {
        get
        {
            if (!IsActive)
            {
                return false;
            }

            if (ExpiresAt is null)
            {
                return true;
            }

            if (ExpiresAt.Value <= SystemClock.UtcNow)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    ///     Creates a user-based ACL entry.
    /// </summary>
    public static AccessControlListEntry ForUser(
        Guid tenantId,
        Guid userId,
        string resourceType,
        string resourceId,
        AccessLevel accessLevel,
        Guid grantedBy,
        bool isDenied = false)
    {
        return new AccessControlListEntry
        {
            TenantId = tenantId,
            PrincipalType = AclPrincipalType.User,
            PrincipalId = userId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            AccessLevel = accessLevel,
            IsDenied = isDenied,
            GrantedBy = grantedBy,
            GrantedAt = SystemClock.UtcNow,
            IsActive = true
        };
    }

    /// <summary>
    ///     Creates a role-based ACL entry.
    /// </summary>
    public static AccessControlListEntry ForRole(
        Guid tenantId,
        Guid roleId,
        string resourceType,
        string resourceId,
        AccessLevel accessLevel,
        Guid grantedBy,
        bool isDenied = false)
    {
        return new AccessControlListEntry
        {
            TenantId = tenantId,
            PrincipalType = AclPrincipalType.Role,
            PrincipalId = roleId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            AccessLevel = accessLevel,
            IsDenied = isDenied,
            GrantedBy = grantedBy,
            GrantedAt = SystemClock.UtcNow,
            IsActive = true
        };
    }

    /// <summary>
    ///     Creates a group-based ACL entry.
    /// </summary>
    public static AccessControlListEntry ForGroup(
        Guid tenantId,
        Guid groupId,
        string resourceType,
        string resourceId,
        AccessLevel accessLevel,
        Guid grantedBy,
        bool isDenied = false)
    {
        return new AccessControlListEntry
        {
            TenantId = tenantId,
            PrincipalType = AclPrincipalType.Group,
            PrincipalId = groupId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            AccessLevel = accessLevel,
            IsDenied = isDenied,
            GrantedBy = grantedBy,
            GrantedAt = SystemClock.UtcNow,
            IsActive = true
        };
    }

    /// <summary>
    ///     Creates an anonymous (public) ACL entry.
    /// </summary>
    public static AccessControlListEntry ForAnonymous(
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel accessLevel,
        Guid grantedBy,
        bool isDenied = false)
    {
        return new AccessControlListEntry
        {
            TenantId = tenantId,
            PrincipalType = AclPrincipalType.Anonymous,
            PrincipalId = null,
            ResourceType = resourceType,
            ResourceId = resourceId,
            AccessLevel = accessLevel,
            IsDenied = isDenied,
            GrantedBy = grantedBy,
            GrantedAt = SystemClock.UtcNow,
            IsActive = true
        };
    }
}
