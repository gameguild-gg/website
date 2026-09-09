using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Grade statistics for reporting
/// </summary>
public class GradeStatistics {
  public int TotalGrades { get; set; }

  public PercentValue AverageGrade { get; set; } = PercentValue.Zero;

  public PercentValue MinGrade { get; set; } = PercentValue.Zero;

  public PercentValue MaxGrade { get; set; } = PercentValue.Zero;

  public PercentValue PassingRate { get; set; } = PercentValue.Zero;
}
