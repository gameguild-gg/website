using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using GameGuild.Common.Entities;

namespace GameGuild.Modules.Comment.Models;

/// <summary>
/// Resource-specific permissions for Comment entities (Layer 3 of DAC permission system)
/// Provides granular permission control for individual comments
/// </summary>
[Table("CommentPermissions")]
[Index(nameof(UserId), nameof(TenantId), nameof(ResourceId), IsUnique = true, Name = "IX_CommentPermissions_User_Tenant_Resource")]
[Index(nameof(ResourceId), nameof(UserId), Name = "IX_CommentPermissions_Resource_User")]
[Index(nameof(TenantId), Name = "IX_CommentPermissions_TenantId")]
[Index(nameof(ExpiresAt), Name = "IX_CommentPermissions_Expiration")]
public class CommentPermission : ResourcePermission<Common.Entities.Comment>
{
    // Comment-specific computed properties
    
    /// <summary>
    /// Check if user can edit this specific comment
    /// </summary>
    public bool CanEdit => HasPermission(PermissionType.Edit) && IsValid;
    
    /// <summary>
    /// Check if user can reply to this specific comment
    /// </summary>
    public bool CanReply => HasPermission(PermissionType.Reply) && IsValid;
    
    /// <summary>
    /// Check if user can moderate this specific comment
    /// </summary>
    public bool CanModerate => HasPermission(PermissionType.Review) && IsValid;
    
    /// <summary>
    /// Check if user can delete this specific comment (owners always can delete)
    /// </summary>
    public bool CanDelete => HasPermission(PermissionType.Delete) && IsValid;
    
    /// <summary>
    /// Check if user can flag this comment for review
    /// </summary>
    public bool CanFlag => HasPermission(PermissionType.Flag) && IsValid;
    
    /// <summary>
    /// Check if user can vote on this comment
    /// </summary>
    public bool CanVote => HasPermission(PermissionType.Vote) && IsValid;
}
