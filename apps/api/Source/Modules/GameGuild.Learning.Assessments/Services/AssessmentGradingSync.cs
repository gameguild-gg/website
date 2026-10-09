using System.Globalization;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

public sealed class AssessmentGradingSync(IApplicationDbContext context) : IAssessmentGradingSync
{
    public async Task SyncAsync(Guid contentId, int maxScore, CancellationToken ct = default)
    {
        var assessment = await context.Set<Assessment>()
            .FirstOrDefaultAsync(a => a.ContentId == contentId && a.DeletedAt == null, ct)
            .ConfigureAwait(false);

        if (assessment == null)
        {
            var content = await context.Set<ProgramContent>()
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == contentId && value.DeletedAt == null, ct)
                .ConfigureAwait(false);
            if (content is null || content.Type != ProgramContentType.Code) return;

            assessment = Assessment.Create(
                content.ProgramId,
                content.Title,
                AssessmentType.Assignment,
                ScoreValue.FromPoints(maxScore.ToString(CultureInfo.InvariantCulture)),
                content.IsRequired,
                contentId: content.Id,
                reviewMethods: ReviewMethods.AutomatedReview | ReviewMethods.InstructorReview,
                slug: content.Slug);
            assessment.TenantId = content.TenantId;
            assessment.SetDescription(content.Description);
            assessment.SetDeliveryContract(
                SubmissionModality.Code,
                AssessmentPresentationMode.SingleStep);
            assessment.SetReviewPolicy(assessment.ReviewMethods,
                "{\"schemaVersion\":1,\"instructor\":{\"requireOverrideReason\":false}}",
                assessment.AttemptContributionMode, assessment.ContentCompletionMode,
                assessment.ResultReleaseMode, assessment.ResultReleaseScheduledFor);
            context.Set<Assessment>().Add(assessment);
            await context.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        assessment.SetMaxScore(ScoreValue.FromPoints(maxScore.ToString(CultureInfo.InvariantCulture)));
        context.Set<Assessment>().Update(assessment);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
