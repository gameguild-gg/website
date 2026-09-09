using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Bounties;

public static class BountyFeePolicy
{
    public const int PartsPerMillion = 1_000_000;

    public static long Calculate(long units, int feePpm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(units);
        if (feePpm is < 0 or >= PartsPerMillion)
            throw new ArgumentOutOfRangeException(nameof(feePpm));
        return (long)(new BigInteger(units) * feePpm / PartsPerMillion);
    }
}

public sealed class BountyEscrowCoordinator
{
    private readonly object _gate = new();
    private readonly BountyEligibilityPolicy _eligibility;
    private readonly BountyClaimRiskGate _risk;
    private readonly RootReversalFenceRegistry _fences;
    private readonly Dictionary<BountyId, BountyEscrowPosition> _bounties = [];
    private readonly Dictionary<string, BountyEscrowPosition> _postIdempotency =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, TerminalRecord> _terminalIdempotency =
        new(StringComparer.Ordinal);

    public BountyEscrowCoordinator(
        BountyEligibilityPolicy eligibility,
        BountyClaimRiskGate risk,
        RootReversalFenceRegistry fences)
    {
        _eligibility = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
        _risk = risk ?? throw new ArgumentNullException(nameof(risk));
        _fences = fences ?? throw new ArgumentNullException(nameof(fences));
    }

    public BountyEscrowPosition Post(PostBountyCommand command)
    {
        var position = BountyEscrowPositionFactory.Create(command);

        lock (_gate)
        {
            if (_postIdempotency.TryGetValue(command.IdempotencyKey.Value, out var duplicate))
            {
                if (duplicate.Id == command.Id) return duplicate;
                throw new BountyIdempotencyConflictException(
                    "A bounty post idempotency key cannot identify another bounty.");
            }

            if (_bounties.ContainsKey(position.Id))
                throw new BountyIdempotencyConflictException("The bounty ID already exists.");
            var roots = position.EscrowFragments.SelectMany(fragment => fragment.SelectedRanges)
                .Select(range => range.Root).Distinct().ToArray();
            var fence = _fences.Capture(roots);

            return _fences.WithAllocationFence(fence, roots, () =>
            {
                _bounties.Add(position.Id, position);
                _postIdempotency.Add(command.IdempotencyKey.Value, position);
                return position;
            });
        }
    }

