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

        if (assessment == null) return;

        assessment.SetMaxScore(ScoreValue.FromPoints(maxScore.ToString(CultureInfo.InvariantCulture)));
        context.Set<Assessment>().Update(assessment);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
