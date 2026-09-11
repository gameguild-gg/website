using System.ComponentModel.DataAnnotations;

namespace GameGuild.Learning.Courses;

public enum AiAuthoringRunStatus
{
    Queued = 1,
    Reserved = 2,
    Running = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6,
}

public sealed class AiAuthoringConversation : EntityBase
{
    private AiAuthoringConversation() { }

    public Guid ProgramId { get; private set; }
    public Guid ContentId { get; private set; }
    public Guid AuthorId { get; private set; }
    public DateTimeOffset LastMessageAt { get; private set; }

    public static AiAuthoringConversation Create(
        Guid tenantId,
        Guid programId,
        Guid contentId,
        Guid authorId,
        DateTimeOffset now)
    {
        ValidateActor(tenantId, authorId);
        if (programId == Guid.Empty || contentId == Guid.Empty)
            throw new ArgumentException("Program and content IDs are required.");

        return new AiAuthoringConversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProgramId = programId,
            ContentId = contentId,
            AuthorId = authorId,
            LastMessageAt = now,
            CreatedAt = now.UtcDateTime,
            UpdatedAt = now.UtcDateTime,
        };
    }

    public void Touch(DateTimeOffset now)
    {
        LastMessageAt = now;
        UpdatedAt = now.UtcDateTime;
    }

    private static void ValidateActor(Guid tenantId, Guid authorId)
    {
        if (tenantId == Guid.Empty || authorId == Guid.Empty)
            throw new UnauthorizedAccessException("AI authoring requires a tenant-scoped user actor.");
    }
}

public sealed class AiAuthoringMessage
{
    private AiAuthoringMessage() { }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid? RunId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static AiAuthoringMessage Create(Guid conversationId, Guid? runId, string role, string content, DateTimeOffset now)
    {
        if (conversationId == Guid.Empty)
            throw new ArgumentException("Conversation ID is required.", nameof(conversationId));
        if (role is not ("user" or "assistant"))
            throw new ArgumentOutOfRangeException(nameof(role), "Only user and assistant messages are supported.");
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new AiAuthoringMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            RunId = runId,
            Role = role,
            Content = content.Trim(),
            CreatedAt = now,
        };
    }
}

public sealed class AiAuthoringRun : EntityBase
{
    private AiAuthoringRun() { }

    public Guid ProgramId { get; private set; }
    public Guid ContentId { get; private set; }
    public Guid DraftId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid ActorId { get; private set; }
    public int BaseDraftRevision { get; private set; }
    public AiProposalKind ProposalKind { get; private set; }
    public AiAuthoringRunStatus Status { get; private set; }

    [Required]
    public string Instruction { get; private set; } = string.Empty;
    public string? Selection { get; private set; }

    [Required]
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? Provider { get; private set; }
    public string? Model { get; private set; }
    public int MaximumInputTokens { get; private set; }
    public int MaximumOutputTokens { get; private set; }
    public long MaximumEstimatedCost { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public long SettledCost { get; private set; }
    public long ReleasedAmount { get; private set; }
    public string? ResponseText { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static AiAuthoringRun Create(
        Guid tenantId,
        Guid actorId,
        Guid programId,
        Guid contentId,
        Guid draftId,
        Guid conversationId,
        int baseDraftRevision,
        AiProposalKind proposalKind,
        string instruction,
        string? selection,
        string idempotencyKey,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new UnauthorizedAccessException("AI authoring requires a tenant-scoped user actor.");
        if (programId == Guid.Empty || contentId == Guid.Empty || draftId == Guid.Empty || conversationId == Guid.Empty)
            throw new ArgumentException("Program, content, draft, and conversation IDs are required.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseDraftRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new AiAuthoringRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorId = actorId,
            ProgramId = programId,
            ContentId = contentId,
            DraftId = draftId,
            ConversationId = conversationId,
            BaseDraftRevision = baseDraftRevision,
            ProposalKind = proposalKind,
            Instruction = instruction.Trim(),
            Selection = string.IsNullOrWhiteSpace(selection) ? null : selection,
            IdempotencyKey = idempotencyKey.Trim(),
            Status = AiAuthoringRunStatus.Queued,
            CreatedAt = now.UtcDateTime,
            UpdatedAt = now.UtcDateTime,
        };
    }

    public void Reserve(
        string provider,
        string model,
        int maximumInputTokens,
        int maximumOutputTokens,
        long maximumEstimatedCost,
        DateTimeOffset now)
    {
        EnsureStatus(AiAuthoringRunStatus.Queued);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumInputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumOutputTokens);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEstimatedCost);
        Provider = provider;
        Model = model;
        MaximumInputTokens = maximumInputTokens;
        MaximumOutputTokens = maximumOutputTokens;
        MaximumEstimatedCost = maximumEstimatedCost;
        Status = AiAuthoringRunStatus.Reserved;
        UpdatedAt = now.UtcDateTime;
    }

    public void Start(DateTimeOffset now)
    {
        EnsureStatus(AiAuthoringRunStatus.Reserved);
        Status = AiAuthoringRunStatus.Running;
        StartedAt = now;
        UpdatedAt = now.UtcDateTime;
    }

    public void Complete(string responseText, int inputTokens, int outputTokens, long settledCost, long releasedAmount, DateTimeOffset now)
    {
        EnsureStatus(AiAuthoringRunStatus.Running);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseText);
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(settledCost);
        ArgumentOutOfRangeException.ThrowIfNegative(releasedAmount);
        ResponseText = responseText;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        SettledCost = settledCost;
        ReleasedAmount = releasedAmount;
        Status = AiAuthoringRunStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now.UtcDateTime;
    }

    public void Fail(string code, string message, long releasedAmount, DateTimeOffset now)
    {
        if (Status is AiAuthoringRunStatus.Completed or AiAuthoringRunStatus.Cancelled)
            throw new InvalidOperationException("A terminal AI run cannot fail.");
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ErrorCode = code.Trim();
        ErrorMessage = message;
        ReleasedAmount = Math.Max(0, releasedAmount);
        Status = AiAuthoringRunStatus.Failed;
        CompletedAt = now;
        UpdatedAt = now.UtcDateTime;
    }

    private void EnsureStatus(AiAuthoringRunStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"AI run must be {expected} but is {Status}.");
    }
}

public sealed class AiAuthoringStreamEvent
{
    private AiAuthoringStreamEvent() { }

    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public long Sequence { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? Delta { get; private set; }
    public string? PayloadJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static AiAuthoringStreamEvent Create(
        Guid runId,
        long sequence,
        string type,
        string status,
        string? delta,
        string? payloadJson,
        DateTimeOffset now)
    {
        if (runId == Guid.Empty)
            throw new ArgumentException("Run ID is required.", nameof(runId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        return new AiAuthoringStreamEvent
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            Sequence = sequence,
            Type = type.Trim(),
            Status = status.Trim(),
            Delta = delta,
            PayloadJson = payloadJson,
            CreatedAt = now,
        };
    }
}
