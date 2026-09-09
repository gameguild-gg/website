using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Capabilities;
using GameGuild.Learning.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments.Grading.Authoring;

public sealed record SaveAssessmentDraftRequest(
    int ExpectedContentVersion,
    int? ExpectedAssessmentVersion,
    string Title,
    string Slug,
    string? Description,
    JsonElement Document,
    Visibility Visibility,
    bool IsRequired,
    int? EstimatedMinutes,
    EstimatedMinutesSource EstimatedMinutesSource,
    ReviewMethods? ReviewMethods = null,
    ScoreValue? PassingScore = null,
    int? TimeLimitMinutes = null,
    int MaxAttempts = 1,
    AssessmentPresentationMode PresentationMode = AssessmentPresentationMode.SingleStep,
    ContentCompletionMode ContentCompletionMode = ContentCompletionMode.OnReleaseAndPass,
    ResultReleaseMode ResultReleaseMode = ResultReleaseMode.Manual,
    DateTime? ResultReleaseScheduledFor = null,
    AttemptContributionMode? AttemptContributionMode = null,
    string? ReviewConfigurationCanonicalJson = null);

public sealed record AssessmentDraftResult(Guid ContentId, int ContentVersion, Guid? AssessmentId, int? AssessmentVersion);

public sealed record PreparedAssessmentRevisionResult(
    Guid RevisionId,
    int RevisionNumber,
    string AuthoringSourceHash,
    string ExecutionSnapshotHash);

public sealed record AssessmentRevisionStateResult(
    Guid RevisionId,
    int RevisionNumber,
    string AuthoringSourceHash,
    string ExecutionSnapshotHash,
    DateTime CreatedAt);

public sealed record AssessmentCapabilityStateResult(
    bool Available,
    string? Code = null,
    string? Message = null);

public sealed record AssessmentAuthoringStateResult(
    Guid AssessmentId,
    int AssessmentVersion,
    string Lifecycle,
    string CurrentAuthoringSourceHash,
    AssessmentRevisionStateResult? Candidate,
    AssessmentRevisionStateResult? Published,
    bool CandidateMatchesDraft,
    bool PublishedMatchesDraft,
    AssessmentCapabilityStateResult Prepare,
    AssessmentCapabilityStateResult Publish);

public sealed record PrepareAssessmentRevisionRequest(int ExpectedAssessmentVersion);

public sealed record PublishAssessmentRevisionRequest(Guid RevisionId, int ExpectedAssessmentVersion);

public sealed record UnpublishAssessmentRevisionRequest(
    Guid ExpectedRevisionId,
    int ExpectedAssessmentVersion,
    string IdempotencyKey);

public interface IAssessmentAuthoringService
{
    Task<Result<AssessmentDraftResult>> SaveDraftAsync(
        Guid courseId,
        Guid contentId,
        Guid actorId,
        SaveAssessmentDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AssessmentAuthoringStateResult>> GetStateAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);

