using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public sealed class PostgreSqlRegisteredPostingCapabilityResolver : IRegisteredPostingCapabilityResolver
{
    private readonly DbContext _db;

    public PostgreSqlRegisteredPostingCapabilityResolver(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Registered economy capability resolution requires the application's relational DbContext.");
    }

    public async Task<RegisteredPostingCapability> ResolveAsync(
        string capabilityName,
        PostingTemplateKind templateKind,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);
        if (!Enum.IsDefined(templateKind))
            throw new ArgumentOutOfRangeException(nameof(templateKind));

        var normalizedName = capabilityName.Trim();
        var row = await _db.Set<EconomyRegisteredCapabilityRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                capability => capability.Name == normalizedName &&
                              capability.IsEnabled &&
                              capability.RevokedAt == null,
                cancellationToken);

        if (row is null)
            throw new RegisteredPostingCapabilityUnavailableException(
                $"Registered economy capability '{normalizedName}' is unavailable.");

        int[] allowedTemplateKinds;
        try
        {
            allowedTemplateKinds = JsonSerializer.Deserialize<int[]>(row.AllowedTemplateKinds) ?? [];
        }
        catch (JsonException exception)
        {
            throw new RegisteredPostingCapabilityUnavailableException(
                $"Registered economy capability '{normalizedName}' has an invalid template policy.",
                exception);
        }

        if (!allowedTemplateKinds.Contains((int)templateKind))
            throw new RegisteredPostingCapabilityUnavailableException(
                $"Registered economy capability '{normalizedName}' does not authorize template '{templateKind}'.");

        return new RegisteredPostingCapability(row.Id, row.Name, templateKind);
    }

    public async Task<RegisteredPostingAuthority> ResolveAuthorityAsync(
        string capabilityName,
        PostingTemplateKind templateKind,
        CapabilityAuthorizationReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.RiskDecisionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(receipt.OperationFingerprint))
            throw new RegisteredPostingCapabilityUnavailableException(
                "The capability receipt has no durable risk-decision binding.");

        var capability = await ResolveAsync(capabilityName, templateKind, cancellationToken);
        var riskDecision = await _db.Set<EconomyRiskDecisionRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                decision => decision.Id == receipt.RiskDecisionId &&
                            decision.Outcome == RiskOutcome.Allow &&
                            decision.TemplateKind == templateKind &&
                            decision.OperationFingerprint == receipt.OperationFingerprint &&
                            decision.PolicyVersion == receipt.PolicyVersion &&
                            decision.ReserveVersion == receipt.ReserveVersion &&
                            decision.ExpiresAt > receipt.IssuedAt,
                cancellationToken);
        if (riskDecision is null || riskDecision.CounterVersion <= 0)
            throw new RegisteredPostingCapabilityUnavailableException(
                "The capability receipt is not bound to an active registered-posting risk decision.");

        return new RegisteredPostingAuthority(
            capability.Id,
            receipt.ActorId,
            receipt.TenantId,
            receipt.RiskDecisionId,
            receipt.OperationFingerprint,
            riskDecision.CounterVersion);
    }
}
