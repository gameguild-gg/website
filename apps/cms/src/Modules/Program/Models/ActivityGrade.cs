using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using GameGuild.Common.Entities;

namespace GameGuild.Modules.Program.Models;

[Table("activity_grades")]
public class ActivityGrade : BaseEntity
{
   

    public Guid ContentInteractionId
    {
        get;
        set;
    }

    public Guid GraderProgramUserId
    {
        get;
        set;
    }

    /// <summary>
    /// Grade awarded (0-100 scale or points based on content max_points)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal Grade
    {
        get;
        set;
    }

    /// <summary>
    /// Written feedback from the grader
    /// </summary>
    public string? Feedback
    {
        get;
        set;
    }

    /// <summary>
    /// Detailed grading breakdown stored as JSON
    /// Examples: rubric scores, test case results, peer review criteria
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? GradingDetails
    {
        get;
        set;
    }

    /// <summary>
    /// Date when the grade was awarded
    /// </summary>
    public DateTime GradedAt
    {
        get;
        set;
    }

    // Navigation properties
    public virtual ContentInteraction ContentInteraction
    {
        get;
        set;
    } = null!;

    public virtual ProgramUser GraderProgramUser
    {
        get;
        set;
    } = null!;

    // Helper methods for JSON grading details
    public T? GetGradingDetail<T>(string key) where T : class
    {
        if (string.IsNullOrEmpty(GradingDetails)) return null;

        try
        {
            JsonDocument json = JsonDocument.Parse(GradingDetails);
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

    public void SetGradingDetail<T>(string key, T value)
    {
        var details = string.IsNullOrEmpty(GradingDetails) ? new Dictionary<string, object>() : JsonSerializer.Deserialize<Dictionary<string, object>>(GradingDetails) ?? new Dictionary<string, object>();

        details[key] = value!;
        GradingDetails = JsonSerializer.Serialize(details);
    }
}

/// <summary>
/// Entity Framework configuration for ActivityGrade entity
/// </summary>
public class ActivityGradeConfiguration : IEntityTypeConfiguration<ActivityGrade>
{
    public void Configure(EntityTypeBuilder<ActivityGrade> builder)
    {
        // Configure relationship with ContentInteraction (can't be done with annotations)
        builder.HasOne(ag => ag.ContentInteraction)
            .WithMany(ci => ci.ActivityGrades)
            .HasForeignKey(ag => ag.ContentInteractionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship with GraderProgramUser (can't be done with annotations)
        builder.HasOne(ag => ag.GraderProgramUser)
            .WithMany()
            .HasForeignKey(ag => ag.GraderProgramUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
