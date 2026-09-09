
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary> Interface for content interaction tracking services </summary>
public interface IContentInteractionService {
  Task<ContentInteraction> StartContentAsync(Guid programUserId, Guid contentId);

  Task<ContentInteraction> UpdateProgressAsync(Guid interactionId, PercentValue completionPercentage);

  Task<ContentInteraction> SubmitContentAsync(Guid interactionId, string submissionData);

  Task<ContentInteraction> CompleteContentAsync(Guid interactionId);

  Task<ContentInteraction?> GetInteractionAsync(Guid programUserId, Guid contentId);

  Task<IEnumerable<ContentInteraction>> GetUserInteractionsAsync(Guid programUserId);

  Task<IEnumerable<SurveyResponseResultDto>> GetSurveyResponsesAsync(Guid expectedProgramId, Guid contentId);

  Task<IEnumerable<SurveyResponseResultDto>> GetVisibleSurveyResponsesAsync(Guid expectedProgramId, Guid contentId);

  Task<IEnumerable<ReflectionResponseResultDto>> GetReflectionResponsesAsync(Guid expectedProgramId, Guid contentId);

  Task<IEnumerable<ReflectionResponseResultDto>> GetVisibleReflectionResponsesAsync(Guid expectedProgramId, Guid contentId);

  Task<ContentInteraction> UpdateTimeSpentAsync(Guid interactionId, int additionalMinutes);
}
