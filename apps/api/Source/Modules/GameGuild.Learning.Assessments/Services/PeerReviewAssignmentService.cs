using System.Text.Json;
using GameGuild.Identity.Users;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Assigns canonical peer-review stage leases and dispatches submitted evidence to the
/// shared grading lifecycle. AssessmentPeerReview is intentionally not mutated here.
/// </summary>
public sealed class PeerReviewAssignmentService(
    IApplicationDbContext context,
    IGradingExecutionOrchestrator orchestrator,
    IAcademicOutboxWriter outbox,
    ILogger<PeerReviewAssignmentService> logger) : IPeerReviewAssignmentService
{
    private const string ClaimRaceCode = "PeerReviewClaim.Race";

    public async Task<Result<PeerReviewClaimResult>> ClaimAsync(Guid assessmentId, Guid actorUserId)
    {
        var first = await ClaimOfficialOnceAsync(assessmentId, actorUserId).ConfigureAwait(false);
        if (first.IsSuccess || first.Error.Code != ClaimRaceCode)
        {
            return first;
        }

        var retry = await ClaimOfficialOnceAsync(assessmentId, actorUserId).ConfigureAwait(false);
        return retry.IsSuccess || retry.Error.Code != ClaimRaceCode
            ? retry
            : Result.Failure<PeerReviewClaimResult>(Error.Failure(
                "PeerReviewClaim.RetryExhausted",
                "Could not assign a peer review, try again"));
    }

    public async Task<Result<PeerReviewClaimResult>> ClaimAuthorTestAsync(
        Guid testRunId,
        Guid reviewerTestRunSubjectId,
        Guid actorUserId)
    {
        if (testRunId == Guid.Empty || reviewerTestRunSubjectId == Guid.Empty || actorUserId == Guid.Empty)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                "PeerReview.AuthorTestIdentity",
                "Test run, synthetic reviewer, and authenticated actor are required"));
        }

        var run = await context.Set<AssessmentTestRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == testRunId)
            .ConfigureAwait(false);
        if (run is null || run.CreatedByUserId != actorUserId || run.Status != AssessmentTestRunStatus.Running)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.NotFound("AssessmentTestRun", "Running test run not found"));
        }

        var reviewer = await context.Set<AssessmentTestRunSubject>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == reviewerTestRunSubjectId && value.TestRunId == testRunId)
            .ConfigureAwait(false);
        if (reviewer is null)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                "PeerReview.AuthorTestSubject",
                "Synthetic reviewer does not belong to this test run"));
        }

        var reviewerRevision = await (
                from execution in context.Set<GradingExecution>().AsNoTracking()
                join revision in context.Set<AssessmentDefinitionRevision>().AsNoTracking()
                    on execution.DefinitionRevisionId equals revision.Id
                where execution.TestRunSubjectId == reviewerTestRunSubjectId &&
                      execution.ExecutionContext == ReviewExecutionContext.AuthorTest &&
                      execution.SubmittedAt.HasValue
                select revision)
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);
        if (reviewerRevision is null)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                "PeerReview.AuthorTestResponseRequired",
                "The synthetic reviewer must submit its own response before reviewing peers"));
        }

        var targets = await LoadAuthorTestTargetsAsync(testRunId).ConfigureAwait(false);
        var runStageIds = await LoadAuthorTestPeerStageIdsAsync(testRunId).ConfigureAwait(false);
        var now = SystemClock.UtcNow;
        await ExpireClaimsAsync(runStageIds, now).ConfigureAwait(false);
        var active = await context.Set<PeerReviewClaim>()
            .Where(value => value.ReviewerTestRunSubjectId == reviewerTestRunSubjectId &&
                            runStageIds.Contains(value.ReviewStageId) &&
                            value.Status == PeerReviewClaimStatus.Assigned &&
                            value.LeaseExpiresAt > now)
            .OrderBy(value => value.ClaimedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (active is not null)
        {
            return Result.Success(new PeerReviewClaimResult(active.Id, "Anonymous test subject"));
        }

        var policy = ReadPeerPolicy(reviewerRevision);
        var used = await context.Set<PeerReviewClaim>()
            .CountAsync(value => value.ReviewerTestRunSubjectId == reviewerTestRunSubjectId &&
                                 runStageIds.Contains(value.ReviewStageId) &&
                                 (value.Status == PeerReviewClaimStatus.Submitted ||
                                  value.Status == PeerReviewClaimStatus.Assigned && value.LeaseExpiresAt > now))
            .ConfigureAwait(false);
        if (used >= policy.ReviewsPerReviewer)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                "PeerReview.QuotaReached",
                "Review quota reached"));
        }

        var eligible = await FilterAvailableTargetsAsync(
                targets.Where(value => value.Execution.TestRunSubjectId != reviewerTestRunSubjectId).ToArray(),
                policy,
                now)
            .ConfigureAwait(false);
        if (eligible.Count == 0)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Failure(
                "PeerReview.NoEligibleTargets",
                "No peer test subjects are available to review right now"));
        }

        var chosen = eligible[Random.Shared.Next(eligible.Count)];
        var leaseEnd = ResolveLeaseEnd(chosen.Stage, policy, now);
        var claim = PeerReviewClaim.CreateAuthorTest(
            chosen.Stage.TenantId,
            chosen.Stage.Id,
            reviewerTestRunSubjectId,
            leaseEnd);
        context.Set<PeerReviewClaim>().Add(claim);
        try
        {
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Author-test peer claim race on run {TestRunId}", testRunId);
            return Result.Failure<PeerReviewClaimResult>(Error.Failure(ClaimRaceCode, "Claim race"));
        }

        return Result.Success(new PeerReviewClaimResult(claim.Id, "Anonymous test subject"));
    }

    public async Task<PeerReviewClaimView?> GetReviewAsync(Guid reviewId)
    {
        var owned = await LoadClaimAsync(reviewId).ConfigureAwait(false);
        if (owned is null)
        {
            return null;
        }

        if (owned.Claim.Status == PeerReviewClaimStatus.Assigned && owned.Claim.LeaseExpiresAt <= SystemClock.UtcNow)
        {
            owned.Claim.Expire(SystemClock.UtcNow);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        return ToView(owned);
    }

    public async Task<Result<PeerReviewClaimView>> SubmitReviewAsync(
        Guid reviewId,
        Guid actorUserId,
        ScoreValue score,
        string feedback,
        string? rubricScores)
    {
        if (reviewId == Guid.Empty || actorUserId == Guid.Empty)
        {
            return Result.Failure<PeerReviewClaimView>(Error.Validation(
                "PeerReview.Identity",
                "Review and actor IDs are required"));
        }

        var executionId = await GetExecutionIdForClaimAsync(reviewId).ConfigureAwait(false);
        if (!executionId.HasValue)
        {
            return Result.Failure<PeerReviewClaimView>(Error.NotFound("PeerReviewClaim", "Peer review not found"));
        }

        await using var transaction = await GradingRuntimeDatabaseLock.AcquireExecutionAsync(
            context,
            executionId.Value).ConfigureAwait(false);
        var owned = await LoadClaimAsync(reviewId).ConfigureAwait(false);
        if (owned is null)
        {
            return Result.Failure<PeerReviewClaimView>(Error.NotFound("PeerReviewClaim", "Peer review not found"));
        }

        if (owned.Execution.ExecutionContext == ReviewExecutionContext.OfficialSubmission &&
            owned.Claim.ReviewerUserId != actorUserId)
        {
            return Result.Failure<PeerReviewClaimView>(Error.Forbidden("PeerReview.Reviewer", "Only the assigned reviewer can submit"));
        }

        if (owned.Execution.ExecutionContext == ReviewExecutionContext.AuthorTest)
        {
            var reviewerSubjectId = owned.Claim.ReviewerTestRunSubjectId
                ?? throw new InvalidOperationException("Author-test claim is missing its synthetic reviewer.");
            var testRunId = await context.Set<AssessmentTestRunSubject>()
                .Where(value => value.Id == reviewerSubjectId)
                .Select(value => value.TestRunId)
                .SingleAsync()
                .ConfigureAwait(false);
            var ownsTestRun = await context.Set<AssessmentTestRun>()
                .AnyAsync(value => value.Id == testRunId && value.CreatedByUserId == actorUserId)
                .ConfigureAwait(false);
            if (!ownsTestRun)
            {
                return Result.Failure<PeerReviewClaimView>(Error.Forbidden(
                    "PeerReview.AuthorTestOwner",
                    "Only the instructor who started the test run can submit synthetic peer evidence"));
            }
        }

        if (owned.Claim.Status == PeerReviewClaimStatus.Submitted)
        {
            await GradingRuntimeDatabaseLock.CommitAsync(transaction).ConfigureAwait(false);
            return Result.Success(ToView(owned));
        }

        var now = SystemClock.UtcNow;
        if (owned.Claim.Status != PeerReviewClaimStatus.Assigned || owned.Claim.LeaseExpiresAt <= now)
        {
            if (owned.Claim.Status == PeerReviewClaimStatus.Assigned)
            {
                owned.Claim.Expire(now);
                await context.SaveChangesAsync().ConfigureAwait(false);
            }

            return Result.Failure<PeerReviewClaimView>(Error.Conflict(
                "PeerReview.LeaseExpired",
                "The peer-review lease expired"));
        }

        var maximum = ReadAssessmentMaximum(owned.Revision);
        if (score.CompareTo(ScoreValue.Zero) < 0 || score.CompareTo(maximum) > 0 || string.IsNullOrWhiteSpace(feedback))
        {
            return Result.Failure<PeerReviewClaimView>(Error.Validation(
                "PeerReview.InvalidEvidence",
                "Score must be within the assessment maximum and feedback is required"));
        }

        var evidencePayload = new PeerReviewEvidenceV1(
            1,
            owned.Claim.Id,
            score,
            feedback.Trim(),
            string.IsNullOrWhiteSpace(rubricScores) ? null : rubricScores,
            owned.Claim.ReviewerUserId,
            owned.Claim.ReviewerTestRunSubjectId);
        var evidence = new ReviewStageEvidenceV1(
            $"peer-claim:{owned.Claim.Id:N}",
            "peer-review-evidence",
            "1",
            JsonSerializer.SerializeToElement(evidencePayload, GradingJson.Options),
            ProducedByActorId: owned.Claim.ReviewerUserId,
            ProducedByService: owned.Claim.ReviewerTestRunSubjectId.HasValue
                ? $"author-test-subject:{owned.Claim.ReviewerTestRunSubjectId:N}"
                : null);
        await orchestrator.AcceptReviewEvidenceAsync(
                owned.Execution.Id,
                ReviewMethod.PeerReview,
                evidence)
            .ConfigureAwait(false);
        var storedEvidence = await context.Set<ReviewEvidence>()
            .SingleAsync(value => value.ReviewStageId == owned.Stage.Id && value.EvidenceKey == evidence.EvidenceKey)
            .ConfigureAwait(false);
        owned.Claim.Submit(storedEvidence.Id, now);
        if (owned.Execution.TenantId.HasValue)
        {
            Enqueue(owned.Execution.TenantId.Value, "peer-review-submitted", new
            {
                schemaVersion = 1,
                claimId = owned.Claim.Id,
                reviewStageId = owned.Stage.Id,
                gradingExecutionId = owned.Execution.Id,
                reviewEvidenceId = storedEvidence.Id,
                submittedAt = now,
            });
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction).ConfigureAwait(false);
        return Result.Success(ToView(owned));
    }

    public async Task<IReadOnlyList<PeerReviewEvidenceView>> GetReviewsForSubmissionAsync(Guid submissionId)
    {
        var rows = await (
                from execution in context.Set<GradingExecution>().AsNoTracking()
                join round in context.Set<GradeRound>().AsNoTracking() on execution.Id equals round.GradingExecutionId
                join stage in context.Set<ReviewStage>().AsNoTracking() on round.Id equals stage.GradeRoundId
                join claim in context.Set<PeerReviewClaim>().AsNoTracking() on stage.Id equals claim.ReviewStageId
                join evidence in context.Set<ReviewEvidence>().AsNoTracking() on claim.ReviewEvidenceId equals evidence.Id
                where execution.AssessmentSubmissionId == submissionId &&
                      claim.Status == PeerReviewClaimStatus.Submitted &&
                      claim.ReviewerUserId.HasValue
                orderby claim.SubmittedAt
                select new { claim, evidence })
            .ToArrayAsync()
            .ConfigureAwait(false);
        return rows.Select(value =>
        {
            var payload = JsonSerializer.Deserialize<PeerReviewEvidenceV1>(value.evidence.CanonicalJson, GradingJson.Options)
                ?? throw new JsonException("Persisted peer evidence is invalid.");
            return new PeerReviewEvidenceView(
                value.claim.Id,
                value.evidence.Id,
                value.claim.ReviewerUserId!.Value,
                payload.Score,
                payload.Feedback,
                payload.RubricScores,
                value.claim.SubmittedAt!.Value);
        }).ToArray();
    }

    public Task<bool> IsSubmissionOwnerOrParticipantAsync(Guid submissionId, Guid userId) =>
        context.Set<AssessmentSubmission>()
            .Where(submission => submission.Id == submissionId && submission.DeletedAt == null)
            .AnyAsync(submission =>
                submission.UserId == userId ||
                context.Set<AssessmentSubmissionParticipant>()
                    .Any(participant => participant.SubmissionId == submission.Id && participant.UserId == userId));

    public async Task<IReadOnlyDictionary<Guid, string>> GetReviewerDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds)
    {
        var users = await context.Set<User>()
            .Where(user => userIds.Contains(user.Id) && user.DeletedAt == null)
            .ToListAsync()
            .ConfigureAwait(false);
        return users
            .Where(user => !string.IsNullOrWhiteSpace(user.Name))
            .ToDictionary(user => user.Id, user => user.Name);
    }

    public Task<Guid?> GetAssessmentIdForStageAsync(
        Guid reviewStageId,
        CancellationToken cancellationToken = default) =>
        (
            from stage in context.Set<ReviewStage>().AsNoTracking()
            join round in context.Set<GradeRound>().AsNoTracking() on stage.GradeRoundId equals round.Id
            join execution in context.Set<GradingExecution>().AsNoTracking() on round.GradingExecutionId equals execution.Id
            join revision in context.Set<AssessmentDefinitionRevision>().AsNoTracking() on execution.DefinitionRevisionId equals revision.Id
            where stage.Id == reviewStageId && stage.ReviewMethod == ReviewMethod.PeerReview
            select (Guid?)revision.AssessmentId)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task ExtendEvidenceWindowAsync(
        Guid reviewStageId,
        Guid actorUserId,
        int expectedStageVersion,
        DateTime deadlineAt,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Actor and reason are required.");
        }

        var executionId = await GetExecutionIdForStageAsync(reviewStageId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Peer-review stage was not found.");
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireExecutionAsync(
            context,
            executionId,
            cancellationToken).ConfigureAwait(false);
        var stage = await context.Set<ReviewStage>()
            .SingleOrDefaultAsync(value => value.Id == reviewStageId && value.ReviewMethod == ReviewMethod.PeerReview, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Peer-review stage was not found.");
        var round = await context.Set<GradeRound>()
            .SingleAsync(value => value.Id == stage.GradeRoundId, cancellationToken)
            .ConfigureAwait(false);
        var execution = await context.Set<GradingExecution>()
            .SingleAsync(value => value.Id == round.GradingExecutionId, cancellationToken)
            .ConfigureAwait(false);
        var tenantId = execution.TenantId
            ?? throw new InvalidOperationException("Peer-review operations require a tenant-scoped execution.");
        var normalizedDeadline = deadlineAt.ToUniversalTime();
        var normalizedReason = reason.Trim();
        var key = RequireIdempotencyKey(idempotencyKey);
        var requestHash = CanonicalJson.Sha256(JsonSerializer.SerializeToElement(new
        {
            schemaVersion = 1,
            reviewStageId,
            expectedStageVersion,
            deadlineAt = normalizedDeadline,
            reason = normalizedReason,
        }, GradingJson.Options));
        if (await IsOperationReplayAsync(
                tenantId,
                reviewStageId,
                "extend-peer-review-window",
                actorUserId,
                key,
                requestHash,
                cancellationToken).ConfigureAwait(false))
        {
            await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (stage.Version != expectedStageVersion)
        {
            throw new DbUpdateConcurrencyException("Peer-review stage version is stale.");
        }

        if (stage.EvidenceDeadlineAt.HasValue && normalizedDeadline <= stage.EvidenceDeadlineAt.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(deadlineAt), "The new evidence deadline must extend the current window.");
        }

        stage.SetEvidenceDeadline(normalizedDeadline);
        stage.AwaitEvidence();
        round.AwaitEvidence();
        execution.AwaitReview();
        foreach (var claim in await context.Set<PeerReviewClaim>()
                     .Where(value => value.ReviewStageId == stage.Id && value.Status == PeerReviewClaimStatus.Assigned)
                     .ToArrayAsync(cancellationToken)
                     .ConfigureAwait(false))
        {
            if (claim.LeaseExpiresAt < normalizedDeadline)
            {
                claim.ExtendLease(normalizedDeadline);
            }
        }

        if (execution.TenantId.HasValue)
        {
            Enqueue(execution.TenantId.Value, "peer-review-window-extended", new
            {
                schemaVersion = 1,
                reviewStageId,
                actorUserId,
                deadlineAt = normalizedDeadline,
                reason = normalizedReason,
            });
        }

        AddOperationReceipt(
            tenantId,
            reviewStageId,
            "extend-peer-review-window",
            actorUserId,
            key,
            requestHash);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task ReassignClaimAsync(
        Guid reviewId,
        Guid actorUserId,
        int expectedClaimVersion,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Actor and reason are required.");
        }

        var executionId = await GetExecutionIdForClaimAsync(reviewId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Peer-review claim was not found.");
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireExecutionAsync(
            context,
            executionId,
            cancellationToken).ConfigureAwait(false);
        var owned = await LoadClaimAsync(reviewId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Peer-review claim was not found.");
        var tenantId = owned.Execution.TenantId
            ?? throw new InvalidOperationException("Peer-review operations require a tenant-scoped execution.");
        var normalizedReason = reason.Trim();
        var key = RequireIdempotencyKey(idempotencyKey);
        var requestHash = CanonicalJson.Sha256(JsonSerializer.SerializeToElement(new
        {
            schemaVersion = 1,
            reviewId,
            expectedClaimVersion,
            reason = normalizedReason,
        }, GradingJson.Options));
        if (await IsOperationReplayAsync(
                tenantId,
                reviewId,
                "reassign-peer-review-claim",
                actorUserId,
                key,
                requestHash,
                cancellationToken).ConfigureAwait(false))
        {
            await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (owned.Claim.Version != expectedClaimVersion)
        {
            throw new DbUpdateConcurrencyException("Peer-review claim version is stale.");
        }

        owned.Claim.Cancel();
        if (owned.Execution.TenantId.HasValue)
        {
            Enqueue(owned.Execution.TenantId.Value, "peer-review-claim-reassigned", new
            {
                schemaVersion = 1,
                claimId = reviewId,
                reviewStageId = owned.Stage.Id,
                actorUserId,
                reason = normalizedReason,
            });
        }

        AddOperationReceipt(
            tenantId,
            reviewId,
            "reassign-peer-review-claim",
            actorUserId,
            key,
            requestHash);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task ResolveInsufficientEvidenceAsync(
        Guid reviewStageId,
        Guid actorUserId,
        PeerReviewInsufficiencyResolutionV1 resolution,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty || resolution.SchemaVersion != 1 ||
            resolution.Items is null || string.IsNullOrWhiteSpace(resolution.Reason))
        {
            throw new ArgumentException("Actor and a valid peer-review resolution are required.");
        }

        var key = RequireIdempotencyKey(idempotencyKey);
        var executionId = await GetExecutionIdForStageAsync(reviewStageId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Peer-review stage was not found.");
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireExecutionAsync(
            context,
            executionId,
            cancellationToken).ConfigureAwait(false);
        var stage = await context.Set<ReviewStage>()
            .SingleOrDefaultAsync(value => value.Id == reviewStageId && value.ReviewMethod == ReviewMethod.PeerReview, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Peer-review stage was not found.");
        if (stage.Status is not (PersistedReviewStageStatus.AwaitingInstructorResolution or
            PersistedReviewStageStatus.Completed))
        {
            throw new InvalidOperationException("Peer-review stage is not awaiting instructor resolution.");
        }

        var round = await context.Set<GradeRound>()
            .SingleAsync(value => value.Id == stage.GradeRoundId, cancellationToken)
            .ConfigureAwait(false);
        var execution = await context.Set<GradingExecution>()
            .SingleAsync(value => value.Id == round.GradingExecutionId, cancellationToken)
            .ConfigureAwait(false);
        var tenantId = execution.TenantId
            ?? throw new InvalidOperationException("Peer-review operations require a tenant-scoped execution.");
        var requestHash = CanonicalJson.Sha256(JsonSerializer.SerializeToElement(new
        {
            schemaVersion = 1,
            reviewStageId,
            resolution,
        }, GradingJson.Options));
        if (await IsOperationReplayAsync(
                tenantId,
                reviewStageId,
                "resolve-peer-review-insufficiency",
                actorUserId,
                key,
                requestHash,
                cancellationToken).ConfigureAwait(false))
        {
            await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return;
        }

        var keyHash = CanonicalJson.Sha256(JsonSerializer.SerializeToElement(key, GradingJson.Options));
        await orchestrator.AcceptReviewEvidenceAsync(
                execution.Id,
                ReviewMethod.PeerReview,
                new ReviewStageEvidenceV1(
                    $"peer-insufficiency:{actorUserId:N}:{keyHash[..32]}",
                    "peer-review-insufficiency-resolution",
                    "1",
                    JsonSerializer.SerializeToElement(resolution, GradingJson.Options),
                    ProducedByActorId: actorUserId),
                cancellationToken)
            .ConfigureAwait(false);
        AddOperationReceipt(
            tenantId,
            reviewStageId,
            "resolve-peer-review-insufficiency",
            actorUserId,
            key,
            requestHash);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<PeerReviewClaimResult>> ClaimOfficialOnceAsync(Guid assessmentId, Guid actorUserId)
    {
        if (assessmentId == Guid.Empty || actorUserId == Guid.Empty)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                "PeerReview.Identity",
                "Assessment and actor IDs are required"));
        }

        var assessment = await context.Set<Assessment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == assessmentId && value.DeletedAt == null)
            .ConfigureAwait(false);
        if (assessment is null)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.NotFound("Assessment", "Assessment not found"));
        }

        if (!assessment.ReviewMethods.HasFlag(ReviewMethods.PeerReview))
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                "PeerReview.NotEnabled",
                "Peer review is not enabled for this assessment"));
        }

        var ownsIndividual = await context.Set<AssessmentSubmission>()
            .AnyAsync(value => value.AssessmentId == assessmentId &&
                               value.UserId == actorUserId &&
                               value.SubmittedAt.HasValue &&
                               value.DeletedAt == null)
            .ConfigureAwait(false);
        var ownsCollective = await context.Set<AssessmentSubmissionParticipant>()
            .Join(
                context.Set<AssessmentSubmission>(),
                participant => participant.SubmissionId,
                submission => submission.Id,
                (participant, submission) => new { participant, submission })
            .AnyAsync(value => value.participant.UserId == actorUserId &&
                               value.submission.AssessmentId == assessmentId &&
                               value.submission.SubmittedAt.HasValue &&
                               value.submission.DeletedAt == null)
            .ConfigureAwait(false);
        if (!ownsIndividual && !ownsCollective)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                "PeerReview.OwnSubmissionRequired",
                "Submit your own work before reviewing peers"));
        }

        var targets = await LoadOfficialTargetsAsync(assessmentId).ConfigureAwait(false);
        if (targets.Count == 0)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Failure(
                "PeerReview.NoEligibleTargets",
                "No peer submissions are available to review right now"));
        }

        var policy = ReadPeerPolicy(targets[0].Revision);
        var now = SystemClock.UtcNow;
        var targetStageIds = targets.Select(value => value.Stage.Id).ToArray();
        await ExpireClaimsAsync(targetStageIds, now).ConfigureAwait(false);
        var active = await context.Set<PeerReviewClaim>()
            .Where(value => targetStageIds.Contains(value.ReviewStageId) &&
                            value.ReviewerUserId == actorUserId &&
                            value.Status == PeerReviewClaimStatus.Assigned &&
                            value.LeaseExpiresAt > now)
            .OrderBy(value => value.ClaimedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (active is not null)
        {
            var target = targets.Single(value => value.Stage.Id == active.ReviewStageId);
            return Result.Success(new PeerReviewClaimResult(
                active.Id,
                $"Anonymous submission · attempt {target.Submission!.AttemptNumber}"));
        }

        var used = await context.Set<PeerReviewClaim>()
            .CountAsync(value => targetStageIds.Contains(value.ReviewStageId) &&
                                 value.ReviewerUserId == actorUserId &&
                                 (value.Status == PeerReviewClaimStatus.Submitted ||
                                  value.Status == PeerReviewClaimStatus.Assigned && value.LeaseExpiresAt > now))
            .ConfigureAwait(false);
        if (used >= policy.ReviewsPerReviewer)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Validation(
                "PeerReview.QuotaReached",
                "Review quota reached"));
        }

        var participantSubmissionIds = await context.Set<AssessmentSubmissionParticipant>()
            .Where(value => value.UserId == actorUserId)
            .Select(value => value.SubmissionId)
            .ToArrayAsync()
            .ConfigureAwait(false);
        var eligible = await FilterAvailableTargetsAsync(
                targets.Where(value => value.Submission!.UserId != actorUserId &&
                                       !participantSubmissionIds.Contains(value.Submission.Id)).ToArray(),
                policy,
                now,
                actorUserId)
            .ConfigureAwait(false);
        if (eligible.Count == 0)
        {
            return Result.Failure<PeerReviewClaimResult>(Error.Failure(
                "PeerReview.NoEligibleTargets",
                "No peer submissions are available to review right now"));
        }

        var chosen = eligible[Random.Shared.Next(eligible.Count)];
        var claim = PeerReviewClaim.CreateOfficial(
            chosen.Stage.TenantId,
            chosen.Stage.Id,
            actorUserId,
            ResolveLeaseEnd(chosen.Stage, policy, now));
        context.Set<PeerReviewClaim>().Add(claim);
        try
        {
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Peer-review claim race on assessment {AssessmentId}", assessmentId);
            return Result.Failure<PeerReviewClaimResult>(Error.Failure(ClaimRaceCode, "Claim race"));
        }

        return Result.Success(new PeerReviewClaimResult(
            claim.Id,
            $"Anonymous submission · attempt {chosen.Submission!.AttemptNumber}"));
    }

    private async Task<List<PeerTarget>> LoadOfficialTargetsAsync(Guid assessmentId) =>
        await (
                from submission in context.Set<AssessmentSubmission>().AsNoTracking()
                join execution in context.Set<GradingExecution>().AsNoTracking()
                    on submission.Id equals execution.AssessmentSubmissionId
                join round in context.Set<GradeRound>().AsNoTracking()
                    on execution.ActiveGradeRoundId equals (Guid?)round.Id
                join stage in context.Set<ReviewStage>().AsNoTracking()
                    on round.Id equals stage.GradeRoundId
                join revision in context.Set<AssessmentDefinitionRevision>().AsNoTracking()
                    on execution.DefinitionRevisionId equals revision.Id
                where submission.AssessmentId == assessmentId &&
                      submission.SubmittedAt.HasValue &&
                      submission.DeletedAt == null &&
                      execution.ExecutionContext == ReviewExecutionContext.OfficialSubmission &&
                      stage.ReviewMethod == ReviewMethod.PeerReview &&
                      (stage.Status == PersistedReviewStageStatus.AwaitingEvidence ||
                       stage.Status == PersistedReviewStageStatus.Running)
                select new PeerTarget(stage, execution, revision, submission))
            .ToListAsync()
            .ConfigureAwait(false);

    private async Task<List<PeerTarget>> LoadAuthorTestTargetsAsync(Guid testRunId) =>
        await (
                from subject in context.Set<AssessmentTestRunSubject>().AsNoTracking()
                join execution in context.Set<GradingExecution>().AsNoTracking()
                    on subject.Id equals execution.TestRunSubjectId
                join round in context.Set<GradeRound>().AsNoTracking()
                    on execution.ActiveGradeRoundId equals (Guid?)round.Id
                join stage in context.Set<ReviewStage>().AsNoTracking()
                    on round.Id equals stage.GradeRoundId
                join revision in context.Set<AssessmentDefinitionRevision>().AsNoTracking()
                    on execution.DefinitionRevisionId equals revision.Id
                where subject.TestRunId == testRunId &&
                      execution.ExecutionContext == ReviewExecutionContext.AuthorTest &&
                      execution.SubmittedAt.HasValue &&
                      stage.ReviewMethod == ReviewMethod.PeerReview &&
                      (stage.Status == PersistedReviewStageStatus.AwaitingEvidence ||
                       stage.Status == PersistedReviewStageStatus.Running)
                select new PeerTarget(stage, execution, revision, null))
            .ToListAsync()
            .ConfigureAwait(false);

    private async Task<Guid[]> LoadAuthorTestPeerStageIdsAsync(Guid testRunId) =>
        await (
                from subject in context.Set<AssessmentTestRunSubject>().AsNoTracking()
                join execution in context.Set<GradingExecution>().AsNoTracking()
                    on subject.Id equals execution.TestRunSubjectId
                join round in context.Set<GradeRound>().AsNoTracking()
                    on execution.ActiveGradeRoundId equals (Guid?)round.Id
                join stage in context.Set<ReviewStage>().AsNoTracking()
                    on round.Id equals stage.GradeRoundId
                where subject.TestRunId == testRunId &&
                      execution.ExecutionContext == ReviewExecutionContext.AuthorTest &&
                      stage.ReviewMethod == ReviewMethod.PeerReview
                select stage.Id)
            .ToArrayAsync()
            .ConfigureAwait(false);

    private async Task<List<PeerTarget>> FilterAvailableTargetsAsync(
        IReadOnlyList<PeerTarget> targets,
        PeerReviewPolicyV1 policy,
        DateTime now,
        Guid? reviewerUserId = null)
    {
        if (targets.Count == 0)
        {
            return [];
        }

        var stageIds = targets.Select(value => value.Stage.Id).ToArray();
        var claims = await context.Set<PeerReviewClaim>()
            .AsNoTracking()
            .Where(value => stageIds.Contains(value.ReviewStageId) &&
                            (value.Status == PeerReviewClaimStatus.Submitted ||
                             value.Status == PeerReviewClaimStatus.Assigned && value.LeaseExpiresAt > now))
            .ToArrayAsync()
            .ConfigureAwait(false);
        var counts = claims.GroupBy(value => value.ReviewStageId).ToDictionary(group => group.Key, group => group.Count());
        HashSet<Guid> alreadyReviewed = reviewerUserId.HasValue
            ? claims.Where(value => value.ReviewerUserId == reviewerUserId).Select(value => value.ReviewStageId).ToHashSet()
            : [];
        var candidates = targets
            .Where(value => !value.Stage.EvidenceDeadlineAt.HasValue || value.Stage.EvidenceDeadlineAt > now)
            .Where(value => !alreadyReviewed.Contains(value.Stage.Id))
            .Where(value => counts.GetValueOrDefault(value.Stage.Id) < policy.ReviewsRequiredPerSubmission)
            .ToList();
        if (candidates.Count == 0)
        {
            return [];
        }

        var minimum = candidates.Min(value => counts.GetValueOrDefault(value.Stage.Id));
        return candidates.Where(value => counts.GetValueOrDefault(value.Stage.Id) == minimum).ToList();
    }

    private async Task ExpireClaimsAsync(Guid[] stageIds, DateTime now)
    {
        if (stageIds.Length == 0)
        {
            return;
        }

        var expired = await context.Set<PeerReviewClaim>()
            .Where(value => stageIds.Contains(value.ReviewStageId) &&
                            value.Status == PeerReviewClaimStatus.Assigned &&
                            value.LeaseExpiresAt <= now)
            .ToArrayAsync()
            .ConfigureAwait(false);
        foreach (var claim in expired)
        {
            claim.Expire(now);
        }

        if (expired.Length > 0)
        {
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    private async Task<PeerClaimContext?> LoadClaimAsync(
        Guid claimId,
        CancellationToken cancellationToken = default)
    {
        var claim = await context.Set<PeerReviewClaim>()
            .SingleOrDefaultAsync(value => value.Id == claimId, cancellationToken)
            .ConfigureAwait(false);
        if (claim is null)
        {
            return null;
        }

        var stage = await context.Set<ReviewStage>()
            .SingleAsync(value => value.Id == claim.ReviewStageId, cancellationToken)
            .ConfigureAwait(false);
        var round = await context.Set<GradeRound>()
            .SingleAsync(value => value.Id == stage.GradeRoundId, cancellationToken)
            .ConfigureAwait(false);
        var execution = await context.Set<GradingExecution>()
            .SingleAsync(value => value.Id == round.GradingExecutionId, cancellationToken)
            .ConfigureAwait(false);
        var revision = await context.Set<AssessmentDefinitionRevision>()
            .AsNoTracking()
            .SingleAsync(value => value.Id == execution.DefinitionRevisionId, cancellationToken)
            .ConfigureAwait(false);
        var submission = execution.AssessmentSubmissionId.HasValue
            ? await context.Set<AssessmentSubmission>()
                .AsNoTracking()
                .SingleAsync(value => value.Id == execution.AssessmentSubmissionId, cancellationToken)
                .ConfigureAwait(false)
            : null;
        return new PeerClaimContext(claim, stage, round, execution, revision, submission);
    }

    private Task<Guid?> GetExecutionIdForClaimAsync(
        Guid claimId,
        CancellationToken cancellationToken = default) =>
        (
            from claim in context.Set<PeerReviewClaim>().AsNoTracking()
            join stage in context.Set<ReviewStage>().AsNoTracking() on claim.ReviewStageId equals stage.Id
            join round in context.Set<GradeRound>().AsNoTracking() on stage.GradeRoundId equals round.Id
            where claim.Id == claimId
            select (Guid?)round.GradingExecutionId)
        .SingleOrDefaultAsync(cancellationToken);

    private Task<Guid?> GetExecutionIdForStageAsync(
        Guid stageId,
        CancellationToken cancellationToken = default) =>
        (
            from stage in context.Set<ReviewStage>().AsNoTracking()
            join round in context.Set<GradeRound>().AsNoTracking() on stage.GradeRoundId equals round.Id
            where stage.Id == stageId && stage.ReviewMethod == ReviewMethod.PeerReview
            select (Guid?)round.GradingExecutionId)
        .SingleOrDefaultAsync(cancellationToken);

    private static PeerReviewClaimView ToView(PeerClaimContext value) => new(
        value.Claim.Id,
        value.Stage.Id,
        value.Revision.AssessmentId,
        value.Submission?.Id,
        value.Claim.ReviewerUserId,
        value.Claim.ReviewerTestRunSubjectId,
        value.Claim.Status,
        value.Claim.Version,
        value.Claim.ClaimedAt,
        value.Claim.LeaseExpiresAt,
        value.Claim.SubmittedAt,
        value.Submission?.AttemptNumber ?? 1,
        value.Execution.DeliveryCanonicalJson,
        value.Execution.ResponseEnvelopeCanonicalJson);

    private static PeerReviewPolicyV1 ReadPeerPolicy(AssessmentDefinitionRevision revision) =>
        AssessmentDefinitionRevisionReader.ReadValidated(revision).AuthoringSource.Policy.Review.Peer
        ?? throw new InvalidOperationException("PeerReview revision is missing its frozen policy.");

    private static ScoreValue ReadAssessmentMaximum(AssessmentDefinitionRevision revision)
    {
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        return ScoreValue.Sum(snapshot.Manifest.Items.Select(item =>
            ScoreValue.FromUnits(snapshot.ItemProjections[item.ItemId].GetProperty("maxScore").GetInt32())));
    }

    private static DateTime ResolveLeaseEnd(ReviewStage stage, PeerReviewPolicyV1 policy, DateTime now)
    {
        var leaseEnd = now.AddMinutes(policy.ClaimLeaseMinutes);
        return stage.EvidenceDeadlineAt.HasValue && stage.EvidenceDeadlineAt < leaseEnd
            ? stage.EvidenceDeadlineAt.Value
            : leaseEnd;
    }

    private void Enqueue(Guid tenantId, string eventType, object payload) =>
        outbox.Enqueue(
            tenantId,
            eventType,
            "1",
            CanonicalJson.Serialize(JsonSerializer.SerializeToElement(payload, GradingJson.Options)));

    private async Task<bool> IsOperationReplayAsync(
        Guid tenantId,
        Guid resourceId,
        string commandType,
        Guid actorId,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var receipt = await context.Set<GradingCommandReceipt>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.TenantId == tenantId &&
                value.ResourceId == resourceId &&
                value.CommandType == commandType &&
                value.ActorId == actorId &&
                value.IdempotencyKey == idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (receipt is null)
        {
            return false;
        }

        if (!string.Equals(receipt.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency key was used with a different peer-review request.");
        }

        return true;
    }

    private void AddOperationReceipt(
        Guid tenantId,
        Guid resourceId,
        string commandType,
        Guid actorId,
        string idempotencyKey,
        string requestHash) =>
        context.Set<GradingCommandReceipt>().Add(GradingCommandReceipt.Create(
            tenantId,
            resourceId,
            commandType,
            actorId,
            idempotencyKey,
            requestHash,
            "1",
            "{\"schemaVersion\":1}",
            SystemClock.UtcNow.AddDays(90)));

    private static string RequireIdempotencyKey(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Idempotency key is required.", nameof(value))
            : value.Trim();
        return normalized.Length <= 200
            ? normalized
            : throw new ArgumentException("Idempotency key cannot exceed 200 characters.", nameof(value));
    }

    private sealed record PeerTarget(
        ReviewStage Stage,
        GradingExecution Execution,
        AssessmentDefinitionRevision Revision,
        AssessmentSubmission? Submission);

    private sealed record PeerClaimContext(
        PeerReviewClaim Claim,
        ReviewStage Stage,
        GradeRound Round,
        GradingExecution Execution,
        AssessmentDefinitionRevision Revision,
        AssessmentSubmission? Submission);
}
