

using Microsoft.EntityFrameworkCore;
using GameGuild.Learning.Grading.Contracts;


namespace GameGuild.Learning.Courses;

/// <summary> Service implementation for ActivityGrade management with full permission inheritance Handles grading operations following permission chain: ActivityGrade → ContentInteraction → ProgramContent → Program </summary>
public class ActivityGradeService(
  IApplicationDbContext context,
  IEnumerable<IProgramContentAcademicMutationGuard>? academicGuards = null) : IActivityGradeService {
  private readonly IEnumerable<IProgramContentAcademicMutationGuard> academicMutationGuards = academicGuards ?? [];
  /// <summary> Grade a content interaction - creates or updates existing grade </summary>
  public async Task<ActivityGrade> GradeActivityAsync(Guid contentInteractionId, Guid graderProgramUserId, ScoreValue points, ScoreValue maxPoints, string? feedback = null, string? gradingDetails = null) {
    // Validate the content interaction exists and get the program context
    var contentInteraction = await context.Set<ContentInteraction>().Include(ci => ci.Content).ThenInclude(c => c.Program).Include(ci => ci.ProgramUser).FirstOrDefaultAsync(ci => ci.Id == contentInteractionId);

    if (contentInteraction == null) throw new ArgumentException("Content interaction not found", nameof(contentInteractionId));
    if (contentInteraction.Content.Type == ProgramContentType.Survey)
      throw new InvalidOperationException("Surveys cannot be graded.");
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, contentInteraction.Content, ProgramContentAcademicMutation.Grade);

    // Validate the grader is part of the same program
    var graderProgramUser = await context.Set<ProgramUser>().FirstOrDefaultAsync(pu => pu.Id == graderProgramUserId && pu.ProgramId == contentInteraction.Content.ProgramId);

    if (graderProgramUser == null) throw new ArgumentException("Grader is not a member of this program", nameof(graderProgramUserId));

    // Check if a grade already exists for this interaction
    var existingGrade = await context.Set<ActivityGrade>().FirstOrDefaultAsync(ag => ag.ContentInteractionId == contentInteractionId);

    if (existingGrade != null) {
      // Update existing grade
      existingGrade.AssignPoints(points, maxPoints);
      existingGrade.Feedback = feedback;
      existingGrade.GradingDetails = gradingDetails;
      existingGrade.GraderProgramUserId = graderProgramUserId;
      existingGrade.GradedAt = SystemClock.UtcNow;
      existingGrade.Touch();

      await context.SaveChangesAsync().ConfigureAwait(false);

      return existingGrade;
    }

    // Create new grade
    var newGrade = new ActivityGrade { ContentInteractionId = contentInteractionId, GraderProgramUserId = graderProgramUserId, Feedback = feedback, GradingDetails = gradingDetails ?? "{}", GradedAt = SystemClock.UtcNow };
    newGrade.AssignPoints(points, maxPoints);

    context.Set<ActivityGrade>().Add(newGrade);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return newGrade;
  }

  /// <summary> Get grade for a specific content interaction </summary>
  public async Task<ActivityGrade?> GetGradeAsync(Guid contentInteractionId) {
    return await context.Set<ActivityGrade>().Include(ag => ag.ContentInteraction)
                        .ThenInclude(ci => ci.Content)
                        .Include(ag => ag.GraderProgramUser)
                        .ThenInclude(gpu => gpu!.User)
                        .FirstOrDefaultAsync(ag => ag.ContentInteractionId == contentInteractionId && ag.ContentInteraction.Content.Type != ProgramContentType.Survey);
  }

  /// <summary> Get grade by its ID </summary>
  public async Task<ActivityGrade?> GetGradeByIdAsync(Guid gradeId) {
    return await context.Set<ActivityGrade>().Include(ag => ag.ContentInteraction)
                        .ThenInclude(ci => ci.Content)
                        .ThenInclude(c => c.Program)
                        .Include(ag => ag.ContentInteraction)
                        .ThenInclude(ci => ci.ProgramUser)
                        .ThenInclude(pu => pu!.User)
                        .Include(ag => ag.GraderProgramUser)
                        .ThenInclude(gpu => gpu!.User)
                        .FirstOrDefaultAsync(ag => ag.Id == gradeId && ag.ContentInteraction.Content.Type != ProgramContentType.Survey);
  }

  /// <summary> Get all grades given by a specific grader </summary>
  public async Task<IEnumerable<ActivityGrade>> GetGradesByGraderAsync(Guid graderProgramUserId) {
    return await context.Set<ActivityGrade>().Include(ag => ag.ContentInteraction)
                        .ThenInclude(ci => ci.Content)
                        .Include(ag => ag.ContentInteraction)
                        .ThenInclude(ci => ci.ProgramUser)
                        .ThenInclude(pu => pu!.User)
                        .Where(ag => ag.GraderProgramUserId == graderProgramUserId && ag.ContentInteraction.Content.Type != ProgramContentType.Survey)
                        .OrderByDescending(ag => ag.GradedAt)
                        .ToListAsync();
  }

  /// <summary> Get all grades received by a specific program user </summary>
  public async Task<IEnumerable<ActivityGrade>> GetGradesByStudentAsync(Guid programUserId) {
    return await context.Set<ActivityGrade>().Include(ag => ag.ContentInteraction)
                        .ThenInclude(ci => ci.Content)
                        .Include(ag => ag.GraderProgramUser)
                        .ThenInclude(gpu => gpu!.User)
                        .Where(ag => ag.ContentInteraction.ProgramUserId == programUserId && ag.ContentInteraction.Content.Type != ProgramContentType.Survey)
                        .OrderByDescending(ag => ag.GradedAt)
                        .ToListAsync();
  }

  /// <summary> Update an existing grade </summary>
  public async Task<ActivityGrade?> UpdateGradeAsync(Guid gradeId, ScoreValue? newPoints = null, ScoreValue? newMaxPoints = null, string? newFeedback = null, string? newGradingDetails = null) {
    var grade = await context.Set<ActivityGrade>()
      .Include(ag => ag.ContentInteraction)
      .ThenInclude(interaction => interaction.Content)
      .FirstOrDefaultAsync(ag => ag.Id == gradeId);

    if (grade == null) return null;
    if (grade.ContentInteraction.Content.Type == ProgramContentType.Survey)
      throw new InvalidOperationException("Surveys cannot be graded.");
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, grade.ContentInteraction.Content, ProgramContentAcademicMutation.Grade);

    if (newPoints.HasValue) grade.AssignPoints(newPoints.Value, newMaxPoints ?? grade.MaxPoints);
    if (newFeedback != null) grade.Feedback = newFeedback;
    if (newGradingDetails != null) grade.GradingDetails = newGradingDetails;
    grade.GradedAt = SystemClock.UtcNow;
    grade.Touch();

    await context.SaveChangesAsync().ConfigureAwait(false);

    return grade;
  }

  /// <summary> Delete a grade </summary>
  public async Task<bool> DeleteGradeAsync(Guid gradeId) {
    var grade = await context.Set<ActivityGrade>()
      .Include(item => item.ContentInteraction)
      .ThenInclude(interaction => interaction.Content)
      .FirstOrDefaultAsync(ag => ag.Id == gradeId);

    if (grade == null) return false;
    ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, grade.ContentInteraction.Content, ProgramContentAcademicMutation.Grade);

    context.Set<ActivityGrade>().Remove(grade);
    await context.SaveChangesAsync().ConfigureAwait(false);

    return true;
  }

  /// <summary> Get all pending grades for a program (content interactions that need grading) </summary>
  public async Task<IEnumerable<ContentInteraction>> GetPendingGradesAsync(Guid programId) {
    var candidates = await context.Set<ContentInteraction>().Include(ci => ci.Content)
                        .Include(ci => ci.ProgramUser)
                        .ThenInclude(pu => pu.User)
                        .Where(ci => ci.Content.ProgramId == programId && ci.Content.Type != ProgramContentType.Survey && ci.SubmittedAt.HasValue && !context.Set<ActivityGrade>().Any(ag => ag.ContentInteractionId == ci.Id))
                        .OrderBy(ci => ci.SubmittedAt)
                        .ToListAsync();
    return candidates.Where(interaction =>
      academicMutationGuards.All(guard => guard.GetRejection(interaction.Content, ProgramContentAcademicMutation.Grade) is null));
  }

  /// <summary> Get grade statistics for a program </summary>
  public async Task<GradeStatistics> GetGradeStatisticsAsync(Guid programId) {
    var grades = await context.Set<ActivityGrade>()
      .Include(ag => ag.ContentInteraction)
      .ThenInclude(ci => ci.Content)
      .Where(ag => ag.ContentInteraction.Content.ProgramId == programId &&
                   ag.ContentInteraction.Content.Type != ProgramContentType.Survey &&
                   ag.Points.HasValue && ag.MaxPoints.HasValue)
      .ToListAsync();

    var percentages = grades.Select(value => value.PercentageScore).Where(value => value.HasValue).Select(value => value!.Value).ToArray();
    if (percentages.Length == 0) return new GradeStatistics { TotalGrades = 0 };

    return new GradeStatistics {
      TotalGrades = percentages.Length,
      AverageGrade = PercentValue.Average(percentages),
      MinGrade = percentages.MinBy(value => value.Units),
      MaxGrade = percentages.MaxBy(value => value.Units),
      PassingRate = PercentValue.FromRatio(percentages.Count(value => value.CompareTo(PercentValue.FromPercentage("60")) >= 0), percentages.Length),
    };
  }

  /// <summary> Get grades for a specific content item across all students </summary>
  public async Task<IEnumerable<ActivityGrade>> GetGradesByContentAsync(Guid contentId) {
    return await context.Set<ActivityGrade>().Include(ag => ag.ContentInteraction)
                        .ThenInclude(ci => ci.ProgramUser)
                        .ThenInclude(pu => pu!.User)
                        .Include(ag => ag.GraderProgramUser)
                        .ThenInclude(gpu => gpu!.User)
                        .Where(ag => ag.ContentInteraction.ContentId == contentId && ag.ContentInteraction.Content.Type != ProgramContentType.Survey)
                        .OrderByDescending(ag => ag.GradedAt)
                        .ToListAsync();
  }
}
