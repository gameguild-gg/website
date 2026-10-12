using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Persistence;

public enum AssessmentTestRunStatus
{
    Draft,
    Running,
    Completed,
    Cancelled,
}

public enum PersistedGradingExecutionStatus
{
    Pending,
    Running,
    AwaitingReview,
    Completed,
    Failed,
}

public enum PersistedGradeRoundStatus
{
    Pending,
    Running,
    AwaitingEvidence,
    AwaitingInstructorResolution,
    Failed,
    Finalized,
}

public enum PersistedReviewStageStatus
{
    Pending,
    Running,
    AwaitingEvidence,
    AwaitingInstructorResolution,
    Completed,
    Failed,
}

public enum PersistedGradeItemState
{
    Graded,
    Pending,
    Unsupported,
}

public enum AcademicOutboxStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
}

public enum AcademicOutboxDeliveryStatus
{
    Pending,
    Processing,
    Confirmed,
    Failed,
}

public enum GradeResultReleaseStatus
{
    Released,
}

/// <summary>An immutable, executable assessment definition prepared by the server.</summary>
public sealed class AssessmentDefinitionRevision
{
    private AssessmentDefinitionRevision() { }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid AssessmentId { get; private set; }
    public int RevisionNumber { get; private set; }
    public int SchemaVersion { get; private set; }
    public string AuthoringSourceCanonicalJson { get; private set; } = string.Empty;
    public string AuthoringSourceHash { get; private set; } = string.Empty;
    public string AuthoringSourceHashVersion { get; private set; } = string.Empty;
    public string ExecutionSnapshotCanonicalJson { get; private set; } = string.Empty;
    public string ExecutionSnapshotHash { get; private set; } = string.Empty;
    public string ExecutionSnapshotHashVersion { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static AssessmentDefinitionRevision Create(
        Guid? tenantId,
        Guid assessmentId,
        int revisionNumber,
        string authoringSourceCanonicalJson,
        string executionSnapshotCanonicalJson,
        Guid createdByUserId)
    {
        if (assessmentId == Guid.Empty)
        {
            throw new ArgumentException("Assessment ID is required.", nameof(assessmentId));
        }

        if (revisionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Creator ID is required.", nameof(createdByUserId));
        }

        return new AssessmentDefinitionRevision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssessmentId = assessmentId,
            RevisionNumber = revisionNumber,
            SchemaVersion = 1,
            AuthoringSourceCanonicalJson = CanonicalPayload.Require(authoringSourceCanonicalJson, 4 * 1024 * 1024, "authoring source"),
            AuthoringSourceHash = CanonicalPayload.Hash(authoringSourceCanonicalJson),
            AuthoringSourceHashVersion = GradingContractVersions.Hash,
            ExecutionSnapshotCanonicalJson = CanonicalPayload.Require(executionSnapshotCanonicalJson, 8 * 1024 * 1024, "execution snapshot"),
            ExecutionSnapshotHash = CanonicalPayload.Hash(executionSnapshotCanonicalJson),
            ExecutionSnapshotHashVersion = GradingContractVersions.Hash,
            CreatedByUserId = createdByUserId,
            CreatedAt = SystemClock.UtcNow,
        };
    }
}

public sealed class AssessmentTestRun : EntityBase
{
    private AssessmentTestRun() { }

    public Guid AssessmentId { get; private set; }
    public Guid DefinitionRevisionId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public AssessmentTestRunStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public static AssessmentTestRun Create(Guid? tenantId, Guid assessmentId, Guid revisionId, Guid actorId)
    {
        if (assessmentId == Guid.Empty || revisionId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException("Assessment, revision, and actor IDs are required.");
        }

        var run = new AssessmentTestRun
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            DefinitionRevisionId = revisionId,
            CreatedByUserId = actorId,
            Status = AssessmentTestRunStatus.Draft,
        };
        run.TenantId = tenantId;
        return run;
    }

    public void Start()
    {
        if (Status == AssessmentTestRunStatus.Running)
        {
            return;
        }

        if (Status != AssessmentTestRunStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft test run can be started.");
        }

        Status = AssessmentTestRunStatus.Running;
        Touch();
    }

    public void Complete(DateTime completedAt)
    {
        if (Status == AssessmentTestRunStatus.Completed)
        {
            return;
        }

        if (Status != AssessmentTestRunStatus.Running)
        {
            throw new InvalidOperationException("Only a running test run can be completed.");
        }

        Status = AssessmentTestRunStatus.Completed;
        CompletedAt = completedAt.ToUniversalTime();
        Touch();
    }

    public void Cancel(DateTime completedAt)
    {
        if (Status == AssessmentTestRunStatus.Cancelled)
        {
            return;
        }

        if (Status == AssessmentTestRunStatus.Completed)
        {
            throw new InvalidOperationException("A completed test run cannot be cancelled.");
        }

        Status = AssessmentTestRunStatus.Cancelled;
        CompletedAt = completedAt.ToUniversalTime();
        Touch();
    }
}

