using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public interface IAssessmentGradingRuntimeService
{
    Task<AssessmentTestRunViewV1> StartTestRunAsync(
        Guid assessmentId,
        Guid actorId,
        StartAssessmentTestRunCommand command,
        CancellationToken cancellationToken = default);

    Task<AssessmentTestRunViewV1> GetTestRunAsync(
        Guid testRunId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<AssessmentTestRunViewV1> SubmitTestRunAsync(
        Guid testRunId,
        Guid actorId,
        SubmitAssessmentResponseCommand command,
        CancellationToken cancellationToken = default);

    Task<AssessmentTestRunViewV1> ResolveTestInstructorReviewAsync(
        Guid testRunId,
        Guid actorId,
        InstructorReviewResolutionV1 resolution,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<AssessmentTestRunViewV1> RestartTestRunAsync(
        Guid testRunId,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<AssessmentSubmissionViewV1> StartIndividualSubmissionAsync(
        Guid assessmentId,
        StartIndividualSubmissionCommand command,
        CancellationToken cancellationToken = default);

    Task<AssessmentSubmissionViewV1> StartCollectiveSubmissionAsync(
        Guid assessmentId,
        StartCollectiveSubmissionCommand command,
        CancellationToken cancellationToken = default);

    Task<AssessmentSubmissionViewV1> SaveCollectiveDraftAsync(
        Guid submissionId,
        Guid actorId,
        SaveCollectiveAssessmentDraftCommand command,
        CancellationToken cancellationToken = default);

    Task<AssessmentSubmissionViewV1> SubmitOfficialAsync(
        Guid submissionId,
        Guid actorId,
        SubmitAssessmentResponseCommand command,
        CancellationToken cancellationToken = default);

    Task<AssessmentSubmissionViewV1> ResolveOfficialInstructorReviewAsync(
        Guid submissionId,
        Guid actorId,
        InstructorReviewResolutionV1 resolution,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<AssessmentSubmissionViewV1> RegradeOfficialAsync(
        Guid submissionId,
        Guid actorId,
        RegradeExecutionCommand command,
        CancellationToken cancellationToken = default);

    Task<AssessmentSubmissionViewV1> GetSubmissionAsync(
        Guid submissionId,
        Guid actorId,
        bool instructorView,
        CancellationToken cancellationToken = default);
}

public sealed class AssessmentGradingRuntimeService(
    IApplicationDbContext context,
    IGradingExecutionOrchestrator orchestrator,
    IAssessmentAuthoringService authoringService,
    OfficialGradingFinalizationSink finalizationSink) : IAssessmentGradingRuntimeService
{
    public async Task<AssessmentTestRunViewV1> StartTestRunAsync(
        Guid assessmentId,
        Guid actorId,
        StartAssessmentTestRunCommand command,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor ID is required.", nameof(actorId));
        }

        var assessment = await RequireAssessmentAsync(assessmentId, cancellationToken).ConfigureAwait(false);
        var tenantId = RequireTenant(assessment.TenantId);
        var revision = await context.Set<AssessmentDefinitionRevision>()
            .SingleOrDefaultAsync(value => value.Id == command.RevisionId && value.AssessmentId == assessmentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Prepared assessment revision was not found.");
        var requestHash = Hash(new
        {
            schemaVersion = 1,
            assessmentId,
            command.RevisionId,
            command.PersonaKey,
            command.PersonaDisplayName,
        });
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "start-test-run",
            assessmentId,
            actorId,
            cancellationToken).ConfigureAwait(false);
        var replay = await FindReceiptAsync(
            tenantId,
            assessmentId,
            "start-assessment-test-run",
            actorId,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return await GetTestRunAsync(replay.ResourceId, actorId, cancellationToken).ConfigureAwait(false);
        }

        var run = AssessmentTestRun.Create(assessment.TenantId, assessmentId, revision.Id, actorId);
        run.Start();
        var subject = AssessmentTestRunSubject.Create(
            assessment.TenantId,
            run.Id,
            RequireText(command.PersonaKey, nameof(command.PersonaKey)),
            RequireText(command.PersonaDisplayName, nameof(command.PersonaDisplayName)));
        var execution = GradingExecution.CreateAuthorTest(assessment.TenantId, revision.Id, subject.Id);
        context.Set<AssessmentTestRun>().Add(run);
        context.Set<AssessmentTestRunSubject>().Add(subject);
        context.Set<GradingExecution>().Add(execution);
        orchestrator.MaterializeDelivery(execution, revision);
        AddReceipt(
            tenantId,
            assessmentId,
            "start-assessment-test-run",
            actorId,
            command.IdempotencyKey,
            requestHash,
            new ResourceOutcome(1, run.Id));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return await BuildTestRunViewAsync(run, subject, execution, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentTestRunViewV1> GetTestRunAsync(
        Guid testRunId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var run = await context.Set<AssessmentTestRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == testRunId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Assessment test run was not found.");
        if (run.CreatedByUserId != actorId)
        {
            throw new UnauthorizedAccessException("Test run belongs to another instructor.");
        }

        var subject = await context.Set<AssessmentTestRunSubject>()
            .AsNoTracking()
            .Where(value => value.TestRunId == run.Id)
            .OrderByDescending(value => value.CreatedAt)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        var execution = await context.Set<GradingExecution>()
            .AsNoTracking()
            .Where(value => value.TestRunSubjectId == subject.Id)
            .OrderByDescending(value => value.CreatedAt)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        return await BuildTestRunViewAsync(run, subject, execution, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentTestRunViewV1> SubmitTestRunAsync(
        Guid testRunId,
        Guid actorId,
        SubmitAssessmentResponseCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "submit-test-run",
            testRunId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var owned = await RequireOwnedTestExecutionAsync(testRunId, actorId, cancellationToken).ConfigureAwait(false);
        var requestHash = Hash(command.Response);
        var replay = await FindReceiptAsync(
            RequireTenant(owned.Run.TenantId),
            owned.Execution.Id,
            "submit-assessment-test-run",
            actorId,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is null)
        {
            await orchestrator.SubmitAsync(owned.Execution.Id, command.Response, cancellationToken).ConfigureAwait(false);
            AddReceipt(
                RequireTenant(owned.Run.TenantId),
                owned.Execution.Id,
                "submit-assessment-test-run",
                actorId,
                command.IdempotencyKey,
                requestHash,
                new ResourceOutcome(1, owned.Run.Id));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return await GetTestRunAsync(testRunId, actorId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentTestRunViewV1> ResolveTestInstructorReviewAsync(
        Guid testRunId,
        Guid actorId,
        InstructorReviewResolutionV1 resolution,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "resolve-test-instructor-review",
            testRunId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var owned = await RequireOwnedTestExecutionAsync(testRunId, actorId, cancellationToken).ConfigureAwait(false);
        var requestHash = Hash(resolution);
        var replay = await FindReceiptAsync(
            RequireTenant(owned.Run.TenantId),
            owned.Execution.Id,
            "resolve-test-instructor-review",
            actorId,
            idempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is null)
        {
            await orchestrator.ResolveInstructorReviewAsync(
                owned.Execution.Id,
                actorId,
                resolution,
                cancellationToken).ConfigureAwait(false);
            AddReceipt(
                RequireTenant(owned.Run.TenantId),
                owned.Execution.Id,
                "resolve-test-instructor-review",
                actorId,
                idempotencyKey,
                requestHash,
                new ResourceOutcome(1, owned.Run.Id));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return await GetTestRunAsync(testRunId, actorId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentTestRunViewV1> RestartTestRunAsync(
        Guid testRunId,
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "restart-test-run",
            testRunId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var owned = await RequireOwnedTestExecutionAsync(testRunId, actorId, cancellationToken).ConfigureAwait(false);
        var tenantId = RequireTenant(owned.Run.TenantId);
        var requestHash = Hash(new
        {
            schemaVersion = 1,
            testRunId,
            owned.Run.DefinitionRevisionId,
            owned.Subject.PersonaKey,
            owned.Subject.DisplayName,
        });
        var replay = await FindReceiptAsync(
            tenantId,
            testRunId,
            "restart-assessment-test-run",
            actorId,
            idempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return await GetTestRunAsync(replay.ResourceId, actorId, cancellationToken).ConfigureAwait(false);
        }

        var revision = await RequireRevisionAsync(owned.Run.DefinitionRevisionId, cancellationToken).ConfigureAwait(false);
        var run = AssessmentTestRun.Create(owned.Run.TenantId, owned.Run.AssessmentId, revision.Id, actorId);
        run.Start();
        var subject = AssessmentTestRunSubject.Create(
            owned.Run.TenantId,
            run.Id,
            owned.Subject.PersonaKey,
            owned.Subject.DisplayName);
        var execution = GradingExecution.CreateAuthorTest(owned.Run.TenantId, revision.Id, subject.Id);
        context.Set<AssessmentTestRun>().Add(run);
        context.Set<AssessmentTestRunSubject>().Add(subject);
        context.Set<GradingExecution>().Add(execution);
        orchestrator.MaterializeDelivery(execution, revision);
        AddReceipt(
            tenantId,
            testRunId,
            "restart-assessment-test-run",
            actorId,
            idempotencyKey,
            requestHash,
            new ResourceOutcome(1, run.Id));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return await BuildTestRunViewAsync(run, subject, execution, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentSubmissionViewV1> StartIndividualSubmissionAsync(
        Guid assessmentId,
        StartIndividualSubmissionCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "start-individual-submission",
            assessmentId,
            command.EnrollmentId,
            cancellationToken).ConfigureAwait(false);
        if (command.ActorId != command.UserId)
        {
            throw new UnauthorizedAccessException("An individual learner can start only their own submission.");
        }

        var assessment = await RequireAssessmentAsync(assessmentId, cancellationToken).ConfigureAwait(false);
        await RequireMembershipAsync(assessment.CourseId, command.EnrollmentId, command.UserId, cancellationToken)
            .ConfigureAwait(false);
        var tenantId = RequireTenant(assessment.TenantId);
        var requestHash = Hash(new
        {
            schemaVersion = 1,
            assessmentId,
            command.EnrollmentId,
            command.UserId,
        });
        var replay = await FindReceiptAsync(
            tenantId,
            assessmentId,
            "start-individual-assessment-submission",
            command.ActorId,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return await GetSubmissionAsync(replay.ResourceId, command.ActorId, false, cancellationToken).ConfigureAwait(false);
        }

        var existingSubmission = await context.Set<AssessmentSubmission>()
            .Where(value => value.AssessmentId == assessmentId && value.EnrollmentId == command.EnrollmentId)
            .OrderByDescending(value => value.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existingSubmission is not null)
        {
            if (existingSubmission.UserId != command.UserId)
            {
                throw new UnauthorizedAccessException("Submission belongs to another learner.");
            }

            AddReceipt(
                tenantId,
                assessmentId,
                "start-individual-assessment-submission",
                command.ActorId,
                command.IdempotencyKey,
                requestHash,
                new ResourceOutcome(1, existingSubmission.Id));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return await GetSubmissionAsync(existingSubmission.Id, command.ActorId, false, cancellationToken).ConfigureAwait(false);
        }

        if (!assessment.PublishedDefinitionRevisionId.HasValue)
        {
            throw new InvalidOperationException("Assessment does not have a published executable revision.");
        }

        var revision = await RequireRevisionAsync(assessment.PublishedDefinitionRevisionId.Value, cancellationToken).ConfigureAwait(false);
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        ValidateStartPolicy(snapshot.AuthoringSource.Policy);

        var attemptCount = await context.Set<AssessmentSubmission>()
            .CountAsync(value => value.AssessmentId == assessmentId && value.EnrollmentId == command.EnrollmentId, cancellationToken)
            .ConfigureAwait(false);
        if (attemptCount >= snapshot.AuthoringSource.Policy.MaxAttempts!.Value)
        {
            throw new InvalidOperationException("Maximum attempts reached.");
        }

        var submission = AssessmentSubmission.StartIndividual(
            assessment.TenantId,
            assessment.Id,
            assessment.PublishedDefinitionRevisionId!.Value,
            command.EnrollmentId,
            command.UserId,
            attemptCount + 1);
        var execution = GradingExecution.CreateOfficial(
            assessment.TenantId,
            assessment.PublishedDefinitionRevisionId.Value,
            submission.Id);
        context.Set<AssessmentSubmission>().Add(submission);
        context.Set<GradingExecution>().Add(execution);
        orchestrator.MaterializeDelivery(execution, revision);
        AddReceipt(
            tenantId,
            assessmentId,
            "start-individual-assessment-submission",
            command.ActorId,
            command.IdempotencyKey,
            requestHash,
            new ResourceOutcome(1, submission.Id));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return await BuildSubmissionViewAsync(submission, execution, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentSubmissionViewV1> StartCollectiveSubmissionAsync(
        Guid assessmentId,
        StartCollectiveSubmissionCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "start-collective-submission",
            assessmentId,
            command.CourseGroupId,
            cancellationToken).ConfigureAwait(false);
        var assessment = await RequireAssessmentAsync(assessmentId, cancellationToken).ConfigureAwait(false);
        var tenantId = RequireTenant(assessment.TenantId);
        var requestHash = Hash(new { schemaVersion = 1, assessmentId, command.CourseGroupId });
        var replay = await FindReceiptAsync(
            tenantId,
            assessmentId,
            "start-collective-assessment-submission",
            command.ActorId,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return await GetSubmissionAsync(replay.ResourceId, command.ActorId, false, cancellationToken).ConfigureAwait(false);
        }

        var existingSubmission = await context.Set<AssessmentSubmission>()
            .Where(value => value.AssessmentId == assessmentId && value.CourseGroupId == command.CourseGroupId)
            .OrderByDescending(value => value.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existingSubmission is not null)
        {
            await RequireSubmissionActorAsync(existingSubmission, command.ActorId, cancellationToken).ConfigureAwait(false);
            AddReceipt(
                tenantId,
                assessmentId,
                "start-collective-assessment-submission",
                command.ActorId,
                command.IdempotencyKey,
                requestHash,
                new ResourceOutcome(1, existingSubmission.Id));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return await GetSubmissionAsync(existingSubmission.Id, command.ActorId, false, cancellationToken).ConfigureAwait(false);
        }

        if (!assessment.PublishedDefinitionRevisionId.HasValue)
        {
            throw new InvalidOperationException("Assessment does not have a published executable revision.");
        }

        var revision = await RequireRevisionAsync(assessment.PublishedDefinitionRevisionId.Value, cancellationToken).ConfigureAwait(false);
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        ValidateStartPolicy(snapshot.AuthoringSource.Policy);
        if (!assessment.GroupSetId.HasValue)
        {
            throw new InvalidOperationException("Assessment is not configured for collective submissions.");
        }

        var groupSetBelongsToCourse = await context.Set<CourseGroupSet>()
            .AsNoTracking()
            .AnyAsync(value => value.Id == assessment.GroupSetId.Value && value.CourseId == assessment.CourseId, cancellationToken)
            .ConfigureAwait(false);
        if (!groupSetBelongsToCourse)
        {
            throw new InvalidOperationException("Assessment group set does not belong to its course.");
        }

        var group = await context.Set<CourseGroup>()
            .SingleOrDefaultAsync(value => value.Id == command.CourseGroupId && value.GroupSetId == assessment.GroupSetId.Value, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Course group was not found for this assessment.");
        var memberUserIds = await context.Set<CourseGroupMember>()
            .AsNoTracking()
            .Where(value => value.GroupId == group.Id)
            .OrderBy(value => value.UserId)
            .Select(value => value.UserId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!memberUserIds.Contains(command.ActorId))
        {
            throw new UnauthorizedAccessException("Only a current group member can start the collective submission.");
        }

        if (memberUserIds.Length == 0)
        {
            throw new InvalidOperationException("Course group has no members.");
        }

        var memberships = new List<(Guid EnrollmentId, Guid UserId)>();
        foreach (var userId in memberUserIds)
        {
            memberships.Add((
                await ResolveMembershipIdAsync(assessment.CourseId, userId, cancellationToken).ConfigureAwait(false),
                userId));
        }

        var attemptCount = await context.Set<AssessmentSubmission>()
            .CountAsync(value => value.AssessmentId == assessmentId && value.CourseGroupId == group.Id, cancellationToken)
            .ConfigureAwait(false);
        if (attemptCount >= snapshot.AuthoringSource.Policy.MaxAttempts!.Value)
        {
            throw new InvalidOperationException("Maximum attempts reached.");
        }

        var submission = AssessmentSubmission.StartCollective(
            assessment.TenantId,
            assessment.Id,
            assessment.PublishedDefinitionRevisionId!.Value,
            group.Id,
            command.ActorId,
            attemptCount + 1);
        var execution = GradingExecution.CreateOfficial(
            assessment.TenantId,
            assessment.PublishedDefinitionRevisionId.Value,
            submission.Id);
        context.Set<AssessmentSubmission>().Add(submission);
        foreach (var membership in memberships)
        {
            context.Set<AssessmentSubmissionParticipant>().Add(AssessmentSubmissionParticipant.Create(
                assessment.TenantId,
                submission.Id,
                membership.EnrollmentId,
                membership.UserId));
        }
        context.Set<GradingExecution>().Add(execution);
        orchestrator.MaterializeDelivery(execution, revision);
        AddReceipt(
            tenantId,
            assessmentId,
            "start-collective-assessment-submission",
            command.ActorId,
            command.IdempotencyKey,
            requestHash,
            new ResourceOutcome(1, submission.Id));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return await BuildSubmissionViewAsync(submission, execution, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentSubmissionViewV1> SaveCollectiveDraftAsync(
        Guid submissionId,
        Guid actorId,
        SaveCollectiveAssessmentDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "save-collective-draft",
            submissionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var owned = await RequireOfficialExecutionAsync(submissionId, cancellationToken).ConfigureAwait(false);
        await RequireSubmissionActorAsync(owned.Submission, actorId, cancellationToken).ConfigureAwait(false);
        if (!owned.Submission.IsCollective)
        {
            throw new InvalidOperationException("Submission is not collective.");
        }

        var canonicalResponse = Serialize(command.Response);
        var requestHash = Hash(new
        {
            schemaVersion = 1,
            submissionId,
            command.ExpectedVersion,
            response = command.Response,
        });
        var replay = await FindReceiptAsync(
            RequireTenant(owned.Submission.TenantId),
            submissionId,
            "save-collective-assessment-draft",
            actorId,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is null)
        {
            var previousVersion = owned.Submission.DraftVersion;
            owned.Submission.AdvanceCollectiveDraft(command.ExpectedVersion);
            owned.Execution.SaveResponseDraft(command.Response, canonicalResponse);
            context.Set<CollectiveAttemptDraftChange>().Add(CollectiveAttemptDraftChange.Create(
                RequireTenant(owned.Submission.TenantId),
                submissionId,
                actorId,
                previousVersion,
                owned.Submission.DraftVersion,
                command.IdempotencyKey,
                requestHash,
                owned.Execution.ResponseHash!));
            AddReceipt(
                RequireTenant(owned.Submission.TenantId),
                submissionId,
                "save-collective-assessment-draft",
                actorId,
                command.IdempotencyKey,
                requestHash,
                new ResourceOutcome(1, submissionId));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return await GetSubmissionAsync(submissionId, actorId, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentSubmissionViewV1> SubmitOfficialAsync(
        Guid submissionId,
        Guid actorId,
        SubmitAssessmentResponseCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "submit-official",
            submissionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var owned = await RequireOfficialExecutionAsync(submissionId, cancellationToken).ConfigureAwait(false);
        await RequireSubmissionActorAsync(owned.Submission, actorId, cancellationToken).ConfigureAwait(false);
        if (owned.Submission.IsCollective && command.ExpectedDraftVersion != owned.Submission.DraftVersion)
        {
            throw new InvalidOperationException("The collective draft version is stale.");
        }

        var requestHash = Hash(new
        {
            schemaVersion = 1,
            submissionId,
            command.ExpectedDraftVersion,
            response = command.Response,
        });
        var replay = await FindReceiptAsync(
            RequireTenant(owned.Submission.TenantId),
            submissionId,
            "submit-assessment-response",
            actorId,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is null)
        {
            var assessment = await RequireAssessmentAsync(owned.Submission.AssessmentId, cancellationToken).ConfigureAwait(false);
            var revision = await RequireRevisionAsync(owned.Execution.DefinitionRevisionId, cancellationToken).ConfigureAwait(false);
            var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
            var submittedAt = SystemClock.UtcNow;
            if (!TryGetSubmissionTiming(snapshot.AuthoringSource.Policy, owned.Submission.StartedAt, submittedAt, out var isLate))
            {
                throw new InvalidOperationException("Assessment is not accepting submissions at this time.");
            }

            orchestrator.ValidateResponse(owned.Execution, revision, command.Response);
            if (snapshot.AuthoringSource.ContentType == Code.CodeAssessmentContracts.ContentType)
            {
                // This compatibility projection is derived only after the revision-bound
                // decoder accepts the response; scores and private overrides are rejected.
                owned.Submission.SetPayload(new SubmitAssessmentRequest
                {
                    CodePayload = command.Response.Payload.GetProperty("files").GetRawText(),
                }, SubmissionModality.Code);
            }
            owned.Submission.Submit(isLate, submittedAt, actorId);
            if (snapshot.AuthoringSource.Policy.Completion.Mode == ContentCompletionMode.OnSubmit)
            {
                await finalizationSink.ProjectCompletionAsync(
                    owned.Submission,
                    assessment,
                    null,
                    await finalizationSink.ResolveEnrollmentIdsAsync(owned.Submission, cancellationToken).ConfigureAwait(false),
                    "submit",
                    cancellationToken).ConfigureAwait(false);
            }
            await orchestrator.SubmitAsync(owned.Execution.Id, command.Response, cancellationToken).ConfigureAwait(false);
            AddReceipt(
                RequireTenant(owned.Submission.TenantId),
                submissionId,
                "submit-assessment-response",
                actorId,
                command.IdempotencyKey,
                requestHash,
                new ResourceOutcome(1, submissionId));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return await GetSubmissionAsync(submissionId, actorId, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentSubmissionViewV1> ResolveOfficialInstructorReviewAsync(
        Guid submissionId,
        Guid actorId,
        InstructorReviewResolutionV1 resolution,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "resolve-official-instructor-review",
            submissionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var owned = await RequireOfficialExecutionAsync(submissionId, cancellationToken).ConfigureAwait(false);
        var requestHash = Hash(resolution);
        var replay = await FindReceiptAsync(
            RequireTenant(owned.Submission.TenantId),
            submissionId,
            "resolve-instructor-review",
            actorId,
            idempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is null)
        {
            var revision = await RequireRevisionAsync(owned.Execution.DefinitionRevisionId, cancellationToken).ConfigureAwait(false);
            var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
            var rubricScores = Code.CodeRubricSnapshot.ValidateResolution(snapshot, resolution);
            await orchestrator.ResolveInstructorReviewAsync(owned.Execution.Id, actorId, resolution, cancellationToken)
                .ConfigureAwait(false);
            if (rubricScores is not null)
            {
                owned.Submission.SetRuntimeRubricScores(rubricScores);
            }
            AddReceipt(
                RequireTenant(owned.Submission.TenantId),
                submissionId,
                "resolve-instructor-review",
                actorId,
                idempotencyKey,
                requestHash,
                new ResourceOutcome(1, submissionId));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return await GetSubmissionAsync(submissionId, actorId, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentSubmissionViewV1> RegradeOfficialAsync(
        Guid submissionId,
        Guid actorId,
        RegradeExecutionCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "regrade-official",
            submissionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var owned = await RequireOfficialExecutionAsync(submissionId, cancellationToken).ConfigureAwait(false);
        var requestHash = Hash(new { schemaVersion = 1, submissionId, command.Reason });
        var replay = await FindReceiptAsync(
            RequireTenant(owned.Submission.TenantId),
            submissionId,
            "regrade-assessment-submission",
            actorId,
            command.IdempotencyKey,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is null)
        {
            await orchestrator.RegradeAsync(
                owned.Execution.Id,
                actorId,
                command.Reason,
                cancellationToken).ConfigureAwait(false);
            AddReceipt(
                RequireTenant(owned.Submission.TenantId),
                submissionId,
                "regrade-assessment-submission",
                actorId,
                command.IdempotencyKey,
                requestHash,
                new ResourceOutcome(1, submissionId));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return await GetSubmissionAsync(submissionId, actorId, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AssessmentSubmissionViewV1> GetSubmissionAsync(
        Guid submissionId,
        Guid actorId,
        bool instructorView,
        CancellationToken cancellationToken = default)
    {
        var owned = await RequireOfficialExecutionAsync(submissionId, cancellationToken, noTracking: true).ConfigureAwait(false);
        if (!instructorView)
        {
            await RequireSubmissionActorAsync(owned.Submission, actorId, cancellationToken).ConfigureAwait(false);
        }

        return await BuildSubmissionViewAsync(owned.Submission, owned.Execution, instructorView, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<AssessmentTestRunViewV1> BuildTestRunViewAsync(
        AssessmentTestRun run,
        AssessmentTestRunSubject subject,
        GradingExecution execution,
        CancellationToken cancellationToken)
    {
        var executionView = await BuildExecutionViewAsync(execution, true, cancellationToken).ConfigureAwait(false);
        var state = await authoringService.GetStateAsync(run.AssessmentId, cancellationToken).ConfigureAwait(false);
        var candidateMatches = state.IsSuccess && state.Value.Candidate?.RevisionId == run.DefinitionRevisionId && state.Value.CandidateMatchesDraft;
        var ready = run.Status == AssessmentTestRunStatus.Completed && candidateMatches && state.IsSuccess && state.Value.Publish.Available;
        var diagnostics = new List<string>();
        if (executionView.InstructorVisibleResult is { State: "partial" } partial)
        {
            diagnostics.AddRange(partial.Items
                .Where(item => item.State != GradeItemState.Graded)
                .Select(item => $"{item.ItemId}:{item.State.ToString().ToLowerInvariant()}"));
        }
        if (!candidateMatches)
        {
            diagnostics.Add("candidate-no-longer-matches-draft");
        }

        return new AssessmentTestRunViewV1(
            run.Id,
            run.AssessmentId,
            run.DefinitionRevisionId,
            run.Status,
            subject.PersonaKey,
            subject.DisplayName,
            executionView,
            candidateMatches,
            ready,
            diagnostics);
    }

    private async Task<AssessmentSubmissionViewV1> BuildSubmissionViewAsync(
        AssessmentSubmission submission,
        GradingExecution execution,
        bool instructorView,
        CancellationToken cancellationToken)
    {
        var definitionRevisionId = submission.DefinitionRevisionId
            ?? throw new InvalidOperationException("A runtime submission must reference an immutable definition revision.");
        var executionView = await BuildExecutionViewAsync(execution, instructorView, cancellationToken).ConfigureAwait(false);
        var contentCompleted = await context.Set<AssessmentContentCompletionProjection>()
            .AsNoTracking()
            .AnyAsync(value => value.SubmissionId == submission.Id, cancellationToken)
            .ConfigureAwait(false);
        return new AssessmentSubmissionViewV1(
            submission.Id,
            submission.AssessmentId,
            definitionRevisionId,
            submission.EnrollmentId,
            submission.CourseGroupId,
            submission.AttemptNumber,
            submission.Status,
            submission.DraftVersion,
            submission.Version,
            submission.StartedAt,
            submission.SubmittedAt,
            submission.SubmittedByUserId,
            contentCompleted,
            executionView);
    }

    private async Task<AssessmentExecutionViewV1> BuildExecutionViewAsync(
        GradingExecution execution,
        bool instructorView,
        CancellationToken cancellationToken)
    {
        var revision = await RequireRevisionAsync(execution.DefinitionRevisionId, cancellationToken).ConfigureAwait(false);
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        var delivery = Deserialize<AssessmentExecutionDeliveryV1>(
            execution.DeliveryCanonicalJson ?? throw new InvalidOperationException("Execution delivery was not materialized."));
        var itemMaxScores = snapshot.Manifest.Items.ToDictionary(
            item => item.ItemId,
            item => ReadProjectionMaxScore(snapshot.ItemProjections[item.ItemId]),
            StringComparer.Ordinal);
        var submittedResponse = execution.ResponseEnvelopeCanonicalJson is null
            ? null
            : Deserialize<AssessmentResponseEnvelopeV1>(execution.ResponseEnvelopeCanonicalJson);
        GradeResultV1? instructorResult = null;
        GradeResultV1? learnerResult = null;
        var requiresInstructor = false;
        if (execution.ActiveGradeRoundId.HasValue)
        {
            var activeRound = await context.Set<GradeRound>()
                .AsNoTracking()
                .SingleAsync(value => value.Id == execution.ActiveGradeRoundId.Value, cancellationToken)
                .ConfigureAwait(false);
            instructorResult = await BuildRoundResultAsync(activeRound, cancellationToken).ConfigureAwait(false);
            requiresInstructor = activeRound.Status == PersistedGradeRoundStatus.AwaitingInstructorResolution;
        }

        var releases = execution.ExecutionContext == ReviewExecutionContext.OfficialSubmission
            ? await (
                    from round in context.Set<GradeRound>().AsNoTracking()
                    join release in context.Set<GradeResultRelease>().AsNoTracking()
                        on round.Id equals release.GradeRoundId
                    where round.GradingExecutionId == execution.Id
                    select new { round.Id, release.ReleasedAt })
                .ToDictionaryAsync(value => value.Id, value => value.ReleasedAt, cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<Guid, DateTime>();

        if (execution.ExecutionContext == ReviewExecutionContext.OfficialSubmission)
        {
            var releasedRound = await (
                    from round in context.Set<GradeRound>().AsNoTracking()
                    join release in context.Set<GradeResultRelease>().AsNoTracking()
                        on round.Id equals release.GradeRoundId
                    where round.GradingExecutionId == execution.Id
                    orderby round.RoundNumber descending
                    select round)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (releasedRound is not null)
            {
                learnerResult = await BuildRoundResultAsync(releasedRound, cancellationToken).ConfigureAwait(false);
            }
        }

        var historyRounds = await context.Set<GradeRound>()
            .AsNoTracking()
            .Where(value => value.GradingExecutionId == execution.Id)
            .OrderBy(value => value.RoundNumber)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var visibleHistory = new List<GradeRoundViewV1>();
        foreach (var round in historyRounds)
        {
            var released = releases.TryGetValue(round.Id, out var releasedAt);
            if (!instructorView && execution.ExecutionContext == ReviewExecutionContext.OfficialSubmission && !released)
            {
                continue;
            }

            visibleHistory.Add(new GradeRoundViewV1(
                round.Id,
                round.RoundNumber,
                round.Reason,
                instructorView ? round.ReasonDetail : null,
                instructorView ? round.InitiatedByActorId : null,
                round.Status,
                round.StartedAt,
                round.FinalizedAt,
                await BuildRoundResultAsync(round, cancellationToken).ConfigureAwait(false),
                released,
                released ? releasedAt : null));
        }

        IReadOnlyDictionary<Guid, ScoreValue>? instructorRubricScores = null;
        if (instructorView && snapshot.AuthoringSource.ContentType == Code.CodeAssessmentContracts.ContentType &&
            execution.ActiveGradeRoundId.HasValue)
        {
            var evidenceBytes = await (
                from stage in context.Set<ReviewStage>().AsNoTracking()
                join evidence in context.Set<ReviewEvidence>().AsNoTracking() on stage.Id equals evidence.ReviewStageId
                where stage.GradeRoundId == execution.ActiveGradeRoundId.Value &&
                    stage.ReviewMethod == ReviewMethod.InstructorReview &&
                    stage.Status == PersistedReviewStageStatus.Completed &&
                    evidence.EvidenceType == "instructor-review-evidence"
                select evidence.CanonicalJson).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (evidenceBytes is not null)
            {
                using var evidence = JsonDocument.Parse(evidenceBytes);
                if (evidence.RootElement.TryGetProperty("rubricScores", out var scores) && scores.ValueKind == JsonValueKind.Object)
                {
                    instructorRubricScores = scores.Deserialize<Dictionary<Guid, ScoreValue>>(GradingJson.Options);
                }
            }
        }
        return new AssessmentExecutionViewV1(
            execution.Id,
            execution.DefinitionRevisionId,
            execution.ExecutionContext,
            revision.ExecutionSnapshotHash,
            execution.DeliveryHash ?? throw new InvalidOperationException("Execution delivery hash is missing."),
            delivery,
            itemMaxScores,
            submittedResponse,
            execution.Status,
            execution.ActiveGradeRoundId,
            instructorView ? instructorResult : null,
            learnerResult,
            requiresInstructor,
            learnerResult is not null,
            visibleHistory,
            instructorView && snapshot.AuthoringSource.ContentType == Code.CodeAssessmentContracts.ContentType
                ? snapshot.ItemProjections[Code.CodeAssessmentContracts.ItemId].GetProperty("definition").Clone() : null,
            instructorView && snapshot.AuthoringSource.ContentType == Code.CodeAssessmentContracts.ContentType &&
                snapshot.ItemProjections[Code.CodeAssessmentContracts.ItemId].TryGetProperty("rubric", out var frozenRubric)
                ? frozenRubric.Clone() : null,
            instructorRubricScores);
    }

    private static ScoreValue ReadProjectionMaxScore(JsonElement projection)
    {
        if (!projection.TryGetProperty("maxScore", out var value))
        {
            throw new JsonException("Assessment item projection maxScore is missing.");
        }

        return value.Deserialize<ScoreValue>(GradingJson.Options);
    }

    private async Task<GradeResultV1?> BuildRoundResultAsync(
        GradeRound round,
        CancellationToken cancellationToken)
    {
        var stage = await context.Set<ReviewStage>()
            .AsNoTracking()
            .Where(value => value.GradeRoundId == round.Id && value.Status == PersistedReviewStageStatus.Completed)
            .OrderByDescending(value => value.Sequence)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (stage is null)
        {
            return null;
        }

        var rows = await context.Set<GradeItemResult>()
            .AsNoTracking()
            .Where(value => value.ReviewStageId == stage.Id)
            .OrderBy(value => value.ItemId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var items = rows.Select(row => new GradeItemResultV1(
            row.ItemId,
            row.State switch
            {
                PersistedGradeItemState.Graded => GradeItemState.Graded,
                PersistedGradeItemState.Pending => GradeItemState.Pending,
                PersistedGradeItemState.Unsupported => GradeItemState.Unsupported,
                _ => throw new ArgumentOutOfRangeException(),
            },
            row.Score,
            row.MaxScore,
            [],
            stage.ReviewMethod,
            stage.HandlerKey,
            stage.HandlerVersion,
            row.Feedback,
            stage.ProviderKey)).ToArray();
        if (items.Length == 0)
        {
            return null;
        }

        var final = round.Status == PersistedGradeRoundStatus.Finalized;
        return new GradeResultV1(
            GradingContractVersions.GradeResult,
            final ? "final" : "partial",
            final ? round.Score : null,
            round.MaxScore,
            items,
            [],
            round.Feedback);
    }

    private async Task<OwnedTestExecution> RequireOwnedTestExecutionAsync(
        Guid testRunId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var run = await context.Set<AssessmentTestRun>()
            .SingleOrDefaultAsync(value => value.Id == testRunId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Assessment test run was not found.");
        if (run.CreatedByUserId != actorId)
        {
            throw new UnauthorizedAccessException("Test run belongs to another instructor.");
        }

        var subject = await context.Set<AssessmentTestRunSubject>()
            .Where(value => value.TestRunId == run.Id)
            .OrderByDescending(value => value.CreatedAt)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        var execution = await context.Set<GradingExecution>()
            .Where(value => value.TestRunSubjectId == subject.Id)
            .OrderByDescending(value => value.CreatedAt)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);
        return new OwnedTestExecution(run, subject, execution);
    }

    private async Task<OfficialExecution> RequireOfficialExecutionAsync(
        Guid submissionId,
        CancellationToken cancellationToken,
        bool noTracking = false)
    {
        IQueryable<AssessmentSubmission> submissions = context.Set<AssessmentSubmission>();
        IQueryable<GradingExecution> executions = context.Set<GradingExecution>();
        if (noTracking)
        {
            submissions = submissions.AsNoTracking();
            executions = executions.AsNoTracking();
        }
        var submission = await submissions.SingleOrDefaultAsync(value => value.Id == submissionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Assessment submission was not found.");
        var execution = await executions.SingleOrDefaultAsync(value => value.AssessmentSubmissionId == submissionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Assessment submission does not have an official grading execution.");
        if (execution.ExecutionContext != ReviewExecutionContext.OfficialSubmission)
        {
            throw new InvalidOperationException("Assessment submission is not bound to an official execution.");
        }

        return new OfficialExecution(submission, execution);
    }

    private async Task RequireSubmissionActorAsync(
        AssessmentSubmission submission,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (submission.UserId == actorId)
        {
            return;
        }

        if (!submission.IsCollective)
        {
            throw new UnauthorizedAccessException("Submission belongs to another learner.");
        }

        var participant = await context.Set<AssessmentSubmissionParticipant>()
            .AsNoTracking()
            .AnyAsync(value => value.SubmissionId == submission.Id && value.UserId == actorId, cancellationToken)
            .ConfigureAwait(false);
        if (!participant)
        {
            throw new UnauthorizedAccessException("Actor is not in the frozen group participant snapshot.");
        }
    }

    private async Task RequireMembershipAsync(
        Guid courseId,
        Guid enrollmentId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var enrollmentExists = await context.Set<Enrollment>()
            .AsNoTracking()
            .AnyAsync(value => value.Id == enrollmentId && value.CourseId == courseId && value.UserId == userId &&
                               value.Status != GameGuild.Learning.Enrollments.EnrollmentStatus.Dropped &&
                               value.Status != GameGuild.Learning.Enrollments.EnrollmentStatus.Expired,
                cancellationToken)
            .ConfigureAwait(false);
        if (enrollmentExists)
        {
            return;
        }

        var canonicalEnrollmentExists = await ProgramEnrollmentAssessmentMembership.ActiveForCourse(context, courseId)
            .AnyAsync(value => value.Id == enrollmentId && value.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (canonicalEnrollmentExists)
        {
            return;
        }

        var programUserExists = await context.Set<ProgramUser>()
            .AsNoTracking()
            .AnyAsync(value => value.Id == enrollmentId && value.ProgramId == courseId && value.UserId == userId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!programUserExists)
        {
            throw new UnauthorizedAccessException("Active course membership was not found.");
        }
    }

    private async Task<Guid> ResolveMembershipIdAsync(
        Guid courseId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var enrollmentId = await context.Set<Enrollment>()
            .AsNoTracking()
            .Where(value => value.CourseId == courseId && value.UserId == userId &&
                            value.Status != GameGuild.Learning.Enrollments.EnrollmentStatus.Dropped &&
                            value.Status != GameGuild.Learning.Enrollments.EnrollmentStatus.Expired)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (enrollmentId.HasValue)
        {
            return enrollmentId.Value;
        }

        var canonicalEnrollmentId = await ProgramEnrollmentAssessmentMembership.ActiveForCourse(context, courseId)
            .Where(value => value.UserId == userId)
            .Select(value => (Guid?)value.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (canonicalEnrollmentId.HasValue)
        {
            return canonicalEnrollmentId.Value;
        }

        var programUserId = await context.Set<ProgramUser>()
            .AsNoTracking()
            .Where(value => value.ProgramId == courseId && value.UserId == userId)
            .Select(value => value.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return programUserId != Guid.Empty
            ? programUserId
            : throw new InvalidOperationException($"Group member {userId} does not have an active course membership.");
    }

    private async Task<Assessment> RequirePublishedAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var assessment = await RequireAssessmentAsync(assessmentId, cancellationToken).ConfigureAwait(false);
        if (!assessment.PublishedDefinitionRevisionId.HasValue)
        {
            throw new InvalidOperationException("Assessment does not have a published executable revision.");
        }

        return assessment;
    }

    private async Task<Assessment> RequireAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken) =>
        await context.Set<Assessment>()
            .SingleOrDefaultAsync(value => value.Id == assessmentId && value.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new KeyNotFoundException("Assessment was not found.");

    private async Task<AssessmentDefinitionRevision> RequireRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken) =>
        await context.Set<AssessmentDefinitionRevision>()
            .SingleAsync(value => value.Id == revisionId, cancellationToken)
            .ConfigureAwait(false);

    private static void ValidateStartPolicy(AssessmentExecutionPolicyV1 policy)
    {
        if (!TryGetSubmissionTiming(policy, SystemClock.UtcNow, SystemClock.UtcNow, out _))
        {
            throw new InvalidOperationException("Assessment is not currently available.");
        }

        if (policy.MaxAttempts != 1)
        {
            throw new InvalidOperationException("The current runtime supports exactly one attempt.");
        }
    }

    private static bool TryGetSubmissionTiming(
        AssessmentExecutionPolicyV1 policy,
        DateTime startedAt,
        DateTime submittedAt,
        out bool isLate)
    {
        var timestamp = submittedAt.ToUniversalTime();
        if (policy.TimeLimitMinutes.HasValue &&
            timestamp > startedAt.ToUniversalTime().AddMinutes(policy.TimeLimitMinutes.Value))
        {
            isLate = false;
            return false;
        }

        var availableFrom = ParseInstant(policy.Availability.AvailableFrom);
        var availableUntil = ParseInstant(policy.Availability.AvailableUntil);
        var dueAt = ParseInstant(policy.Availability.DueAt);
        var lateDeadline = ParseInstant(policy.Availability.LateSubmissionDeadline);
        if (availableFrom.HasValue && timestamp < availableFrom.Value ||
            availableUntil.HasValue && timestamp > availableUntil.Value)
        {
            isLate = false;
            return false;
        }

        isLate = dueAt.HasValue && timestamp > dueAt.Value;
        return !isLate || policy.Availability.AllowLateSubmissions &&
            lateDeadline.HasValue && timestamp <= lateDeadline.Value;
    }

    private static DateTime? ParseInstant(string? value) =>
        value is null
            ? null
            : DateTime.Parse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                System.Globalization.DateTimeStyles.AdjustToUniversal);

    private async Task<ResourceOutcome?> FindReceiptAsync(
        Guid tenantId,
        Guid resourceId,
        string commandType,
        Guid actorId,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var key = RequireIdempotencyKey(idempotencyKey);
        var receipt = await context.Set<GradingCommandReceipt>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.TenantId == tenantId &&
                value.ResourceId == resourceId &&
                value.CommandType == commandType &&
                value.ActorId == actorId &&
                value.IdempotencyKey == key,
                cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null && !string.Equals(receipt.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency key was used with a different request.");
        }

        if (receipt is null)
        {
            return null;
        }

        return Deserialize<ResourceOutcome>(receipt.OutcomeCanonicalJson);
    }

    private void AddReceipt(
        Guid tenantId,
        Guid resourceId,
        string commandType,
        Guid actorId,
        string idempotencyKey,
        string requestHash,
        ResourceOutcome outcome)
    {
        context.Set<GradingCommandReceipt>().Add(GradingCommandReceipt.Create(
            tenantId,
            resourceId,
            commandType,
            actorId,
            RequireIdempotencyKey(idempotencyKey),
            requestHash,
            "1",
            Serialize(outcome),
            SystemClock.UtcNow.AddDays(90)));
    }

    private Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        context is DbContext dbContext && dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null
            ? BeginRelationalTransactionAsync(context, cancellationToken)
            : Task.FromResult<IDbContextTransaction?>(null);

    private static async Task<IDbContextTransaction?> BeginRelationalTransactionAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken) =>
        await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction is null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);

    private static Guid RequireTenant(Guid? tenantId) =>
        tenantId is { } value && value != Guid.Empty
            ? value
            : throw new InvalidOperationException("Grading runtime requires a tenant-scoped assessment.");

    private static string RequireText(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", parameterName) : value.Trim();

    private static string RequireIdempotencyKey(string value)
    {
        var normalized = RequireText(value, nameof(value));
        return normalized.Length <= 200
            ? normalized
            : throw new ArgumentException("Idempotency key cannot exceed 200 characters.", nameof(value));
    }

    private static string Hash<T>(T value) =>
        CanonicalJson.Sha256(JsonSerializer.SerializeToElement(value, GradingJson.Options));

    private static string Serialize<T>(T value) =>
        CanonicalJson.Serialize(JsonSerializer.SerializeToElement(value, GradingJson.Options));

    private static T Deserialize<T>(string value) =>
        JsonSerializer.Deserialize<T>(value, GradingJson.Options)
        ?? throw new JsonException("Persisted grading payload is invalid.");

    private sealed record OwnedTestExecution(
        AssessmentTestRun Run,
        AssessmentTestRunSubject Subject,
        GradingExecution Execution);

    private sealed record OfficialExecution(
        AssessmentSubmission Submission,
        GradingExecution Execution);

    private record ResourceOutcome(int SchemaVersion, Guid ResourceId);

}
