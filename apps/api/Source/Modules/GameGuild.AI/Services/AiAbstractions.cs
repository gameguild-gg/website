using GameGuild;

namespace GameGuild.AI;

/// <summary>
///     Orchestrates provider selection, credential resolution, and execution of AI requests.
/// </summary>
public interface IAiOrchestrator
{
    Task<Result<AiCompletionResponse>> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default);

    Task<Result<AiCompletionResponse>> GenerateAsync(AiGenerateRequest request, CancellationToken cancellationToken = default);

    Task<Result<AiResolvedModelDto>> DescribeGenerateAsync(
        AiExecutionActor actor,
        AiGenerateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AiCompletionResponse>> GenerateForActorAsync(
        AiExecutionActor actor,
        AiGenerateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AiCompletionResponse>> GenerateForActorStreamingAsync(
        AiExecutionActor actor,
        AiGenerateRequest request,
        Func<string, CancellationToken, ValueTask> onDelta,
        CancellationToken cancellationToken = default);
}

public interface IAiConversationHistoryReader
{
    Task<IReadOnlyList<AiConversationHistoryEntryDto>> GetRecentAsync(Guid tenantId, int take, CancellationToken cancellationToken = default);
}

public interface IAiPromptTemplateService
{
    Task<Result<IReadOnlyList<AiPromptTemplateDto>>> ListAsync(
        Guid tenantId,
        string? category = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Result<AiPromptTemplateDto>> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    Task<Result<AiPromptTemplateDto>> CreateAsync(
        Guid tenantId,
        Guid? userId,
        CreateAiPromptTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AiPromptTemplateDto>> UpdateAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        UpdateAiPromptTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid tenantId, Guid id, Guid? userId, CancellationToken cancellationToken = default);

    Task<Result<AiPromptTemplateRenderResponse>> RenderAsync(
        Guid tenantId,
        Guid id,
        IReadOnlyDictionary<string, string?>? variables,
        CancellationToken cancellationToken = default);
}

internal interface IAiConversationHistoryRepository
    : IAiConversationHistoryReader
{
    Task AddAsync(AiConversationLog entry, CancellationToken cancellationToken = default);
}

internal interface IAiProviderAdapter
{
    AiProvider Provider { get; }

    Task<Result<AiProviderExecutionResult>> CompleteAsync(AiResolvedRequest request, CancellationToken cancellationToken = default);

    async Task<Result<AiProviderExecutionResult>> CompleteStreamingAsync(
        AiResolvedRequest request,
        Func<string, CancellationToken, ValueTask> onDelta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onDelta);
        var completion = await CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        if (completion.IsSuccess)
            await onDelta(completion.Value.Text, cancellationToken).ConfigureAwait(false);
        return completion;
    }
}
