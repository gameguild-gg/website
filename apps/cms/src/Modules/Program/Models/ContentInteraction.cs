using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;
using cms.Common.Enums;
using System.Text.Json;

namespace cms.Modules.Program.Models;

[Table("content_interactions")]
public class ContentInteraction : BaseEntity
{    public Guid ProgramUserId
    {
        get;
        set;
    }

    public Guid ContentId
    {
        get;
        set;
    }

    public ProgressStatus Status
    {
        get;
        set;
    } = ProgressStatus.NotStarted;

    /// <summary>
    /// User's submission or response to the content
    /// Structure varies by content type:
    /// - Assignment: {files: [], text: "", submittedAt: ""}
    /// - Code: {code: "", language: "", testResults: []}
    /// - Discussion: {posts: [], lastParticipated: ""}
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? SubmissionData
    {
        get;
        set;
    }

    /// <summary>
    /// Completion percentage for this specific content (0-100)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal CompletionPercentage
    {
        get;
        set;
    } = 0;

    /// <summary>
    /// Time spent on this content in minutes
    /// </summary>
    public int? TimeSpentMinutes
    {
        get;
        set;
    }

    /// <summary>
    /// Date when user first accessed this content
    /// </summary>
    public DateTime? FirstAccessedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Date when user last accessed this content
    /// </summary>
    public DateTime? LastAccessedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Date when user completed this content
    /// </summary>
    public DateTime? CompletedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Date when user submitted their work (for gradeable content)
    /// </summary>
    public DateTime? SubmittedAt
    {
        get;
        set;
    }

    // Navigation properties
    public virtual ProgramUser ProgramUser
    {
        get;
        set;
    } = null!;

    public virtual ProgramContent Content
    {
        get;
        set;
    } = null!;

    public virtual ICollection<ActivityGrade> ActivityGrades
    {
        get;
        set;
    } = new List<ActivityGrade>();

    // Helper methods for JSON submission data
    public T? GetSubmissionData<T>(string key) where T : class
    {
        if (string.IsNullOrEmpty(SubmissionData)) return null;

        try
        {
            var json = JsonDocument.Parse(SubmissionData);
            if (json.RootElement.TryGetProperty(key, out var element))
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

    public void SetSubmissionData<T>(string key, T value)
    {
        var data = string.IsNullOrEmpty(SubmissionData) ? new Dictionary<string, object>() : JsonSerializer.Deserialize<Dictionary<string, object>>(SubmissionData) ?? new Dictionary<string, object>();

        data[key] = value!;
        SubmissionData = JsonSerializer.Serialize(data);
    }
}

/// <summary>
/// Entity Framework configuration for ContentInteraction entity
/// </summary>
public class ContentInteractionConfiguration : IEntityTypeConfiguration<ContentInteraction>
{
    public void Configure(EntityTypeBuilder<ContentInteraction> builder)
    {
        // Configure relationship with ProgramUser (can't be done with annotations)
        builder.HasOne(ci => ci.ProgramUser)
            .WithMany(pu => pu.ContentInteractions)
            .HasForeignKey(ci => ci.ProgramUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship with Content (can't be done with annotations)
        builder.HasOne(ci => ci.Content)
            .WithMany()
            .HasForeignKey(ci => ci.ContentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
