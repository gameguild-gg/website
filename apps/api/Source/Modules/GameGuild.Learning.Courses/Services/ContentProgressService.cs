using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;


namespace GameGuild.Learning.Courses;

/// <summary> Service implementation for content progress tracking </summary>
public class ContentProgressService : IContentProgressService {
  private readonly IApplicationDbContext _context;

  private readonly IProgramEnrollmentService _enrollmentService;

  private readonly IEnumerable<IProgramContentAcademicMutationGuard> _academicMutationGuards;

  public ContentProgressService(
    IApplicationDbContext context,
    IProgramEnrollmentService enrollmentService,
    IEnumerable<IProgramContentAcademicMutationGuard>? academicMutationGuards = null) {
    _context = context;
    _enrollmentService = enrollmentService;
    _academicMutationGuards = academicMutationGuards ?? [];
  }

  /// <summary> Track user access to content </summary>
  public async Task<ContentProgress> TrackContentAccessAsync(Guid userId, Guid contentId, Guid programEnrollmentId) {
    await EnsureAcademicMutationAllowedAsync(contentId, ProgramContentAcademicMutation.Start).ConfigureAwait(false);
    var progress = await _context.Set<ContentProgress>().FirstOrDefaultAsync(cp => cp.UserId == userId && cp.ContentId == contentId);

    if (progress == null) {
      progress = new ContentProgress { UserId = userId, ContentId = contentId, ProgramEnrollmentId = programEnrollmentId };
      _context.Set<ContentProgress>().Add(progress);
    }

    progress.MarkAsAccessed();
    await _context.SaveChangesAsync().ConfigureAwait(false);

    // Update program-level progress
    await UpdateProgramProgressAsync(userId, programEnrollmentId).ConfigureAwait(false);

    return progress;
  }

  /// <summary> Start tracking content progress (initial access) </summary>
  public async Task<ContentProgress> StartContentAsync(Guid userId, Guid contentId, Guid programEnrollmentId) { return await TrackContentAccessAsync(userId, contentId, programEnrollmentId).ConfigureAwait(false); }

  /// <summary> Update content progress </summary>
  public async Task<ContentProgress> UpdateContentProgressAsync(Guid userId, Guid contentId, PercentValue progressPercentage, int? timeSpentSeconds = null) {
    await EnsureAcademicMutationAllowedAsync(contentId, ProgramContentAcademicMutation.UpdateProgress).ConfigureAwait(false);
    var progress = await _context.Set<ContentProgress>().FirstOrDefaultAsync(cp => cp.UserId == userId && cp.ContentId == contentId);

    if (progress == null) { throw new ArgumentException("Content progress not found. Track access first.", nameof(contentId)); }

    progress.UpdateProgress(progressPercentage);

    if (timeSpentSeconds.HasValue) { progress.AddTimeSpent(timeSpentSeconds.Value); }

    await _context.SaveChangesAsync().ConfigureAwait(false);

    // Update program-level progress
    await UpdateProgramProgressAsync(userId, progress.ProgramEnrollmentId).ConfigureAwait(false);

    return progress;
  }

  /// <summary> Mark content as completed </summary>
  public async Task<ContentProgress> CompleteContentAsync(Guid userId, Guid contentId, ScoreValue? score = null, ScoreValue? maxScore = null) {
    await EnsureAcademicMutationAllowedAsync(contentId, ProgramContentAcademicMutation.Complete).ConfigureAwait(false);
    var progress = await _context.Set<ContentProgress>().FirstOrDefaultAsync(cp => cp.UserId == userId && cp.ContentId == contentId);

    if (progress == null) { throw new ArgumentException("Content progress not found. Track access first.", nameof(contentId)); }

    progress.MarkAsCompleted(score, maxScore);
    await _context.SaveChangesAsync().ConfigureAwait(false);

    // Update program-level progress
    await UpdateProgramProgressAsync(userId, progress.ProgramEnrollmentId).ConfigureAwait(false);

    return progress;
  }

