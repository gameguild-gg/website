using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Content-Type permissions (Layer 2 of the 3-layer permission system)
///     Allows setting permissions for specific content types within tenants
///     Provides more granular control than tenant-wide permissions
/// </summary>
public class ContentTypePermission : WithPermissions
{
    /// <summary>
    ///     Default parameterless constructor (required by Entity Framework)
    /// </summary>
    public ContentTypePermission() { }

    /// <summary>
    ///     Constructor for creating a content-type permission
    /// </summary>
    /// <param name="userId">User ID (null for default permissions)</param>
    /// <param name="tenantId">Tenant ID (null for global permissions)</param>
    /// <param name="contentTypeName">Name of the content type</param>
    public ContentTypePermission(Guid? userId, Guid? tenantId, string contentTypeName) : base(userId, tenantId) { ContentTypeName = contentTypeName ?? throw new ArgumentNullException(nameof(contentTypeName)); }

    /// <summary>
    ///     Name of the content type this permission applies to
    ///     Examples: "Property", "Document", "Report", "Contract", etc.
    /// </summary>
    [MaxLength(256)]
    public string ContentTypeName { get; set; } = string.Empty;

    /// <summary>
    ///     Description of what this content type represents
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    ///     Check if this is a default permission for the content type (applies to all users)
    /// </summary>
    /// <returns>True if this is a default permission</returns>
    public bool IsDefaultPermission() { return !UserId.HasValue; }

    /// <summary>
    ///     Check if this is a user-specific permission for the content type
    /// </summary>
    /// <returns>True if this is a user-specific permission</returns>
    public bool IsUserSpecificPermission() { return UserId.HasValue; }

    /// <summary>
    ///     Update the content type name
    /// </summary>
    /// <param name="newContentTypeName">New content type name</param>
    public void UpdateContentTypeName(string newContentTypeName)
    {
        if (string.IsNullOrWhiteSpace(newContentTypeName))
        {
            throw new ArgumentException("Content type name cannot be null or empty", nameof(newContentTypeName));
        }

        ContentTypeName = newContentTypeName;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Update the description
    /// </summary>
    /// <param name="newDescription">New description</param>
    public void UpdateDescription(string? newDescription)
    {
        Description = newDescription;
        UpdatedAt = SystemClock.UtcNow;
    }
}