public sealed class AssessmentTestRunSubject : EntityBase
{
    private AssessmentTestRunSubject() { }

    public Guid TestRunId { get; private set; }
    public string PersonaKey { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;

    public static AssessmentTestRunSubject Create(Guid? tenantId, Guid testRunId, string personaKey, string displayName)
    {
        if (testRunId == Guid.Empty)
        {
            throw new ArgumentException("Test run ID is required.", nameof(testRunId));
        }

        if (string.IsNullOrWhiteSpace(personaKey))
        {
            throw new ArgumentException("Persona key is required.", nameof(personaKey));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        var subject = new AssessmentTestRunSubject
        {
            Id = Guid.NewGuid(),
            TestRunId = testRunId,
            PersonaKey = personaKey.Trim(),
            DisplayName = displayName.Trim(),
        };
        subject.TenantId = tenantId;
        return subject;
    }
}

public sealed class GradingExecution : EntityBase
{
    private GradingExecution() { }

    public Guid DefinitionRevisionId { get; private set; }
    public ReviewExecutionContext ExecutionContext { get; private set; }
    public Guid? TestRunSubjectId { get; private set; }
    public Guid? AssessmentSubmissionId { get; private set; }
    public string? DeliverySchemaVersion { get; private set; }
    public string? DeliveryCanonicalJson { get; private set; }
    public string? DeliveryHash { get; private set; }
    public string? DeliveryHashVersion { get; private set; }
    public string? ResponseSchemaVersion { get; private set; }
    public string? ResponseContentType { get; private set; }
    public string? ResponsePayloadSchema { get; private set; }
    public string? ResponseEnvelopeCanonicalJson { get; private set; }
    public string? ResponseHash { get; private set; }
    public string? ResponseHashVersion { get; private set; }
    public PersistedGradingExecutionStatus Status { get; private set; }
    public Guid? ActiveGradeRoundId { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? FinalizedAt { get; private set; }

    public static GradingExecution CreateAuthorTest(Guid? tenantId, Guid revisionId, Guid testRunSubjectId)
        => Create(tenantId, revisionId, ReviewExecutionContext.AuthorTest, testRunSubjectId, null);

    public static GradingExecution CreateOfficial(Guid? tenantId, Guid revisionId, Guid submissionId)
        => Create(tenantId, revisionId, ReviewExecutionContext.OfficialSubmission, null, submissionId);

    public void MaterializeDelivery(AssessmentExecutionDeliveryV1 delivery, string canonicalJson)
    {
        GradingContractValidator.Validate(delivery);
        if (delivery.DefinitionRevisionId != DefinitionRevisionId)
        {
            throw new InvalidOperationException("Execution delivery must reference the execution definition revision.");
        }

        var canonical = CanonicalPayload.Require(canonicalJson, 8 * 1024 * 1024, "execution delivery");
        CanonicalPayload.RequireMatchesContract(delivery, canonical, "execution delivery");
        var hash = CanonicalPayload.Hash(canonical);
        if (DeliveryCanonicalJson is not null)
        {
            if (DeliveryCanonicalJson != canonical || DeliveryHash != hash)
            {
                throw new InvalidOperationException("Execution delivery is immutable once materialized.");
            }

            return;
        }

        DeliverySchemaVersion = delivery.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DeliveryCanonicalJson = canonical;
        DeliveryHash = hash;
        DeliveryHashVersion = GradingContractVersions.Hash;
        Touch();
    }

    public void SaveResponseDraft(AssessmentResponseEnvelopeV1 response, string canonicalJson)
    {
        if (SubmittedAt.HasValue)
        {
            throw new InvalidOperationException("A submitted response is immutable.");
        }

        GradingContractValidator.Validate(response);
        var canonical = CanonicalPayload.Require(canonicalJson, 8 * 1024 * 1024, "response envelope");
        CanonicalPayload.RequireMatchesContract(response, canonical, "response envelope");
        ResponseSchemaVersion = response.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ResponseContentType = response.ContentType;
        ResponsePayloadSchema = response.PayloadSchema;
        ResponseEnvelopeCanonicalJson = canonical;
        ResponseHash = CanonicalPayload.Hash(canonical);
        ResponseHashVersion = GradingContractVersions.Hash;
        Touch();
    }

    public void Submit(DateTime submittedAt)
    {
        if (ResponseEnvelopeCanonicalJson is null)
        {
            throw new InvalidOperationException("A response is required before submit.");
        }

        if (SubmittedAt.HasValue)
        {
            return;
        }

        SubmittedAt = submittedAt.ToUniversalTime();
        Status = PersistedGradingExecutionStatus.Running;
        Touch();
    }

    public void SetActiveRound(Guid roundId)
    {
        if (roundId == Guid.Empty)
        {
            throw new ArgumentException("Round ID is required.", nameof(roundId));
        }

        ActiveGradeRoundId = roundId;
        Touch();
    }

    public void BeginRegrade(Guid roundId)
    {
        if (!SubmittedAt.HasValue)
        {
            throw new InvalidOperationException("An execution must be submitted before regrade.");
        }

        if (roundId == Guid.Empty)
        {
            throw new ArgumentException("Round ID is required.", nameof(roundId));
        }

        ActiveGradeRoundId = roundId;
        Status = PersistedGradingExecutionStatus.Running;
        FinalizedAt = null;
        Touch();
    }

    public void AwaitReview()
    {
        if (Status == PersistedGradingExecutionStatus.Completed)
        {
            throw new InvalidOperationException("A completed execution cannot await review.");
        }

        Status = PersistedGradingExecutionStatus.AwaitingReview;
        Touch();
    }

    public void Complete(DateTime finalizedAt)
    {
        if (Status == PersistedGradingExecutionStatus.Completed)
        {
            return;
        }

        if (!SubmittedAt.HasValue)
        {
            throw new InvalidOperationException("An execution must be submitted before it can complete.");
        }

        Status = PersistedGradingExecutionStatus.Completed;
        FinalizedAt = finalizedAt.ToUniversalTime();
        Touch();
    }

    public void Fail(DateTime finalizedAt)
    {
        if (Status == PersistedGradingExecutionStatus.Completed)
        {
            throw new InvalidOperationException("A completed execution cannot fail.");
        }

        if (!SubmittedAt.HasValue)
        {
            throw new InvalidOperationException("An execution must be submitted before it can fail.");
        }

        Status = PersistedGradingExecutionStatus.Failed;
        FinalizedAt = finalizedAt.ToUniversalTime();
        Touch();
    }

    private static GradingExecution Create(
        Guid? tenantId,
        Guid revisionId,
        ReviewExecutionContext context,
        Guid? testRunSubjectId,
        Guid? submissionId)
    {
        if (revisionId == Guid.Empty)
        {
            throw new ArgumentException("Definition revision ID is required.", nameof(revisionId));
        }

        var authorTest = context == ReviewExecutionContext.AuthorTest;
        if (authorTest != testRunSubjectId.HasValue || authorTest == submissionId.HasValue)
        {
            throw new ArgumentException("Execution context must have exactly one matching owner.");
        }

        var execution = new GradingExecution
        {
            Id = Guid.NewGuid(),
            DefinitionRevisionId = revisionId,
            ExecutionContext = context,
            TestRunSubjectId = testRunSubjectId,
            AssessmentSubmissionId = submissionId,
            Status = PersistedGradingExecutionStatus.Pending,
        };
        execution.TenantId = tenantId;
        return execution;
    }

    private static string RequireText(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", parameterName) : value;
}

public sealed class GradeRound : EntityBase
{
    private GradeRound() { }

    public Guid GradingExecutionId { get; private set; }
    public int RoundNumber { get; private set; }
    public Guid? SupersedesGradeRoundId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? ReasonDetail { get; private set; }
    public Guid? InitiatedByActorId { get; private set; }
    public PersistedGradeRoundStatus Status { get; private set; }
    public int ResultSchemaVersion { get; private set; }
    public string? ResultState { get; private set; }
    public ScoreValue? Score { get; private set; }
    public ScoreValue MaxScore { get; private set; }
    public string? Feedback { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinalizedAt { get; private set; }

    public static GradeRound Create(
        Guid? tenantId,
        Guid executionId,
        int number,
        ScoreValue maxScore,
        string reason,
        Guid? supersedesId = null,
        Guid? initiatedByActorId = null,
        string? reasonDetail = null)
    {
        if (executionId == Guid.Empty)
        {
            throw new ArgumentException("Execution ID is required.", nameof(executionId));
        }

        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason is required.", nameof(reason));
        }

        var round = new GradeRound
        {
            Id = Guid.NewGuid(),
            GradingExecutionId = executionId,
            RoundNumber = number,
            SupersedesGradeRoundId = supersedesId,
            Reason = reason,
            ReasonDetail = string.IsNullOrWhiteSpace(reasonDetail) ? null : reasonDetail.Trim(),
            InitiatedByActorId = initiatedByActorId,
            Status = PersistedGradeRoundStatus.Pending,
            ResultSchemaVersion = GradingContractVersions.GradeResult,
            MaxScore = maxScore,
            StartedAt = SystemClock.UtcNow,
        };
        round.TenantId = tenantId;
        return round;
    }

    public void Start()
    {
        if (Status == PersistedGradeRoundStatus.Running)
        {
            return;
        }

        if (Status != PersistedGradeRoundStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending grade round can be started.");
        }

        Status = PersistedGradeRoundStatus.Running;
        Touch();
    }

    public void AwaitInstructorResolution()
    {
        if (Status is PersistedGradeRoundStatus.Finalized or PersistedGradeRoundStatus.Failed)
        {
            throw new InvalidOperationException("A terminal grade round cannot await instructor resolution.");
        }

        Status = PersistedGradeRoundStatus.AwaitingInstructorResolution;
        ResultState = "partial";
        Score = null;
        Touch();
    }

    public void AwaitEvidence()
    {
        if (Status is PersistedGradeRoundStatus.Finalized or PersistedGradeRoundStatus.Failed)
        {
            throw new InvalidOperationException("A terminal grade round cannot await evidence.");
        }

        Status = PersistedGradeRoundStatus.AwaitingEvidence;
        ResultState = "partial";
        Score = null;
        Touch();
    }

    public void Finalize(GradeResultV1 result, DateTime finalizedAt)
    {
        GradingContractValidator.Validate(result);
        if (!string.Equals(result.State, "final", StringComparison.Ordinal) || !result.Score.HasValue)
        {
            throw new InvalidOperationException("Only a final grade result can finalize a round.");
        }

        if (result.MaxScore != MaxScore)
        {
            throw new InvalidOperationException("The final result maximum score must match the round snapshot.");
        }

        if (Status == PersistedGradeRoundStatus.Finalized)
        {
            if (Score != result.Score || !string.Equals(Feedback, result.Feedback, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A finalized grade round is immutable.");
            }

            return;
        }

        ResultState = "final";
        Score = result.Score;
        Feedback = result.Feedback;
        Status = PersistedGradeRoundStatus.Finalized;
        FinalizedAt = finalizedAt.ToUniversalTime();
        Touch();
    }
}

public sealed class ReviewStage : EntityBase
{
    private ReviewStage() { }

    public Guid GradeRoundId { get; private set; }
    public int Sequence { get; private set; }
    public ReviewMethod ReviewMethod { get; private set; }
    public string HandlerKey { get; private set; } = string.Empty;
    public string HandlerVersion { get; private set; } = string.Empty;
    public string? ProviderKey { get; private set; }
    public string? ProviderPolicyVersion { get; private set; }
    public PersistedReviewStageStatus Status { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public static ReviewStage Create(Guid? tenantId, Guid roundId, int sequence, AssessmentReviewStageManifestV1 manifest)
    {
        if (roundId == Guid.Empty)
        {
            throw new ArgumentException("Round ID is required.", nameof(roundId));
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        var stage = new ReviewStage
        {
            Id = Guid.NewGuid(),
            GradeRoundId = roundId,
            Sequence = sequence,
            ReviewMethod = manifest.Method,
            HandlerKey = manifest.HandlerKey,
            HandlerVersion = manifest.HandlerVersion,
            ProviderKey = manifest.ProviderKey,
            ProviderPolicyVersion = manifest.ProviderPolicyVersion,
            Status = PersistedReviewStageStatus.Pending,
        };
        stage.TenantId = tenantId;
        return stage;
    }

    public void Start(DateTime startedAt)
    {
        if (Status == PersistedReviewStageStatus.Running)
        {
            return;
        }

        if (Status != PersistedReviewStageStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending review stage can be started.");
        }

        Status = PersistedReviewStageStatus.Running;
        StartedAt = startedAt.ToUniversalTime();
        Touch();
    }

    public void AwaitInstructorResolution()
    {
        if (Status is PersistedReviewStageStatus.Completed or PersistedReviewStageStatus.Failed)
        {
            throw new InvalidOperationException("A terminal review stage cannot await instructor resolution.");
        }

        StartedAt ??= SystemClock.UtcNow;
        Status = PersistedReviewStageStatus.AwaitingInstructorResolution;
        Touch();
    }

    public void AwaitEvidence()
    {
        if (Status is PersistedReviewStageStatus.Completed or PersistedReviewStageStatus.Failed)
        {
            throw new InvalidOperationException("A terminal review stage cannot await evidence.");
        }

        StartedAt ??= SystemClock.UtcNow;
        Status = PersistedReviewStageStatus.AwaitingEvidence;
        Touch();
    }

    public void Complete(DateTime completedAt)
    {
        if (Status == PersistedReviewStageStatus.Completed)
        {
            return;
        }

        if (Status == PersistedReviewStageStatus.Failed)
        {
            throw new InvalidOperationException("A failed review stage cannot complete.");
        }

        StartedAt ??= completedAt.ToUniversalTime();
        Status = PersistedReviewStageStatus.Completed;
        CompletedAt = completedAt.ToUniversalTime();
        Touch();
    }

    public void Fail(DateTime completedAt)
    {
        if (Status == PersistedReviewStageStatus.Completed)
        {
            throw new InvalidOperationException("A completed review stage cannot fail.");
        }

        StartedAt ??= completedAt.ToUniversalTime();
        Status = PersistedReviewStageStatus.Failed;
        CompletedAt = completedAt.ToUniversalTime();
        Touch();
    }
}

public sealed class GradeItemResult
{
    private GradeItemResult() { }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid ReviewStageId { get; private set; }
    public string ItemId { get; private set; } = string.Empty;
    public PersistedGradeItemState State { get; private set; }
    public ScoreValue? Score { get; private set; }
    public ScoreValue MaxScore { get; private set; }
    public string? Feedback { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static GradeItemResult Create(Guid? tenantId, Guid stageId, GradeItemResultV1 result)
    {
        if (stageId == Guid.Empty)
        {
            throw new ArgumentException("Review stage ID is required.", nameof(stageId));
        }

        if (string.IsNullOrWhiteSpace(result.ItemId))
        {
            throw new ArgumentException("Item ID is required.", nameof(result));
        }

        return new GradeItemResult
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReviewStageId = stageId,
            ItemId = result.ItemId,
            State = result.State switch
            {
                GradeItemState.Graded => PersistedGradeItemState.Graded,
                GradeItemState.Pending => PersistedGradeItemState.Pending,
                GradeItemState.Unsupported => PersistedGradeItemState.Unsupported,
                _ => throw new ArgumentOutOfRangeException(nameof(result)),
            },
            Score = result.Score,
            MaxScore = result.MaxScore,
            Feedback = result.Feedback,
            CreatedAt = SystemClock.UtcNow,
        };
    }
}

public sealed class ReviewEvidence
{
    private ReviewEvidence() { }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid ReviewStageId { get; private set; }
    public string EvidenceKey { get; private set; } = string.Empty;
    public string? ItemId { get; private set; }
    public string EvidenceType { get; private set; } = string.Empty;
    public string SchemaVersion { get; private set; } = string.Empty;
    public string CanonicalJson { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public string HashVersion { get; private set; } = string.Empty;
    public Guid? ProducedByActorId { get; private set; }
    public string? ProducedByService { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static ReviewEvidence CreateForActor(
        Guid? tenantId,
        Guid stageId,
        string evidenceKey,
        string evidenceType,
        string schemaVersion,
        string canonicalJson,
        Guid actorId,
        string? itemId = null)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor ID is required.", nameof(actorId));
        }

        return Create(tenantId, stageId, evidenceKey, evidenceType, schemaVersion, canonicalJson, itemId, actorId, null);
    }

    public static ReviewEvidence CreateForService(
        Guid? tenantId,
        Guid stageId,
        string evidenceKey,
        string evidenceType,
        string schemaVersion,
        string canonicalJson,
        string service,
        string? itemId = null)
    {
        if (string.IsNullOrWhiteSpace(service))
        {
            throw new ArgumentException("Service is required.", nameof(service));
        }

        return Create(tenantId, stageId, evidenceKey, evidenceType, schemaVersion, canonicalJson, itemId, null, service.Trim());
    }

    private static ReviewEvidence Create(
        Guid? tenantId,
        Guid stageId,
        string evidenceKey,
        string evidenceType,
        string schemaVersion,
        string canonicalJson,
        string? itemId,
        Guid? actorId,
        string? service)
    {
        if (stageId == Guid.Empty)
        {
            throw new ArgumentException("Review stage ID is required.", nameof(stageId));
        }

        var canonical = CanonicalPayload.Require(canonicalJson, 1024 * 1024, "review evidence");
        return new ReviewEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReviewStageId = stageId,
            EvidenceKey = RequireText(evidenceKey, nameof(evidenceKey)),
            ItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId.Trim(),
            EvidenceType = RequireText(evidenceType, nameof(evidenceType)),
            SchemaVersion = RequireText(schemaVersion, nameof(schemaVersion)),
            CanonicalJson = canonical,
            PayloadHash = CanonicalPayload.Hash(canonical),
            HashVersion = GradingContractVersions.Hash,
            ProducedByActorId = actorId,
            ProducedByService = service,
            CreatedAt = SystemClock.UtcNow,
        };
    }

    private static string RequireText(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", parameterName) : value.Trim();
}

/// <summary>Append-only learner-visible release for one immutable grade round.</summary>
public sealed class GradeResultRelease
{
    private GradeResultRelease() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid GradeRoundId { get; private set; }
    public Guid GradingExecutionId { get; private set; }
    public ReviewExecutionContext ExecutionContext { get; private set; }
    public GradeResultReleaseStatus Status { get; private set; }
    public Guid? ReleasedByActorId { get; private set; }
    public string? ReleasedByService { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ReleasedAt { get; private set; }

    public static GradeResultRelease CreateByActor(
        Guid tenantId,
        Guid roundId,
        Guid executionId,
        Guid actorId,
        string? reason = null)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor ID is required.", nameof(actorId));
        }

        return Create(tenantId, roundId, executionId, actorId, null, reason);
    }

    public static GradeResultRelease CreateByService(
        Guid tenantId,
        Guid roundId,
        Guid executionId,
        string service,
        string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(service))
        {
            throw new ArgumentException("Service is required.", nameof(service));
        }

        return Create(tenantId, roundId, executionId, null, service.Trim(), reason);
    }

    private static GradeResultRelease Create(
        Guid tenantId,
        Guid roundId,
        Guid executionId,
        Guid? actorId,
        string? service,
        string? reason)
    {
        if (tenantId == Guid.Empty || roundId == Guid.Empty || executionId == Guid.Empty)
        {
            throw new ArgumentException("Tenant, grade round, and execution IDs are required.");
        }

        return new GradeResultRelease
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GradeRoundId = roundId,
            GradingExecutionId = executionId,
            ExecutionContext = ReviewExecutionContext.OfficialSubmission,
            Status = GradeResultReleaseStatus.Released,
            ReleasedByActorId = actorId,
            ReleasedByService = service,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ReleasedAt = SystemClock.UtcNow,
        };
    }
}

/// <summary>Frozen member snapshot for a collective assessment submission.</summary>
public sealed class AssessmentSubmissionParticipant
{
    private AssessmentSubmissionParticipant() { }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CapturedAt { get; private set; }

    public static AssessmentSubmissionParticipant Create(
        Guid? tenantId,
        Guid submissionId,
        Guid enrollmentId,
        Guid userId)
    {
        if (submissionId == Guid.Empty || enrollmentId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Submission, enrollment, and user IDs are required.");
        }

        return new AssessmentSubmissionParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubmissionId = submissionId,
            EnrollmentId = enrollmentId,
            UserId = userId,
            CapturedAt = SystemClock.UtcNow,
        };
    }
}

/// <summary>Append-only audit record for an accepted collective response mutation.</summary>
public sealed class CollectiveAttemptDraftChange
{
    private CollectiveAttemptDraftChange() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public Guid ActorId { get; private set; }
    public long PreviousVersion { get; private set; }
    public long NewVersion { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public string ResponseHash { get; private set; } = string.Empty;
    public DateTime ChangedAt { get; private set; }

    public static CollectiveAttemptDraftChange Create(
        Guid tenantId,
        Guid submissionId,
        Guid actorId,
        long previousVersion,
        long newVersion,
        string idempotencyKey,
        string requestHash,
        string responseHash)
    {
        if (tenantId == Guid.Empty || submissionId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException("Tenant, submission, and actor IDs are required.");
        }

        if (previousVersion < 0 || newVersion != previousVersion + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(newVersion), "Draft version must advance exactly once.");
        }

        return new CollectiveAttemptDraftChange
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubmissionId = submissionId,
            ActorId = actorId,
            PreviousVersion = previousVersion,
            NewVersion = newVersion,
            IdempotencyKey = RequireText(idempotencyKey, nameof(idempotencyKey)),
            RequestHash = CanonicalPayload.RequireHash(requestHash, nameof(requestHash)),
            ResponseHash = CanonicalPayload.RequireHash(responseHash, nameof(responseHash)),
            ChangedAt = SystemClock.UtcNow,
        };
    }

    private static string RequireText(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", parameterName) : value.Trim();
}

/// <summary>Mutable, rebuildable internal contribution selected for an enrollment and assessment.</summary>
public sealed class AssessmentGradebookEntry : EntityBase
{
    private AssessmentGradebookEntry() { }

    public Guid CourseId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid AssessmentId { get; private set; }
    public Guid? AssessmentGroupId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public Guid GradeRoundId { get; private set; }
    public ScoreValue EffectiveScore { get; private set; }
    public ScoreValue CapturedMaxScore { get; private set; }
    public PercentValue? CapturedWeightPercent { get; private set; }

    public static AssessmentGradebookEntry Create(
        Guid? tenantId,
        Guid courseId,
        Guid enrollmentId,
        Guid assessmentId,
        Guid? assessmentGroupId,
        Guid submissionId,
        Guid roundId,
        ScoreValue score,
        ScoreValue maxScore,
        PercentValue? weight)
    {
        var entry = new AssessmentGradebookEntry();
        entry.Id = Guid.NewGuid();
        entry.TenantId = tenantId;
        entry.Apply(courseId, enrollmentId, assessmentId, assessmentGroupId, submissionId, roundId, score, maxScore, weight);
        return entry;
    }

    public void Reproject(
        Guid? assessmentGroupId,
        Guid submissionId,
        Guid roundId,
        ScoreValue score,
        ScoreValue maxScore,
        PercentValue? weight)
    {
        Apply(CourseId, EnrollmentId, AssessmentId, assessmentGroupId, submissionId, roundId, score, maxScore, weight);
        Touch();
    }

    private void Apply(
        Guid courseId,
        Guid enrollmentId,
        Guid assessmentId,
        Guid? assessmentGroupId,
        Guid submissionId,
        Guid roundId,
        ScoreValue score,
        ScoreValue maxScore,
        PercentValue? weight)
    {
        if (courseId == Guid.Empty || enrollmentId == Guid.Empty || assessmentId == Guid.Empty ||
            submissionId == Guid.Empty || roundId == Guid.Empty)
        {
            throw new ArgumentException("Gradebook projection identifiers are required.");
        }

        if (maxScore.CompareTo(ScoreValue.Zero) <= 0 ||
            score.CompareTo(ScoreValue.Zero) < 0 ||
            score.CompareTo(maxScore) > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Gradebook score is outside its captured bounds.");
        }

        CourseId = courseId;
        EnrollmentId = enrollmentId;
        AssessmentId = assessmentId;
        AssessmentGroupId = assessmentGroupId;
        SubmissionId = submissionId;
        GradeRoundId = roundId;
        EffectiveScore = score;
        CapturedMaxScore = maxScore;
        CapturedWeightPercent = weight;
    }
}

/// <summary>Idempotent completion projection emitted only by the grading runtime.</summary>
public sealed class AssessmentContentCompletionProjection : EntityBase
{
    private AssessmentContentCompletionProjection() { }

    public Guid AssessmentId { get; private set; }
    public Guid ContentId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid SubmissionId { get; private set; }
    public Guid? GradeRoundId { get; private set; }
    public string Transition { get; private set; } = string.Empty;
    public DateTime CompletedAt { get; private set; }

    public static AssessmentContentCompletionProjection Create(
        Guid? tenantId,
        Guid assessmentId,
        Guid contentId,
        Guid enrollmentId,
        Guid submissionId,
        Guid? gradeRoundId,
        string transition)
    {
        if (assessmentId == Guid.Empty || contentId == Guid.Empty || enrollmentId == Guid.Empty || submissionId == Guid.Empty)
        {
            throw new ArgumentException("Completion projection identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(transition))
        {
            throw new ArgumentException("Transition is required.", nameof(transition));
        }

        var projection = new AssessmentContentCompletionProjection
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            ContentId = contentId,
            EnrollmentId = enrollmentId,
            SubmissionId = submissionId,
            GradeRoundId = gradeRoundId,
            Transition = transition.Trim(),
            CompletedAt = SystemClock.UtcNow,
        };
        projection.TenantId = tenantId;
        return projection;
    }
}

public sealed class GradingCommandReceipt
{
    private GradingCommandReceipt() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ResourceId { get; private set; }
    public string CommandType { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public string OutcomeSchemaVersion { get; private set; } = string.Empty;
    public string OutcomeCanonicalJson { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    public static GradingCommandReceipt Create(
        Guid tenantId,
        Guid resourceId,
        string commandType,
        Guid actorId,
        string idempotencyKey,
        string requestHash,
        string outcomeSchemaVersion,
        string outcomeCanonicalJson,
        DateTime expiresAt)
    {
        if (tenantId == Guid.Empty || resourceId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException("Tenant, resource, and actor IDs are required.");
        }

        var createdAt = SystemClock.UtcNow;
        var normalizedExpiry = expiresAt.ToUniversalTime();
        if (normalizedExpiry <= createdAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Receipt expiry must be later than creation.");
        }

        return new GradingCommandReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ResourceId = resourceId,
            CommandType = RequireText(commandType, 128, nameof(commandType)),
            ActorId = actorId,
            IdempotencyKey = RequireText(idempotencyKey, 200, nameof(idempotencyKey)),
            RequestHash = CanonicalPayload.RequireHash(requestHash, nameof(requestHash)),
            OutcomeSchemaVersion = RequireText(outcomeSchemaVersion, 64, nameof(outcomeSchemaVersion)),
            OutcomeCanonicalJson = CanonicalPayload.Require(outcomeCanonicalJson, 1024 * 1024, "command outcome"),
            CreatedAt = createdAt,
            ExpiresAt = normalizedExpiry,
        };
    }

    private static string RequireText(string value, int maximumLength, string parameterName)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Text value is required.", parameterName)
            : value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"Text value cannot exceed {maximumLength} characters.", parameterName);
    }
}

public sealed class AcademicOutboxMessage
{
    private AcademicOutboxMessage() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string EventSchemaVersion { get; private set; } = string.Empty;
    public string PayloadCanonicalJson { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }
    public AcademicOutboxStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public static AcademicOutboxMessage Create(
        Guid tenantId,
        string eventType,
        string eventSchemaVersion,
        string payloadCanonicalJson)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        }

        var canonical = CanonicalPayload.Require(payloadCanonicalJson, 1024 * 1024, "academic event");
        return new AcademicOutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = RequireBoundedText(eventType, 160, nameof(eventType)),
            EventSchemaVersion = RequireBoundedText(eventSchemaVersion, 64, nameof(eventSchemaVersion)),
            PayloadCanonicalJson = canonical,
            PayloadHash = CanonicalPayload.Hash(canonical),
            OccurredAt = SystemClock.UtcNow,
            Status = AcademicOutboxStatus.Pending,
        };
    }

