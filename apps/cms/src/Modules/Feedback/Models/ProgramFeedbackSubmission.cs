using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;
using System.Text.Json;

namespace cms.Modules.Feedback.Models;

[Table("program_feedback_submissions")]
public class ProgramFeedbackSubmission : BaseEntity
{
    
    public int UserId { get; set; }
    public int ProgramId { get; set; }
    public int? ProductId { get; set; }
    public int ProgramUserId { get; set; }
    
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
    public virtual User.Models.User User { get; set; } = null!;
    public virtual Program.Models.Program Program { get; set; } = null!;
    public virtual Product.Models.Product? Product { get; set; }
    public virtual Program.Models.ProgramUser ProgramUser { get; set; } = null!;
    
    // Helper methods for JSON feedback data
    public T? GetFeedbackResponse<T>(string questionId) where T : class
    {
        if (string.IsNullOrEmpty(FeedbackData)) return null;
        
        try
        {
            var json = JsonDocument.Parse(FeedbackData);
            if (json.RootElement.TryGetProperty(questionId, out var element))
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
