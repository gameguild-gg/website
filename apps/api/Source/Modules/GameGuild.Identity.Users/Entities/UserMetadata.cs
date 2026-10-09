using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Users;

/// <summary>
///     User metadata entity for storing custom fields, tags, and external references
/// </summary>
[Table("UserMetadata")]
[Index(nameof(UserId), IsUnique = true)]
public class UserMetadata : EntityBase
{
    /// <summary>
    ///     Default constructor
    /// </summary>
    public UserMetadata() { }

    /// <summary>
    ///     Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial user metadata data</param>
    public UserMetadata(object partial) : base(partial) { }

    /// <summary>
    ///     ID of the user this metadata belongs to
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    ///     Navigation property to the user
    /// </summary>
    public virtual User User { get; set; } = null!;

    /// <summary>
    ///     Custom fields as JSON (key-value pairs)
    /// </summary>
    [Column(TypeName = "jsonb")]
    [MaxLength(50000)]
    public string CustomFields { get; set; } = "{}";

    /// <summary>
    ///     User tags for categorization and filtering
    /// </summary>
    [Column(TypeName = "jsonb")]
    [MaxLength(10000)]
    public string Tags { get; set; } = "[]";

    /// <summary>
    ///     External system references and IDs
    /// </summary>
    [Column(TypeName = "jsonb")]
    [MaxLength(25000)]
    public string ExternalReferences { get; set; } = "{}";

    /// <summary>
    ///     Additional metadata notes
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    ///     Get custom fields as dictionary
    /// </summary>
    public Dictionary<string, object?> GetCustomFields()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(CustomFields) ?? new Dictionary<string, object?>(); }
        catch { return new Dictionary<string, object?>(); }
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
    public List<string> GetTags()
    {
        try { return JsonSerializer.Deserialize<List<string>>(Tags) ?? new List<string>(); }
        catch { return new List<string>(); }
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
    public Dictionary<string, string> GetExternalReferences()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(ExternalReferences) ?? new Dictionary<string, string>(); }
        catch { return new Dictionary<string, string>(); }
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
    ///     Update metadata notes
    /// </summary>
    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        Touch();
    }

    /// <summary>
    ///     Factory method to create user metadata
    /// </summary>
    public static UserMetadata Create(Guid userId, Dictionary<string, object?>? customFields = null, List<string>? tags = null)
    {
        var metadata = new UserMetadata { UserId = userId };

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
