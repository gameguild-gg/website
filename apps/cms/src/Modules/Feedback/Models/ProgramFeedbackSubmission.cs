using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using GameGuild.Common.Entities;

namespace GameGuild.Modules.Feedback.Models;

[Table("program_feedback_submissions")]
[Index(nameof(UserId), nameof(ProgramId), IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(ProgramId))]
[Index(nameof(ProductId))]
[Index(nameof(ProgramUserId))]
[Index(nameof(OverallRating))]
[Index(nameof(SubmittedAt))]
public class ProgramFeedbackSubmission : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid ProgramUserId { get; set; }
    
    /// <summary>
    /// Feedback responses stored as JSON
    /// Structure: {questionId: response, questionId: response, ...}
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string FeedbackData { get; set; } = "{}";
    
    /// <summary>
    /// Overall satisfaction rating (1-5)
    /// </summary>
    [Column(TypeName = "decimal(2,1)")]
    public decimal? OverallRating { get; set; }
    
    /// <summary>
    /// General comments about the program
    /// </summary>
    public string? Comments { get; set; }
    
    /// <summary>
    /// Whether the user would recommend this program
    /// </summary>
    public bool? WouldRecommend { get; set; }
    
    /// <summary>
    /// Date when feedback was submitted
    /// </summary>
    public DateTime SubmittedAt { get; set; }
    
    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public virtual User.Models.User User { get; set; } = null!;
    
    [ForeignKey(nameof(ProgramId))]
    public virtual Program.Models.Program Program { get; set; } = null!;
    
    [ForeignKey(nameof(ProductId))]
    public virtual Product.Models.Product? Product { get; set; }
    
    [ForeignKey(nameof(ProgramUserId))]
    public virtual Program.Models.ProgramUser ProgramUser { get; set; } = null!;
    
    // Helper methods for JSON feedback data
    public T? GetFeedbackResponse<T>(string questionId) where T : class
    {
        if (string.IsNullOrEmpty(FeedbackData)) return null;
        
        try
        {
            JsonDocument json = JsonDocument.Parse(FeedbackData);
            if (json.RootElement.TryGetProperty(questionId, out JsonElement element))
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
    
    public void SetFeedbackResponse<T>(string questionId, T value)
    {
        var data = string.IsNullOrEmpty(FeedbackData) ? new Dictionary<string, object>() : 
            JsonSerializer.Deserialize<Dictionary<string, object>>(FeedbackData) ?? new Dictionary<string, object>();
        
        data[questionId] = value!;
        FeedbackData = JsonSerializer.Serialize(data);
    }
}

public class ProgramFeedbackSubmissionConfiguration : IEntityTypeConfiguration<ProgramFeedbackSubmission>
{
    public void Configure(EntityTypeBuilder<ProgramFeedbackSubmission> builder)
    {
        // Configure relationship with Program (can't be done with annotations)
        builder.HasOne(pfs => pfs.Program)
            .WithMany()
            .HasForeignKey(pfs => pfs.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship with User (can't be done with annotations)
        builder.HasOne(pfs => pfs.User)
            .WithMany()
            .HasForeignKey(pfs => pfs.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure optional relationship with Product (can't be done with annotations)
        builder.HasOne(pfs => pfs.Product)
            .WithMany()
            .HasForeignKey(pfs => pfs.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure relationship with ProgramUser (can't be done with annotations)
        builder.HasOne(pfs => pfs.ProgramUser)
            .WithMany()
            .HasForeignKey(pfs => pfs.ProgramUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
