using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cms.Common.Entities;

/// <summary>
/// Base class for resource-specific permissions (Layer 3 of DAC permission system)
/// Generic implementation allows strong typing for each content type
/// </summary>
public abstract class ResourcePermission<T> : WithPermissions where T : BaseEntity
{
    /// <summary>
    /// Resource reference - strongly typed to the content entity
    /// </summary>
    [Required]
    public Guid ResourceId { get; set; }
    
    /// <summary>
    /// Navigation property to the resource entity
    /// </summary>
    [ForeignKey(nameof(ResourceId))]
    public virtual T Resource { get; set; } = null!;
    
    // Computed properties specific to resource permissions
    
    /// <summary>
    /// Check if this is a default permission for this resource in a specific tenant
    /// </summary>
    public bool IsDefaultResourcePermission => UserId == null && TenantId != null;
    
    /// <summary>
    /// Check if this is a global default permission for this resource
    /// </summary>
    public bool IsGlobalResourceDefault => UserId == null && TenantId == null;
    
    /// <summary>
    /// Check if this is a user-specific permission for this resource
    /// </summary>
    public bool IsUserResourcePermission => UserId != null;
}
