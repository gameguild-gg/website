using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Assessments.Grading.Code;
public static class CodeRubricSnapshot
{
    public static async Task<AssessmentAuthoringProjectionV1> MaterializeAsync(IApplicationDbContext context, Assessment assessment, AssessmentAuthoringProjectionV1 projection, CancellationToken cancellationToken)
    {
        if (projection.ContentType != CodeAssessmentContracts.ContentType || !assessment.RubricId.HasValue)
        {
            return projection;
        }

        var rubric = await context.Set<AssessmentRubric>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == assessment.RubricId && value.DeletedAt == null, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The Code rubric is missing.");
        var criteria = await context.Set<RubricCriterion>().AsNoTracking().Where(value => value.RubricId == rubric.Id && value.DeletedAt == null).OrderBy(value => value.Order).ThenBy(value => value.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (criteria.Length is 0 or > 100 || ScoreValue.Sum(criteria.Select(value => value.Points)) != projection.MaxScore)
        {
            throw new InvalidOperationException("The Code rubric must contain bounded criteria totaling the frozen maximum.");
        }

        var frozen = JsonSerializer.SerializeToElement(RubricDto.From(rubric, criteria), GradingJson.Options);
        var item = projection.Items.Single();
        var privateProjection = JsonNode.Parse(item.PrivateProjection.GetRawText())!.AsObject();
        privateProjection["rubric"] = JsonNode.Parse(frozen.GetRawText());
        // Both the authoring hash and execution hash include concrete rubric
        // bytes. Changed criteria must make the prepared candidate stale.
        return projection with
        {
            Content = JsonSerializer.SerializeToElement(new { definition = projection.Content, rubric = frozen }),
            Items = [item with
            {
                PrivateProjection = JsonSerializer.SerializeToElement(privateProjection)
            }

            ],
        };
    }

    public static string? ValidateResolution(AssessmentExecutionSnapshotV1 snapshot, InstructorReviewResolutionV1 resolution)
    {
        if (snapshot.AuthoringSource.ContentType != CodeAssessmentContracts.ContentType)
        {
            if (resolution.RubricScores is not null)
            {
                throw new ArgumentException("This assessment does not accept Code rubric scores.");
            }

            return null;
        }

        var projection = snapshot.ItemProjections[CodeAssessmentContracts.ItemId];
        if (!projection.TryGetProperty("rubric", out var rubric))
        {
            if (resolution.RubricScores is not null)
            {
                throw new ArgumentException("The frozen Code revision has no rubric.");
            }

            return null;
        }

        var criteria = rubric.GetProperty("criteria").EnumerateArray().ToDictionary(value => value.GetProperty("id").GetGuid(), value => ScoreValue.FromUnits(value.GetProperty("points").GetInt32()));
        if (resolution.RubricScores is null || !criteria.Keys.ToHashSet().SetEquals(resolution.RubricScores.Keys))
        {
            throw new ArgumentException("Instructor review must score every frozen rubric criterion exactly once.");
        }

        foreach (var(id, score)in resolution.RubricScores)
        {
            if (score.CompareTo(ScoreValue.Zero) < 0 || score.CompareTo(criteria[id]) > 0)
            {
                throw new ArgumentException("A rubric score is outside its frozen criterion maximum.");
            }
        }

        if (resolution.Items.Count != 1 || resolution.Items[0].ItemId != CodeAssessmentContracts.ItemId || ScoreValue.Sum(resolution.RubricScores.Values) != resolution.Items[0].Score)
        {
            throw new ArgumentException("The rubric scores must total the resolved Code item score.");
        }

        return JsonSerializer.Serialize(resolution.RubricScores, GradingJson.Options);
    }
}
