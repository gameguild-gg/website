using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace cms.Common.Entities;

/// <summary>
/// Content-type-wide permissions (Layer 2 of DAC permission system)
/// Allows setting permissions for specific content types within a tenant
/// </summary>
[Table("ContentTypePermissions")]
[Index(nameof(ContentType), nameof(UserId), nameof(TenantId), IsUnique = true, Name = "IX_ContentTypePermissions_ContentType_User_Tenant")]
[Index(nameof(ContentType), nameof(TenantId), Name = "IX_ContentTypePermissions_ContentType_Tenant")]
[Index(nameof(UserId), Name = "IX_ContentTypePermissions_UserId")]
[Index(nameof(TenantId), Name = "IX_ContentTypePermissions_TenantId")]
[Index(nameof(ExpiresAt), Name = "IX_ContentTypePermissions_ExpiresAt")]
public class ContentTypePermission : WithPermissions
{
    /// <summary>
    /// The content type this permission applies to (e.g., "Article", "Video", "Discussion")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;
}
