namespace GameGuild.Identity.Authorization.Models;

/// <summary>
///     Strongly-typed resource type identifiers for authorization.
///     Prevents typo-based security bypasses when specifying resource types.
/// </summary>
/// <remarks>
///     <para>
///         Resource types are used in:
///         <list type="bullet">
///             <item>ABAC policies (AbacPolicy.ResourceType)</item>
///             <item>DAC permission grants (resource-level permissions)</item>
///             <item>Delegated admin scopes (AllowedResourceTypes)</item>
///             <item>Audit logs (ResourceType field)</item>
///         </list>
///     </para>
///     <para>
///         Example: Instead of "Project", use ResourceTypes.Project
///     </para>
/// </remarks>
public abstract class ResourceType : IEquatable<ResourceType>
{
    /// <summary>
    ///     Gets the unique resource type identifier.
    /// </summary>
    public string Value { get; }

    /// <summary>
    ///     Gets a human-readable description of this resource type.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Initializes a new resource type.
    /// </summary>
    /// <param name="value">The resource type identifier.</param>
    /// <param name="description">Human-readable description.</param>
    protected ResourceType(string value, string description)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(description);

        Value = value;
        Description = description;
    }

    /// <summary>
    ///     Implicitly converts a ResourceType to its string value for backward compatibility.
    /// </summary>
    public static implicit operator string(ResourceType resourceType) => resourceType.Value;

    /// <summary>
    ///     Returns the resource type value as a string.
    /// </summary>
    public override string ToString() => Value;

    /// <inheritdoc />
    public bool Equals(ResourceType? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Value == other.Value;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ResourceType other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>Compares two resource types for equality.</summary>
    public static bool operator ==(ResourceType? left, ResourceType? right) => Equals(left, right);

    /// <summary>Compares two resource types for inequality.</summary>
    public static bool operator !=(ResourceType? left, ResourceType? right) => !Equals(left, right);
}

/// <summary>
///     All known resource types in the system.
///     Use these constants instead of magic strings.
/// </summary>
public static class ResourceTypes
{
    // ========================
    // USER & IDENTITY
    // ========================

    /// <summary>User resource type</summary>
    public static readonly ConcreteResourceType User = new("User", "User accounts and profiles");

    /// <summary>Role resource type</summary>
    public static readonly ConcreteResourceType Role = new("Role", "User roles for RBAC");

    /// <summary>Group resource type</summary>
    public static readonly ConcreteResourceType Group = new("Group", "User groups");

    /// <summary>Permission resource type</summary>
    public static readonly ConcreteResourceType Permission = new("Permission", "Permission grants");

    /// <summary>Tenant resource type</summary>
    public static readonly ConcreteResourceType Tenant = new("Tenant", "Multi-tenant organizations");

    // ========================
    // CONTENT & PROJECTS
    // ========================

    /// <summary>Project resource type</summary>
    public static readonly ConcreteResourceType Project = new("Project", "Projects");

    /// <summary>Content resource type</summary>
    public static readonly ConcreteResourceType Content = new("Content", "Generic content items");

    /// <summary>Document resource type</summary>
    public static readonly ConcreteResourceType Document = new("Document", "Documents and files");

    /// <summary>Course resource type</summary>
    public static readonly ConcreteResourceType Course = new("Course", "Educational courses");

    /// <summary>Program resource type</summary>
    public static readonly ConcreteResourceType Program = new("Program", "Programs (learning paths)");

    /// <summary>Post resource type</summary>
    public static readonly ConcreteResourceType Post = new("Post", "User posts and articles");

    // ========================
    // COMMERCE
    // ========================

    /// <summary>Product resource type</summary>
    public static readonly ConcreteResourceType Product = new("Product", "Products for sale");

    /// <summary>Order resource type</summary>
    public static readonly ConcreteResourceType Order = new("Order", "Purchase orders");

    /// <summary>PromoCode resource type</summary>
    public static readonly ConcreteResourceType PromoCode = new("PromoCode", "Promotional codes");

    /// <summary>Entitlement resource type</summary>
    public static readonly ConcreteResourceType Entitlement = new("Entitlement", "User entitlements");

    // ========================
    // SYSTEM
    // ========================

    /// <summary>System resource type (for audit/admin operations)</summary>
    public static readonly ConcreteResourceType System = new("System", "System-level operations");

    /// <summary>Audit resource type</summary>
    public static readonly ConcreteResourceType Audit = new("Audit", "Audit logs");

    /// <summary>Policy resource type</summary>
    public static readonly ConcreteResourceType Policy = new("Policy", "Authorization policies");

    // ========================
    // TESTING LAB
    // ========================

    /// <summary>Testing session resource type</summary>
    public static readonly ConcreteResourceType TestingSession = new("TestingSession", "Testing lab sessions");

    /// <summary>Testing location resource type</summary>
    public static readonly ConcreteResourceType TestingLocation = new("TestingLocation", "Testing lab locations");

    /// <summary>Testing feedback resource type</summary>
    public static readonly ConcreteResourceType TestingFeedback = new("TestingFeedback", "Testing lab feedback");

    /// <summary>Testing request resource type</summary>
    public static readonly ConcreteResourceType TestingRequest = new("TestingRequest", "Testing lab requests");

    /// <summary>Testing participant resource type</summary>
    public static readonly ConcreteResourceType TestingParticipant = new("TestingParticipant", "Testing lab participants");

    // ========================
    // ASSETS
    // ========================

    /// <summary>Asset resource type (binary assets and media files)</summary>
    public static readonly ConcreteResourceType Asset = new("Asset", "Binary assets and media files");

    /// <summary>Asset report resource type (content moderation reports)</summary>
    public static readonly ConcreteResourceType AssetReport = new("AssetReport", "Content moderation reports for assets");

    // ========================
    // VALIDATION
    // ========================

    /// <summary>
    ///     All registered resource types for validation.
    /// </summary>
    public static readonly IReadOnlyList<ResourceType> All = new ResourceType[]
    {
        User, Role, Group, Permission, Tenant,
        Project, Content, Document, Course, Program, Post,
        Product, Order, PromoCode, Entitlement,
        System, Audit, Policy,
        TestingSession, TestingLocation, TestingFeedback, TestingRequest, TestingParticipant,
        Asset, AssetReport
    };

    /// <summary>
    ///     Validates if a string is a known resource type.
    /// </summary>
    /// <param name="value">The resource type string to validate.</param>
    /// <returns>True if the value matches a known resource type.</returns>
    public static bool IsValid(string value) =>
        All.Any(rt => rt.Value.Equals(value, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Gets a resource type by its string value, or null if not found.
    /// </summary>
    /// <param name="value">The resource type string.</param>
    /// <returns>The matching ResourceType, or null.</returns>
    public static ResourceType? FromString(string value) =>
        All.FirstOrDefault(rt => rt.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
///     Concrete implementation of ResourceType for static definitions.
/// </summary>
public sealed class ConcreteResourceType : ResourceType
{
    /// <summary>
    ///     Creates a new concrete resource type.
    /// </summary>
    public ConcreteResourceType(string value, string description)
        : base(value, description)
    {
    }
}
