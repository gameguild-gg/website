using GameGuild.Common.Enums;

namespace GameGuild.Modules.Program.Interfaces;

/// <summary>
/// Interface for program management services
/// </summary>
public interface IProgramService
{
    Task<Models.Program> CreateProgramAsync(Models.Program program);

    Task<Models.Program?> GetProgramByIdAsync(int id);

    Task<IEnumerable<Models.Program>> GetProgramsByVisibilityAsync(Visibility visibility);

    Task<Models.Program> UpdateProgramAsync(Models.Program program);

    Task<bool> DeleteProgramAsync(int id);

    Task<IEnumerable<Models.Program>> GetProgramsByProductAsync(int productId);

    Task<bool> AddProgramToProductAsync(int productId, int programId, int sortOrder = 0);

    Task<bool> RemoveProgramFromProductAsync(int productId, int programId);
}

/// <summary>
/// Interface for program content management services
/// </summary>
public interface IProgramContentService
{
    Task<Models.ProgramContent> CreateContentAsync(Models.ProgramContent content);

    Task<Models.ProgramContent?> GetContentByIdAsync(int id);

    Task<IEnumerable<Models.ProgramContent>> GetContentByProgramAsync(int programId);

    Task<IEnumerable<Models.ProgramContent>> GetContentByParentAsync(int parentId);

    Task<Models.ProgramContent> UpdateContentAsync(Models.ProgramContent content);

    Task<bool> DeleteContentAsync(int id);

    Task<bool> ReorderContentAsync(int programId, List<(int contentId, int sortOrder)> newOrder);

    Task<IEnumerable<Models.ProgramContent>> GetRequiredContentAsync(int programId);
}

/// <summary>
/// Interface for program user enrollment and progress services
/// </summary>
public interface IProgramUserService
{
    Task<Models.ProgramUser> EnrollUserAsync(int userProductId, int programId);

    Task<Models.ProgramUser?> GetProgramUserAsync(int id);

    Task<Models.ProgramUser?> GetProgramUserAsync(int userId, int programId);

    Task<IEnumerable<Models.ProgramUser>> GetUserProgramsAsync(int userId);

    Task<Models.ProgramUser> UpdateProgressAsync(int programUserId, decimal completionPercentage);

    Task<Models.ProgramUser> UpdateGradeAsync(int programUserId, decimal finalGrade);

    Task<bool> CompleteeProgramAsync(int programUserId);

    Task<IEnumerable<Models.ProgramUser>> GetProgramEnrollmentsAsync(int programId);

    Task<bool> UnenrollUserAsync(int programUserId);
}

/// <summary>
/// Interface for content interaction tracking services
/// </summary>
public interface IContentInteractionService
{
    Task<Models.ContentInteraction> StartContentAsync(int programUserId, int contentId);

    Task<Models.ContentInteraction> UpdateProgressAsync(int interactionId, decimal completionPercentage);

    Task<Models.ContentInteraction> SubmitContentAsync(int interactionId, string submissionData);

    Task<Models.ContentInteraction> CompleteContentAsync(int interactionId);

    Task<Models.ContentInteraction?> GetInteractionAsync(int programUserId, int contentId);

    Task<IEnumerable<Models.ContentInteraction>> GetUserInteractionsAsync(int programUserId);

    Task<Models.ContentInteraction> UpdateTimeSpentAsync(int interactionId, int additionalMinutes);
}

/// <summary>
/// Interface for activity grading services
/// </summary>
public interface IActivityGradeService
{
    Task<Models.ActivityGrade> GradeActivityAsync(int contentInteractionId, int graderProgramUserId, decimal grade, string? feedback = null);

    Task<Models.ActivityGrade?> GetGradeAsync(int contentInteractionId);

    Task<IEnumerable<Models.ActivityGrade>> GetGradesByGraderAsync(int graderProgramUserId);

    Task<Models.ActivityGrade> UpdateGradeAsync(int gradeId, decimal newGrade, string? newFeedback = null);

    Task<bool> DeleteGradeAsync(int gradeId);

    Task<IEnumerable<Models.ActivityGrade>> GetPendingGradesAsync(int programId);
}
