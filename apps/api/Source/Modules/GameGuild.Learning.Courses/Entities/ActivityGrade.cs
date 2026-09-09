using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Learning.Grading.Contracts;


namespace GameGuild.Learning.Courses;

/// <summary>
/// Represents a grade assigned to a user for a specific activity or content interaction
/// </summary>
[Table("activity_grades")]
[Index(nameof(StudentId), nameof(ContentInteractionId), IsUnique = true)]
[Index(nameof(StudentId))]
[Index(nameof(GraderId))]
[Index(nameof(ContentInteractionId))]
[Index(nameof(ProgramUserId))]
[Index(nameof(GradedAt))]
[Index(nameof(Points))]
[Index(nameof(TenantId))]
public class ActivityGrade : EntityBase
{
    /// <summary>
    /// Student user ID
    /// </summary>
    [Required]
    public Guid StudentId { get; set; }

    /// <summary>
    /// Grader user ID (instructor, peer, or system)
    /// </summary>
    [Required]
    public Guid GraderId { get; set; }

    /// <summary>
    /// Content interaction being graded
    /// </summary>
    [Required]
    public Guid ContentInteractionId { get; set; }

    /// <summary>
    /// Program enrollment ID
    /// </summary>
    [Required]
    public Guid ProgramUserId { get; set; }

    /// <summary>
    /// Points awarded (0-100 scale)
    /// </summary>
    public ScoreValue? Points { get; set; }
    /// <summary>
    /// Maximum points possible for this activity
    /// </summary>
    public ScoreValue? MaxPoints { get; set; }

    /// <summary>
    /// Grade letter/symbol (A, B, C, etc.)
    /// </summary>
    [MaxLength(10)]
    public string? GradeLetter { get; set; }

    /// <summary>
    /// Grader feedback/comments
    /// </summary>
    public string? Feedback { get; set; }

    /// <summary>
    /// When the grade was assigned
    /// </summary>
    public DateTime GradedAt { get; set; }

    /// <summary>
    /// Whether this grade is finalized
    /// </summary>
    public bool IsFinalized { get; set; } = false;

    /// <summary>
    /// Rubric data (JSON format)
    /// </summary>
    public string? RubricData { get; set; }

    /// <summary>
    /// Time spent grading in minutes
    /// </summary>
    public int? GradingTimeMinutes { get; set; }

    /// <summary>
    /// Grade type (automatic, manual, peer review)
    /// </summary>
    public GradeType GradeType { get; set; } = GradeType.Manual;

    /// <summary>
    /// Attempt number for this activity
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    // Navigation Properties
    /// <summary>
    /// Student user
    /// </summary>
    public virtual User Student { get; set; } = null!;

    /// <summary>
    /// Grader user
    /// </summary>
    public virtual User Grader { get; set; } = null!;

    /// <summary>
    /// Content interaction
    /// </summary>
    public virtual ContentInteraction ContentInteraction { get; set; } = null!;

    /// <summary>
    /// Program enrollment
    /// </summary>
    public virtual ProgramUser ProgramUser { get; set; } = null!;

    /// <summary>
    /// Grader's program user record (for tracking which program user did the grading)
    /// </summary>
    public virtual ProgramUser? GraderProgramUser { get; set; }

    /// <summary>
    /// Grader program user ID
    /// </summary>
    public Guid? GraderProgramUserId { get; set; }

    /// <summary>
    /// Detailed grading information (JSON format)
    /// </summary>
    public string? GradingDetails { get; set; }

    // Computed Properties
    /// <summary>
    /// Whether this grade is global (tenant-independent)
    /// </summary>
    public override bool IsGlobal => TenantId == null;

    /// <summary>
    /// Percentage score (points / max points * 100)
    /// </summary>
    public PercentValue? PercentageScore => Points.HasValue &&
                                             MaxPoints.HasValue &&
                                             MaxPoints.Value.CompareTo(ScoreValue.Zero) > 0
        ? PercentValue.FromScores(Points.Value, MaxPoints.Value)
        : null;

    /// <summary>
    /// Whether this is a passing grade (>=60%)
    /// </summary>
    public bool? IsPassing => PercentageScore.HasValue
        ? PercentageScore.Value.CompareTo(PercentValue.FromPercentage("60")) >= 0
        : null;

