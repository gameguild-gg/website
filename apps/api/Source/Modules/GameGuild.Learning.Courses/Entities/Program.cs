using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Learning.Grading.Contracts;
// using GameGuild.Modules.Certificates.Entities;
// using GameGuild.Modules.Feedbacks.Entities;


// using GameGuild.Modules.Contents.Models;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Represents a learning program with structured content, enrollment management, and progress tracking
/// </summary>
[Table("programs")]
[Index(nameof(Slug), IsUnique = true)]
[Index(nameof(Status))]
[Index(nameof(Category))]
[Index(nameof(Difficulty))]
[Index(nameof(EnrollmentStatus))]
[Index(nameof(CreatedAt))]
[Index(nameof(TenantId))]
[Index(nameof(CreatorId))]
public class Program : EntityBase {
    private const string SkillsRequiredMetadataKey = "skillsRequired";
    private const string SkillsProvidedMetadataKey = "skillsProvided";

    /// <summary>
    /// The user ID of the program creator/owner
    /// </summary>
    public Guid? CreatorId { get; set; }

    /// <summary>
    /// Program title
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Program description
    /// </summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Access level for the program
    /// </summary>
    public ContentVisibility Visibility { get; set; } = ContentVisibility.Public;

    /// <summary>
    /// Metadata as JSON dictionary for storing additional properties
    /// </summary>
    [MaxLength(4000)]
    public string? Metadata { get; set; }

    /// <summary>
    /// URL-friendly identifier
    /// </summary>
    [MaxLength(200)]
    public string? Slug { get; set; }

    /// <summary>
    /// Content status (draft, published, archived, etc.)
    /// </summary>
    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    /// <summary>
    /// Program thumbnail image URL
    /// </summary>
    [MaxLength(500)]
    public string? Thumbnail { get; set; }

    /// <summary>
    /// Video showcase URL
    /// </summary>
    [MaxLength(500)]
    public string? VideoShowcaseUrl { get; set; }

    /// <summary>
    /// Estimated completion time in hours
    /// </summary>
    public int? EstimatedHours { get; set; }

    /// <summary>
    /// Passing score percentage (0-100) used to determine successful program completion. Default 60.
    /// </summary>
    public PercentValue PassingScore { get; set; } = PercentValue.FromPercentage("60");

    /// <summary>
    /// Current enrollment status
    /// </summary>
    public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.Open;

    /// <summary>
    /// Maximum number of enrollments allowed (null = unlimited)
    /// </summary>
    public int? MaxEnrollments { get; set; }

    /// <summary>
    /// Enrollment deadline (null = no deadline)
    /// </summary>
    public DateTime? EnrollmentDeadline { get; set; }

    /// <summary>
    /// Program category
    /// </summary>
    public ProgramCategory Category { get; set; } = ProgramCategory.General;

    /// <summary>
    /// Program difficulty level
    /// </summary>
    public ProgramDifficulty Difficulty { get; set; } = ProgramDifficulty.Beginner;

    /// <summary>
    /// Skills required to take this program (comma-separated or JSON).
    /// Stored in Metadata because the production EF model ignores this convenience property.
    /// </summary>
    [NotMapped]
    public string? SkillsRequired
    {
        get => GetMetadataString(SkillsRequiredMetadataKey);
        set => SetMetadataString(SkillsRequiredMetadataKey, value);
    }

    /// <summary>
    /// Skills provided upon completing this program (comma-separated or JSON).
    /// Stored in Metadata because the production EF model ignores this convenience property.
    /// </summary>
    [NotMapped]
    public string? SkillsProvided
    {
        get => GetMetadataString(SkillsProvidedMetadataKey);
        set => SetMetadataString(SkillsProvidedMetadataKey, value);
    }

    // Navigation Properties
    /// <summary>
    /// Program content items
    /// </summary>
    public virtual ICollection<ProgramContent> ProgramContents { get; set; } = new List<ProgramContent>();

    /// <summary>
    /// Program enrollments
    /// </summary>
    public virtual ICollection<ProgramUser> ProgramUsers { get; set; } = new List<ProgramUser>();

    // NOTE: ProductProgram navigation requires Commerce module integration
    // public virtual ICollection<ProductProgram> ProductPrograms { get; set; } = new List<ProductProgram>();

    // NOTE: Certificate navigation would require Certificate entity to reference ProgramId instead of CourseId
    // This is a design decision - certificates are currently issued per Course, not per Program
    // public virtual ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();

