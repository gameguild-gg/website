using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Service interface for assessment rubric management and rubric-score validation.
/// </summary>
public interface IRubricService
{
    /// <summary>
    /// Creates or fully replaces the rubric of an assessment (same rubric id on replace).
    /// Fails with Conflict while any submission of the assessment is Graded.
    /// </summary>
    Task<Result<RubricDto>> SaveAsync(Guid assessmentId, SaveRubricRequest request);

    /// <summary>
    /// Gets the rubric of an assessment. Fails with NotFound when none is assigned.
    /// </summary>
    Task<Result<RubricDto>> GetAsync(Guid assessmentId);

    /// <summary>
    /// Removes the rubric association and deletes the rubric with its criteria.
    /// Fails with Conflict while any submission of the assessment is Graded.
    /// </summary>
    Task<Result> DeleteAsync(Guid assessmentId);

    /// <summary>
    /// Validates rubric scores against the assessment's rubric:
    /// required when rubric-graded, absent when not, every criterion present,
    /// each score within [0, criterion.Points], and the sum equal to the submitted score.
    /// Shared by instructor grading and peer review submit.
    /// </summary>
    Task<Result> ValidateScoresAsync(Guid assessmentId, ScoreValue score, string? rubricScores);
}

/// <summary>
/// Request to create or fully replace an assessment rubric.
/// </summary>
public sealed record SaveRubricRequest(string Title, IReadOnlyList<SaveRubricCriterionRequest> Criteria);

/// <summary>
/// One criterion row of a rubric save request.
/// </summary>
public sealed record SaveRubricCriterionRequest(string Description, ScoreValue Points, int Order);

/// <summary>
/// Rubric read shape with criteria ordered by <see cref="RubricCriterionDto.Order"/>.
/// </summary>
public sealed record RubricDto(
    Guid Id,
    string Title,
    IReadOnlyList<RubricCriterionDto> Criteria)
{
    public static RubricDto From(AssessmentRubric rubric, IEnumerable<RubricCriterion> criteria) => new(
        rubric.Id,
        rubric.Title,
        criteria.OrderBy(c => c.Order).Select(RubricCriterionDto.From).ToList());
}

/// <summary>
/// Rubric criterion read shape.
/// </summary>
public sealed record RubricCriterionDto(
    Guid Id,
    string Description,
    ScoreValue Points,
    int Order)
{
    public static RubricCriterionDto From(RubricCriterion entity) => new(
        entity.Id,
        entity.Description,
        entity.Points,
        entity.Order);
}