  /// <summary> Get user's progress for specific content </summary>
  public async Task<ContentProgress?> GetContentProgressAsync(Guid userId, Guid contentId) { return await _context.Set<ContentProgress>().Include(cp => cp.Content).FirstOrDefaultAsync(cp => cp.UserId == userId && cp.ContentId == contentId); }

  /// <summary> Get user's progress for all content in a program </summary>
  public async Task<IEnumerable<ContentProgress>> GetProgramProgressAsync(Guid userId, Guid programId) {
    return await _context.Set<ContentProgress>().Include(cp => cp.Content)
                         .Join(_context.Set<ProgramContent>(), cp => cp.ContentId, pc => pc.Id, (cp, pc) => new { cp, pc })
                         .Where(x => x.cp.UserId == userId && x.pc.ProgramId == programId)
                         .Select(x => x.cp)
                         .OrderBy(cp => cp.Content!.Id) // Using Id instead of SortOrder for now
                         .ToListAsync();
  }

  /// <summary> Calculate overall program progress percentage </summary>
  public async Task<PercentValue> CalculateProgramProgressAsync(Guid userId, Guid programId) {
    // Get all content in the program
    var totalContent = await _context.Set<ProgramContent>().Where(pc => pc.ProgramId == programId && pc.IsRequired).CountAsync();

    if (totalContent == 0) return PercentValue.Zero;

    // Get completed content
    var completedContent = await _context.Set<ContentProgress>().Join(_context.Set<ProgramContent>(), cp => cp.ContentId, pc => pc.Id, (cp, pc) => new { cp, pc })
                                         .Where(x => x.cp.UserId == userId && x.pc.ProgramId == programId && x.pc.IsRequired && x.cp.CompletionStatus == ContentCompletionStatus.Completed)
                                         .CountAsync();

    return PercentValue.FromRatio(completedContent, totalContent);
  }

  /// <summary> Get next content item to access in program </summary>
  public async Task<Guid?> GetNextContentAsync(Guid userId, Guid programId) {
    // Get program content ordered by sort order
    var programContents = await _context.Set<ProgramContent>().Where(pc => pc.ProgramId == programId).OrderBy(pc => pc.SortOrder).Select(pc => pc.Id).ToListAsync();

    // Find first content that's not completed
    foreach (var contentId in programContents) {
      var progress = await _context.Set<ContentProgress>().FirstOrDefaultAsync(cp => cp.UserId == userId && cp.ContentId == contentId);

      if (progress == null || progress.CompletionStatus != ContentCompletionStatus.Completed) {
        // Check if user can access this content (prerequisites)
        if (await CanAccessContentAsync(userId, contentId)) { return contentId; }
      }
    }

    return null; // All content completed or no accessible content
  }

  /// <summary> Check if user can access specific content (based on prerequisites) </summary>
  public async Task<bool> CanAccessContentAsync(Guid userId, Guid contentId) {
    // Get content with its program
    var content = await _context.Set<ProgramContent>().FirstOrDefaultAsync(pc => pc.Id == contentId);

    if (content == null) return false;

    // Get all content items in the same program, ordered by sort order
    var programContents = await _context.Set<ProgramContent>()
        .Where(pc => pc.ProgramId == content.ProgramId)
        .OrderBy(pc => pc.SortOrder)
        .ToListAsync().ConfigureAwait(false);

    // Find the index of the current content
    var contentIndex = programContents.FindIndex(pc => pc.Id == contentId);
    
    // First item is always accessible
    if (contentIndex <= 0) return true;

    // Check if all previous required content items are completed
    var previousRequiredContents = programContents
        .Take(contentIndex)
        .Where(pc => pc.IsRequired)
        .Select(pc => pc.Id)
        .ToList();

    if (previousRequiredContents.Count == 0) return true;

    // Get user progress for previous required content
    var completedCount = await _context.Set<ContentProgress>()
        .Where(cp => cp.UserId == userId 
            && previousRequiredContents.Contains(cp.ContentId)
            && cp.CompletionStatus == ContentCompletionStatus.Completed)
        .CountAsync().ConfigureAwait(false);

    // User can access if all previous required content is completed
    return completedCount >= previousRequiredContents.Count;
  }

