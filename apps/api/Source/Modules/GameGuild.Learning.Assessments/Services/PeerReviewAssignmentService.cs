using GameGuild.Identity.Users;
using GameGuild.Learning.Courses;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Service implementation for anonymous least-reviewed peer review assignment.
/// </summary>
public class PeerReviewAssignmentService : IPeerReviewAssignmentService
{
    private const string RaceErrorCode = "PeerReviewClaim.Race";

    private readonly IApplicationDbContext _context;
    private readonly ILogger<PeerReviewAssignmentService> _logger;
    private readonly INotificationService? _notifications;

    public PeerReviewAssignmentService(
        IApplicationDbContext context,
        ILogger<PeerReviewAssignmentService> logger,
        INotificationService? notifications = null)
    {
        _context = context;
        _logger = logger;
        _notifications = notifications;
    }

    public async Task<Result<PeerReviewClaimResult>> ClaimAsync(Guid assessmentId, Guid actorUserId)
    {
        // The unique index (ReviewerUserId, SubmissionId) makes concurrent claims of the same row
        // surface as DbUpdateException: retry the whole algorithm once with fresh eligibility,
        // then fail with a friendly message.
        var result = await ClaimOnceAsync(assessmentId, actorUserId).ConfigureAwait(false);
        if (result.IsSuccess || result.Error.Code != RaceErrorCode)
        {
            return result;
        }

        result = await ClaimOnceAsync(assessmentId, actorUserId).ConfigureAwait(false);
        return result.IsSuccess || result.Error.Code != RaceErrorCode
            ? result
            : Result.Failure<PeerReviewClaimResult>(Error.Failure(
                "PeerReviewClaim.RetryExhausted",
                "Could not assign a peer review, try again"));
    }

    private async Task<Result<PeerReviewClaimResult>> ClaimOnceAsync(Guid assessmentId, Guid actorUserId)
    {
        try
        {
            var assessment = await _context.Set<Assessment>()
                .FirstOrDefaultAsync(a => a.Id == assessmentId && a.DeletedAt == null)
                .ConfigureAwait(false);
            if (assessment == null)
            {
                return Result.Failure<PeerReviewClaimResult>(Error.NotFound("Assessment", "Assessment not found"));
            }

            if (!assessment.ReviewMethods.HasFlag(ReviewMethods.PeerReview))
            {
                return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                    "PeerReview.NotEnabled",
                    "Peer review is not enabled for this assessment"));
            }

