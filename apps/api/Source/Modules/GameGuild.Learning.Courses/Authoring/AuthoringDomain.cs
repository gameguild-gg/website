using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace GameGuild.Learning.Courses;

public sealed class AuthoringRevisionConflictException(int expectedRevision, int currentRevision)
    : InvalidOperationException($"Draft revision {expectedRevision} is stale; current revision is {currentRevision}.")
{
    public int ExpectedRevision { get; } = expectedRevision;
    public int CurrentRevision { get; } = currentRevision;
}

public enum AiProposalKind
{
    ReplaceDocument = 1,
    InsertAtCursor = 2,
    LexicalPatch = 3,
    QuizPatch = 4,
    MetadataPatch = 5,
}

public enum AiProposalStatus
{
    Pending = 1,
    Applied = 2,
    Discarded = 3,
}

/// <summary>
/// Shared, optimistic-concurrency controlled authoring draft for one published content item.
/// Public and learner reads never query this table.
/// </summary>
public sealed class ProgramContentDraft : EntityBase
{
    private ProgramContentDraft() { }

    public Guid ProgramId { get; private set; }
    public Guid ContentId { get; private set; }
    public int BasePublishedVersion { get; private set; }
    public int Revision { get; private set; }
    public Guid LastEditedBy { get; private set; }
    public DateTimeOffset LastEditedAt { get; private set; }

    [Required]
    public string PayloadJson { get; private set; } = "{}";

    public string ETag => $"\"draft-{Id:N}-{Revision}\"";

    public static ProgramContentDraft Create(
        Guid id,
        Guid programId,
        Guid contentId,
        Guid authorId,
        int basePublishedVersion,
        string payloadJson,
        DateTimeOffset now)
    {
        ValidateIds(id, programId, contentId, authorId);
        ArgumentOutOfRangeException.ThrowIfNegative(basePublishedVersion);
        ValidateJson(payloadJson);

        return new ProgramContentDraft
        {
            Id = id,
            ProgramId = programId,
            ContentId = contentId,
            BasePublishedVersion = basePublishedVersion,
            Revision = 1,
            LastEditedBy = authorId,
            LastEditedAt = now,
            PayloadJson = payloadJson,
            CreatedAt = now.UtcDateTime,
            UpdatedAt = now.UtcDateTime,
        };
    }

    public void Update(int expectedRevision, string payloadJson, Guid authorId, DateTimeOffset now)
    {
        if (expectedRevision != Revision)
            throw new AuthoringRevisionConflictException(expectedRevision, Revision);
        if (authorId == Guid.Empty)
            throw new ArgumentException("Author ID is required.", nameof(authorId));
        ValidateJson(payloadJson);

        PayloadJson = payloadJson;
        LastEditedBy = authorId;
        LastEditedAt = now;
        Revision = checked(Revision + 1);
        UpdatedAt = now.UtcDateTime;
    }

    public void Rebase(int publishedVersion, Guid authorId, string payloadJson, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(publishedVersion);
        ValidateJson(payloadJson);
        BasePublishedVersion = publishedVersion;
        PayloadJson = payloadJson;
        LastEditedBy = authorId;
        LastEditedAt = now;
        Revision = checked(Revision + 1);
        UpdatedAt = now.UtcDateTime;
    }

    private static void ValidateIds(Guid id, Guid programId, Guid contentId, Guid authorId)
    {
        if (id == Guid.Empty || programId == Guid.Empty || contentId == Guid.Empty || authorId == Guid.Empty)
            throw new ArgumentException("Draft, program, content, and author IDs are required.");
    }

    private static void ValidateJson(string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        try
        {
            using var _ = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Authoring payload must be valid JSON.", nameof(payloadJson), exception);
        }
    }
}

public sealed class AiAuthoringProposal : EntityBase
{
    private AiAuthoringProposal() { }

    public Guid RunId { get; private set; }
    public Guid ContentId { get; private set; }
    public int BaseDraftRevision { get; private set; }
    public AiProposalKind Kind { get; private set; }
    public AiProposalStatus Status { get; private set; }
    public string OriginalContent { get; private set; } = string.Empty;
    public string ProposedContent { get; private set; } = string.Empty;
    public DateTimeOffset ProposedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }

    public static AiAuthoringProposal Create(
        Guid runId,
        Guid contentId,
        int baseDraftRevision,
        AiProposalKind kind,
        string originalContent,
        string proposedContent,
        DateTimeOffset now)
    {
        if (runId == Guid.Empty || contentId == Guid.Empty)
            throw new ArgumentException("Run and content IDs are required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseDraftRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposedContent);

        return new AiAuthoringProposal
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            ContentId = contentId,
            BaseDraftRevision = baseDraftRevision,
            Kind = kind,
            OriginalContent = originalContent ?? string.Empty,
            ProposedContent = proposedContent,
            Status = AiProposalStatus.Pending,
            ProposedAt = now,
            CreatedAt = now.UtcDateTime,
            UpdatedAt = now.UtcDateTime,
        };
    }

    public void EnsureApplicableTo(int currentDraftRevision)
    {
        if (BaseDraftRevision != currentDraftRevision)
            throw new AuthoringRevisionConflictException(BaseDraftRevision, currentDraftRevision);
        if (Status != AiProposalStatus.Pending)
            throw new AiProposalStateConflictException(Status);
    }

    public void MarkApplied(Guid actorId, DateTimeOffset now)
    {
        Resolve(AiProposalStatus.Applied, actorId, now);
    }

    public void Discard(Guid actorId, DateTimeOffset now)
    {
        Resolve(AiProposalStatus.Discarded, actorId, now);
    }

    private void Resolve(AiProposalStatus status, Guid actorId, DateTimeOffset now)
    {
        if (Status != AiProposalStatus.Pending)
            throw new AiProposalStateConflictException(Status);
        if (actorId == Guid.Empty)
            throw new ArgumentException("Actor ID is required.", nameof(actorId));
        Status = status;
        ResolvedBy = actorId;
        ResolvedAt = now;
        UpdatedAt = now.UtcDateTime;
    }
}

public sealed class AiProposalStateConflictException(AiProposalStatus currentStatus)
    : InvalidOperationException($"AI proposal has already been resolved as {currentStatus}.")
{
    public AiProposalStatus CurrentStatus { get; } = currentStatus;
}
