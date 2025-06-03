using System.ComponentModel.DataAnnotations;

namespace cms.Common.Entities;

/// <summary>
/// Abstract base class for resources that can have permissions and be localized.
/// Provides common functionality for content resources like articles, courses, media, etc.
/// Mirrors the TypeScript ResourceBase functionality from the API.
/// </summary>
public abstract class ResourceBase : BaseEntity, ILocalizable, IPermissionable, ITenantable
{
    /// <summary>
    /// Navigation property to the owner of this resource
    /// Entity Framework will automatically create the OwnerId foreign key
    /// </summary>
    [Required]
    public virtual Modules.User.Models.User Owner
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Visibility status of the resource (Public, Private, Restricted, etc.)
    /// </summary>
    [Required]
    public VisibilityStatus Visibility
    {
        get;
        set;
    } = VisibilityStatus.Private;

    /// <summary>
    /// Navigation property to resource metadata
    /// Entity Framework will automatically create the MetadataId foreign key
    /// </summary>
    public virtual ResourceMetadata? Metadata
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the tenant
    /// Entity Framework will automatically create the TenantId foreign key
    /// </summary>
    public virtual Modules.Tenant.Models.Tenant? Tenant
    {
        get;
        set;
    }

    /// <summary>
    /// Indicates whether this resource is accessible across all tenants (when Tenant is null)
    /// or only within a specific tenant
    /// </summary>
    public bool IsGlobal => Tenant == null;

    /// <summary>
    /// Collection of permissions assigned to this resource
    /// </summary>
    public virtual ICollection<ResourcePermission> ResourcePermissions
    {
        get;
        set;
    } = new List<ResourcePermission>();

    /// <summary>
    /// Collection of roles assigned to this resource
    /// </summary>
    public virtual ICollection<ResourceRole> ResourceRoles
    {
        get;
        set;
    } = new List<ResourceRole>();

    /// <summary>
    /// Collection of localizations for this resource
    /// </summary>
    public virtual ICollection<ResourceLocalization> Localizations
    {
        get;
        set;
    } = new List<ResourceLocalization>();


    /// <summary>
    /// Adds a localization for a specific field in the given language
    /// </summary>
    public virtual ResourceLocalization AddLocalization(string fieldName, string content, Language language, LocalizationStatus status = LocalizationStatus.Draft)
    {
        var localization = new ResourceLocalization
        {
            ResourceType = GetType().Name,
            Language = language,
            FieldName = fieldName,
            Content = content,
            Status = status
        };

        Localizations.Add(localization);

        return localization;
    }

    /// <summary>
    /// Grants permission to a user for this resource
    /// </summary>
    public virtual ResourcePermission GrantPermission(Modules.User.Models.User user, PermissionLevel permission)
    {
        var resourcePermission = new ResourcePermission
        {
            User = user,
            ResourceType = GetType().Name,
            Permission = permission,
            GrantedAt = DateTime.UtcNow,
            IsActive = true
        };

        ResourcePermissions.Add(resourcePermission);

        return resourcePermission;
    }

    /// <summary>
    /// Assigns this resource to a specific tenant
    /// </summary>
    /// <param name="tenant">The tenant to assign this resource to</param>
    public void AssignToTenant(Modules.Tenant.Models.Tenant tenant)
    {
        Tenant = tenant;
    }

    /// <summary>
    /// Makes this resource global (accessible across all tenants)
    /// </summary>
    public void MakeGlobal()
    {
        Tenant = null;
    }
}
