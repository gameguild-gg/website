using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed class PostgreSqlDurableAdRewardPolicyReader : IDurableAdRewardPolicyReader
{
    private readonly DbContext _db;
    private readonly ICapabilityPolicySignatureVerifier _signatureVerifier;

    public PostgreSqlDurableAdRewardPolicyReader(
        IApplicationDbContext context,
        ICapabilityPolicySignatureVerifier signatureVerifier)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(signatureVerifier);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Durable ad reward policies require the application's relational DbContext.");
        _signatureVerifier = signatureVerifier;
    }

    public async ValueTask<AdRewardNetworkPolicySnapshot> GetEffectiveAsync(
        Guid tenantId,
        string network,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(tenantId, network);
        var normalizedNetwork = network.Trim();
        var row = await _db.Set<AdNetworkPolicyVersionRow>()
            .AsNoTracking()
            .Where(policy => policy.TenantId == tenantId &&
                             policy.Network == normalizedNetwork &&
                             policy.EffectiveAt <= at && at < policy.ExpiresAt)
            .OrderByDescending(policy => policy.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return await ValidateAndMapAsync(row, tenantId, normalizedNetwork, cancellationToken);
    }

    public async ValueTask<AdRewardNetworkPolicySnapshot> GetVersionAsync(
        Guid tenantId,
        string network,
        PolicyVersion version,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(tenantId, network);
        var normalizedNetwork = network.Trim();
        var row = await _db.Set<AdNetworkPolicyVersionRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                policy => policy.TenantId == tenantId &&
                          policy.Network == normalizedNetwork &&
                          policy.Version == version.Value,
                cancellationToken);
        return await ValidateAndMapAsync(row, tenantId, normalizedNetwork, cancellationToken);
    }

    private async ValueTask<AdRewardNetworkPolicySnapshot> ValidateAndMapAsync(
        AdNetworkPolicyVersionRow? row,
        Guid tenantId,
        string network,
        CancellationToken cancellationToken)
    {
        if (row is null)
            throw new AdNetworkPolicyUnavailableException("No signed ad network policy is available.");
        var payloadHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(row.CanonicalPayload)));
        var signatureValid = string.Equals(payloadHash, row.PayloadHash, StringComparison.Ordinal) &&
                             await _signatureVerifier.VerifyAsync(
                                 row.CanonicalPayload, row.KeyId, row.Signature, cancellationToken);
        if (!signatureValid || row.ProposedBy == row.ApprovedBy)
            throw new AdNetworkPolicyUnavailableException("The ad network policy is invalid or not effective.");

        var policy = new AdNetworkPolicy(
            row.Network,
            new PolicyVersion(row.Version),
            row.EffectiveAt,
            row.ExpiresAt,
            row.IssuanceMode,
            row.YieldState,
            row.EstimatedNetEcpmUsdNanos,
            row.ContractedRevenueSharePpm,
            row.SafetyBufferPpm,
            row.MinimumVisiblePpm,
            TimeSpan.FromTicks(row.MaximumFocusLossTicks),
            row.MaximumRewardSoftUnits,
            row.ReportsCurrentThrough,
            TimeSpan.FromTicks(row.ReportStaleAfterTicks),
            row.Ranking);
        var budget = new AdRewardBudgetPolicy(
            row.MaximumUserSoftUnits,
            row.MaximumDeviceSoftUnits,
            row.MaximumNetworkSoftUnits,
            row.MaximumGlobalSoftUnits,
            row.FundedLossBudgetUsdNanos,
            TimeSpan.FromTicks(row.BudgetWindowTicks));
        return new AdRewardNetworkPolicySnapshot(
            tenantId,
            policy,
            budget,
            row.MaximumIpSoftUnits,
            row.MaximumAsnSoftUnits,
            row.ProviderHash,
            row.ProviderCertified,
            row.PayloadHash,
            row.KeyId,
            row.Signature);
    }

    private static void ValidateScope(Guid tenantId, string network)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(network);
    }
}
