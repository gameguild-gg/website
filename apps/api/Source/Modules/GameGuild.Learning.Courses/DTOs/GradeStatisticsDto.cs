using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary> DTO for grade statistics responses </summary>
public class GradeStatisticsDto {
  public int TotalGrades { get; set; }

  public PercentValue AverageGrade { get; set; } = PercentValue.Zero;

  public PercentValue MinGrade { get; set; } = PercentValue.Zero;

  public PercentValue MaxGrade { get; set; } = PercentValue.Zero;

  public PercentValue PassingRate { get; set; } = PercentValue.Zero;

  // Additional computed properties for better UX
  public string AverageGradeFormatted { get => $"{AverageGrade}%"; }

  public string PassingRateFormatted { get => $"{PassingRate}%"; }

  public bool HasGrades { get => TotalGrades > 0; }
}
