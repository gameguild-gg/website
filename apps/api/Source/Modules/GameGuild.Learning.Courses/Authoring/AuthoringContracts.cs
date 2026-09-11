using System.Text.Json;

namespace GameGuild.Learning.Courses;

public sealed record AuthoringContentPayload(
    string Title,
    string Slug,
    string? Description,
    ProgramContentType Type,
    string? Body,
    JsonElement? JsonBody,
    LessonContentFormat? LessonFormat,
    ActivitySettings? ActivitySettings,
    bool IsRequired,
    int? EstimatedMinutes,
    EstimatedMinutesSource EstimatedMinutesSource,
    Visibility Visibility)
{
    public static AuthoringContentPayload From(ProgramContent content) => new(
        content.Title,
        content.Slug,
        content.Description,
        ProgramContentMappingExtensions.NormalizeProfessorFacingType(content.Type),
        content.Body,
        content.JsonBody is null ? null : JsonDocument.Parse(content.JsonBody).RootElement.Clone(),
        content.LessonFormat,
        content.GetActivitySettings(),
        content.IsRequired,
        content.EstimatedMinutes,
        content.EstimatedMinutesSource,
        content.Visibility);
}

public sealed record AuthoringDraftDto(
    Guid Id,
    Guid ProgramId,
    Guid ContentId,
    AuthoringContentPayload Payload,
    int BasePublishedVersion,
    int Revision,
    string ETag,
    Guid LastEditedBy,
    DateTimeOffset LastEditedAt);

public sealed record SaveAuthoringDraftRequest(int Revision, AuthoringContentPayload Payload);

public sealed record PublishAuthoringDraftRequest(int Revision);

public sealed record PublishAuthoringResult(AuthoringDraftDto Draft, ProgramContentDto PublishedContent);

public sealed record AiAuthoringRunRequest(
    Guid? ConversationId,
    int DraftRevision,
    string Instruction,
    AiProposalKind ProposalKind,
    string? Selection,
    string IdempotencyKey);

public sealed record AiStreamEvent(
    long Sequence,
    string Type,
    string? Delta,
    Guid RunId,
    string Status,
    AiCreditUsageDto? Usage = null,
    AiProposalDto? Proposal = null,
    string? ErrorCode = null);

public sealed record AiProposalDto(
    Guid Id,
    Guid RunId,
    int BaseDraftRevision,
    AiProposalKind Kind,
    AiProposalStatus Status,
    string OriginalContent,
    string ProposedContent,
    DateTimeOffset ProposedAt);

public sealed record AiCreditUsageDto(
    long AvailableSoftCredits,
    long MaximumEstimatedCost,
    int InputTokens,
    int OutputTokens,
    long SettledCost,
    long ReleasedAmount,
    string Currency = "SoftCoin");

public sealed record AiAuthoringMessageDto(
    Guid Id,
    string Role,
    string Content,
    Guid? RunId,
    DateTimeOffset CreatedAt);

public sealed record AiAuthoringConversationDto(
    Guid Id,
    Guid ContentId,
    Guid AuthorId,
    DateTimeOffset LastMessageAt,
    IReadOnlyList<AiAuthoringMessageDto> Messages);

public sealed record AiAuthoringRunDto(
    Guid Id,
    Guid ConversationId,
    Guid ContentId,
    int BaseDraftRevision,
    AiProposalKind ProposalKind,
    AiAuthoringRunStatus Status,
    string Instruction,
    string? Provider,
    string? Model,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    AiCreditUsageDto Usage,
    AiProposalDto? Proposal,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record AiEntitlementDto(
    long AvailableSoftCredits,
    long ReservedSoftCredits,
    long SettledSoftCredits,
    string Currency = "SoftCoin");

public sealed record ApplyAiProposalRequest(int DraftRevision, int? CursorOffset);
