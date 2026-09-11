using System.Text.Json;

namespace GameGuild.AI;

/// <summary>
///     Supported AI provider backends.
/// </summary>
public enum AiProvider
{
    OpenAi,
    Anthropic,
    Google
}

/// <summary>
///     Chat message payload for AI requests.
/// </summary>
public sealed record AiChatMessage(string Role, string Content);

/// <summary>
///     Canonical request shape for conversational completions.
/// </summary>
public sealed record AiChatRequest(
    string? Provider,
    string? Model,
    string? SystemPrompt,
    IReadOnlyList<AiChatMessage> Messages,
    double? Temperature,
    int? MaxTokens);

/// <summary>
///     Canonical request shape for single-prompt generation.
/// </summary>
public sealed record AiGenerateRequest(
    string? Provider,
    string? Model,
    string? SystemPrompt,
    string Prompt,
    double? Temperature,
    int? MaxTokens);

/// <summary>
/// Server-derived billing actor for an AI execution. This value is created from the
/// authenticated request context and is never accepted from a browser payload.
/// </summary>
public sealed record AiExecutionActor(Guid TenantId, Guid UserId);

public sealed record AiResolvedModelDto(string Provider, string Model, int MaximumOutputTokens);

/// <summary>
///     Normalized token usage information returned by a provider.
/// </summary>
public sealed record AiUsageDto(int? InputTokens, int? OutputTokens, int? TotalTokens);

/// <summary>
///     Canonical completion response shape exposed by the GameGuild API.
/// </summary>
public sealed record AiCompletionResponse(
    string Provider,
    string Model,
    string Text,
    string? FinishReason,
    AiUsageDto Usage);

public enum AiGeneratedContentKind
{
    Email = 0,
    Report = 1,
    ListingDescription = 2
}

public sealed record AiGeneratedContentRequest(
    AiGeneratedContentKind Kind,
    string Subject,
    string Context,
    string? Audience,
    string? Tone,
    string? Provider,
    string? Model,
    int? MaxTokens);

public sealed record AiGeneratedContentDraftRequest(
    string Subject,
    string Context,
    string? Audience,
    string? Tone,
    string? Provider,
    string? Model,
    int? MaxTokens);

public sealed record AiProviderStatusDto(
    string Provider,
    bool Configured,
    string? DefaultModel,
    string BaseUrl,
    bool CredentialsConfigured);

public sealed record AiStatusResponse(
    bool Enabled,
    string? DefaultProvider,
    bool AllowTenantOverrides,
    IReadOnlyList<AiProviderStatusDto> Providers);

public sealed record AiQuotaStatusDto(
    string ResourceType,
    long CurrentUsage,
    long? SoftLimit,
    long? HardLimit,
    long Remaining,
    double UsagePercent,
    string Period,
    bool IsActive,
    DateTime? LastReset,
    DateTime? NextReset);

public sealed record AiQuotaStatusResponse(
    Guid TenantId,
    IReadOnlyList<AiQuotaStatusDto> Quotas,
    DateTime GeneratedAtUtc);

/// <summary>
///     Conversation history entry returned by the AI history endpoint.
/// </summary>
public sealed record AiConversationHistoryEntryDto(
    Guid Id,
    Guid? UserId,
    string RequestKind,
    string Provider,
    string Model,
    string RequestText,
    string? SystemPrompt,
    string? ResponseText,
    string Outcome,
    string? OutcomeCode,
    string? OutcomeReason,
    string? FinishReason,
    AiUsageDto Usage,
    DateTime OccurredAt);

public sealed record AiPromptTemplateDto(
    Guid Id,
    Guid? TenantId,
    string Key,
    string Name,
    string? Description,
    string Category,
    string? SystemPrompt,
    string Prompt,
    bool IsActive,
    bool IsSystemTemplate,
    Guid? CreatedByUserId,
    Guid? UpdatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateAiPromptTemplateRequest(
    string Key,
    string Name,
    string Prompt,
    string? Description = null,
    string? Category = null,
    string? SystemPrompt = null,
    bool? IsActive = null);

public sealed record UpdateAiPromptTemplateRequest(
    string? Name = null,
    string? Prompt = null,
    string? Description = null,
    string? Category = null,
    string? SystemPrompt = null,
    bool? IsActive = null);

public sealed record AiPromptTemplateRenderRequest(
    IReadOnlyDictionary<string, string?>? Variables = null);

public sealed record AiPromptTemplateRenderResponse(
    Guid TemplateId,
    string Key,
    string? SystemPrompt,
    string Prompt,
    IReadOnlyDictionary<string, string?> Variables);

public sealed record AiPromptTemplateGenerateRequest(
    IReadOnlyDictionary<string, string?>? Variables = null,
    string? Provider = null,
    string? Model = null,
    double? Temperature = null,
    int? MaxTokens = null);

internal enum AiRequestKind
{
    Chat,
    Generate
}

internal sealed record AiResolvedRequest(
    Guid TenantId,
    AiRequestKind RequestKind,
    AiProvider Provider,
    string Model,
    string? SystemPrompt,
    IReadOnlyList<AiChatMessage> Messages,
    double? Temperature,
    int? MaxTokens,
    string ApiKey,
    string BaseUrl,
    AiModerationConfiguration Moderation,
    AiHistoryConfiguration History);

internal sealed record AiProviderExecutionResult(
    string Model,
    string Text,
    string? FinishReason,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens);

internal sealed record AiTenantProviderConfiguration(bool Enabled, string? DefaultModel);

internal sealed record AiModerationConfiguration(
    bool Enabled,
    IReadOnlyList<string> BlockedTerms,
    int? MaxPromptCharacters,
    int? MaxResponseCharacters);

internal sealed record AiHistoryConfiguration(bool Enabled);

internal sealed record AiTenantConfiguration(
    bool Enabled,
    bool AllowRequestOverride,
    AiProvider? DefaultProvider,
    string? DefaultChatModel,
    string? DefaultGenerateModel,
    IReadOnlyDictionary<AiProvider, AiTenantProviderConfiguration> Providers,
    IReadOnlyDictionary<AiProvider, string> ApiKeys,
    AiModerationConfiguration Moderation,
    AiHistoryConfiguration History);

internal static class AiProviderParser
{
    public static bool TryParse(string? value, out AiProvider provider)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "openai":
            case "open-ai":
            case "gpt":
                provider = AiProvider.OpenAi;
                return true;

            case "anthropic":
            case "claude":
                provider = AiProvider.Anthropic;
                return true;

            case "google":
            case "gemini":
            case "googleai":
                provider = AiProvider.Google;
                return true;

            default:
                provider = default;
                return false;
        }
    }

    public static string ToResponseValue(AiProvider provider) => provider switch
    {
        AiProvider.OpenAi => "OpenAi",
        AiProvider.Anthropic => "Anthropic",
        AiProvider.Google => "Google",
        _ => provider.ToString()
    };

    public static string ToApiKeySetting(AiProvider provider) => provider switch
    {
        AiProvider.OpenAi => "ai:openai",
        AiProvider.Anthropic => "ai:anthropic",
        AiProvider.Google => "ai:google",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };

    public static string GetDefaultBaseUrl(AiProvider provider) => provider switch
    {
        AiProvider.OpenAi => "https://api.openai.com",
        AiProvider.Anthropic => "https://api.anthropic.com",
        AiProvider.Google => "https://generativelanguage.googleapis.com",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };

    public static JsonElement? ToJsonElement(object? value)
    {
        if (value is null)
            return null;

        if (value is JsonElement element)
            return element;

        return JsonSerializer.SerializeToElement(value, value.GetType());
    }
}