    public void MarkProcessing()
    {
        if (Status == AcademicOutboxStatus.Pending)
        {
            Status = AcademicOutboxStatus.Processing;
        }
    }

    public void MarkCompleted(DateTime completedAt)
    {
        Status = AcademicOutboxStatus.Completed;
        CompletedAt = completedAt.ToUniversalTime();
    }

    public void MarkFailed()
    {
        if (Status == AcademicOutboxStatus.Completed)
        {
            return;
        }

        Status = AcademicOutboxStatus.Failed;
    }

    private static string RequireBoundedText(string value, int maximumLength, string parameterName)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
    }
}

public sealed class AcademicOutboxDelivery
{
    private AcademicOutboxDelivery() { }

    public Guid Id { get; private set; }
    public Guid OutboxMessageId { get; private set; }
    public string ConsumerKey { get; private set; } = string.Empty;
    public AcademicOutboxDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime? ClaimedAt { get; private set; }
    public string? ClaimedBy { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public string? LastError { get; private set; }

    public static AcademicOutboxDelivery Create(Guid messageId, string consumerKey)
    {
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("Message ID is required.", nameof(messageId));
        }

        if (string.IsNullOrWhiteSpace(consumerKey))
        {
            throw new ArgumentException("Consumer key is required.", nameof(consumerKey));
        }

        return new AcademicOutboxDelivery
        {
            Id = Guid.NewGuid(),
            OutboxMessageId = messageId,
            ConsumerKey = consumerKey.Trim().Length <= 160
                ? consumerKey.Trim()
                : throw new ArgumentException("Consumer key cannot exceed 160 characters.", nameof(consumerKey)),
            Status = AcademicOutboxDeliveryStatus.Pending,
        };
    }

