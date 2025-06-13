using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using GameGuild.Common.Entities;
using GameGuild.Common.Enums;

namespace GameGuild.Modules.Program.Models;

[Table("program_contents")]
[Index(nameof(ProgramId))]
[Index(nameof(ParentId))]
[Index(nameof(Type))]
[Index(nameof(Visibility))]
[Index(nameof(SortOrder))]
[Index(nameof(ProgramId), nameof(SortOrder))]
[Index(nameof(ParentId), nameof(SortOrder))]
[Index(nameof(IsRequired))]
public class ProgramContent : BaseEntity
{
    [Required]
    [ForeignKey(nameof(Program))]
    public Guid ProgramId
    {
        get;
        set;
    }

    /// <summary>
    /// For hierarchical content structure (e.g., modules containing lessons)
    /// </summary>
    [ForeignKey(nameof(Parent))]
    public Guid? ParentId
    {
        get;
        set;
    }

    [Required]
    [MaxLength(255)]
    public string Title
    {
        get;
        set;
    } = string.Empty;

    public string Description
    {
        get;
        set;
    } = string.Empty;

    public ProgramContentType Type
    {
        get;
        set;
    }

    /// <summary>
    /// Main content body stored as JSON to support rich content
    /// Structure varies by content type:
    /// - Page: {content: "HTML/Markdown", resources: []}
    /// - Assignment: {instructions: "", rubric: {}, submissionFormat: ""}
    /// - Code: {starterCode: "", testCases: [], language: ""}
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Body
    {
        get;
        set;
    } = "{}";

    /// <summary>
    /// Display order within the program or parent content
    /// </summary>
    public int SortOrder
    {
        get;
        set;
    } = 0;

    /// <summary>
    /// Whether this content is required for program completion
    /// </summary>
    public bool IsRequired
    {
        get;
        set;
    } = true;

    /// <summary>
    /// How this content should be graded (if applicable)
    /// </summary>
    public GradingMethod? GradingMethod
    {
        get;
        set;
    }

    /// <summary>
    /// Maximum points/score for this content (if gradeable)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? MaxPoints
    {
        get;
        set;
    }

    /// <summary>
    /// Estimated time to complete in minutes
    /// </summary>
    public int? EstimatedMinutes
    {
        get;
        set;
    }

    public Common.Enums.Visibility Visibility
    {
        get;
        set;
    } = GameGuild.Common.Enums.Visibility.Published;

    // Navigation properties
    public virtual Program Program
    {
        get;
        set;
    } = null!;

    public virtual ProgramContent? Parent
    {
        get;
        set;
    }

    public virtual ICollection<ProgramContent> Children
    {
        get;
        set;
    } = new List<ProgramContent>();

    public virtual ICollection<ContentInteraction> ContentInteractions
    {
        get;
        set;
    } = new List<ContentInteraction>();

    // Helper methods for JSON body
    public T? GetBodyContent<T>(string key) where T : class
    {
        if (string.IsNullOrEmpty(Body)) return null;

        try
        {
            JsonDocument json = JsonDocument.Parse(Body);
            if (json.RootElement.TryGetProperty(key, out JsonElement element))
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
        }
        catch
        {
            // Handle JSON parsing errors gracefully
        }

        return null;
    }

    public void SetBodyContent<T>(string key, T value)
    {
        var body = string.IsNullOrEmpty(Body) ? new Dictionary<string, object>() : JsonSerializer.Deserialize<Dictionary<string, object>>(Body) ?? new Dictionary<string, object>();

        body[key] = value!;
        Body = JsonSerializer.Serialize(body);
    }

    public string GetContent()
    {
        return GetBodyContent<string>("content") ?? string.Empty;
    }

    public void SetContent(string content)
    {
        SetBodyContent("content", content);
    }
}

/// <summary>
/// Entity Framework configuration for ProgramContent entity
/// </summary>
public class ProgramContentConfiguration : IEntityTypeConfiguration<ProgramContent>
{
    public void Configure(EntityTypeBuilder<ProgramContent> builder)
    {
        // Configure relationship with Program (can't be done with annotations)
        builder.HasOne(pc => pc.Program)
            .WithMany(p => p.ProgramContents)
            .HasForeignKey(pc => pc.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship with Parent (self-referencing, can't be done with annotations)
        builder.HasOne(pc => pc.Parent)
            .WithMany(pc => pc.Children)
            .HasForeignKey(pc => pc.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
