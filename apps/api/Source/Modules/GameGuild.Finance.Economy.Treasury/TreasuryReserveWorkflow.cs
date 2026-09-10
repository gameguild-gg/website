using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury;

public static class TreasuryReservePlanner
{
    public static TreasuryProposalEnvelope Build(
        TreasuryProposalRequest request,
        TreasuryProposalSigner signer,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(signer);
        if (request.BufferPolicy.Version != request.PolicyVersion)
            throw new ReserveInputUnknownException("The Treasury buffer policy version does not match the proposal.");

        var liabilities = TreasuryLiabilityCalculator.Calculate(
            request.Ledger,
            request.CompanyOwnedWallets,
            request.ServiceCosts,
            request.OpenAuthorizations,
            now);
        var buffers = request.BufferPolicy.Calculate(liabilities.Position, request.BufferExposure, now);
        var allocations = TreasuryAssetAllocator.Allocate(request.Assets, now);
        var unsigned = new ReserveProposal(
            request.Version,
            request.ExpectedActiveVersion,
            request.PolicyVersion,
            request.AuthorizationEpoch,
            request.ObservedAt,
            request.ExpiresAt,
            liabilities.Position,
            buffers,
            liabilities.Services,
            allocations,
            "pending");
        var evidenceManifest = TreasuryEvidenceManifestCanonicalizer.Canonicalize(request, liabilities, unsigned);
        var evidenceHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(evidenceManifest)));
        var proposal = unsigned with { EvidenceHash = evidenceHash };
        return new TreasuryProposalEnvelope(
            proposal,
            liabilities,
            request.Assets.OrderBy(asset => asset.AssetKey, StringComparer.Ordinal).ToArray(),
            evidenceManifest,
            signer.Sign(proposal));
    }
}

public sealed class TreasuryProposalSigner
{
    private readonly byte[] _secret;

    public TreasuryProposalSigner(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length < 32)
            throw new ArgumentException("Treasury signing secret must contain at least 32 bytes.", nameof(secret));
        _secret = [.. secret];
    }

    public string Sign(ReserveProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return Convert.ToBase64String(HMACSHA256.HashData(
            _secret,
            Encoding.UTF8.GetBytes(TreasuryProposalCanonicalizer.Canonicalize(proposal, true))));
    }

    public bool Verify(ReserveProposal proposal, string signature)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (string.IsNullOrWhiteSpace(signature)) return false;
        byte[] supplied;
        try { supplied = Convert.FromBase64String(signature); }
        catch (FormatException) { return false; }
        var expected = Convert.FromBase64String(Sign(proposal));
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}

public sealed class TreasuryCoreActivationGateway
{
    private readonly CoreReserveAuthority _authority;
    private readonly TreasuryProposalSigner _signer;

    public TreasuryCoreActivationGateway(CoreReserveAuthority authority, TreasuryProposalSigner signer)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
    }

    public ReserveHead Activate(TreasuryProposalEnvelope envelope, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var manifestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(envelope.EvidenceManifest)));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(manifestHash),
                Encoding.ASCII.GetBytes(envelope.Proposal.EvidenceHash)) ||
            !_signer.Verify(envelope.Proposal, envelope.Signature))
            throw new TreasurySignatureException("Treasury reserve proposal signature is invalid.");
        return _authority.ValidateAndActivate(envelope.Proposal, now);
    }
}