    public void Claim(string workerId, DateTime claimedAt)
    {
        if (Status == AcademicOutboxDeliveryStatus.Confirmed)
        {
            throw new InvalidOperationException("A confirmed delivery cannot be claimed again.");
        }

        if (string.IsNullOrWhiteSpace(workerId))
        {
            throw new ArgumentException("Worker ID is required.", nameof(workerId));
        }

        Status = AcademicOutboxDeliveryStatus.Processing;
        AttemptCount++;
        ClaimedAt = claimedAt.ToUniversalTime();
        ClaimedBy = workerId;
        NextAttemptAt = null;
        LastError = null;
    }

    public void Confirm(DateTime confirmedAt)
    {
        if (Status == AcademicOutboxDeliveryStatus.Confirmed)
        {
            return;
        }

        if (Status != AcademicOutboxDeliveryStatus.Processing)
        {
            throw new InvalidOperationException("Only a claimed delivery can be confirmed.");
        }

        Status = AcademicOutboxDeliveryStatus.Confirmed;
        ConfirmedAt = confirmedAt.ToUniversalTime();
        ClaimedAt = null;
        ClaimedBy = null;
        NextAttemptAt = null;
        LastError = null;
    }

    public void Fail(string error, DateTime nextAttemptAt)
    {
        if (Status == AcademicOutboxDeliveryStatus.Confirmed)
        {
            return;
        }

        Status = AcademicOutboxDeliveryStatus.Failed;
        LastError = TruncateUtf8(
            string.IsNullOrWhiteSpace(error) ? "Academic outbox consumer failed." : error,
            4096);
        NextAttemptAt = nextAttemptAt.ToUniversalTime();
        ClaimedAt = null;
        ClaimedBy = null;
    }

