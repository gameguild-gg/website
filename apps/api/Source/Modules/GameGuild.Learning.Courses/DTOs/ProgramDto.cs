using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Response DTO for Program entity — prevents domain entity exposure via API.
/// Maps scalar and computed properties only; excludes navigation collections and audit internals.
/// </summary>
public sealed record ProgramDto
{
    public Guid Id { get; init; }
    public Guid? CreatorId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Metadata { get; init; }
    public ContentVisibility Visibility { get; init; }
    public string? Slug { get; init; }
    public ContentStatus Status { get; init; }
    public string? Thumbnail { get; init; }
    public string? VideoShowcaseUrl { get; init; }
    public int? EstimatedHours { get; init; }
    public PercentValue PassingScore { get; init; }
    public EnrollmentStatus EnrollmentStatus { get; init; }
    public int? MaxEnrollments { get; init; }
    public DateTime? EnrollmentDeadline { get; init; }
    public ProgramCategory Category { get; init; }
    public ProgramDifficulty Difficulty { get; init; }
    public string? SkillsRequired { get; init; }
    public string? SkillsProvided { get; init; }

    // Computed properties — safe to expose
    public int CurrentEnrollments { get; init; }
    public decimal AverageRating { get; init; }
    public int TotalRatings { get; init; }
    public bool IsEnrollmentOpen { get; init; }

    // Audit fields — only safe subset
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