internal static class TreasuryEvidenceManifestCanonicalizer
{
    internal static string Canonicalize(
        TreasuryProposalRequest request,
        TreasuryLiabilityCalculation liabilities,
        ReserveProposal proposal)
    {
        var policy = request.BufferPolicy;
        var exposure = request.BufferExposure;
        var rules = new[]
        {
            policy.ChargebackRefund, policy.PayoutSettlement, policy.HardOperatingLiquidity,
            policy.AdEstimateVariance, policy.FraudLoss, policy.ProviderFx, policy.SoftOperatingLiquidity
        };
        var lots = liabilities.Lots.OrderBy(lot => lot.LotId.Value)
            .Select(lot => $"{lot.LotId.Value:N},{lot.WalletId.Value:N},{(int)lot.Currency},{lot.OutstandingUnits},{(int)lot.State}");
        var authorizations = request.OpenAuthorizations.OrderBy(item => item.AuthorizationKey, StringComparer.Ordinal)
            .Select(item => $"{item.AuthorizationKey.Trim()},{item.ServiceCode.Trim()},{item.ReservedSoftUnits},{item.IrreversibleProviderCostUsdNanos}");
        var assets = request.Assets.OrderBy(item => item.AssetKey, StringComparer.Ordinal)
            .Select(item => string.Join(',', item.AssetKey, (int)item.Kind, (int)item.Purpose, item.GrossUsdNanos,
                (int)item.Finality, item.HaircutPpm, item.ObservedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                item.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), item.EvidenceHash.Trim()));
        return string.Join('|',
            TreasuryProposalCanonicalizer.Canonicalize(proposal, false),
            policy.Version.Value, policy.Owner.Trim(),
            policy.ObservedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            policy.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            string.Join(';', rules.Select(rule => $"{rule.AbsoluteFloor},{rule.PercentageFloorPpm}")),
            string.Join(',', exposure.ChargebackRefundUsdMinor, exposure.PayoutSettlementUsdMinor,
                exposure.HardOperatingLiquidityUsdMinor, exposure.AdEstimateVarianceUsdNanos,
                exposure.FraudLossUsdNanos, exposure.ProviderFxUsdNanos, exposure.SoftOperatingLiquidityUsdNanos),
            string.Join(';', lots), string.Join(';', authorizations), string.Join(';', assets));
    }
}
internal static class TreasuryProposalCanonicalizer
{
    internal static string Canonicalize(ReserveProposal proposal, bool includeEvidenceHash)
    {
        var liabilities = proposal.Liabilities;
        var buffers = proposal.Buffers;
        var services = proposal.Services.OrderBy(service => service.ServiceCode, StringComparer.Ordinal)
            .Select(service => string.Join(',',
                Clean(service.ServiceCode), Number(service.CurrentServicePriceSoftUnits),
                Number(service.CurrentProviderCostUsdNanos), Number(service.TrailingHighPercentileCostUsdNanos),
                Number(service.ProviderFxStressCostUsdNanos), Number(service.ReservedSoftUnits),
                service.Enabled ? "1" : "0", Timestamp(service.ObservedAt), Timestamp(service.ExpiresAt)));
        var assets = proposal.AssetAllocations.OrderBy(asset => asset.AssetKey, StringComparer.Ordinal)
            .Select(asset => string.Join(',', Clean(asset.AssetKey), Number((int)asset.Purpose), Number(asset.EligibleUsdNanos)));
        return string.Join('|',
            Number(proposal.Version.Value),
            proposal.ExpectedActiveVersion is null ? "none" : Number(proposal.ExpectedActiveVersion.Value.Value),
            Number(proposal.PolicyVersion.Value), Number(proposal.AuthorizationEpoch),
            Timestamp(proposal.ObservedAt), Timestamp(proposal.ExpiresAt),
            Number(liabilities.OutstandingHardUnits), Number(liabilities.OutstandingSoftUnits),
            Number(liabilities.UnreservedSoftUnits), Number(liabilities.IrreversibleInFlightProviderCostUsdNanos),
            Number(buffers.ChargebackRefundBufferUsdMinor), Number(buffers.PayoutSettlementBufferUsdMinor),
            Number(buffers.HardOperatingLiquidityBufferUsdMinor), Number(buffers.AdEstimateVarianceBufferUsdNanos),
            Number(buffers.FraudLossBudgetUsdNanos), Number(buffers.ProviderFxBufferUsdNanos),
            Number(buffers.SoftOperatingLiquidityBufferUsdNanos),
            string.Join(';', services), string.Join(';', assets),
            includeEvidenceHash ? Clean(proposal.EvidenceHash) : string.Empty);
    }

    private static string Clean(string value) => value.Trim().Replace("|", "%7C", StringComparison.Ordinal)
        .Replace(",", "%2C", StringComparison.Ordinal).Replace(";", "%3B", StringComparison.Ordinal);
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Timestamp(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