    Task<Result<PreparedAssessmentRevisionResult>> PrepareAsync(
        Guid assessmentId,
        Guid actorId,
        PrepareAssessmentRevisionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PreparedAssessmentRevisionResult>> PublishAsync(
        Guid assessmentId,
        Guid actorId,
        PublishAssessmentRevisionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> UnpublishAsync(
        Guid assessmentId,
        Guid actorId,
        UnpublishAssessmentRevisionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AssessmentAuthoringService(
    IApplicationDbContext context,
    IAssessmentTypeAdapterResolver assessmentTypeAdapters,
    IAssessmentExecutionPolicyResolver executionPolicies,
    IReviewStageHandlerResolver stageHandlers,
    IEnumerable<IReviewStageHandler> registeredStageHandlers,
    IAcademicOutboxWriter outbox,
    ILogger<AssessmentAuthoringService> logger) : IAssessmentAuthoringService
{

    public async Task<Result<AssessmentAuthoringStateResult>> GetStateAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await MaterializeAuthoringAsync(assessmentId, cancellationToken).ConfigureAwait(false);
            if (!source.IsSuccess) return Result.Failure<AssessmentAuthoringStateResult>(source.Error);

            var revisions = await context.Set<AssessmentDefinitionRevision>()
                .AsNoTracking()
                .Where(revision => revision.AssessmentId == assessmentId)
                .OrderByDescending(revision => revision.RevisionNumber)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            var candidate = revisions.FirstOrDefault();
            var published = source.Value.Assessment.PublishedDefinitionRevisionId is { } publishedId
                ? revisions.FirstOrDefault(revision => revision.Id == publishedId)
                : null;
            if (source.Value.Assessment.PublishedDefinitionRevisionId.HasValue && published is null)
            {
                return Result.Failure<AssessmentAuthoringStateResult>(Error.Conflict(
                    "AssessmentRevision.PublishedRevisionMissing",
                    "The assessment points to a missing published revision."));
            }

            var candidateMatchesDraft = candidate is not null &&
                string.Equals(candidate.AuthoringSourceHash, source.Value.AuthoringHash, StringComparison.Ordinal);
            var publishedMatchesDraft = published is not null &&
                string.Equals(published.AuthoringSourceHash, source.Value.AuthoringHash, StringComparison.Ordinal);

            var prepareCapability = ProbeCapability(() =>
            {
                var manifest = BuildManifest(source.Value.Projection, source.Value.Policy, ReviewExecutionContext.AuthorTest);
                GradingContractValidator.Validate(manifest);
            }, "AssessmentRevision.AuthorTestUnavailable");

            AssessmentCapabilityStateResult publishCapability;
            if (!candidateMatchesDraft)
            {
                publishCapability = new AssessmentCapabilityStateResult(
                    false,
                    candidate is null ? "AssessmentRevision.CandidateRequired" : "AssessmentRevision.ChangesPending",
                    candidate is null
                        ? "Prepare a candidate revision before publishing."
                        : "The draft changed after the latest candidate was prepared.");
            }
            else
            {
                publishCapability = ProbeCapability(
                    () => ValidatePersistedSnapshot(candidate!, ReviewExecutionContext.OfficialSubmission),
                    "AssessmentRevision.OfficialSubmissionUnavailable");
            }

            var lifecycle = published is not null
                ? publishedMatchesDraft ? "published" : "changes-pending"
                : candidateMatchesDraft ? "candidate" : "draft";
            return Result.Success(new AssessmentAuthoringStateResult(
                assessmentId,
                source.Value.Assessment.Version,
                lifecycle,
                source.Value.AuthoringHash,
                ToState(candidate),
                ToState(published),
                candidateMatchesDraft,
                publishedMatchesDraft,
                prepareCapability,
                publishCapability));
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Assessment authoring state could not be resolved for {AssessmentId}", assessmentId);
            return Result.Failure<AssessmentAuthoringStateResult>(Error.Validation("AssessmentRevision.State", exception.Message));
        }
    }

    public async Task<Result<AssessmentDraftResult>> SaveDraftAsync(
        Guid courseId,
        Guid contentId,
        Guid actorId,
        SaveAssessmentDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (actorId == Guid.Empty)
                return Result.Failure<AssessmentDraftResult>(Error.Validation("AssessmentAuthoring.Actor", "An authenticated actor is required."));
            await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var contentLifecycleLock = await AssessmentLifecycleDatabaseLock
                .AcquireAsync(context, contentId, cancellationToken)
                .ConfigureAwait(false);
            var content = await context.Set<ProgramContent>()
                .FirstOrDefaultAsync(item => item.Id == contentId && item.ProgramId == courseId && item.DeletedAt == null, cancellationToken)
                .ConfigureAwait(false);
            if (content is null) return Result.Failure<AssessmentDraftResult>(Error.NotFound("ProgramContent", "Assessment content was not found."));
            if (content.Version != request.ExpectedContentVersion)
                return Result.Failure<AssessmentDraftResult>(Error.Conflict("AssessmentAuthoring.ContentVersion", "Assessment content changed before it was saved."));

            var adapter = assessmentTypeAdapters.ResolveForAuthoring(content.Type);
            var projection = adapter.ProjectAuthoring(request.Document);

            var assessment = await context.Set<Assessment>()
                .FirstOrDefaultAsync(value => value.ContentId == contentId && value.DeletedAt == null, cancellationToken)
                .ConfigureAwait(false);
            if (assessment is not null)
            {
                await AssessmentLifecycleDatabaseLock
                    .AcquireAsync(context, assessment.Id, cancellationToken)
                    .ConfigureAwait(false);
            }
            if (assessment is not null && request.ExpectedAssessmentVersion != assessment.Version)
                return Result.Failure<AssessmentDraftResult>(Error.Conflict("AssessmentAuthoring.AssessmentVersion", "Assessment policy changed before it was saved."));
            if (assessment is null && request.ExpectedAssessmentVersion.HasValue)
                return Result.Failure<AssessmentDraftResult>(Error.Conflict("AssessmentAuthoring.AssessmentMissing", "The linked assessment no longer exists."));
            if (assessment is not null && assessment.Type != adapter.AssessmentType)
                return Result.Failure<AssessmentDraftResult>(Error.Conflict("AssessmentAuthoring.TypeMismatch", "The linked assessment type does not match its content adapter."));

            ApplyContent(content, request);

            if (projection.Grading is null)
            {
                if (assessment is not null)
                {
                    if (assessment.PublishedDefinitionRevisionId.HasValue)
                        assessment.UnpublishRevision(assessment.PublishedDefinitionRevisionId.Value, assessment.Version);
                    assessment.SoftDelete();
                    context.Set<Assessment>().Update(assessment);
                }

                context.Set<ProgramContent>().Update(content);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result.Success(new AssessmentDraftResult(content.Id, content.Version, null, null));
            }

            if (projection.Items.Count == 0)
                return Result.Failure<AssessmentDraftResult>(Error.Validation("AssessmentAuthoring.Empty", "Graded content requires at least one assessable item."));

            var methods = request.ReviewMethods ?? assessment?.ReviewMethods ??
                (Contracts.ReviewMethods.AutomatedReview | Contracts.ReviewMethods.InstructorReview);
            methods.EnsureValid();
            var reviewConfiguration = NormalizeReviewConfiguration(methods, request.ReviewConfigurationCanonicalJson ?? assessment?.ReviewConfigurationCanonicalJson);
            if (assessment is null)
            {
                assessment = Assessment.Create(
                    courseId,
                    request.Title,
                    adapter.AssessmentType,
                    projection.MaxScore,
                    request.IsRequired,
                    contentId: contentId,
                    reviewMethods: methods,
                    slug: request.Slug);
                assessment.TenantId = content.TenantId;
                context.Set<Assessment>().Add(assessment);
            }

            assessment.Update(
                title: request.Title,
                description: request.Description,
                clearDescription: request.Description is null,
                maxScore: projection.MaxScore,
                passingScore: request.PassingScore ?? assessment.PassingScore,
                timeLimitMinutes: request.TimeLimitMinutes,
                clearTimeLimitMinutes: request.TimeLimitMinutes is null,
                maxAttempts: request.MaxAttempts,
                isRequired: request.IsRequired,
                availableFrom: assessment.AvailableFrom,
                clearAvailableFrom: false,
                availableUntil: assessment.AvailableUntil,
                clearAvailableUntil: false,
                contentId: contentId,
                submissionModalities: adapter.SubmissionModalities,
                presentationMode: request.PresentationMode,
                dueAt: assessment.DueAt,
                allowLateSubmissions: assessment.AllowLateSubmissions,
                lateSubmissionDeadline: assessment.LateSubmissionDeadline,
                reviewMethods: methods,
                reviewConfigurationCanonicalJson: reviewConfiguration,
                attemptContributionMode: request.AttemptContributionMode ?? assessment.AttemptContributionMode,
                contentCompletionMode: request.ContentCompletionMode,
                resultReleaseMode: request.ResultReleaseMode,
                resultReleaseScheduledFor: request.ResultReleaseScheduledFor,
                slug: request.Slug);

            context.Set<ProgramContent>().Update(content);
            if (assessment.Version > 0) context.Set<Assessment>().Update(assessment);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return Result.Success(new AssessmentDraftResult(content.Id, content.Version, assessment.Id, assessment.Version));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AssessmentDraftResult>(Error.Conflict("AssessmentAuthoring.ConcurrentWrite", "Assessment authoring data changed before commit."));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Concurrent assessment draft save failed for content {ContentId}", contentId);
            return Result.Failure<AssessmentDraftResult>(Error.Conflict("AssessmentAuthoring.ConcurrentWrite", "Assessment authoring data changed before commit."));
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or InvalidOperationException)
        {
            return Result.Failure<AssessmentDraftResult>(Error.Validation("AssessmentAuthoring.Invalid", exception.Message));
        }
    }

    public async Task<Result<PreparedAssessmentRevisionResult>> PrepareAsync(
        Guid assessmentId,
        Guid actorId,
        PrepareAssessmentRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (actorId == Guid.Empty)
                return Result.Failure<PreparedAssessmentRevisionResult>(Error.Validation("AssessmentRevision.Actor", "An authenticated actor is required."));
            await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var lifecycleLock = await AssessmentLifecycleDatabaseLock
                .AcquireAsync(context, assessmentId, cancellationToken)
                .ConfigureAwait(false);
            var materialized = await MaterializeAsync(assessmentId, ReviewExecutionContext.AuthorTest, cancellationToken).ConfigureAwait(false);
            if (!materialized.IsSuccess) return Result.Failure<PreparedAssessmentRevisionResult>(materialized.Error);
            if (materialized.Value.Assessment.Version != request.ExpectedAssessmentVersion)
                return Result.Failure<PreparedAssessmentRevisionResult>(Error.Conflict("AssessmentRevision.ConcurrentWrite", "Assessment policy changed before prepare."));

            var latest = await context.Set<AssessmentDefinitionRevision>()
                .Where(revision => revision.AssessmentId == assessmentId)
                .OrderByDescending(revision => revision.RevisionNumber)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (latest is not null &&
                latest.AuthoringSourceHash == materialized.Value.AuthoringHash &&
                latest.ExecutionSnapshotHash == materialized.Value.ExecutionHash)
            {
                await CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result.Success(ToResult(latest));
            }

            var revision = AssessmentDefinitionRevision.Create(
                materialized.Value.Assessment.TenantId,
                assessmentId,
                (latest?.RevisionNumber ?? 0) + 1,
                materialized.Value.AuthoringCanonicalJson,
                materialized.Value.ExecutionCanonicalJson,
                actorId);
            context.Set<AssessmentDefinitionRevision>().Add(revision);
            AddOutbox(materialized.Value.Assessment, "assessment-definition-prepared", new
            {
                schemaVersion = 1,
                assessmentId,
                revisionId = revision.Id,
                revisionNumber = revision.RevisionNumber,
                revision.AuthoringSourceHash,
                revision.ExecutionSnapshotHash,
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return Result.Success(ToResult(revision));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PreparedAssessmentRevisionResult>(Error.Conflict("AssessmentRevision.ConcurrentWrite", "Assessment policy changed before prepare."));
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Concurrent assessment revision prepare failed for {AssessmentId}", assessmentId);
            return Result.Failure<PreparedAssessmentRevisionResult>(Error.Conflict("AssessmentRevision.ConcurrentPrepare", "Another revision was prepared concurrently."));
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Assessment revision prepare failed for {AssessmentId}", assessmentId);
            return Result.Failure<PreparedAssessmentRevisionResult>(Error.Validation("AssessmentRevision.Prepare", exception.Message));
        }
    }

    public async Task<Result<PreparedAssessmentRevisionResult>> PublishAsync(
        Guid assessmentId,
        Guid actorId,
        PublishAssessmentRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (actorId == Guid.Empty)
                return Result.Failure<PreparedAssessmentRevisionResult>(Error.Validation("AssessmentRevision.Actor", "An authenticated actor is required."));
            await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var lifecycleLock = await AssessmentLifecycleDatabaseLock
                .AcquireAsync(context, assessmentId, cancellationToken)
                .ConfigureAwait(false);
            var assessment = await context.Set<Assessment>()
                .FirstOrDefaultAsync(value => value.Id == assessmentId && value.DeletedAt == null, cancellationToken)
                .ConfigureAwait(false);
            if (assessment is null) return Result.Failure<PreparedAssessmentRevisionResult>(Error.NotFound("Assessment", "Assessment was not found."));
            var revision = await context.Set<AssessmentDefinitionRevision>()
                .FirstOrDefaultAsync(value => value.Id == request.RevisionId && value.AssessmentId == assessmentId, cancellationToken)
                .ConfigureAwait(false);
            if (revision is null) return Result.Failure<PreparedAssessmentRevisionResult>(Error.NotFound("AssessmentRevision", "Prepared revision was not found."));

            var current = await MaterializeAsync(assessmentId, ReviewExecutionContext.AuthorTest, cancellationToken).ConfigureAwait(false);
            if (!current.IsSuccess) return Result.Failure<PreparedAssessmentRevisionResult>(current.Error);
            if (!string.Equals(current.Value.AuthoringHash, revision.AuthoringSourceHash, StringComparison.Ordinal))
                return Result.Failure<PreparedAssessmentRevisionResult>(Error.Conflict("AssessmentRevision.ChangesPending", "The draft changed after this revision was prepared."));

            ValidatePersistedSnapshot(revision, ReviewExecutionContext.OfficialSubmission);
            assessment.PublishRevision(revision.Id, request.ExpectedAssessmentVersion);
            context.Set<Assessment>().Update(assessment);
            AddOutbox(assessment, "assessment-definition-published", new
            {
                schemaVersion = 1,
                assessmentId,
                revisionId = revision.Id,
                actorId,
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return Result.Success(ToResult(revision));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PreparedAssessmentRevisionResult>(Error.Conflict("AssessmentRevision.ConcurrentPublish", "The active assessment revision changed before publish."));
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Assessment revision publish failed for {AssessmentId}", assessmentId);
            return Result.Failure<PreparedAssessmentRevisionResult>(Error.Validation("AssessmentRevision.Publish", exception.Message));
        }
    }

    public async Task<Result> UnpublishAsync(
        Guid assessmentId,
        Guid actorId,
        UnpublishAssessmentRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (actorId == Guid.Empty)
                return Result.Failure(Error.Validation("AssessmentRevision.Actor", "An authenticated actor is required."));
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 200)
                return Result.Failure(Error.Validation("AssessmentRevision.IdempotencyKey", "Idempotency key must contain 1 to 200 characters."));
            await using var transaction = await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var lifecycleLock = await AssessmentLifecycleDatabaseLock
                .AcquireAsync(context, assessmentId, cancellationToken)
                .ConfigureAwait(false);
            var assessment = await context.Set<Assessment>()
                .FirstOrDefaultAsync(value => value.Id == assessmentId && value.DeletedAt == null, cancellationToken)
                .ConfigureAwait(false);
            if (assessment is null) return Result.Failure(Error.NotFound("Assessment", "Assessment was not found."));
            if (!assessment.TenantId.HasValue)
                return Result.Failure(Error.Validation("AssessmentRevision.Tenant", "A tenant-scoped assessment is required."));

            var requestElement = JsonSerializer.SerializeToElement(new
            {
                schemaVersion = 1,
                assessmentId,
                request.ExpectedRevisionId,
                request.ExpectedAssessmentVersion,
            }, GradingJson.Options);
            var requestHash = CanonicalJson.Sha256(requestElement);
            var idempotencyKey = request.IdempotencyKey.Trim();
            const string commandType = "assessment-definition-unpublish";
            var receipt = await context.Set<GradingCommandReceipt>()
                .AsNoTracking()
                .FirstOrDefaultAsync(value =>
                    value.TenantId == assessment.TenantId.Value &&
                    value.ResourceId == assessmentId &&
                    value.CommandType == commandType &&
                    value.ActorId == actorId &&
                    value.IdempotencyKey == idempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (receipt is not null)
            {
                if (!string.Equals(receipt.RequestHash, requestHash, StringComparison.Ordinal))
                    return Result.Failure(Error.Conflict("AssessmentRevision.IdempotencyConflict", "The idempotency key was already used with a different request."));
                await CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
                return Result.Success();
            }

            assessment.UnpublishRevision(request.ExpectedRevisionId, request.ExpectedAssessmentVersion);
            context.Set<Assessment>().Update(assessment);
            AddOutbox(assessment, "assessment-definition-unpublished", new
            {
                schemaVersion = 1,
                assessmentId,
                revisionId = request.ExpectedRevisionId,
                actorId,
                idempotencyKey,
            });
            var outcomeElement = JsonSerializer.SerializeToElement(new
            {
                schemaVersion = 1,
                assessmentId,
                revisionId = request.ExpectedRevisionId,
                unpublished = true,
            }, GradingJson.Options);
            context.Set<GradingCommandReceipt>().Add(GradingCommandReceipt.Create(
                assessment.TenantId.Value,
                assessmentId,
                commandType,
                actorId,
                idempotencyKey,
                requestHash,
                "1",
                CanonicalJson.Serialize(outcomeElement),
                SystemClock.UtcNow.AddDays(90)));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure(Error.Conflict("AssessmentRevision.Unpublish", exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict("AssessmentRevision.ConcurrentUnpublish", "The active assessment revision changed before unpublish."));
        }
    }

    private async Task<Result<MaterializedRevision>> MaterializeAsync(
        Guid assessmentId,
        ReviewExecutionContext contextKind,
        CancellationToken cancellationToken)
    {
        var source = await MaterializeAuthoringAsync(assessmentId, cancellationToken).ConfigureAwait(false);
        if (!source.IsSuccess) return Result.Failure<MaterializedRevision>(source.Error);

        var manifest = BuildManifest(source.Value.Projection, source.Value.Policy, contextKind);
        var projections = source.Value.Projection.Items.ToDictionary(item => item.ItemId, item => item.PrivateProjection, StringComparer.Ordinal);
        var snapshot = new AssessmentExecutionSnapshotV1(1, source.Value.Authoring, manifest, projections);
        GradingContractValidator.Validate(snapshot);

        var snapshotElement = JsonSerializer.SerializeToElement(snapshot, GradingJson.Options);
        var executionCanonical = CanonicalJson.Serialize(snapshotElement);
        return Result.Success(new MaterializedRevision(
            source.Value.Assessment,
            source.Value.AuthoringCanonicalJson,
            source.Value.AuthoringHash,
            executionCanonical,
            CanonicalJson.HashExecutionSnapshot(snapshotElement)));
    }

    private async Task<Result<MaterializedAuthoring>> MaterializeAuthoringAsync(
        Guid assessmentId,
        CancellationToken cancellationToken)
    {
        var assessment = await context.Set<Assessment>()
            .FirstOrDefaultAsync(value => value.Id == assessmentId && value.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (assessment?.ContentId is not { } contentId)
            return Result.Failure<MaterializedAuthoring>(Error.NotFound("Assessment", "A content-linked assessment was not found."));
        var content = await context.Set<ProgramContent>()
            .FirstOrDefaultAsync(value => value.Id == contentId && value.ProgramId == assessment.CourseId && value.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (content?.JsonBody is null)
            return Result.Failure<MaterializedAuthoring>(Error.NotFound("ProgramContent", "Assessment authoring content was not found."));

        using var contentDocument = JsonDocument.Parse(content.JsonBody);
        var adapter = assessmentTypeAdapters.ResolveForAuthoring(content.Type);
        if (adapter.AssessmentType != assessment.Type)
            return Result.Failure<MaterializedAuthoring>(Error.Conflict("AssessmentRevision.TypeMismatch", "The assessment type does not match its content adapter."));
        var projection = adapter.ProjectAuthoring(contentDocument.RootElement);
        if (projection.Grading is null)
            return Result.Failure<MaterializedAuthoring>(Error.Validation("AssessmentRevision.GradingDisabled", "Grading is not enabled for this content."));

        var policy = BuildPolicy(assessment);
        var authoring = new AssessmentAuthoringSourceV1(1, projection.ContentType, projection.Content, projection.Grading, policy);
        var authoringElement = JsonSerializer.SerializeToElement(authoring, GradingJson.Options);
        var authoringCanonical = CanonicalJson.Serialize(authoringElement);
        return Result.Success(new MaterializedAuthoring(
            assessment,
            projection,
            policy,
            authoring,
            authoringCanonical,
            CanonicalJson.HashAuthoringSource(authoringElement)));
    }

    private AssessmentExecutionManifestV1 BuildManifest(
        AssessmentAuthoringProjectionV1 projection,
        AssessmentExecutionPolicyV1 policy,
        ReviewExecutionContext contextKind)
    {
        var items = projection.Items.Select(item =>
        {
            assessmentTypeAdapters.Resolve(projection.ContentType, item.AdapterKey, item.AdapterVersion, contextKind);
            return new AssessmentItemManifestV1(
                item.ItemId,
                item.ItemType,
                item.AdapterKey,
                item.AdapterVersion);
        }).ToArray();

        var handlers = registeredStageHandlers.ToArray();
        var stages = policy.Review.Methods.ToSequence().Select(method =>
        {
            var candidates = handlers.Where(handler => handler.Method == method && handler.Contexts.Contains(contextKind)).Take(2).ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException($"Exactly one {method} handler must be registered for {contextKind}.");
            var handler = candidates[0];
            stageHandlers.Resolve(method, handler.Key, handler.Version, contextKind);
            return new AssessmentReviewStageManifestV1(
                method,
                handler.Key,
                handler.Version,
                handler.ProviderKey,
                handler.ProviderPolicyVersion);
        }).ToArray();

        executionPolicies.Resolve(
            AssessmentExecutionPolicyCapability.Key,
            AssessmentExecutionPolicyCapability.Version,
            contextKind);

        return new AssessmentExecutionManifestV1(
            GradingContractVersions.ExecutionManifest,
            items,
            stages,
            [new AssessmentPolicyManifestV1(
                AssessmentExecutionPolicyCapability.Key,
                AssessmentExecutionPolicyCapability.Version)]);
    }

    private void ValidatePersistedSnapshot(AssessmentDefinitionRevision revision, ReviewExecutionContext contextKind)
    {
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        foreach (var item in snapshot.Manifest.Items)
        {
            assessmentTypeAdapters.Resolve(snapshot.AuthoringSource.ContentType, item.AdapterKey, item.AdapterVersion, contextKind);
        }
        foreach (var stage in snapshot.Manifest.Stages)
        {
            stageHandlers.Resolve(stage.Method, stage.HandlerKey, stage.HandlerVersion, contextKind);
        }
        foreach (var policy in snapshot.Manifest.Policies)
        {
            executionPolicies.Resolve(policy.PolicyKey, policy.PolicyVersion, contextKind);
        }
    }

    private static AssessmentExecutionPolicyV1 BuildPolicy(Assessment assessment)
    {
        var configuration = ReadReviewConfiguration(assessment.ReviewConfigurationCanonicalJson);
        var review = new AssessmentReviewPolicyV1(
            1,
            assessment.ReviewMethods,
            configuration.Peer,
            configuration.Ai,
            configuration.Self,
            configuration.Instructor);
        return new AssessmentExecutionPolicyV1(
            GradingContractVersions.ExecutionPolicy,
            assessment.PassingScore,
            assessment.MaxAttempts,
            assessment.AttemptContributionMode.HasValue
                ? new AttemptContributionPolicyV1(assessment.AttemptContributionMode.Value)
                : null,
            assessment.TimeLimitMinutes,
            new AssessmentAvailabilityPolicyV1(
                FormatInstant(assessment.AvailableFrom),
                FormatInstant(assessment.AvailableUntil),
                FormatInstant(assessment.DueAt),
                assessment.AllowLateSubmissions,
                FormatInstant(assessment.LateSubmissionDeadline)),
            new AssessmentContentCompletionPolicyV1(assessment.ContentCompletionMode),
            new AssessmentResultReleasePolicyV1(assessment.ResultReleaseMode, FormatInstant(assessment.ResultReleaseScheduledFor)),
            new AssessmentPresentationPolicyV1(assessment.PresentationMode == AssessmentPresentationMode.Continuous ? "continuous" : "single-step"),
            review);
    }

    private static AssessmentReviewConfigurationV1 ReadReviewConfiguration(string? canonicalJson)
    {
        if (string.IsNullOrWhiteSpace(canonicalJson))
            return new AssessmentReviewConfigurationV1(1);
        return JsonSerializer.Deserialize<AssessmentReviewConfigurationV1>(canonicalJson, GradingJson.Options)
            ?? throw new JsonException("Review configuration is required.");
    }

    private static string NormalizeReviewConfiguration(ReviewMethods methods, string? canonicalJson)
    {
        var current = ReadReviewConfiguration(canonicalJson);
        var normalized = new
        {
            schemaVersion = 1,
            peer = methods.HasFlag(Contracts.ReviewMethods.PeerReview) ? current.Peer : null,
            ai = methods.HasFlag(Contracts.ReviewMethods.AIReview) ? current.Ai : null,
            self = methods.HasFlag(Contracts.ReviewMethods.SelfReview) ? current.Self : null,
            instructor = methods.HasFlag(Contracts.ReviewMethods.InstructorReview)
                ? current.Instructor ?? new InstructorReviewPolicyV1(false)
                : null,
        };
        var element = JsonSerializer.SerializeToElement(normalized, GradingJson.Options);
        return GradingContractValidator.NormalizeReviewConfiguration(methods, CanonicalJson.Serialize(element))!;
    }

    private static void ApplyContent(ProgramContent content, SaveAssessmentDraftRequest request)
    {
        content.Title = string.IsNullOrWhiteSpace(request.Title) ? throw new ArgumentException("Title is required.") : request.Title.Trim();
        content.Slug = string.IsNullOrWhiteSpace(request.Slug) ? request.Title.ToSlugCase() : request.Slug.Trim();
        content.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        content.JsonBody = request.Document.GetRawText();
        content.Body = null;
        content.Visibility = request.Visibility;
        content.IsRequired = request.IsRequired;
        content.EstimatedMinutes = request.EstimatedMinutes;
        content.EstimatedMinutesSource = request.EstimatedMinutesSource;
        content.NormalizeLearningContract();
        content.Touch();
    }

    private void AddOutbox(Assessment assessment, string eventType, object payload)
    {
        if (!assessment.TenantId.HasValue)
            throw new InvalidOperationException("Academic outbox events require a tenant-scoped assessment.");
        var element = JsonSerializer.SerializeToElement(payload, GradingJson.Options);
        outbox.Enqueue(
            assessment.TenantId.Value,
            eventType,
            "1",
            CanonicalJson.Serialize(element));
    }

    private static PreparedAssessmentRevisionResult ToResult(AssessmentDefinitionRevision revision) =>
        new(revision.Id, revision.RevisionNumber, revision.AuthoringSourceHash, revision.ExecutionSnapshotHash);

    private static AssessmentRevisionStateResult? ToState(AssessmentDefinitionRevision? revision) =>
        revision is null
            ? null
            : new AssessmentRevisionStateResult(
                revision.Id,
                revision.RevisionNumber,
                revision.AuthoringSourceHash,
                revision.ExecutionSnapshotHash,
                revision.CreatedAt);

    private static AssessmentCapabilityStateResult ProbeCapability(Action probe, string unavailableCode)
    {
        try
        {
            probe();
            return new AssessmentCapabilityStateResult(true);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or InvalidOperationException)
        {
            return new AssessmentCapabilityStateResult(false, unavailableCode, exception.Message);
        }
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        context is DbContext dbContext && dbContext.Database.IsRelational()
            ? await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction is null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);

    private static string? FormatInstant(DateTime? value) =>
        value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private sealed record MaterializedRevision(
        Assessment Assessment,
        string AuthoringCanonicalJson,
        string AuthoringHash,
        string ExecutionCanonicalJson,
        string ExecutionHash);

    private sealed record MaterializedAuthoring(
        Assessment Assessment,
        AssessmentAuthoringProjectionV1 Projection,
        AssessmentExecutionPolicyV1 Policy,
        AssessmentAuthoringSourceV1 Authoring,
        string AuthoringCanonicalJson,
        string AuthoringHash);
}