    public BountyClaimResult Claim(ClaimBountyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Eligibility);
        ArgumentNullException.ThrowIfNull(command.Risk);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.JournalSequence);

        lock (_gate)
        {
            if (TryGetTerminal<BountyClaimResult>(
                    command.IdempotencyKey, command.BountyId, out var duplicate))
                return duplicate;

            var bounty = Find(command.BountyId);
            EnsureOpen(bounty);
            if (command.ClaimedAt >= bounty.ExpiresAt)
                throw new BountyExpiredException("The bounty can no longer be claimed.");
            if (command.ClaimantId == bounty.PosterId ||
                command.ClaimantWalletId == bounty.PosterWalletId ||
                command.ClaimantWalletId == bounty.EscrowWalletId)
                throw new BountyClaimIneligibleException("A poster cannot claim their own bounty.");

            _eligibility.EnsureEligible(
                bounty.Eligibility, command.Eligibility, command.ClaimantId, command.ClaimedAt);

            var roots = bounty.EscrowFragments.SelectMany(fragment => fragment.SelectedRanges)
                .Select(range => range.Root).Distinct().ToArray();
            var fence = _fences.Capture(roots);
            return _fences.WithAllocationFence(fence, roots, () =>
            {
                _risk.Authorize(
                    bounty, command.ClaimantId, command.ClaimantWalletId, command.Risk, command.ClaimedAt);

                var sourceId = SourceStampId.New();
                var source = new SourceStampContract(
                    sourceId,
                    ClaimEvidenceHash(bounty, command),
                    SourceConfirmationState.Confirmed,
                    command.ClaimedAt,
                    command.ClaimedAt,
                    $"bounty:{bounty.Id.Value:N}:claim");
                var scale = CurrencyTraceScale.For(bounty.Amount.Currency);
                var provenance = bounty.Amount.Currency == CurrencyCode.HardCoin
                    ? ProvenanceKind.EarnedHard
                    : ProvenanceKind.EscrowReturn;
                var maturesAt = bounty.Amount.Currency == CurrencyCode.HardCoin
                    ? command.ClaimedAt.AddDays(120)
                    : command.ClaimedAt;
                var proceeds = new CreditLot(
                    CreditLotId.New(),
                    command.ClaimantWalletId,
                    bounty.Amount,
                    provenance,
                    command.ClaimedAt,
                    maturesAt,
                    command.JournalSequence,
                    CreditLotState.Active,
                    [new RootTraceRange(sourceId, 0, checked(bounty.Amount.Units * scale), 0)],
                    scale);
                var parents = bounty.EscrowFragments.Select(fragment =>
                    new ParentFragmentLineage(
                        fragment.ParentLot.Id,
                        fragment.Amount,
                        fragment.SelectedRanges.ToArray())).ToArray();
                var result = new BountyClaimResult(bounty.Id, source, proceeds, parents);
                bounty.Status = BountyStatus.Claimed;
                _terminalIdempotency.Add(
                    command.IdempotencyKey.Value, new TerminalRecord(bounty.Id, result));
                return result;
            });
        }
    }

    public BountyReclaimResult Reclaim(ReclaimBountyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.FirstJournalSequence);

        lock (_gate)
        {
            if (TryGetTerminal<BountyReclaimResult>(
                    command.IdempotencyKey, command.BountyId, out var duplicate))
                return duplicate;

            var bounty = Find(command.BountyId);
            EnsureOpen(bounty);
            if (command.PosterId != bounty.PosterId || command.PosterWalletId != bounty.PosterWalletId)
                throw new BountyOwnershipException("Only the poster can reclaim this bounty.");
            if (command.ReclaimedAt < bounty.ExpiresAt)
                throw new BountyNotExpiredException("The bounty cannot be reclaimed before expiry.");

            var roots = bounty.EscrowFragments.SelectMany(fragment => fragment.SelectedRanges)
                .Select(range => range.Root).Distinct().ToArray();
            var fence = _fences.Capture(roots);
            return _fences.WithAllocationFence(fence, roots, () =>
            {
                var feeUnits = BountyFeePolicy.Calculate(bounty.Amount.Units, bounty.ReclaimFeePpm);
                var returned = new CoinAmount(bounty.Amount.Currency, bounty.Amount.Units - feeUnits);
                var fee = new CoinAmount(bounty.Amount.Currency, feeUnits);
                var sources = bounty.EscrowFragments.Select(fragment => fragment.Selection).ToArray();
                var outputs = feeUnits == 0
                    ? new[] { returned }
                    : new[] { returned, fee };
                var partitions = LineagePartitioner.Partition(sources, outputs);
                var parents = bounty.EscrowFragments.ToDictionary(
                    fragment => fragment.ParentLot.Id, fragment => fragment.ParentLot);
                var nextSequence = command.FirstJournalSequence;
                var restoredLots = CreateRestoredLots(
                    partitions[0], parents, bounty.PosterWalletId, ref nextSequence);
                var feeLots = feeUnits == 0
                    ? []
                    : CreateFeeLots(
                        partitions[1], parents, command.FeeWalletId, command.ReclaimedAt, ref nextSequence);

                var result = new BountyReclaimResult(
                    bounty.Id, returned, fee, restoredLots, feeLots);
                bounty.Status = BountyStatus.Reclaimed;
                _terminalIdempotency.Add(
                    command.IdempotencyKey.Value, new TerminalRecord(bounty.Id, result));
                return result;
            });
        }
    }

    public BountyEscrowPosition Get(BountyId id)
    {
        lock (_gate) return Find(id);
    }

    public BountyStatus GetStatus(BountyId id, DateTimeOffset asOf)
    {
        lock (_gate)
        {
            var bounty = Find(id);
            return bounty.Status == BountyStatus.Open && asOf >= bounty.ExpiresAt
                ? BountyStatus.Expired
                : bounty.Status;
        }
    }

    private static IReadOnlyList<CreditLot> CreateRestoredLots(
        LineagePartition partition,
        IReadOnlyDictionary<CreditLotId, CreditLot> parents,
        WalletId posterWalletId,
        ref long sequence)
    {
        var lots = new List<CreditLot>();
        foreach (var fragment in partition.Selections)
        {
            var parent = parents[fragment.ParentLotId];
            lots.Add(new CreditLot(
                CreditLotId.New(),
                posterWalletId,
                fragment.Amount,
                parent.Provenance,
                parent.ConfirmedAt,
                parent.OriginalMaturesAt,
                sequence++,
                CreditLotState.Active,
                fragment.SelectedRanges,
                parent.TraceUnitsPerCoinUnit));
        }

        return lots;
    }

    private static IReadOnlyList<CreditLot> CreateFeeLots(
        LineagePartition partition,
        IReadOnlyDictionary<CreditLotId, CreditLot> parents,
        WalletId feeWalletId,
        DateTimeOffset confirmedAt,
        ref long sequence)
    {
        var lots = new List<CreditLot>();
        foreach (var fragment in partition.Selections)
        {
            var parent = parents[fragment.ParentLotId];
            var hard = fragment.Amount.Currency == CurrencyCode.HardCoin;
            lots.Add(new CreditLot(
                CreditLotId.New(),
                feeWalletId,
                fragment.Amount,
                hard ? ProvenanceKind.EarnedHard : ProvenanceKind.EscrowReturn,
                confirmedAt,
                hard ? confirmedAt.AddDays(120) : confirmedAt,
                sequence++,
                CreditLotState.Active,
                fragment.SelectedRanges,
                parent.TraceUnitsPerCoinUnit));
        }

        return lots;
    }

    private bool TryGetTerminal<T>(
        IdempotencyKey key,
        BountyId bountyId,
        out T result)
        where T : class
    {
        if (!_terminalIdempotency.TryGetValue(key.Value, out var record))
        {
            result = null!;
            return false;
        }

        if (record.BountyId == bountyId && record.Result is T typed)
        {
            result = typed;
            return true;
        }

        throw new BountyIdempotencyConflictException(
            "A terminal idempotency key cannot identify another bounty operation.");
    }

    private BountyEscrowPosition Find(BountyId id) =>
        _bounties.TryGetValue(id, out var bounty)
            ? bounty
            : throw new KeyNotFoundException($"Bounty {id.Value:N} was not found.");

    private static void EnsureOpen(BountyEscrowPosition bounty)
    {
        if (bounty.Status != BountyStatus.Open)
            throw new BountyTerminalConflictException("The bounty already has a terminal outcome.");
    }

    private static string ClaimEvidenceHash(BountyEscrowPosition bounty, ClaimBountyCommand command)
    {
        var roots = string.Join(',', bounty.EscrowFragments
            .SelectMany(fragment => fragment.SelectedRanges)
            .Select(range => range.Root.Value.ToString("N"))
            .OrderBy(value => value, StringComparer.Ordinal));
        var canonical = string.Join('|',
            bounty.Id.Value.ToString("N"),
            command.ClaimantId.ToString("N"),
            (int)bounty.Amount.Currency,
            bounty.Amount.Units,
            command.ClaimedAt.UtcTicks,
            roots);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record TerminalRecord(BountyId BountyId, object Result);
}
