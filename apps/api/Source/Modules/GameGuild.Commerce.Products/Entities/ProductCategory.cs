using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Products;

/// <summary>
///     A node in the hierarchical product catalog (issue #401).
///     Categories form a single-parent tree via <see cref="ParentCategoryId"/>; the
///     depth of the tree is capped at <see cref="MaxDepth"/> and cycles are rejected
///     by <c>ProductCategoryValidator</c> before any write. Each category optionally
///     carries a metadata dictionary (<see cref="AttributesJson"/>, jsonb, at most
///     <see cref="MaxAttributeKeys"/> keys) for storefront-specific attributes.
/// </summary>
[Table("product_categories")]
[Index(nameof(ParentCategoryId))]
[Index(nameof(DisplayOrder))]
[Index(nameof(TenantId), nameof(Slug), IsUnique = true)]
public class ProductCategory : EntityBase
{
    /// <summary>Maximum depth of the category tree (root = depth 0).</summary>
    public const int MaxDepth = 5;

    /// <summary>Maximum number of attribute keys allowed on one category.</summary>
    public const int MaxAttributeKeys = 20;

    /// <summary>Default constructor (EF Core).</summary>
    public ProductCategory() { }

    /// <summary>Constructor for partial initialization.</summary>
    public ProductCategory(object partial) : base(partial) { }

    /// <summary>Display name of the category (tenant-unique).</summary>
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe identifier, tenant-unique alongside <see cref="EntityBase{TKey}.TenantId"/>.</summary>
    [Required]
    [MaxLength(150)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Optional description of the category.</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    ///     Parent category ID. <c>null</c> marks a root category. The chain of parents
    ///     must stay acyclic and within <see cref="MaxDepth"/>.
    /// </summary>
    public Guid? ParentCategoryId { get; set; }

    /// <summary>Navigation property to the parent category.</summary>
    [ForeignKey(nameof(ParentCategoryId))]
    public virtual ProductCategory? Parent { get; set; }

    /// <summary>Navigation property to direct child categories.</summary>
    public virtual ICollection<ProductCategory> Children { get; set; } = new List<ProductCategory>();

    /// <summary>
    ///     Optional category metadata as a JSON object (jsonb column). Values are
    ///     free-form strings (for example <c>{"theme":"dark","locale":"pt-BR"}</c>);
    ///     at most <see cref="MaxAttributeKeys"/> keys are accepted.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? AttributesJson { get; set; }

    /// <summary>Sort order for storefront rendering.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    ///     Factory method to create a new product category.
    /// </summary>
    public static ProductCategory Create(
        string name,
        string slug,
        Guid? parentCategoryId = null,
        string? description = null,
        string? attributesJson = null,
        int displayOrder = 0,
        Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Category slug is required", nameof(slug));

        ValidateAttributes(attributesJson);

        return new ProductCategory
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = NormalizeSlug(slug),
            Description = description,
            ParentCategoryId = parentCategoryId,
            AttributesJson = attributesJson,
            DisplayOrder = displayOrder,
            TenantId = tenantId
        };
    }

    /// <summary>
    ///     Renames the category and updates its optional description.
    /// </summary>
    public void UpdateDetails(string? name, string? description)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();

        if (description != null)
            Description = description;

        Touch();
    }

    /// <summary>
    ///     Replaces the slug (normalized to a URL-safe form).
    /// </summary>
    public void SetSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Category slug is required", nameof(slug));

        Slug = NormalizeSlug(slug);
        Touch();
    }

    /// <summary>
    ///     Replaces the metadata dictionary (JSON object, ≤ <see cref="MaxAttributeKeys"/> keys).
    ///     A <c>null</c> value clears the attributes.
    /// </summary>
    public void SetAttributes(string? attributesJson)
    {
        ValidateAttributes(attributesJson);
        AttributesJson = attributesJson;
        Touch();
    }

    /// <summary>
    ///     Parses <see cref="AttributesJson"/> into a dictionary. Returns an empty
    ///     dictionary when no attributes are stored.
    /// </summary>
    public Dictionary<string, string> GetAttributes()
    {
        if (string.IsNullOrWhiteSpace(AttributesJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(AttributesJson) ?? [];
        }
        catch (JsonException)
        {
            // Malformed payloads must never surface as server errors; treat as absent.
            return [];
        }
    }

    /// <summary>Normalizes a slug to lowercase with hyphens.</summary>
    public static string NormalizeSlug(string slug)
    {
        var trimmed = slug.Trim().ToLowerInvariant();
        return trimmed.Replace(' ', '-');
    }

    private static void ValidateAttributes(string? attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson))
            return;

        Dictionary<string, string> attributes;
        try
        {
            attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(attributesJson) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Category attributes must be a JSON object of string values", ex);
        }

        if (attributes.Count > MaxAttributeKeys)
            throw new ArgumentException($"Category attributes support at most {MaxAttributeKeys} keys");
    }
}
