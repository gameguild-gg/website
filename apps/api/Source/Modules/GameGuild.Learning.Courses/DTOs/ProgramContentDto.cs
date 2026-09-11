using System.Text.Json;
using System.Text.Json.Serialization;


namespace GameGuild.Learning.Courses;

/// <summary>
/// DTO for program content responses
/// </summary>
public class ProgramContentDto {
  public Guid Id { get; set; }

  public int Version { get; set; }

  public Guid ProgramId { get; set; }

  public Guid? ParentId { get; set; }

  public string Title { get; set; } = string.Empty;

  public string Slug { get; set; } = string.Empty;

  public string Description { get; set; } = string.Empty;

  public ProgramContentType Type { get; set; }

  public string? Body { get; set; }

  public JsonElement? JsonBody { get; set; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public LessonContentFormat? LessonFormat { get; set; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public ActivitySettings? ActivitySettings { get; set; }

  public int SortOrder { get; set; }

  public bool IsRequired { get; set; }

  public int? EstimatedMinutes { get; set; }

  public EstimatedMinutesSource? EstimatedMinutesSource { get; set; }

  public Visibility Visibility { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime? UpdatedAt { get; set; }

  // Navigation properties
  public string? ProgramTitle { get; set; }

  public string? ParentTitle { get; set; }

  public int ChildrenCount { get; set; }

  public List<ProgramContentDto> Children { get; set; } = new();
}
