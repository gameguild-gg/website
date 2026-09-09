
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary> Interface for content progress tracking services </summary>
public interface IContentProgressService {
  /// <summary> Track user access to content </summary>
  Task<ContentProgress> TrackContentAccessAsync(Guid userId, Guid contentId, Guid programEnrollmentId);

  /// <summary> Start tracking content progress (initial access) </summary>
  Task<ContentProgress> StartContentAsync(Guid userId, Guid contentId, Guid programEnrollmentId);

  /// <summary> Update content progress </summary>
  Task<ContentProgress> UpdateContentProgressAsync(Guid userId, Guid contentId, PercentValue progressPercentage, int? timeSpentSeconds = null);

  /// <summary> Mark content as completed </summary>
  Task<ContentProgress> CompleteContentAsync(Guid userId, Guid contentId, ScoreValue? score = null, ScoreValue? maxScore = null);

  /// <summary> Get user's progress for specific content </summary>
  Task<ContentProgress?> GetContentProgressAsync(Guid userId, Guid contentId);

  /// <summary> Get user's progress for all content in a program </summary>
  Task<IEnumerable<ContentProgress>> GetProgramProgressAsync(Guid userId, Guid programId);

  /// <summary> Calculate overall program progress percentage </summary>
  Task<PercentValue> CalculateProgramProgressAsync(Guid userId, Guid programId);

  /// <summary> Get next content item to access in program </summary>
  Task<Guid?> GetNextContentAsync(Guid userId, Guid programId);

  /// <summary> Check if user can access specific content (based on prerequisites) </summary>
  Task<bool> CanAccessContentAsync(Guid userId, Guid contentId);

  /// <summary> Get content completion statistics for a program </summary>
  Task<ContentCompletionStats> GetCompletionStatsAsync(Guid programId);

  /// <summary> Reset user progress for a program (admin function) </summary>
  Task<bool> ResetProgramProgressAsync(Guid userId, Guid programId);
}
