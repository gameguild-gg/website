using cms.Common.Enums;

namespace cms.Modules.Feedback.Interfaces;

/// <summary>
/// Interface for program feedback services
/// </summary>
public interface IProgramFeedbackService
{
    Task<Models.ProgramFeedbackSubmission> SubmitFeedbackAsync(Models.ProgramFeedbackSubmission feedback);
    Task<Models.ProgramFeedbackSubmission?> GetFeedbackByIdAsync(int id);
    Task<IEnumerable<Models.ProgramFeedbackSubmission>> GetProgramFeedbackAsync(int programId);
    Task<Models.ProgramFeedbackSubmission?> GetUserFeedbackAsync(int userId, int programId);
    Task<bool> HasUserSubmittedFeedbackAsync(int userId, int programId);
    Task<Models.ProgramFeedbackSubmission> UpdateFeedbackAsync(Models.ProgramFeedbackSubmission feedback);
    Task<bool> DeleteFeedbackAsync(int id);
}

/// <summary>
/// Interface for program rating services
/// </summary>
public interface IProgramRatingService
{
    Task<Models.ProgramRating> SubmitRatingAsync(Models.ProgramRating rating);
    Task<Models.ProgramRating?> GetRatingByIdAsync(int id);
    Task<IEnumerable<Models.ProgramRating>> GetProgramRatingsAsync(int programId);
    Task<Models.ProgramRating?> GetUserRatingAsync(int userId, int programId);
    Task<bool> HasUserSubmittedRatingAsync(int userId, int programId);
    Task<Models.ProgramRating> UpdateRatingAsync(Models.ProgramRating rating);
    Task<Models.ProgramRating> ModerateRatingAsync(int ratingId, int moderatorId, ModerationStatus status);
    Task<decimal> GetAverageRatingAsync(int programId);
    Task<IEnumerable<Models.ProgramRating>> GetPendingModerationAsync();
    Task<bool> DeleteRatingAsync(int id);
}
