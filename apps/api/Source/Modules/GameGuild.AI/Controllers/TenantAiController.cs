using System.Text;
using System.Text.Json;
using Asp.Versioning;
using GameGuild;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.Resources;

namespace GameGuild.AI;

/// <summary>
///     Tenant-scoped AI administration endpoints.
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("tenants/ai")]
[Authorize]
public sealed class TenantAiController(
    IAiConversationHistoryReader historyReader,
    IResourceQuotaReader quotaReader,
    IActorContextAccessor actorContextAccessor,
    ITenantMembershipChecker tenantMembershipChecker) : BaseApiController
{
    private async Task<bool> ValidateTenantMembershipAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;

        if (actor is null || !actor.IsAuthenticated || !actor.SubjectIdAsGuid.HasValue)
        {
            return false;
        }

        if (actor.IsSystemAdmin)
        {
            return true;
        }

        if (actor.TenantId.HasValue && actor.TenantId.Value == tenantId)
        {
            return true;
        }

        return await tenantMembershipChecker
            .IsUserMemberOfTenantAsync(actor.SubjectIdAsGuid.Value, tenantId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Retrieve recent AI conversation history for a tenant.
    /// </summary>
    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/ai/history")]
    [EndpointSummary("Get tenant AI history")]
    [EndpointDescription("Retrieves recent AI conversation history for a specific tenant.")]
    [ProducesResponseType<IReadOnlyList<AiConversationHistoryEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AiConversationHistoryEntryDto>>> GetHistory(
        Guid tenantId,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var normalizedTake = Math.Clamp(take, 1, 100);
        var entries = await historyReader.GetRecentAsync(tenantId, normalizedTake, cancellationToken).ConfigureAwait(false);

        return Ok(entries);
    }

    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/ai/history/export")]
    [EndpointSummary("Export tenant AI history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportHistory(
        Guid tenantId,
        [FromQuery] string format = "csv",
        [FromQuery] int take = 500,
        CancellationToken cancellationToken = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var entries = await historyReader
            .GetRecentAsync(tenantId, Math.Clamp(take, 1, 1000), cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
            return File(Encoding.UTF8.GetBytes(json), "application/json", $"tenant-ai-history-{tenantId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        }

        return File(Encoding.UTF8.GetBytes(BuildHistoryCsv(entries)), "text/csv", $"tenant-ai-history-{tenantId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpGet("v{version:apiVersion}/tenants/{tenantId:guid}/ai/quotas")]
    [EndpointSummary("Get tenant AI quotas")]
    [ProducesResponseType<AiQuotaStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AiQuotaStatusResponse>> GetQuotas(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!await ValidateTenantMembershipAsync(tenantId, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

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

            quotas.Add(new AiQuotaStatusDto(
                type.ToString(),
                usage,
                quota?.SoftLimit,
                hardLimit,
                hardLimit.HasValue ? Math.Max(0, hardLimit.Value - usage) : long.MaxValue,
                hardLimit is > 0 ? (double)usage / hardLimit.Value * 100 : 0,
                quota?.Period.ToString() ?? "Unlimited",
                quota?.IsActive ?? false,
                quota?.LastReset,
                quota?.GetNextResetTime()));
        }

        return new AiQuotaStatusResponse(tenantId, quotas, DateTime.UtcNow);
    }

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
