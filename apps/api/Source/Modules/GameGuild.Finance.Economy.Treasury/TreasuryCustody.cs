using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury;

public sealed class TreasuryCustodyReconciler
{
    private readonly TreasuryCustodySigner _signer;

    public TreasuryCustodyReconciler(TreasuryCustodySigner signer) =>
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));

    public TreasuryCustodyReport Reconcile(
        ReserveHead head,
        IReadOnlyCollection<TreasuryCustodyObservation> observations,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(observations);
        if (head.ObservedAt > now || head.ExpiresAt <= now)
            throw new ReserveInputUnknownException("The reserve head is stale for custody reconciliation.");

        var byAsset = new Dictionary<string, TreasuryCustodyObservation>(StringComparer.Ordinal);
        foreach (var observation in observations)
        {
            if (observation is null || string.IsNullOrWhiteSpace(observation.AssetKey) ||
                string.IsNullOrWhiteSpace(observation.EvidenceHash) || observation.ActualUsdNanos < 0 ||
                observation.ObservedAt > now || observation.ExpiresAt <= now ||
                !byAsset.TryAdd(observation.AssetKey.Trim(), observation with { AssetKey = observation.AssetKey.Trim() }))
                throw new ReserveInputUnknownException("Custody evidence is missing, stale, invalid, or duplicated.");
        }

        if (head.AssetAllocations.Any(asset => !byAsset.ContainsKey(asset.AssetKey)))
            throw new ReserveInputUnknownException("Custody evidence is missing for an allocated reserve asset.");

        var expected = head.AssetAllocations.ToDictionary(asset => asset.AssetKey, asset => asset.EligibleUsdNanos, StringComparer.Ordinal);
        var variances = expected.Keys.Union(byAsset.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(assetKey =>
            {
                var expectedValue = expected.GetValueOrDefault(assetKey);
                var observation = byAsset[assetKey];
                return new TreasuryCustodyVariance(
                    assetKey,
                    expectedValue,
                    observation.ActualUsdNanos,
                    observation.ExplainedVarianceUsdNanos,
                    checked(observation.ActualUsdNanos - expectedValue - observation.ExplainedVarianceUsdNanos));
            }).ToArray();
        var reportObservedAt = observations.Count == 0 ? head.ObservedAt : observations.Max(item => item.ObservedAt);
        var earliestObservationExpiry = observations.Count == 0 ? head.ExpiresAt : observations.Min(item => item.ExpiresAt);
        var reportExpiresAt = head.ExpiresAt <= earliestObservationExpiry ? head.ExpiresAt : earliestObservationExpiry;
        var expectedTotal = Sum(variances.Select(item => item.ExpectedUsdNanos));
        var actualTotal = Sum(variances.Select(item => item.ActualUsdNanos));
        var explainedTotal = Sum(variances.Select(item => item.ExplainedVarianceUsdNanos));
        var unexplainedTotal = Sum(variances.Select(item => item.UnexplainedVarianceUsdNanos));
        var canonical = string.Join('|',
            head.Version.Value.ToString(CultureInfo.InvariantCulture),
            head.AuthorizationEpoch.ToString(CultureInfo.InvariantCulture),
            reportObservedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            reportExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            string.Join(';', variances.Select(item => string.Join(',', item.AssetKey, item.ExpectedUsdNanos,
                item.ActualUsdNanos, item.ExplainedVarianceUsdNanos, item.UnexplainedVarianceUsdNanos))));
        var evidenceHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var unsigned = new TreasuryCustodyReport(
            head.Version,
            head.AuthorizationEpoch,
            reportObservedAt,
            reportExpiresAt,
            expectedTotal,
            actualTotal,
            explainedTotal,
            unexplainedTotal,
            variances,
            evidenceHash,
            string.Empty);
        return unsigned with { Signature = _signer.Sign(unsigned) };
    }

    private static long Sum(IEnumerable<long> values)
    {
        var total = values.Aggregate(BigInteger.Zero, (current, value) => current + value);
        if (total > long.MaxValue || total < long.MinValue)
            throw new OverflowException("Custody reconciliation exceeded the supported range.");
        return (long)total;
    }
}

public sealed class TreasuryCustodySigner
{
    private readonly byte[] _secret;