            var hasOwnSubmission = await _context.Set<AssessmentSubmission>()
                .AnyAsync(s => s.AssessmentId == assessmentId &&
                               s.UserId == actorUserId &&
                               s.DeletedAt == null &&
                               (s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.Late))
                .ConfigureAwait(false);
            if (!hasOwnSubmission)
            {
                return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                    "PeerReview.OwnSubmissionRequired",
                    "Submit your own work before reviewing peers"));
            }

            var assignedCount = await _context.Set<AssessmentPeerReview>()
                .CountAsync(r => r.AssessmentId == assessmentId &&
                                 r.ReviewerUserId == actorUserId &&
                                 r.DeletedAt == null)
                .ConfigureAwait(false);
            if (assignedCount >= assessment.GetRequiredPeerReviewCount())
            {
                return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                    "PeerReview.QuotaReached",
                    "Review quota reached"));
            }

            var chosen = await SelectLeastReviewedTargetAsync(assessmentId, actorUserId).ConfigureAwait(false);
            if (chosen == null)
            {
                return Result.Failure<PeerReviewClaimResult>(Error.Failure(
                    "PeerReview.NoEligibleTargets",
                    "No peer submissions are available to review right now"));
            }

            var review = AssessmentPeerReview.Create(assessmentId, chosen.Id, actorUserId);
            await SaveClaimAsync(review).ConfigureAwait(false);

            _logger.LogInformation(
                "Peer review {PeerReviewId} claimed on assessment {AssessmentId}",
                review.Id,
                assessmentId);
            return Result.Success(new PeerReviewClaimResult(
                review.Id,
                $"Anonymous submission · attempt {chosen.AttemptNumber}"));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Peer review claim race on assessment {AssessmentId}", assessmentId);
            return Result.Failure<PeerReviewClaimResult>(Error.Failure(RaceErrorCode, "Claim race"));
        }
    }

    public async Task<AssessmentPeerReview?> GetReviewAsync(Guid reviewId)
    {
        return await _context.Set<AssessmentPeerReview>()
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.DeletedAt == null)
            .ConfigureAwait(false);
    }

    public async Task<Result<AssessmentPeerReview>> SubmitReviewAsync(
        AssessmentPeerReview review, ScoreValue score, string feedback, string? rubricScores)
    {
        try
        {
            review.SubmitReview(score, feedback, rubricScores);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            if (_notifications is not null)
            {
                try
                {
                    await NotifyReviewTargetOwnersAsync(review).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Peer feedback notification failed for review {ReviewId}", review.Id);
                }
            }

            return Result.Success(review);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<AssessmentPeerReview>(Error.Conflict(
                "PeerReview.AlreadySubmitted", "Peer review already submitted"));
        }
    }

    /// <summary>
    ///     Every owner of a row sharing the reviewed (CourseGroupId, AttemptNumber) sees the review
    ///     through the todo-8 union read — so they all get notified. Content is anonymous by
    ///     construction: the reviewer's identity is never part of the payload.
    /// </summary>
    private async Task NotifyReviewTargetOwnersAsync(AssessmentPeerReview review)
    {
        var submission = await _context.Set<AssessmentSubmission>()
            .FirstOrDefaultAsync(s => s.Id == review.SubmissionId && s.DeletedAt == null)
            .ConfigureAwait(false);
        if (submission == null)
        {
            return;
        }

        var ownerIds = submission.CourseGroupId is { } groupId
            ? await _context.Set<AssessmentSubmission>()
                .Where(s => s.CourseGroupId == groupId &&
                            s.AttemptNumber == submission.AttemptNumber &&
                            s.DeletedAt == null)
                .Select(s => s.UserId)
                .ToListAsync().ConfigureAwait(false)
            : [submission.UserId];

        var title = await _context.Set<Assessment>()
            .Where(a => a.Id == review.AssessmentId && a.DeletedAt == null)
            .Select(a => a.Title)
            .FirstOrDefaultAsync().ConfigureAwait(false) ?? "your assessment";

        foreach (var owner in ownerIds.Distinct())
        {
            await _notifications!.SendAsync(
                    owner,
                    NotificationType.System,
                    "Peer feedback received",
                    $"You received peer feedback on {title}",
                    NotificationChannel.InApp,
                    actionUrl: "/dashboard/tasks")
                .ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<AssessmentPeerReview>> GetReviewsForSubmissionAsync(Guid submissionId)
    {
        var submission = await _context.Set<AssessmentSubmission>()
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.DeletedAt == null)
            .ConfigureAwait(false);
        if (submission == null)
        {
            return [];
        }

        // Group members see the union of reviews across the group's rows for that attempt;
        // individual submissions only ever see their own row.
        var submissionIds = submission.CourseGroupId is { } groupId
            ? await _context.Set<AssessmentSubmission>()
                .Where(s => s.CourseGroupId == groupId &&
                            s.AttemptNumber == submission.AttemptNumber &&
                            s.DeletedAt == null)
                .Select(s => s.Id)
                .ToListAsync().ConfigureAwait(false)
            : [submissionId];

        return await _context.Set<AssessmentPeerReview>()
            .Where(r => submissionIds.Contains(r.SubmissionId) &&
                        r.Status == PeerReviewStatus.Submitted &&
                        r.DeletedAt == null)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetReviewerDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds)
    {
        var users = await _context.Set<User>()
            .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
            .ToListAsync().ConfigureAwait(false);
        return users
            .Where(u => !string.IsNullOrWhiteSpace(u.Name))
            .ToDictionary(u => u.Id, u => u.Name);
    }

    private async Task<AssessmentSubmission?> SelectLeastReviewedTargetAsync(Guid assessmentId, Guid actorUserId)
    {
        var submissions = await _context.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == assessmentId && s.DeletedAt == null)
            .ToListAsync().ConfigureAwait(false);

        var actorGroupIds = await _context.Set<CourseGroupMember>()
            .Where(m => m.UserId == actorUserId && m.DeletedAt == null)
            .Select(m => m.GroupId)
            .ToListAsync().ConfigureAwait(false);

        var reviewedSubmissionIds = await _context.Set<AssessmentPeerReview>()
            .Where(r => r.AssessmentId == assessmentId && r.ReviewerUserId == actorUserId && r.DeletedAt == null)
            .Select(r => r.SubmissionId)
            .ToListAsync().ConfigureAwait(false);

        // Eligibility = latest attempt per target only. Individual: max attempt row per user;
        // group: max attempt among the group's rows, one canonical row per group-attempt.
        var targets = new List<(AssessmentSubmission Canonical, HashSet<Guid> AttemptRowIds)>();
        foreach (var userRows in submissions.Where(s => s.CourseGroupId == null).GroupBy(s => s.UserId))
        {
            var latest = userRows.OrderByDescending(r => r.AttemptNumber).First();
            if (latest.Status is SubmissionStatus.Submitted or SubmissionStatus.Late)
            {
                targets.Add((latest, [latest.Id]));
            }
        }

        foreach (var groupRows in submissions.Where(s => s.CourseGroupId != null).GroupBy(s => s.CourseGroupId!.Value))
        {
            var latestAttempt = groupRows.Max(r => r.AttemptNumber);
            var attemptRows = groupRows
                .Where(r => r.AttemptNumber == latestAttempt &&
                            (r.Status == SubmissionStatus.Submitted || r.Status == SubmissionStatus.Late))
                .ToList();
            if (attemptRows.Count > 0)
            {
                targets.Add((CanonicalRow(attemptRows), attemptRows.Select(r => r.Id).ToHashSet()));
            }
        }

        var eligible = targets
            .Where(t => t.Canonical.UserId != actorUserId)
            .Where(t => t.Canonical.CourseGroupId == null ||
                        !actorGroupIds.Contains(t.Canonical.CourseGroupId.Value))
            .Where(t => !t.AttemptRowIds.Overlaps(reviewedSubmissionIds))
            .ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        var reviewCounts = await _context.Set<AssessmentPeerReview>()
            .Where(r => r.AssessmentId == assessmentId && r.DeletedAt == null)
            .GroupBy(r => r.SubmissionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count)
            .ConfigureAwait(false);

        var leastReviewedCount = eligible.Min(t => reviewCounts.GetValueOrDefault(t.Canonical.Id));
        var leastReviewed = eligible
            .Where(t => reviewCounts.GetValueOrDefault(t.Canonical.Id) == leastReviewedCount)
            .ToList();
        return leastReviewed[Random.Shared.Next(leastReviewed.Count)].Canonical;
    }

    /// <summary>
    /// Canonical row rule shared with the grading queue (todo 9): among rows sharing a group+attempt,
    /// Min(Id) is the deterministic representative (clones share timestamps, so Id is the only tiebreak).
    /// </summary>
    internal static AssessmentSubmission CanonicalRow(IEnumerable<AssessmentSubmission> rows) =>
        rows.OrderBy(r => r.Id).First();

    /// <summary>
    /// Seam for the race-retry tests: EF InMemory never throws unique-index violations, tests
    /// override this to simulate one. Detaches the failed insert so a retry saves clean state.
    /// </summary>
    internal virtual async Task SaveClaimAsync(AssessmentPeerReview review)
    {
        _context.Set<AssessmentPeerReview>().Add(review);
        try
        {
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch
        {
            // Remove on an Added-state entity detaches it: drop the failed insert so a retry saves clean state.
            _context.Set<AssessmentPeerReview>().Remove(review);
            throw;
        }
    }
}