  /// <summary> Get content completion statistics for a program </summary>
  public async Task<ContentCompletionStats> GetCompletionStatsAsync(Guid programId) {
    var allProgress = await _context.Set<ContentProgress>().Include(cp => cp.Content)
                                    .Join(_context.Set<ProgramContent>(), cp => cp.ContentId, pc => pc.Id, (cp, pc) => new { cp, pc })
                                    .Where(x => x.pc.ProgramId == programId)
                                    .Select(x => x.cp)
                                    .ToListAsync();

    var totalContent = await _context.Set<ProgramContent>().Where(pc => pc.ProgramId == programId).CountAsync();

    var completed = allProgress.Count(cp => cp.CompletionStatus == ContentCompletionStatus.Completed);
    var inProgress = allProgress.Count(cp => cp.CompletionStatus == ContentCompletionStatus.InProgress);
    var notStarted = totalContent - allProgress.Count;

    var completionByType = allProgress.GroupBy(cp => "Content") // Using generic type for now
                                      .ToDictionary(g => g.Key, g => g.Count(cp => cp.CompletionStatus == ContentCompletionStatus.Completed));

    return new ContentCompletionStats {
      TotalContentItems = totalContent,
      CompletedContentItems = completed,
      InProgressContentItems = inProgress,
      NotStartedContentItems = notStarted,
      AverageCompletionRate = totalContent > 0
        ? PercentValue.FromRatio(completed, totalContent)
        : PercentValue.Zero,
      AverageScore = ScoreValue.Average(allProgress.Where(cp => cp.Score.HasValue).Select(cp => cp.Score!.Value)),
      TotalTimeSpentHours = allProgress.Sum(cp => cp.TimeSpentSeconds) / 3600,
      CompletionByContentType = completionByType,
    };
  }

  /// <summary> Reset user progress for a program (admin function) </summary>
  public async Task<bool> ResetProgramProgressAsync(Guid userId, Guid programId) {
    var progressRecords = await _context.Set<ContentProgress>().Join(_context.Set<ProgramContent>(), cp => cp.ContentId, pc => pc.Id, (cp, pc) => new { cp, pc })
                                        .Where(x => x.cp.UserId == userId && x.pc.ProgramId == programId)
                                        .Select(x => x.cp)
                                        .ToListAsync();

    _context.Set<ContentProgress>().RemoveRange(progressRecords);
    await _context.SaveChangesAsync().ConfigureAwait(false);

    // Reset program enrollment progress
    var enrollment = await _context.Set<ProgramEnrollment>().FirstOrDefaultAsync(pe => pe.UserId == userId && pe.ProgramId == programId);

    if (enrollment != null) {
      enrollment.ProgressPercentage = PercentValue.Zero;
      enrollment.CompletionStatus = CompletionStatus.NotStarted;
      enrollment.CompletedAt = null;
      enrollment.Touch();
      await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    return true;
  }

  private async Task EnsureAcademicMutationAllowedAsync(Guid contentId, ProgramContentAcademicMutation mutation) {
    var content = await _context.Set<ProgramContent>()
      .AsNoTracking()
      .FirstOrDefaultAsync(item => item.Id == contentId && item.DeletedAt == null)
      .ConfigureAwait(false);
    if (content is null) throw new ArgumentException("Content was not found.", nameof(contentId));
    ProgramContentAcademicMutationGuard.EnsureAllowed(_academicMutationGuards, content, mutation);
  }

  /// <summary> Update program-level progress based on content completion </summary>
  private async Task UpdateProgramProgressAsync(Guid userId, Guid programEnrollmentId) {
    var enrollment = await _context.Set<ProgramEnrollment>().FirstOrDefaultAsync(pe => pe.Id == programEnrollmentId);

    if (enrollment == null) return;

    var programProgress = await CalculateProgramProgressAsync(userId, enrollment.ProgramId).ConfigureAwait(false);
    await _enrollmentService.UpdateProgressAsync(programEnrollmentId, programProgress).ConfigureAwait(false);
  }
}
