using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Tenants;

/// <summary>
///     Tenant metadata entity for storing custom fields, tags, and external references
/// </summary>
[Table("TenantMetadata")]
[Index(nameof(TenantId), IsUnique = true)]
public class TenantMetadata : EntityBase
{
    /// <summary>
    ///     Default constructor
    /// </summary>
    public TenantMetadata() { }

    /// <summary>
    ///     Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial tenant metadata data</param>
    public TenantMetadata(object partial) : base(partial) { }

    /// <summary>
    ///     ID of the tenant this metadata belongs to
    /// </summary>
    [Required]
    public new Guid TenantId { get; set; }

    /// <summary>
    ///     Navigation property to the tenant
    /// </summary>
    public virtual Tenant Tenant { get; set; } = null!;

    /// <summary>
    ///     Custom fields as JSON (key-value pairs)
    /// </summary>
    [Column(TypeName = "jsonb")]
    [MaxLength(10000)]
    public string CustomFields { get; set; } = "{}";

    /// <summary>
    ///     Tenant tags for categorization and filtering
    /// </summary>
    [Column(TypeName = "jsonb")]
    [MaxLength(5000)]
    public string Tags { get; set; } = "[]";

    /// <summary>
    ///     External system references and IDs
    /// </summary>
    [Column(TypeName = "jsonb")]
    [MaxLength(8000)]
    public string ExternalReferences { get; set; } = "{}";

    /// <summary>
    ///     Business information and details
    /// </summary>
    [Column(TypeName = "jsonb")]
    [MaxLength(8000)]
    public string BusinessInfo { get; set; } = "{}";

    /// <summary>
    ///     Contact information
    /// </summary>
    [Column(TypeName = "jsonb")]
    [MaxLength(8000)]
    public string ContactInfo { get; set; } = "{}";

    /// <summary>
    ///     Additional metadata notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    ///     Tenant industry or sector
    /// </summary>
    [MaxLength(100)]
    public string? Industry { get; set; }

    /// <summary>
    ///     Tenant size category
    /// </summary>
    public TenantSize? Size { get; set; }

    /// <summary>
    ///     Tenant type/category
    /// </summary>
    [MaxLength(50)]
    public string? Type { get; set; }

    /// <summary>
    ///     Get custom fields as dictionary
    /// </summary>
    public Dictionary<string, object?> GetCustomFields(ILogger? logger = null)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(CustomFields) ?? new Dictionary<string, object?>(); }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Failed to deserialize CustomFields for TenantMetadata {TenantId}. Returning empty dictionary.", TenantId);
            return new Dictionary<string, object?>();
        }
    }

    /// <summary>
    ///     Set custom fields from dictionary
    /// </summary>
    public void SetCustomFields(Dictionary<string, object?> fields)
    {
        CustomFields = JsonSerializer.Serialize(fields);
        Touch();
    }

    /// <summary>
    ///     Get tags as list
    /// </summary>
    public List<string> GetTags(ILogger? logger = null)
    {
        try { return JsonSerializer.Deserialize<List<string>>(Tags) ?? new List<string>(); }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Failed to deserialize Tags for TenantMetadata {TenantId}. Returning empty list.", TenantId);
            return new List<string>();
        }
    }

    /// <summary>
    ///     Set tags from list
    /// </summary>
    public void SetTags(List<string> tags)
    {
        Tags = JsonSerializer.Serialize(tags);
        Touch();
    }

    /// <summary>
    ///     Get external references as dictionary
    /// </summary>
    public Dictionary<string, string> GetExternalReferences(ILogger? logger = null)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(ExternalReferences) ?? new Dictionary<string, string>(); }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Failed to deserialize ExternalReferences for TenantMetadata {TenantId}. Returning empty dictionary.", TenantId);
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    ///     Set external references from dictionary
    /// </summary>
    public void SetExternalReferences(Dictionary<string, string> references)
    {
        ExternalReferences = JsonSerializer.Serialize(references);
        Touch();
    }

    /// <summary>
    ///     Get business information as dictionary
    /// </summary>
    public Dictionary<string, object?> GetBusinessInfo(ILogger? logger = null)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(BusinessInfo) ?? new Dictionary<string, object?>(); }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Failed to deserialize BusinessInfo for TenantMetadata {TenantId}. Returning empty dictionary.", TenantId);
            return new Dictionary<string, object?>();
        }
    }

    /// <summary>
    ///     Set business information from dictionary
    /// </summary>
    public void SetBusinessInfo(Dictionary<string, object?> info)
    {
        BusinessInfo = JsonSerializer.Serialize(info);
        Touch();
    }

    /// <summary>
    ///     Get contact information as dictionary
    /// </summary>
    public Dictionary<string, object?> GetContactInfo(ILogger? logger = null)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(ContactInfo) ?? new Dictionary<string, object?>(); }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Failed to deserialize ContactInfo for TenantMetadata {TenantId}. Returning empty dictionary.", TenantId);
            return new Dictionary<string, object?>();
        }
    }

    /// <summary>
    ///     Set contact information from dictionary
    /// </summary>
    public void SetContactInfo(Dictionary<string, object?> info)
    {
        ContactInfo = JsonSerializer.Serialize(info);
        Touch();
    }

    /// <summary>
    ///     Update tenant categorization
    /// </summary>
    public void UpdateCategorization(string? industry = null, TenantSize? size = null, string? type = null)
    {
        if (industry != null)
        {
            Industry = industry;
        }

        if (size != null)
        {
            Size = size;
        }

        if (type != null)
        {
            Type = type;
        }

        Touch();
    }

    /// <summary>
    ///     Update metadata notes
    /// </summary>
    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        Touch();
    }

    /// <summary>
    ///     Factory method to create tenant metadata
    /// </summary>
    public static TenantMetadata Create(Guid tenantId, Dictionary<string, object?>? customFields = null, List<string>? tags = null)
    {
        var metadata = new TenantMetadata { TenantId = tenantId };

        if (customFields != null)
        {
            metadata.SetCustomFields(customFields);
        }

        if (tags != null)
        {
            metadata.SetTags(tags);
        }

        return metadata;
    }
}

/// <summary>
///     Tenant size categories
/// </summary>
public enum TenantSize { Startup = 0, Small = 1, Medium = 2, Large = 3, Enterprise = 4 }