    // NOTE: ProgramFeedbackSubmission entity does not exist yet - requires Feedbacks module implementation
    // public virtual ICollection<ProgramFeedbackSubmission> FeedbackSubmissions { get; set; } = new List<ProgramFeedbackSubmission>();

    /// <summary>
    /// Program ratings
    /// </summary>
    public virtual ICollection<ProgramRating> ProgramRatings { get; set; } = new List<ProgramRating>();

    /// <summary>
    /// Program wishlists
    /// </summary>
    public virtual ICollection<ProgramWishlist> ProgramWishlists { get; set; } = new List<ProgramWishlist>();

    // Computed Properties
    /// <summary>
    /// Whether the program is part of a global tenant
    /// </summary>
    public override bool IsGlobal => TenantId == null;

    /// <summary>
    /// Current number of active enrollments
    /// </summary>
    public int CurrentEnrollments => ProgramUsers?.Count(pu => pu.IsActive == true) ?? 0;

    /// <summary>
    /// Average rating calculated from all ratings
    /// </summary>
    public decimal AverageRating => ProgramRatings?.Any() == true
        ? ProgramRatings.Average(r => r.Rating)
        : 0m;

    /// <summary>
    /// Total number of ratings
    /// </summary>
    public int TotalRatings => ProgramRatings?.Count ?? 0;

    /// <summary>
    /// Whether enrollment is currently open
    /// </summary>
    public bool IsEnrollmentOpen =>
        EnrollmentStatus == EnrollmentStatus.Open &&
        (!EnrollmentDeadline.HasValue || EnrollmentDeadline.Value > SystemClock.UtcNow) &&
        (MaxEnrollments is null || CurrentEnrollments < MaxEnrollments);

    // Domain Methods
    /// <summary>
    /// Opens enrollment for the program
    /// </summary>
    public void OpenEnrollment() {
        EnrollmentStatus = EnrollmentStatus.Open;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Closes enrollment for the program
    /// </summary>
    public void CloseEnrollment() {
        EnrollmentStatus = EnrollmentStatus.Closed;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Publishes the program
    /// </summary>
    public void Publish() {
        Status = ContentStatus.Published;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Archives the program
    /// </summary>
    public void Archive() {
        Status = ContentStatus.Archived;
        EnrollmentStatus = EnrollmentStatus.Closed;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Updates the estimated completion time based on content
    /// </summary>
    public void CalculateEstimatedHours() {
        if (ProgramContents?.Any() == true) {
            EstimatedHours = (int?)Math.Ceiling(ProgramContents.Sum(pc => pc.EstimatedMinutes ?? 0) / 60.0);
            UpdatedAt = SystemClock.UtcNow;
        }
    }

    /// <summary>
    /// Checks if a user can enroll in this program
    /// </summary>
    public bool CanUserEnroll(Guid userId) {
        if (!IsEnrollmentOpen)
            return false;

        // Check if user is already enrolled
        if (ProgramUsers?.Any(pu => pu.UserId == userId && pu.IsActive == true) == true)
            return false;

        return true;
    }

    /// <summary>
    /// Sets a metadata value by key
    /// </summary>
    public void SetMetadata(string key, object value) {
        var dict = GetMetadataDict();
        dict[key] = value;
        Metadata = System.Text.Json.JsonSerializer.Serialize(dict);
        UpdatedAt = SystemClock.UtcNow;
    }

    private string? GetMetadataString(string key) {
        var dict = GetMetadataDict();

        if (!dict.TryGetValue(key, out var value) || value is null)
            return null;

        if (value is System.Text.Json.JsonElement jsonElement) {
            return jsonElement.ValueKind switch {
                System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                System.Text.Json.JsonValueKind.Null => null,
                System.Text.Json.JsonValueKind.Undefined => null,
                _ => jsonElement.ToString(),
            };
        }

        return value.ToString();
    }

    private void SetMetadataString(string key, string? value) {
        var dict = GetMetadataDict();

        if (string.IsNullOrWhiteSpace(value)) {
            dict.Remove(key);
        }
        else {
            dict[key] = value.Trim();
        }

        Metadata = dict.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(dict);
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Gets the metadata dictionary
    /// </summary>
    private Dictionary<string, object> GetMetadataDict() {
        if (string.IsNullOrWhiteSpace(Metadata))
            return new Dictionary<string, object>();

        try {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(Metadata)
                   ?? new Dictionary<string, object>();
        }
        catch {
            return new Dictionary<string, object>();
        }
    }
}
