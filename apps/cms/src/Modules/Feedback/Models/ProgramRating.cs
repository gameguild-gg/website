using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Feedback.Models;

[Table("program_ratings")]
public class ProgramRating : BaseEntity
{
      public Guid UserId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid ProgramUserId { get; set; }
    
    /// <summary>
    /// Overall rating for the program (1-5)
    /// </summary>
    [Column(TypeName = "decimal(2,1)")]
    public decimal Rating { get; set; }
    
    /// <summary>
    /// Written review of the program
    /// </summary>
    public string? Review { get; set; }
    
    /// <summary>
    /// Rating for content quality (1-5)
    /// </summary>
    [Column(TypeName = "decimal(2,1)")]
    public decimal? ContentQualityRating { get; set; }
    
    /// <summary>
    /// Rating for instructor effectiveness (1-5)
    /// </summary>
    [Column(TypeName = "decimal(2,1)")]
    public decimal? InstructorRating { get; set; }
    
    /// <summary>
    /// Rating for program difficulty (1-5)
    /// </summary>
    [Column(TypeName = "decimal(2,1)")]
    public decimal? DifficultyRating { get; set; }
    
    /// <summary>
    /// Rating for value for money (1-5)
    /// </summary>
    [Column(TypeName = "decimal(2,1)")]
    public decimal? ValueRating { get; set; }
    
    /// <summary>
    /// Whether the user would recommend this program
    /// </summary>
    public bool? WouldRecommend { get; set; }
    
    public ModerationStatus ModerationStatus { get; set; } = ModerationStatus.Pending;
    
    /// <summary>
    /// User who moderated this rating (approved/rejected)
    /// </summary>
    public int? ModeratedBy { get; set; }
    
    /// <summary>
    /// Date when rating was moderated
    /// </summary>
    public DateTime? ModeratedAt { get; set; }
    
    /// <summary>
    /// Date when rating was submitted
    /// </summary>
    public DateTime SubmittedAt { get; set; }
    
    // Navigation properties
    public virtual User.Models.User User { get; set; } = null!;
    public virtual Program.Models.Program Program { get; set; } = null!;
    public virtual Product.Models.Product? Product { get; set; }
    public virtual Program.Models.ProgramUser ProgramUser { get; set; } = null!;
    public virtual User.Models.User? Moderator { get; set; }
}
