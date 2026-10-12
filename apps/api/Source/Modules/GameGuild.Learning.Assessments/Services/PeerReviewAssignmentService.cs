using GameGuild.Identity.Users;
using GameGuild.Learning.Assessments.Grading.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    public PeerReviewAssignmentService(
        IApplicationDbContext context,
        ILogger<PeerReviewAssignmentService> logger)
    {
        _context = context;
        _logger = logger;
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

            var hasOwnIndividualSubmission = await _context.Set<AssessmentSubmission>()
                .AnyAsync(s => s.AssessmentId == assessmentId &&
                               s.UserId == actorUserId &&
                               s.DeletedAt == null &&
                               (s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.Late))
                .ConfigureAwait(false);
            var hasOwnCollectiveSubmission = await _context.Set<AssessmentSubmissionParticipant>()
                .Join(
                    _context.Set<AssessmentSubmission>(),
                    participant => participant.SubmissionId,
                    submission => submission.Id,
                    (participant, submission) => new { participant, submission })
                .AnyAsync(value => value.participant.UserId == actorUserId &&
                                   value.submission.AssessmentId == assessmentId &&
                                   value.submission.DeletedAt == null &&
                                   (value.submission.Status == SubmissionStatus.Submitted ||
                                    value.submission.Status == SubmissionStatus.Late))
                .ConfigureAwait(false);
            var hasOwnSubmission = hasOwnIndividualSubmission || hasOwnCollectiveSubmission;
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

    public Task<Result<AssessmentPeerReview>> SubmitReviewAsync(
        AssessmentPeerReview review, ScoreValue score, string feedback, string? rubricScores)
    {
        _logger.LogWarning(
            "Rejected legacy peer review submit {PeerReviewId}; peer review must run through GradingExecution",
            review.Id);
        return Task.FromResult(Result.Failure<AssessmentPeerReview>(Error.Conflict(
            "PeerReview.CanonicalRuntimeRequired",
            "Peer review submission is unavailable until the canonical grading runtime is enabled")));
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

        return await _context.Set<AssessmentPeerReview>()
            .Where(r => r.SubmissionId == submissionId &&
                        r.Status == PeerReviewStatus.Submitted &&
                        r.DeletedAt == null)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public Task<bool> IsSubmissionOwnerOrParticipantAsync(Guid submissionId, Guid userId) =>
        _context.Set<AssessmentSubmission>()
            .Where(submission => submission.Id == submissionId && submission.DeletedAt == null)
            .AnyAsync(submission =>
                submission.UserId == userId ||
                _context.Set<AssessmentSubmissionParticipant>()
                    .Any(participant => participant.SubmissionId == submission.Id && participant.UserId == userId));

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

        var actorCollectiveSubmissionIds = await _context.Set<AssessmentSubmissionParticipant>()
            .Where(participant => participant.UserId == actorUserId)
            .Select(participant => participant.SubmissionId)
            .ToListAsync().ConfigureAwait(false);

        var reviewedSubmissionIds = await _context.Set<AssessmentPeerReview>()
            .Where(r => r.AssessmentId == assessmentId && r.ReviewerUserId == actorUserId && r.DeletedAt == null)
            .Select(r => r.SubmissionId)
            .ToListAsync().ConfigureAwait(false);

        // Eligibility = latest attempt per target only. A collective attempt is represented by
        // its single persisted submission and frozen participant snapshot.
        var targets = new List<AssessmentSubmission>();
        foreach (var userRows in submissions.Where(s => s.CourseGroupId == null).GroupBy(s => s.UserId))
        {
            var latest = userRows.OrderByDescending(r => r.AttemptNumber).First();
            if (latest.Status is SubmissionStatus.Submitted or SubmissionStatus.Late)
            {
                targets.Add(latest);
            }
        }

        foreach (var groupRows in submissions.Where(s => s.CourseGroupId != null).GroupBy(s => s.CourseGroupId!.Value))
        {
            var latestAttempt = groupRows.Max(r => r.AttemptNumber);
            var attemptRows = groupRows
                .Where(r => r.AttemptNumber == latestAttempt &&
                            (r.Status == SubmissionStatus.Submitted || r.Status == SubmissionStatus.Late))
                .ToList();
            if (attemptRows.Count == 1)
            {
                targets.Add(attemptRows[0]);
            }
            else if (attemptRows.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Collective target {groupRows.Key} has multiple submissions for attempt {latestAttempt}.");
            }
        }

        var eligible = targets
            .Where(target => target.UserId != actorUserId)
            .Where(target => !actorCollectiveSubmissionIds.Contains(target.Id))
            .Where(target => !reviewedSubmissionIds.Contains(target.Id))
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

        var leastReviewedCount = eligible.Min(target => reviewCounts.GetValueOrDefault(target.Id));
        var leastReviewed = eligible
            .Where(target => reviewCounts.GetValueOrDefault(target.Id) == leastReviewedCount)
            .ToList();
        return leastReviewed[Random.Shared.Next(leastReviewed.Count)];
    }

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