    private static string TruncateUtf8(string value, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maximumBytes)
        {
            return value;
        }

        var length = Math.Min(value.Length, maximumBytes);
        while (length > 0 && Encoding.UTF8.GetByteCount(value.AsSpan(0, length)) > maximumBytes)
        {
            length--;
        }

        return value[..length];
    }
}

internal static class CanonicalPayload
{
    public static string Require(string value, int maximumUtf8Bytes, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Canonical {label} is required.");
        }

        if (Encoding.UTF8.GetByteCount(value) > maximumUtf8Bytes)
        {
            throw new ArgumentException($"Canonical {label} exceeds its size limit.");
        }

        using var document = JsonDocument.Parse(value);
        var canonical = CanonicalJson.Serialize(document.RootElement);
        if (!string.Equals(value, canonical, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Canonical {label} must use the canonical JSON representation.");
        }

        return value;
    }

    public static string Hash(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

    public static void RequireMatchesContract<T>(T value, string canonicalJson, string label)
    {
        var serialized = JsonSerializer.SerializeToElement(value, GradingJson.Options);
        if (!string.Equals(CanonicalJson.Serialize(serialized), canonicalJson, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Canonical {label} does not match the validated contract.");
        }
    }

    public static string RequireHash(string value, string parameterName)
    {
        if (value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException("Hash must be a lowercase SHA-256 value.", parameterName);
        }

        return value;
    }
}
