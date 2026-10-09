using System.ComponentModel.DataAnnotations;

namespace GameGuild.Social.Blog;

public enum BlogAiRunStatus
{
    Queued = 1,
    Reserved = 2,
    Running = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6,
}

public enum BlogAiProposalStatus
{
    Pending = 1,
    Applied = 2,
    Discarded = 3,
}

public enum BlogAiProposalKind
{
    ReplaceDocument = 1,
    InsertAtCursor = 2,
    LexicalPatch = 3,
    MetadataPatch = 4,
}

public sealed class BlogAiConversation : EntityBase
{
    private BlogAiConversation() { }

    public Guid BlogPostId { get; private set; }
    public Guid AuthorId { get; private set; }
    public DateTimeOffset LastMessageAt { get; private set; }

    public static BlogAiConversation Create(
        Guid tenantId,
        Guid blogPostId,
        Guid authorId,
        DateTimeOffset now)
    {
        ValidateActor(tenantId, authorId);
        if (blogPostId == Guid.Empty)
        {
            throw new ArgumentException("Blog post ID is required.");
        }

        return new BlogAiConversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BlogPostId = blogPostId,
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
        {
            throw new UnauthorizedAccessException("AI authoring requires a tenant-scoped user actor.");
        }
    }
}

public sealed class BlogAiMessage
{
    private BlogAiMessage() { }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid? RunId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static BlogAiMessage Create(Guid conversationId, Guid? runId, string role, string content, DateTimeOffset now)
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation ID is required.", nameof(conversationId));
        }

        if (role is not ("user" or "assistant"))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Only user and assistant messages are supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new BlogAiMessage
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

public sealed class BlogAiRun : EntityBase
{
    private BlogAiRun() { }

    public Guid BlogPostId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid ActorId { get; private set; }
    public int BasePostRevision { get; private set; }
    public BlogAiProposalKind ProposalKind { get; private set; }
    public BlogAiRunStatus Status { get; private set; }

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

    public static BlogAiRun Create(
        Guid tenantId,
        Guid actorId,
        Guid blogPostId,
        Guid conversationId,
        int basePostRevision,
        BlogAiProposalKind proposalKind,
        string instruction,
        string? selection,
        string idempotencyKey,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("AI authoring requires a tenant-scoped user actor.");
        }

        if (blogPostId == Guid.Empty || conversationId == Guid.Empty)
        {
            throw new ArgumentException("Blog post and conversation IDs are required.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(basePostRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new BlogAiRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorId = actorId,
            BlogPostId = blogPostId,
            ConversationId = conversationId,
            BasePostRevision = basePostRevision,
            ProposalKind = proposalKind,
            Instruction = instruction.Trim(),
            Selection = string.IsNullOrWhiteSpace(selection) ? null : selection,
            IdempotencyKey = idempotencyKey.Trim(),
            Status = BlogAiRunStatus.Queued,
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
        EnsureStatus(BlogAiRunStatus.Queued);
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
        Status = BlogAiRunStatus.Reserved;
        UpdatedAt = now.UtcDateTime;
    }

    public void Start(DateTimeOffset now)
    {
        EnsureStatus(BlogAiRunStatus.Reserved);
        Status = BlogAiRunStatus.Running;
        StartedAt = now;
        UpdatedAt = now.UtcDateTime;
    }

    public void Complete(string responseText, int inputTokens, int outputTokens, long settledCost, long releasedAmount, DateTimeOffset now)
    {
        EnsureStatus(BlogAiRunStatus.Running);
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
        Status = BlogAiRunStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now.UtcDateTime;
    }

    public void Fail(string code, string message, long releasedAmount, DateTimeOffset now)
    {
        if (Status is BlogAiRunStatus.Completed or BlogAiRunStatus.Cancelled)
        {
            throw new InvalidOperationException("A terminal AI run cannot fail.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ErrorCode = code.Trim();
        ErrorMessage = message;
        ReleasedAmount = Math.Max(0, releasedAmount);
        Status = BlogAiRunStatus.Failed;
        CompletedAt = now;
        UpdatedAt = now.UtcDateTime;
    }

    public void RequestCancellation(DateTimeOffset now)
    {
        if (Status == BlogAiRunStatus.Cancelled)
        {
            return;
        }

        EnsureStatus(BlogAiRunStatus.Running);
        ErrorCode = "AI_CANCEL_REQUESTED";
        ErrorMessage = "Cancellation requested by the author.";
        UpdatedAt = now.UtcDateTime;
    }

    public void Cancel(long releasedAmount, DateTimeOffset now)
    {
        if (Status == BlogAiRunStatus.Cancelled)
        {
            return;
        }

        if (Status is not (BlogAiRunStatus.Reserved or BlogAiRunStatus.Running))
        {
            throw new InvalidOperationException("Only a reserved or running AI run can be cancelled.");
        }

        ReleasedAmount = Math.Max(0, releasedAmount);
        ErrorCode = "AI_CANCELLED";
        ErrorMessage = "AI generation was cancelled by the author.";
        Status = BlogAiRunStatus.Cancelled;
        CompletedAt = now;
        UpdatedAt = now.UtcDateTime;
    }

    private void EnsureStatus(BlogAiRunStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"AI run must be {expected} but is {Status}.");
        }
    }
}

public sealed class BlogAiProposal : EntityBase
{
    private BlogAiProposal() { }

