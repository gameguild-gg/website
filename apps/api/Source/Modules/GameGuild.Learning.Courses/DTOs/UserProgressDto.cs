namespace GameGuild.Learning.Courses;

public sealed record UserProgressDto(
  Guid EnrollmentId,
  Guid CourseId,
  Guid UserId,
  decimal CompletionPercentage,
  DateTime? LastAccessedAt,
  DateTime? StartedAt,
  DateTime? CompletedAt,
  IEnumerable<ContentProgressDto> ContentProgress,
  string? UserName = null,
  string? UserEmail = null
) {
  public Guid EnrollmentId { get; init; } = EnrollmentId;

  public Guid CourseId { get; init; } = CourseId;

  public Guid UserId { get; init; } = UserId;

  public decimal CompletionPercentage { get; init; } = CompletionPercentage;

  public DateTime? LastAccessedAt { get; init; } = LastAccessedAt;

  public DateTime? StartedAt { get; init; } = StartedAt;

  public DateTime? CompletedAt { get; init; } = CompletedAt;

  public IEnumerable<ContentProgressDto> ContentProgress { get; init; } = ContentProgress;

  public string? UserName { get; init; } = UserName;

  public string? UserEmail { get; init; } = UserEmail;
}