    public TreasuryCustodySigner(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length < 32)
            throw new ArgumentException("Treasury custody signing secret must contain at least 32 bytes.", nameof(secret));
        _secret = [.. secret];
    }

    public string Sign(TreasuryCustodyReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return Convert.ToBase64String(HMACSHA256.HashData(
            _secret,
            Encoding.UTF8.GetBytes(TreasuryCustodyCanonicalizer.Canonicalize(report))));
    }

    public bool Verify(TreasuryCustodyReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (string.IsNullOrWhiteSpace(report.Signature)) return false;
        byte[] supplied;
        try { supplied = Convert.FromBase64String(report.Signature); }
        catch (FormatException) { return false; }
        var expected = Convert.FromBase64String(Sign(report));
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}

internal static class TreasuryCustodyCanonicalizer
{
    internal static string Canonicalize(TreasuryCustodyReport report) => string.Join('|',
        report.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture),
        report.AuthorizationEpoch.ToString(CultureInfo.InvariantCulture),
        report.ObservedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        report.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        report.ExpectedUsdNanos.ToString(CultureInfo.InvariantCulture),
        report.ActualUsdNanos.ToString(CultureInfo.InvariantCulture),
        report.ExplainedVarianceUsdNanos.ToString(CultureInfo.InvariantCulture),
        report.UnexplainedVarianceUsdNanos.ToString(CultureInfo.InvariantCulture),
        string.Join(';', report.Variances.OrderBy(item => item.AssetKey, StringComparer.Ordinal)
            .Select(item => string.Join(',', item.AssetKey, item.ExpectedUsdNanos, item.ActualUsdNanos,
                item.ExplainedVarianceUsdNanos, item.UnexplainedVarianceUsdNanos))),
        report.EvidenceHash);
 }
 public sealed class TreasuryOperationGate
{
    private readonly CoreReserveAuthority _authority;
    private readonly TreasuryCustodySigner _signer;

    public TreasuryOperationGate(CoreReserveAuthority authority, TreasuryCustodySigner signer)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
    }

    public ReservePostingAuthorization Authorize(
        TreasuryProtectedOperation operation,
        ReserveVersion reserveVersion,
        long authorizationEpoch,
        TreasuryCustodyReport custody,
        CoinAmount? liabilityIncrease,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(custody);
        if (!_signer.Verify(custody))
            throw new TreasurySignatureException("Treasury custody report signature is invalid.");
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (custody.ReserveVersion != reserveVersion || custody.AuthorizationEpoch != authorizationEpoch ||
            custody.ObservedAt > now || custody.ExpiresAt <= now)
            throw new ReserveInputUnknownException("Custody reconciliation is stale or does not bind the active reserve head.");
        if (!custody.IsReconciled)
            throw new TreasuryCustodyVarianceException("Unexplained custody variance blocks protected value movement.");
        if (operation == TreasuryProtectedOperation.Issuance)
        {
            if (liabilityIncrease is null || liabilityIncrease.Value.Units <= 0)
                throw new ArgumentException("Issuance requires a positive liability increase.", nameof(liabilityIncrease));
            return _authority.AuthorizeIssuance(reserveVersion, authorizationEpoch, liabilityIncrease.Value, now);
        }
        if (liabilityIncrease is not null)
            throw new ArgumentException("Only issuance accepts a liability increase.", nameof(liabilityIncrease));
        return _authority.Authorize(reserveVersion, authorizationEpoch, now);
    }
}

public static class TreasuryAuditExporter
{
    public static string Export(
        TreasuryProposalEnvelope envelope,
        ReserveHead head,
        TreasuryCustodyReport custody)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(custody);
        return JsonSerializer.Serialize(new
        {
            reserveVersion = head.Version.Value,
            authorizationEpoch = head.AuthorizationEpoch,
            coverage = head.Coverage.ToString(),
            proposalEvidenceHash = envelope.Proposal.EvidenceHash,
            custodyEvidenceHash = custody.EvidenceHash,
            unexplainedVarianceUsdNanos = custody.UnexplainedVarianceUsdNanos,
            assets = head.AssetAllocations.OrderBy(asset => asset.AssetKey, StringComparer.Ordinal)
                .Select(asset => new { asset.AssetKey, purpose = asset.Purpose.ToString(), asset.EligibleUsdNanos }),
            liabilities = new
            {
                envelope.Proposal.Liabilities.OutstandingHardUnits,
                envelope.Proposal.Liabilities.OutstandingSoftUnits,
                envelope.Proposal.Liabilities.UnreservedSoftUnits,
                envelope.Proposal.Liabilities.IrreversibleInFlightProviderCostUsdNanos
            },
            custody = custody.Variances.OrderBy(item => item.AssetKey, StringComparer.Ordinal)
        });
    }
}
