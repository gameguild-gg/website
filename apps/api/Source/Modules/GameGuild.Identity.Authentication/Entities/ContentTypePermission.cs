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
    ///     Parent content type in the registered type hierarchy (issue #314). When set on
    ///     a registered row, the content type inherits the parent's permission grants:
    ///     the effective resolver mints the parent's grants under the child's qualified
    ///     name (<c>{ChildContentTypeName}.{permission}</c>). Cycle-guarded — a chain that
    ///     loops contributes nothing (fail-closed).
    /// </summary>
    [MaxLength(256)]
    public string? ParentContentTypeName { get; set; }

    /// <summary>
    ///     Restriction contract for the content type (issue #314): a JSON object of
    ///     restriction knobs applied when the type's grants are administered, e.g.
    ///     <c>{"blockedPermissions":["Delete"],"maxGrantDurationHours":720}</c>.
    ///     Restrictions are advisory metadata validated at grant time; an empty value
    ///     imposes no restrictions.
    /// </summary>
    [MaxLength(2000)]
    public string? Restrictions { get; set; }

    /// <summary>
    ///     Scope selectors declaring what the content type applies to (issue #314): a
    ///     JSON array of selectors, e.g. <c>["tenant:all"]</c> or
    ///     <c>["resource-type:Project","module:Content"]</c>. An empty value means the
    ///     type is unscoped (applies wherever its name is referenced).
    /// </summary>
    [MaxLength(2000)]
    public string? AppliesTo { get; set; }

    /// <summary>
    ///     Registration flag (issue #314): only registered content types participate in
    ///     hierarchy traversal (as parents) and type governance. Defaults to false so
    ///     pre-existing grant rows keep their exact-name semantics until an administrator
    ///     registers the type.
    /// </summary>
    public bool IsRegistered { get; set; }

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

    /// <summary>
    ///     Sets the hierarchy parent (issue #314). A content type cannot be its own
    ///     ancestor: self-parenting is rejected, and chain cycles are detected by the
    ///     hierarchy provider at traversal time (fail-closed).
    /// </summary>
    /// <param name="parentContentTypeName">Parent content type name, or null to detach.</param>
    public void SetHierarchyParent(string? parentContentTypeName)
    {
        if (parentContentTypeName is not null
            && string.IsNullOrWhiteSpace(parentContentTypeName.Trim()))
        {
            throw new ArgumentException("Parent content type name cannot be whitespace", nameof(parentContentTypeName));
        }

        var normalized = parentContentTypeName?.Trim();
        if (string.Equals(normalized, ContentTypeName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A content type cannot be its own hierarchy parent");
        }

        ParentContentTypeName = normalized;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Sets the restriction contract (issue #314). Must be a JSON object when supplied.
    /// </summary>
    public void SetRestrictions(string? restrictionsJson)
    {
        if (!string.IsNullOrWhiteSpace(restrictionsJson))
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(restrictionsJson);
                if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    throw new InvalidOperationException("Restrictions must be a JSON object");
                }
            }
            catch (System.Text.Json.JsonException exception)
            {
                throw new InvalidOperationException("Restrictions must be well-formed JSON", exception);
            }
        }

        Restrictions = string.IsNullOrWhiteSpace(restrictionsJson) ? null : restrictionsJson;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Sets the applies-to scope selectors (issue #314). Must be a JSON array when supplied.
    /// </summary>
    public void SetAppliesTo(string? appliesToJson)
    {
        if (!string.IsNullOrWhiteSpace(appliesToJson))
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(appliesToJson);
                if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                {
                    throw new InvalidOperationException("AppliesTo must be a JSON array of scope selectors");
                }
            }
            catch (System.Text.Json.JsonException exception)
            {
                throw new InvalidOperationException("AppliesTo must be well-formed JSON", exception);
            }
        }

        AppliesTo = string.IsNullOrWhiteSpace(appliesToJson) ? null : appliesToJson;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Registers the content type for hierarchy traversal and governance (issue #314).
    /// </summary>
    public void Register(Guid registeredBy)
    {
        IsRegistered = true;
        // The acting administrator is preserved on the shared grant-audit field.
        GrantedBy = registeredBy;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Deregisters the content type (issue #314): the type stops acting as a hierarchy
    ///     parent; existing grants keep their exact-name semantics.
    /// </summary>
    public void Deregister(Guid deregisteredBy)
    {
        IsRegistered = false;
        GrantedBy = deregisteredBy;
        UpdatedAt = SystemClock.UtcNow;
    }
}
