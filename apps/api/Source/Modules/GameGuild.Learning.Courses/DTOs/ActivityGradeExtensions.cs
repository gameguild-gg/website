namespace GameGuild.Learning.Courses;

/// <summary> Extension methods to convert from entity/service models to DTOs </summary>
public static class ActivityGradeExtensions {
  public static ActivityGradeDto ToDto(this ActivityGrade grade) {
    if (grade.ContentInteraction?.Content?.Type == ProgramContentType.Survey) {
      throw new InvalidOperationException("Survey grades are not available.");
    }

    return new ActivityGradeDto {
      Id = grade.Id,
      ContentInteractionId = grade.ContentInteractionId,
      GraderProgramUserId = grade.GraderProgramUserId,
      Points = grade.Points,
      MaxPoints = grade.MaxPoints,
      PercentageScore = grade.PercentageScore,
      Feedback = grade.Feedback,
      GradingDetails = grade.GradingDetails,
      GradedAt = grade.GradedAt,
      CreatedAt = grade.CreatedAt,
      UpdatedAt = grade.UpdatedAt,
      ContentInteraction =
        grade.ContentInteraction != null
          ? new ContentInteractionSummaryDto {
            Id = grade.ContentInteraction.Id,
            ProgramUserId = grade.ContentInteraction.ProgramUserId,
            ContentId = grade.ContentInteraction.ContentId,
            Status = grade.ContentInteraction.Status.ToString(),
            SubmittedAt = grade.ContentInteraction.SubmittedAt,
            Content =
              grade.ContentInteraction.Content != null
                ? new ContentSummaryDto {
                  Id = grade.ContentInteraction.Content.Id,
                  Title = grade.ContentInteraction.Content.Title,
                  ContentType = grade.ContentInteraction.Content.Type.ToString(),
                  EstimatedMinutes = grade.ContentInteraction.Content.EstimatedMinutes,
                }
                : null,
            Student =
              grade.ContentInteraction.ProgramUser?.User != null
                ? new StudentSummaryDto { Id = grade.ContentInteraction.ProgramUser.User.Id, UserDisplayName = grade.ContentInteraction.ProgramUser.User.Name, UserEmail = grade.ContentInteraction.ProgramUser.User.Email }
                : null,
          }
          : null,
      Grader = grade.GraderProgramUser?.User != null
                 ? new GraderSummaryDto {
                   Id = grade.GraderProgramUser.User.Id, UserDisplayName = grade.GraderProgramUser.User.Name, UserEmail = grade.GraderProgramUser.User.Email, Role = "Grader", // Default role since ProgramUser doesn't have a Role property
                 }
                 : null,
    };
  }

  public static IEnumerable<ActivityGradeDto> ToDto(this IEnumerable<ActivityGrade> grades) { return grades.Select(g => g.ToDto()); }

  public static GradeStatisticsDto ToDto(this GradeStatistics statistics) {
    return new GradeStatisticsDto { TotalGrades = statistics.TotalGrades, AverageGrade = statistics.AverageGrade, MinGrade = statistics.MinGrade, MaxGrade = statistics.MaxGrade, PassingRate = statistics.PassingRate };
  }
}
