

using System.Text.Json;

namespace GameGuild.Learning.Courses;

/// <summary> Extension methods for mapping between ProgramContent entities and DTOs </summary>
public static class ProgramContentMappingExtensions
{
  public static ProgramContentType NormalizeProfessorFacingType(ProgramContentType type) =>
    type switch
    {
      ProgramContentType.Page => ProgramContentType.Lesson,
      ProgramContentType.Challenge => ProgramContentType.Assignment,
      _ => type,
    };

  /// <summary> Maps ProgramContent entity to DTO </summary>
  public static ProgramContentDto ToDto(this ProgramContent content)
  {
    var normalizedType = NormalizeProfessorFacingType(content.Type);
    var isLesson = normalizedType == ProgramContentType.Lesson;

    return new ProgramContentDto
    {
      Id = content.Id,
      ProgramId = content.ProgramId,
      ParentId = content.ParentId,
      Title = content.Title,
      Slug = content.Slug,
      Description = content.Description ?? string.Empty,
      Type = normalizedType,
      Body = FormatBody(content.Body),
      JsonBody = content.JsonBody is null ? null : JsonDocument.Parse(content.JsonBody).RootElement.Clone(),
      LessonFormat = isLesson ? content.LessonFormat ?? LessonContentFormatInference.FromBody(content.Body) : null,
      ActivitySettings = content.GetActivitySettings(),
      SortOrder = content.SortOrder,
      IsRequired = content.IsRequired,
      EstimatedMinutes = content.EstimatedMinutes,
      EstimatedMinutesSource = content.EstimatedMinutesSource,
      Visibility = content.Visibility,
      CreatedAt = content.CreatedAt,
      UpdatedAt = content.UpdatedAt,
      Version = content.Version,
      ProgramTitle = content.Program?.Title,
      ParentTitle = content.Parent?.Title,
      ChildrenCount = content.Children?.Count(c => c.DeletedAt == null) ?? 0,
      Children = content.Children?.Where(c => c.DeletedAt == null).Select(c => c.ToDto()).ToList() ?? new List<ProgramContentDto>(),
    };
  }

  /// <summary> Maps collection of ProgramContent entities to DTOs </summary>
  public static IEnumerable<ProgramContentDto> ToDtos(this IEnumerable<ProgramContent> contents) { return contents.Select(c => c.ToDto()); }

  private static string? FormatBody(string? body)
  {
    if (string.IsNullOrWhiteSpace(body)) return null;
    return StripImportMarker(body);
  }

  private static string StripImportMarker(string body)
  {
    const string importMarkerPrefix = "<!-- gameguild-source:";

    if (!body.StartsWith(importMarkerPrefix, StringComparison.OrdinalIgnoreCase))
    {
      return body;
    }

    var markerEnd = body.IndexOf("-->", StringComparison.Ordinal);
    if (markerEnd < 0)
    {
      return body;
    }

    var contentStart = markerEnd + 3;
    while (contentStart < body.Length && body[contentStart] is '\r' or '\n') contentStart++;

    return body[contentStart..];
  }

  /// <summary> Maps CreateProgramContentDto to ProgramContent entity </summary>
  public static ProgramContent ToEntity(this CreateProgramContentDto dto)
  {
    var content = new ProgramContent
    {
      Id = Guid.NewGuid(),
      ProgramId = dto.ProgramId,
      ParentId = dto.ParentId,
      Title = dto.Title,
      Slug = string.IsNullOrWhiteSpace(dto.Slug) ? dto.Title.ToSlugCase() : dto.Slug,
      Description = dto.Description,
      Type = NormalizeProfessorFacingType(dto.Type),
      Body = dto.Body ?? "{}",
      JsonBody = dto.JsonBody is null ? null : JsonSerializer.Serialize(dto.JsonBody),
      LessonFormat = dto.LessonFormat ?? LessonContentFormatInference.FromBody(dto.Body),
      SortOrder = dto.SortOrder,
      IsRequired = dto.IsRequired,
      EstimatedMinutes = dto.EstimatedMinutes,
      EstimatedMinutesSource = dto.EstimatedMinutesSource ?? EstimatedMinutesSource.Auto,
      Visibility = dto.Visibility,
    };

    content.NormalizeLearningContract();
    if (dto.ActivitySettings is not null)
    {
      content.SetActivitySettings(dto.ActivitySettings);
    }
    return content;
  }

  /// <summary> Applies updates from UpdateProgramContentDto to ProgramContent entity </summary>
  public static void ApplyUpdates(this ProgramContent content, UpdateProgramContentDto dto)
  {
    var wasLesson = NormalizeProfessorFacingType(content.Type) == ProgramContentType.Lesson;
    if (dto.Title != null) content.Title = dto.Title;
    if (!string.IsNullOrWhiteSpace(dto.Slug)) content.Slug = dto.Slug;
    if (dto.Description != null) content.Description = dto.Description;
    if (dto.Type != null) content.Type = NormalizeProfessorFacingType(dto.Type.Value);
    if (dto.JsonBody is not null)
    {
      content.JsonBody = JsonSerializer.Serialize(dto.JsonBody);
      content.Body = null;
    }
    else if (dto.Body is not null)
    {
      content.Body = dto.Body;
      content.JsonBody = null;
    }
    if (dto.LessonFormat.HasValue)
    {
      content.LessonFormat = dto.LessonFormat.Value;
    }
    if (dto.ActivitySettings is not null) content.SetActivitySettings(dto.ActivitySettings);
    else if (NormalizeProfessorFacingType(content.Type) == ProgramContentType.Lesson &&
             (!wasLesson || !content.LessonFormat.HasValue))
    {
      content.LessonFormat = LessonContentFormatInference.FromBody(content.Body);
    }
    if (dto.SortOrder != null) content.SortOrder = dto.SortOrder.Value;
    if (dto.IsRequired != null) content.IsRequired = dto.IsRequired.Value;
    if (dto.EstimatedMinutes.HasValue)
    {
        content.EstimatedMinutes = dto.EstimatedMinutes.Value;
        content.EstimatedMinutesSource = EstimatedMinutesSource.Manual;
    }
    else if (dto.EstimatedMinutesSource == EstimatedMinutesSource.Auto)
    {
        content.EstimatedMinutesSource = EstimatedMinutesSource.Auto;
        // Value recomputed by RecalculateEstimatedReadingTime() inside NormalizeLearningContract below.
    }
    // else: leave both fields untouched.
    if (dto.Visibility != null) content.Visibility = dto.Visibility.Value;
    content.NormalizeLearningContract();
  }
}
