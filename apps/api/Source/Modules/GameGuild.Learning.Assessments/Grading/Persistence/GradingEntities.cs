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
        if (assessmentId == Guid.Empty) throw new ArgumentException("Assessment ID is required.", nameof(assessmentId));
        if (revisionNumber < 1) throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creator ID is required.", nameof(createdByUserId));

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
            throw new ArgumentException("Assessment, revision, and actor IDs are required.");

        var run = new AssessmentTestRun
        {
            AssessmentId = assessmentId,
            DefinitionRevisionId = revisionId,
            CreatedByUserId = actorId,
            Status = AssessmentTestRunStatus.Draft,
        };
        run.TenantId = tenantId;
        return run;
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
        if (testRunId == Guid.Empty) throw new ArgumentException("Test run ID is required.", nameof(testRunId));
        if (string.IsNullOrWhiteSpace(personaKey)) throw new ArgumentException("Persona key is required.", nameof(personaKey));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));

        var subject = new AssessmentTestRunSubject
        {
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
            throw new InvalidOperationException("Execution delivery must reference the execution definition revision.");

        var canonical = CanonicalPayload.Require(canonicalJson, 8 * 1024 * 1024, "execution delivery");
        CanonicalPayload.RequireMatchesContract(delivery, canonical, "execution delivery");
        var hash = CanonicalPayload.Hash(canonical);
        if (DeliveryCanonicalJson is not null)
        {
            if (DeliveryCanonicalJson != canonical || DeliveryHash != hash)
                throw new InvalidOperationException("Execution delivery is immutable once materialized.");
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
        if (SubmittedAt.HasValue) throw new InvalidOperationException("A submitted response is immutable.");
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
        if (ResponseEnvelopeCanonicalJson is null) throw new InvalidOperationException("A response is required before submit.");
        if (SubmittedAt.HasValue) return;
        SubmittedAt = submittedAt.ToUniversalTime();
        Status = PersistedGradingExecutionStatus.Running;
        Touch();
    }

    public void SetActiveRound(Guid roundId)
    {
        if (roundId == Guid.Empty) throw new ArgumentException("Round ID is required.", nameof(roundId));
        ActiveGradeRoundId = roundId;
        Touch();
    }

    private static GradingExecution Create(
        Guid? tenantId,
        Guid revisionId,
        ReviewExecutionContext context,
        Guid? testRunSubjectId,
        Guid? submissionId)
    {
        if (revisionId == Guid.Empty) throw new ArgumentException("Definition revision ID is required.", nameof(revisionId));
        var authorTest = context == ReviewExecutionContext.AuthorTest;
        if (authorTest != testRunSubjectId.HasValue || authorTest == submissionId.HasValue)
            throw new ArgumentException("Execution context must have exactly one matching owner.");

        var execution = new GradingExecution
        {
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
        Guid? supersedesId = null)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("Execution ID is required.", nameof(executionId));
        if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));

        var round = new GradeRound
        {
            GradingExecutionId = executionId,
            RoundNumber = number,
            SupersedesGradeRoundId = supersedesId,
            Reason = reason,
            Status = PersistedGradeRoundStatus.Pending,
            ResultSchemaVersion = GradingContractVersions.GradeResult,
            MaxScore = maxScore,
            StartedAt = SystemClock.UtcNow,
        };
        round.TenantId = tenantId;
        return round;
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
        if (roundId == Guid.Empty) throw new ArgumentException("Round ID is required.", nameof(roundId));
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        var stage = new ReviewStage
        {
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
            throw new ArgumentException("Tenant, resource, and actor IDs are required.");
        var createdAt = SystemClock.UtcNow;
        var normalizedExpiry = expiresAt.ToUniversalTime();
        if (normalizedExpiry <= createdAt)
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Receipt expiry must be later than creation.");
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
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
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
            Status = AcademicOutboxStatus.Processing;
    }

    public void MarkCompleted(DateTime completedAt)
    {
        Status = AcademicOutboxStatus.Completed;
        CompletedAt = completedAt.ToUniversalTime();
    }

    public void MarkFailed()
    {
        if (Status == AcademicOutboxStatus.Completed) return;
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
        if (messageId == Guid.Empty) throw new ArgumentException("Message ID is required.", nameof(messageId));
        if (string.IsNullOrWhiteSpace(consumerKey)) throw new ArgumentException("Consumer key is required.", nameof(consumerKey));
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
            throw new InvalidOperationException("A confirmed delivery cannot be claimed again.");
        if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentException("Worker ID is required.", nameof(workerId));
        Status = AcademicOutboxDeliveryStatus.Processing;
        AttemptCount++;
        ClaimedAt = claimedAt.ToUniversalTime();
        ClaimedBy = workerId;
        NextAttemptAt = null;
        LastError = null;
    }

    public void Confirm(DateTime confirmedAt)
    {
        if (Status == AcademicOutboxDeliveryStatus.Confirmed) return;
        if (Status != AcademicOutboxDeliveryStatus.Processing)
            throw new InvalidOperationException("Only a claimed delivery can be confirmed.");
        Status = AcademicOutboxDeliveryStatus.Confirmed;
        ConfirmedAt = confirmedAt.ToUniversalTime();
        ClaimedAt = null;
        ClaimedBy = null;
        NextAttemptAt = null;
        LastError = null;
    }

    public void Fail(string error, DateTime nextAttemptAt)
    {
        if (Status == AcademicOutboxDeliveryStatus.Confirmed) return;
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
        if (Encoding.UTF8.GetByteCount(value) <= maximumBytes) return value;
        var length = Math.Min(value.Length, maximumBytes);
        while (length > 0 && Encoding.UTF8.GetByteCount(value.AsSpan(0, length)) > maximumBytes) length--;
        return value[..length];
    }
}

internal static class CanonicalPayload
{
    public static string Require(string value, int maximumUtf8Bytes, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"Canonical {label} is required.");
        if (Encoding.UTF8.GetByteCount(value) > maximumUtf8Bytes) throw new ArgumentException($"Canonical {label} exceeds its size limit.");
        using var document = JsonDocument.Parse(value);
        var canonical = CanonicalJson.Serialize(document.RootElement);
        if (!string.Equals(value, canonical, StringComparison.Ordinal))
            throw new ArgumentException($"Canonical {label} must use the canonical JSON representation.");
        return value;
    }

    public static string Hash(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

    public static void RequireMatchesContract<T>(T value, string canonicalJson, string label)
    {
        var serialized = JsonSerializer.SerializeToElement(value, GradingJson.Options);
        if (!string.Equals(CanonicalJson.Serialize(serialized), canonicalJson, StringComparison.Ordinal))
            throw new ArgumentException($"Canonical {label} does not match the validated contract.");
    }

    public static string RequireHash(string value, string parameterName)
    {
        if (value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Hash must be a lowercase SHA-256 value.", parameterName);
        return value;
    }
}
