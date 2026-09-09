using System.Text;
using System.Text.Json;
using Asp.Versioning;
using GameGuild;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using GameGuild.Resources;
using GameGuild.CQRS;

namespace GameGuild.AI;

/// <summary>
///     Canonical AI endpoints for chat-style and single-prompt generation requests.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/ai")]
[Microsoft.AspNetCore.Http.Tags("ai")]
[Authorize]
public sealed class AiController(
    ISender sender,
    IAiConversationHistoryReader historyRepository,
    IRequestContextAccessor requestContextAccessor,
    IResourceQuotaReader quotaReader,
    IOptions<AiOptions> aiOptions) : BaseApiController
{
    private readonly AiOptions _aiOptions = aiOptions.Value;

    [HttpGet("status")]
    [ProducesResponseType<AiStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<AiStatusResponse> Status()
    {
        var providers = Enum.GetValues<AiProvider>()
            .Select(provider =>
            {
                var options = ResolvePlatformProviderOptions(provider);
                return new AiProviderStatusDto(
                    AiProviderParser.ToResponseValue(provider),
                    options is not null,
                    options?.DefaultModel,
                    string.IsNullOrWhiteSpace(options?.BaseUrl) ? AiProviderParser.GetDefaultBaseUrl(provider) : options!.BaseUrl!.Trim(),
                    !string.IsNullOrWhiteSpace(options?.ApiKey));
            })
            .ToList();

        return Ok(new AiStatusResponse(
            _aiOptions.Enabled,
            _aiOptions.DefaultProvider,
            _aiOptions.AllowTenantOverrides,
            providers));
    }

    /// <summary>
    ///     Execute a conversational completion request.
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType<AiCompletionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiCompletionResponse>> Chat(
        [FromBody] AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ChatAiCommand(request), cancellationToken).ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("generate-content")]
    [ProducesResponseType<AiCompletionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiCompletionResponse>> GenerateContent(
        [FromBody] AiGeneratedContentRequest request,
        CancellationToken cancellationToken = default)
        => await GenerateTypedContentAsync(
            request.Kind,
            request.Subject,
            request.Context,
            request.Audience,
            request.Tone,
            request.Provider,
            request.Model,
            request.MaxTokens,
            cancellationToken).ConfigureAwait(false);

    [HttpPost("generate-content/email")]
    [HttpPost("email")]
    [ProducesResponseType<AiCompletionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiCompletionResponse>> GenerateEmail(
        [FromBody] AiGeneratedContentDraftRequest request,
        CancellationToken cancellationToken = default)
        => await GenerateTypedContentAsync(
            AiGeneratedContentKind.Email,
            request.Subject,
            request.Context,
            request.Audience,
            request.Tone,
            request.Provider,
            request.Model,
            request.MaxTokens,
            cancellationToken).ConfigureAwait(false);

    [HttpPost("generate-content/report")]
    [HttpPost("report")]
    [ProducesResponseType<AiCompletionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiCompletionResponse>> GenerateReport(
        [FromBody] AiGeneratedContentDraftRequest request,
        CancellationToken cancellationToken = default)
        => await GenerateTypedContentAsync(
            AiGeneratedContentKind.Report,
            request.Subject,
            request.Context,
            request.Audience,
            request.Tone,
            request.Provider,
            request.Model,
            request.MaxTokens,
            cancellationToken).ConfigureAwait(false);

    [HttpPost("generate-content/listing-description")]
    [ProducesResponseType<AiCompletionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiCompletionResponse>> GenerateListingDescription(
        [FromBody] AiGeneratedContentDraftRequest request,
        CancellationToken cancellationToken = default)
        => await GenerateTypedContentAsync(
            AiGeneratedContentKind.ListingDescription,
            request.Subject,
            request.Context,
            request.Audience,
            request.Tone,
            request.Provider,
            request.Model,
            request.MaxTokens,
            cancellationToken).ConfigureAwait(false);

    private async Task<ActionResult<AiCompletionResponse>> GenerateTypedContentAsync(
        AiGeneratedContentKind kind,
        string subject,
        string context,
        string? audience,
        string? tone,
        string? provider,
        string? model,
        int? maxTokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return BadRequest(new ProblemDetails { Title = "Subject is required", Status = StatusCodes.Status400BadRequest });

        if (string.IsNullOrWhiteSpace(context))
            return BadRequest(new ProblemDetails { Title = "Context is required", Status = StatusCodes.Status400BadRequest });

        var prompt = BuildGeneratedContentPrompt(kind, subject, context, audience, tone);
        var result = await sender.Send(new GenerateAiCommand(new AiGenerateRequest(
            provider,
            model,
            BuildGeneratedContentSystemPrompt(kind),
            prompt,
            Temperature: 0.4,
            MaxTokens: maxTokens ?? DefaultMaxTokens(kind))), cancellationToken).ConfigureAwait(false);

        return ToActionResult(result);
    }

    /// <summary>
    ///     Execute a single-prompt generation request.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType<AiCompletionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiCompletionResponse>> Generate(
        [FromBody] AiGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GenerateAiCommand(request), cancellationToken).ConfigureAwait(false);
        return ToActionResult(result);
    }

    /// <summary>
    ///     Retrieve recent AI conversation history for the active tenant.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType<IReadOnlyList<AiConversationHistoryEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AiConversationHistoryEntryDto>>> History(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (!requestContextAccessor.CurrentTenantId.HasValue)
            return Forbid();

        var entries = await historyRepository
            .GetRecentAsync(requestContextAccessor.CurrentTenantId.Value, take, cancellationToken)
            .ConfigureAwait(false);

        return Ok(entries);
    }

    [HttpGet("history/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportHistory(
        [FromQuery] string format = "csv",
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (!requestContextAccessor.CurrentTenantId.HasValue)
            return Forbid();

        var tenantId = requestContextAccessor.CurrentTenantId.Value;
        var entries = await historyRepository
            .GetRecentAsync(tenantId, Math.Clamp(take, 1, 500), cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
            return File(Encoding.UTF8.GetBytes(json), "application/json", $"ai-history-{tenantId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        }

        return File(Encoding.UTF8.GetBytes(BuildHistoryCsv(entries)), "text/csv", $"ai-history-{tenantId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpGet("quotas")]
    [ProducesResponseType<AiQuotaStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AiQuotaStatusResponse>> Quotas(CancellationToken cancellationToken = default)
    {
        if (!requestContextAccessor.CurrentTenantId.HasValue)
            return Forbid();

        var tenantId = requestContextAccessor.CurrentTenantId.Value;
        return Ok(await BuildQuotaStatusAsync(tenantId, cancellationToken).ConfigureAwait(false));
    }

    private async Task<AiQuotaStatusResponse> BuildQuotaStatusAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var quotas = new List<AiQuotaStatusDto>();
        foreach (var type in new[] { ResourceUsageType.AiRequests, ResourceUsageType.AiTokens })
        {
            var quota = await quotaReader.GetQuotaAsync(tenantId, type, cancellationToken).ConfigureAwait(false);
            var usage = quota?.ShouldReset() == true
                ? 0
                : quota?.CurrentUsage ?? await quotaReader.GetCurrentUsageAsync(tenantId, type, cancellationToken).ConfigureAwait(false);
            var hardLimit = quota?.HardLimit;
            var remaining = hardLimit.HasValue ? Math.Max(0, hardLimit.Value - usage) : long.MaxValue;

            quotas.Add(new AiQuotaStatusDto(
                type.ToString(),
                usage,
                quota?.SoftLimit,
                hardLimit,
                remaining,
                hardLimit is > 0 ? (double)usage / hardLimit.Value * 100 : 0,
                quota?.Period.ToString() ?? "Unlimited",
                quota?.IsActive ?? false,
                quota?.LastReset,
                quota?.GetNextResetTime()));
        }

        return new AiQuotaStatusResponse(tenantId, quotas, DateTime.UtcNow);
    }

    private AiProviderOptions? ResolvePlatformProviderOptions(AiProvider provider)
    {
        if (_aiOptions.Providers.TryGetValue(provider.ToString(), out var directMatch))
            return directMatch;

        foreach (var providerEntry in _aiOptions.Providers)
        {
            if (AiProviderParser.TryParse(providerEntry.Key, out var parsedProvider) && parsedProvider == provider)
                return providerEntry.Value;
        }

        return null;
    }

    private static string BuildGeneratedContentSystemPrompt(AiGeneratedContentKind kind)
        => kind switch
        {
            AiGeneratedContentKind.Email => "You write concise, compliant real-estate business emails. Return only the finished email.",
            AiGeneratedContentKind.Report => "You write executive real-estate operations reports with clear sections and concrete next actions.",
            AiGeneratedContentKind.ListingDescription => "You write fair-housing-compliant property listing descriptions. Avoid discriminatory language and unverifiable claims.",
            _ => "You write concise business content for real-estate operations."
        };

    private static string BuildGeneratedContentPrompt(
        AiGeneratedContentKind contentKind,
        string subject,
        string context,
        string? audience,
        string? tone)
    {
        var contentType = contentKind switch
        {
            AiGeneratedContentKind.Email => "an email",
            AiGeneratedContentKind.Report => "a report",
            AiGeneratedContentKind.ListingDescription => "a listing description",
            _ => "business content"
        };

        var normalizedAudience = string.IsNullOrWhiteSpace(audience) ? "the relevant real-estate stakeholders" : audience.Trim();
        var normalizedTone = string.IsNullOrWhiteSpace(tone) ? "professional and direct" : tone.Trim();

        return $"""
                Create {contentType}.

                Subject: {subject.Trim()}
                Audience: {normalizedAudience}
                Tone: {normalizedTone}

                Context:
                {context.Trim()}
                """;
    }

    private static int DefaultMaxTokens(AiGeneratedContentKind kind)
        => kind switch
        {
            AiGeneratedContentKind.Email => 700,
            AiGeneratedContentKind.Report => 1400,
            AiGeneratedContentKind.ListingDescription => 800,
            _ => 900
        };

    private static string BuildHistoryCsv(IReadOnlyList<AiConversationHistoryEntryDto> entries)
    {
        var csv = new StringBuilder();
        csv.AppendLine("id,userId,requestKind,provider,model,outcome,outcomeCode,inputTokens,outputTokens,totalTokens,occurredAt,requestText,responseText");

        foreach (var entry in entries)
        {
            csv.Append(entry.Id).Append(',')
                .Append(entry.UserId).Append(',')
                .Append(EscapeCsv(entry.RequestKind)).Append(',')
                .Append(EscapeCsv(entry.Provider)).Append(',')
                .Append(EscapeCsv(entry.Model)).Append(',')
                .Append(EscapeCsv(entry.Outcome)).Append(',')
                .Append(EscapeCsv(entry.OutcomeCode ?? string.Empty)).Append(',')
                .Append(entry.Usage.InputTokens).Append(',')
                .Append(entry.Usage.OutputTokens).Append(',')
                .Append(entry.Usage.TotalTokens).Append(',')
                .Append(entry.OccurredAt.ToString("O")).Append(',')
                .Append(EscapeCsv(entry.RequestText)).Append(',')
                .Append(EscapeCsv(entry.ResponseText ?? string.Empty))
                .AppendLine();
        }

        return csv.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
