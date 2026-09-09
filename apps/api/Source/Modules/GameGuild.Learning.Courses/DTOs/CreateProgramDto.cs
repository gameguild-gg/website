using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

// Program Management DTOs
public sealed record CreateProgramDto(
  string Title,
  string? Description,
  string Slug,
  string? Thumbnail = null,
  Guid? CreatorId = null,
  PercentValue PassingScore = default)
{
  public string Title { get; init; } = Title;

  public string? Description { get; init; } = Description;

  public string Slug { get; init; } = Slug;

  public string? Thumbnail { get; init; } = Thumbnail;

  public Guid? CreatorId { get; init; } = CreatorId;

  public PercentValue PassingScore { get; init; } = PassingScore == default ? PercentValue.FromPercentage("60") : PassingScore;
}

// Content Management DTOs

// Workflow DTOs

// User Progress DTOs

// Scheduling DTOs

// Monetization DTOs

// Product Integration DTOs

// Analytics DTOs

// Search and Filter DTOs

// Bulk Operations DTOs