    public Guid RunId { get; private set; }
    public Guid BlogPostId { get; private set; }
    public int BasePostRevision { get; private set; }
    public BlogAiProposalKind Kind { get; private set; }
    public BlogAiProposalStatus Status { get; private set; }
    public string OriginalContent { get; private set; } = string.Empty;
    public string ProposedContent { get; private set; } = string.Empty;
    public DateTimeOffset ProposedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }

    public static BlogAiProposal Create(
        Guid runId,
        Guid blogPostId,
        int basePostRevision,
        BlogAiProposalKind kind,
        string originalContent,
        string proposedContent,
        DateTimeOffset now)
    {
        if (runId == Guid.Empty || blogPostId == Guid.Empty)
        {
            throw new ArgumentException("Run and blog post IDs are required.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(basePostRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposedContent);

        return new BlogAiProposal
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            BlogPostId = blogPostId,
            BasePostRevision = basePostRevision,
            Kind = kind,
            OriginalContent = originalContent ?? string.Empty,
            ProposedContent = proposedContent,
            Status = BlogAiProposalStatus.Pending,
            ProposedAt = now,
            CreatedAt = now.UtcDateTime,
            UpdatedAt = now.UtcDateTime,
        };
    }

    public void EnsureApplicableTo(int currentPostRevision)
    {
        if (BasePostRevision != currentPostRevision)
        {
            throw new BlogRevisionConflictException(BasePostRevision, currentPostRevision);
        }

        if (Status != BlogAiProposalStatus.Pending)
        {
            throw new BlogAiProposalStateConflictException(Status);
        }
    }

    public void MarkApplied(Guid actorId, DateTimeOffset now)
    {
        Resolve(BlogAiProposalStatus.Applied, actorId, now);
    }

    public void Discard(Guid actorId, DateTimeOffset now)
    {
        Resolve(BlogAiProposalStatus.Discarded, actorId, now);
    }

    private void Resolve(BlogAiProposalStatus status, Guid actorId, DateTimeOffset now)
    {
        if (Status != BlogAiProposalStatus.Pending)
        {
            throw new BlogAiProposalStateConflictException(Status);
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor ID is required.", nameof(actorId));
        }

        Status = status;
        ResolvedBy = actorId;
        ResolvedAt = now;
        UpdatedAt = now.UtcDateTime;
    }
}

public sealed class BlogAiProposalStateConflictException(BlogAiProposalStatus currentStatus)
    : InvalidOperationException($"AI proposal is {currentStatus} and cannot be resolved again.")
{
    public BlogAiProposalStatus CurrentStatus { get; } = currentStatus;
}

public sealed class BlogAiStreamEvent
{
    private BlogAiStreamEvent() { }

    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public long Sequence { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? Delta { get; private set; }
    public string? PayloadJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static BlogAiStreamEvent Create(
        Guid runId,
        long sequence,
        string type,
        string status,
        string? delta,
        string? payloadJson,
        DateTimeOffset now)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run ID is required.", nameof(runId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        return new BlogAiStreamEvent
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