    /// <summary>
    /// Whether this is an automatic system grade
    /// </summary>
    public bool IsAutomaticGrade => GradeType == GradeType.Automatic;

    /// <summary>
    /// Whether this is a peer review grade
    /// </summary>
    public bool IsPeerReview => GradeType == GradeType.PeerReview;

    /// <summary>
    /// Days since grading
    /// </summary>
    public int DaysSinceGrading => (SystemClock.UtcNow - GradedAt).Days;

    // Domain Methods
    /// <summary>
    /// Assigns points to this grade
    /// </summary>
    public void AssignPoints(ScoreValue points, ScoreValue? maxPoints = null)
    {
        var maximum = maxPoints ?? MaxPoints ?? ScoreValue.FromPoints("100");
        if (maximum.CompareTo(ScoreValue.Zero) <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPoints), "Maximum points must be positive.");
        if (points.CompareTo(maximum) > 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Points cannot exceed maximum points.");
        Points = points;
        MaxPoints = maximum;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Sets the letter grade
    /// </summary>
    public void SetLetterGrade(string grade)
    {
        GradeLetter = grade?.ToUpper();
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Adds or updates feedback
    /// </summary>
    public void UpdateFeedback(string? feedback)
    {
        Feedback = feedback;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Finalizes the grade (prevents further changes)
    /// </summary>
    public void FinalizeGrade()
    {
        IsFinalized = true;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Unlocks the grade for editing
    /// </summary>
    public void Unlock()
    {
        IsFinalized = false;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Records grading time
    /// </summary>
    public void RecordGradingTime(int minutes)
    {
        GradingTimeMinutes = minutes;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Sets rubric evaluation data
    /// </summary>
    public void SetRubricData(string rubricJson)
    {
        RubricData = rubricJson;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Calculates letter grade from percentage
    /// </summary>
    public string? CalculateLetterGrade()
    {
        if (!PercentageScore.HasValue)
            return null;

        var value = PercentageScore.Value;
        foreach (var (threshold, letter) in LetterGradeThresholds)
        {
            if (value.CompareTo(threshold) >= 0) return letter;
        }

        return "F";
    }

    /// <summary>
    /// Validates the grade data
    /// </summary>
    public bool IsValid()
    {
        if (Points.HasValue && MaxPoints.HasValue && Points.Value.CompareTo(MaxPoints.Value) > 0)
            return false;

        if (MaxPoints.HasValue && MaxPoints.Value.CompareTo(ScoreValue.Zero) <= 0)
            return false;

        return true;
    }

    /// <summary>
    /// Creates a revision of this grade
    /// </summary>
    public ActivityGrade CreateRevision(ScoreValue newPoints, string? reason = null)
    {
        return new ActivityGrade
        {
            StudentId = StudentId,
            GraderId = GraderId,
            ContentInteractionId = ContentInteractionId,
            ProgramUserId = ProgramUserId,
            Points = newPoints,
            MaxPoints = MaxPoints,
            GradeLetter = CalculateLetterGrade(),
            Feedback = reason != null ? $"Revision: {reason}\\n\\nOriginal feedback: {Feedback}" : Feedback,
            GradedAt = SystemClock.UtcNow,
            IsFinalized = false,
            GradeType = GradeType,
            AttemptNumber = AttemptNumber + 1,
            TenantId = TenantId
        };
    }

    private static readonly (PercentValue Threshold, string Letter)[] LetterGradeThresholds =
    [
        (PercentValue.FromPercentage("97"), "A+"),
        (PercentValue.FromPercentage("93"), "A"),
        (PercentValue.FromPercentage("90"), "A-"),
        (PercentValue.FromPercentage("87"), "B+"),
        (PercentValue.FromPercentage("83"), "B"),
        (PercentValue.FromPercentage("80"), "B-"),
        (PercentValue.FromPercentage("77"), "C+"),
        (PercentValue.FromPercentage("73"), "C"),
        (PercentValue.FromPercentage("70"), "C-"),
        (PercentValue.FromPercentage("67"), "D+"),
        (PercentValue.FromPercentage("63"), "D"),
        (PercentValue.FromPercentage("60"), "D-"),
    ];
}
