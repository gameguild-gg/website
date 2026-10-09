using GameGuild.Identity.Users;


namespace GameGuild.TestingLab;

/// <summary>
/// Represents feedback quality rating given by users to evaluate feedback usefulness
/// </summary>
[Table("feedback_quality_ratings")]
[Index(nameof(FeedbackId))]
[Index(nameof(RatedByUserId))]
[Index(nameof(QualityRating))]
[Index(nameof(TenantId))]
public class FeedbackQualityRating : EntityBase
{
    /// <summary>
    /// Foreign key to the feedback being rated
    /// </summary>
    [Required]
    public Guid FeedbackId { get; set; }

    /// <summary>
    /// Navigation property to the feedback being rated
    /// </summary>
    public virtual TestingFeedback Feedback { get; set; } = null!;

    /// <summary>
    /// Foreign key to the user giving the rating
    /// </summary>
    [Required]
    public Guid RatedByUserId { get; set; }

    /// <summary>
    /// Navigation property to the user giving the rating
    /// </summary>
    public virtual User RatedBy { get; set; } = null!;

    /// <summary>
    /// Quality rating (1-5 scale)
    /// </summary>
    [Required]
    [Range(1, 5)]
    public int QualityRating { get; set; }

    /// <summary>
    /// Reason for the quality rating
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    // Computed Properties
    /// <summary>
    /// Whether this rating is global (tenant-independent)
    /// </summary>
    public override bool IsGlobal => TenantId == null;

    /// <summary>
    /// Whether this is a positive quality rating
    /// </summary>
    public bool IsPositive => QualityRating >= 4;

    /// <summary>
    /// Whether this is a negative quality rating
    /// </summary>
    public bool IsNegative => QualityRating <= 2;

    // Domain Methods
    /// <summary>
    /// Updates the quality rating
    /// </summary>
    public void UpdateRating(int rating, string? reason = null)
    {
        if (rating < 1 || rating > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Quality rating must be between 1 and 5");
        }

        QualityRating = rating;
        Reason = reason;
        UpdatedAt = SystemClock.UtcNow;
    }
}