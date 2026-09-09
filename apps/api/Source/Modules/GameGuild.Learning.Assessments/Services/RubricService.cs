using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Service implementation for assessment rubric management and rubric-score validation.
/// </summary>
public class RubricService : IRubricService
{
    private const string LockedMessage = "Rubric locked after grading started";

    private readonly IApplicationDbContext _context;
    private readonly ILogger<RubricService> _logger;

    public RubricService(IApplicationDbContext context, ILogger<RubricService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<RubricDto>> SaveAsync(Guid assessmentId, SaveRubricRequest request)
    {
        try
        {
            var assessment = await FindAssessmentAsync(assessmentId).ConfigureAwait(false);
            if (assessment == null)
            {
                return Result.Failure<RubricDto>(Error.NotFound("Assessment", "Assessment not found"));
            }

            if (await IsLockedAsync(assessmentId).ConfigureAwait(false))
            {
                return Result.Failure<RubricDto>(Error.Conflict("Rubric.Locked", LockedMessage));
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Result.Failure<RubricDto>(Error.Validation("Rubric.TitleRequired", "Title is required."));
            }

            if (request.Criteria is not { Count: > 0 })
            {
                return Result.Failure<RubricDto>(Error.Validation("Rubric.NoCriteria", "At least one criterion is required."));
            }

            // ponytail: Σ==MaxScore is re-validated on every PUT; if MaxScore later changes without a
            // follow-up PUT, grading-time Σ==Score still guards score integrity (residual, documented).
            if (ScoreValue.Sum(request.Criteria.Select(c => c.Points)) != assessment.MaxScore)
            {
                return Result.Failure<RubricDto>(Error.Validation(
                    "Rubric.PointsSumMismatch",
                    "Rubric points must sum to assessment max score"));
            }

            var rubric = assessment.RubricId is { } existingId
                ? await _context.Set<AssessmentRubric>()
                    .FirstOrDefaultAsync(r => r.Id == existingId && r.DeletedAt == null)
                    .ConfigureAwait(false)
                : null;

            if (rubric == null)
            {
                rubric = AssessmentRubric.Create(request.Title);
                _context.Set<AssessmentRubric>().Add(rubric);
                assessment.AssignRubric(rubric.Id);
                _context.Set<Assessment>().Update(assessment);
            }
            else
            {
                rubric.Replace(request.Title);
            }

            // Full replace semantics: drop the old criteria rows, insert the new set.
            var oldCriteria = await _context.Set<RubricCriterion>()
                .Where(c => c.RubricId == rubric.Id && c.DeletedAt == null)
                .ToListAsync().ConfigureAwait(false);
            _context.Set<RubricCriterion>().RemoveRange(oldCriteria);
            foreach (var criterion in request.Criteria)
            {
                _context.Set<RubricCriterion>().Add(RubricCriterion.Create(
                    rubric.Id, criterion.Description, criterion.Points, criterion.Order));
            }

            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Rubric saved: {RubricId} for assessment {AssessmentId}", rubric.Id, assessmentId);
            var savedCriteria = await _context.Set<RubricCriterion>()
                .Where(c => c.RubricId == rubric.Id && c.DeletedAt == null)
                .ToListAsync().ConfigureAwait(false);
            return Result.Success(RubricDto.From(rubric, savedCriteria));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<RubricDto>(Error.Validation("Rubric.Invalid", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving rubric for assessment {AssessmentId}", assessmentId);
            return Result.Failure<RubricDto>(Error.Failure("SaveRubric", "Failed to save rubric"));
        }
    }

    public async Task<Result<RubricDto>> GetAsync(Guid assessmentId)
    {
        try
        {
            var assessment = await FindAssessmentAsync(assessmentId).ConfigureAwait(false);
            if (assessment?.RubricId is not { } rubricId)
            {
                return Result.Failure<RubricDto>(Error.NotFound("Rubric", "No rubric is assigned to this assessment."));
            }

            var rubric = await _context.Set<AssessmentRubric>()
                .FirstOrDefaultAsync(r => r.Id == rubricId && r.DeletedAt == null)
                .ConfigureAwait(false);
            if (rubric == null)
            {
                return Result.Failure<RubricDto>(Error.NotFound("Rubric", "No rubric is assigned to this assessment."));
            }

            var criteria = await _context.Set<RubricCriterion>()
                .Where(c => c.RubricId == rubric.Id && c.DeletedAt == null)
                .ToListAsync().ConfigureAwait(false);
            return Result.Success(RubricDto.From(rubric, criteria));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading rubric for assessment {AssessmentId}", assessmentId);
            return Result.Failure<RubricDto>(Error.Failure("GetRubric", "Failed to load rubric"));
        }
    }

    public async Task<Result> DeleteAsync(Guid assessmentId)
    {
        try
        {
            var assessment = await FindAssessmentAsync(assessmentId).ConfigureAwait(false);
            if (assessment?.RubricId is not { } rubricId)
            {
                return Result.Failure(Error.NotFound("Rubric", "No rubric is assigned to this assessment."));
            }

            if (await IsLockedAsync(assessmentId).ConfigureAwait(false))
            {
                return Result.Failure(Error.Conflict("Rubric.Locked", LockedMessage));
            }

            var rubric = await _context.Set<AssessmentRubric>()
                .FirstOrDefaultAsync(r => r.Id == rubricId && r.DeletedAt == null)
                .ConfigureAwait(false);
            if (rubric == null)
            {
                return Result.Failure(Error.NotFound("Rubric", "No rubric is assigned to this assessment."));
            }

            var criteria = await _context.Set<RubricCriterion>()
                .Where(c => c.RubricId == rubric.Id && c.DeletedAt == null)
                .ToListAsync().ConfigureAwait(false);

            _context.Set<RubricCriterion>().RemoveRange(criteria);
            _context.Set<AssessmentRubric>().Remove(rubric);
            assessment.AssignRubric(null);
            _context.Set<Assessment>().Update(assessment);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Rubric deleted: {RubricId} for assessment {AssessmentId}", rubricId, assessmentId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rubric for assessment {AssessmentId}", assessmentId);
            return Result.Failure(Error.Failure("DeleteRubric", "Failed to delete rubric"));
        }
    }

    public async Task<Result> ValidateScoresAsync(Guid assessmentId, ScoreValue score, string? rubricScores)
    {
        var assessment = await FindAssessmentAsync(assessmentId).ConfigureAwait(false);
        if (assessment == null)
        {
            return Result.Failure(Error.NotFound("Assessment", "Assessment not found"));
        }

        if (assessment.RubricId is null)
        {
            return string.IsNullOrWhiteSpace(rubricScores)
                ? Result.Success()
                : Result.Failure(Error.Validation("Rubric.NotRubricGraded", "This assessment is not rubric-graded"));
        }

        if (string.IsNullOrWhiteSpace(rubricScores))
        {
            return Result.Failure(Error.Validation(
                "Rubric.ScoresRequired",
                "A rubric score is required for rubric-graded assessments"));
        }

        var criteria = await _context.Set<RubricCriterion>()
            .Where(c => c.RubricId == assessment.RubricId && c.DeletedAt == null)
            .ToListAsync().ConfigureAwait(false);
        if (criteria.Count == 0)
        {
            return Result.Failure(Error.Validation("Rubric.NoCriteria", "The rubric has no criteria."));
        }

        Dictionary<string, JsonElement> entries;
        try
        {
            using var document = JsonDocument.Parse(rubricScores);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure(Error.Validation(
                    "Rubric.ScoresMalformed",
                    "Rubric scores must be a JSON object keyed by criterion id"));
            }

            entries = document.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return Result.Failure(Error.Validation(
                "Rubric.ScoresMalformed",
                "Rubric scores must be a JSON object keyed by criterion id"));
        }

        var knownIds = criteria.Select(c => c.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in entries.Keys)
        {
            if (!knownIds.Contains(key))
            {
                return Result.Failure(Error.Validation(
                    "Rubric.UnknownCriterion",
                    $"Rubric scores contain an unknown criterion: {key}"));
            }
        }

        var awarded = new List<ScoreValue>();
        foreach (var criterion in criteria)
        {
            if (!entries.TryGetValue(criterion.Id.ToString(), out var entry))
            {
                return Result.Failure(Error.Validation(
                    "Rubric.MissingCriterion",
                    $"Rubric score is missing for criterion: {criterion.Description}"));
            }

            if (!entry.TryGetProperty("points", out var pointsElement) ||
                pointsElement.ValueKind != JsonValueKind.Number ||
                !pointsElement.TryGetInt32(out var pointUnits))
            {
                return Result.Failure(Error.Validation(
                    "Rubric.CriterionPointsInvalid",
                    $"Rubric score for criterion \"{criterion.Description}\" must include integer point units"));
            }

            ScoreValue points;
            try
            {
                points = ScoreValue.FromUnits(pointUnits);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Result.Failure(Error.Validation(
                    "Rubric.CriterionPointsInvalid",
                    $"Rubric score for criterion \"{criterion.Description}\" must be non-negative integer units."));
            }

            if (points.CompareTo(criterion.Points) > 0)
            {
                return Result.Failure(Error.Validation(
                    "Rubric.CriterionOutOfRange",
                    $"Rubric score for criterion \"{criterion.Description}\" must be between 0 and {criterion.Points}"));
            }

            awarded.Add(points);
        }

        if (ScoreValue.Sum(awarded) != score)
        {
            return Result.Failure(Error.Validation(
                "Rubric.ScoreSumMismatch",
                "Rubric scores must sum to the submitted score"));
        }

        return Result.Success();
    }

    private async Task<Assessment?> FindAssessmentAsync(Guid assessmentId)
    {
        return await _context.Set<Assessment>()
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.DeletedAt == null)
            .ConfigureAwait(false);
    }

    private async Task<bool> IsLockedAsync(Guid assessmentId)
    {
        return await _context.Set<AssessmentSubmission>()
            .AnyAsync(s => s.AssessmentId == assessmentId &&
                           s.Status == SubmissionStatus.Graded &&
                           s.DeletedAt == null)
            .ConfigureAwait(false);
    }
}
