using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Posting;

namespace GameGuild.Finance.Economy.Bounties;

/// <summary>
/// Produces the immutable reclaim posting from the materialized bounty escrow. Each debit/credit
/// pair represents one exact escrow fragment, so return lots retain source provenance while a
/// configured fee is auditable as the remaining fragment range rather than a derived balance.
/// </summary>
public static class BountyReclaimPostingFactory
{
    public static RegisteredPostingRequest Create(
        PersistedBountyEscrow escrow,
        DurableBountyReclaimRequest request)
    {
        ArgumentNullException.ThrowIfNull(escrow);
        ArgumentNullException.ThrowIfNull(request);
        if (escrow.Id != request.BountyId)
            throw new ArgumentException("The bounty reclaim must target the persisted escrow.", nameof(request));
        if (escrow.Status != BountyStatus.Open)
            throw new BountyTerminalConflictException("Only an open bounty can be reclaimed.");
        if (request.PosterId != escrow.PosterId || request.PosterWalletId != escrow.PosterWalletId)
            throw new BountyOwnershipException("Only the poster can reclaim this bounty.");
        if (request.Authority.ActorId != request.PosterId)
            throw new ArgumentException("The bounty reclaim authority must be the poster.", nameof(request));
        if (request.ReclaimedAt < escrow.ExpiresAt)
            throw new BountyNotExpiredException("The bounty cannot be reclaimed before expiry.");

        var fragments = escrow.Fragments.OrderBy(EscrowLotSortKey, StringComparer.Ordinal).ToArray();
        if (fragments.Length == 0 || fragments.Any(fragment => fragment.EscrowLotId is null))
            throw new InvalidOperationException("A bounty reclaim requires every materialized escrow lot.");
        if (fragments.Any(fragment => fragment.Amount.Currency != escrow.Amount.Currency) ||
            fragments.Sum(fragment => fragment.Amount.Units) != escrow.Amount.Units)
            throw new InvalidOperationException("Bounty escrow fragments do not conserve the reclaim amount.");

        var feeUnits = BountyFeePolicy.Calculate(escrow.Amount.Units, escrow.ReclaimFeePpm);
        var remainingReturn = checked(escrow.Amount.Units - feeUnits);
        var lines = new List<PostingLine>(fragments.Length * 2 + (feeUnits > 0 ? 2 : 0));
        var allocations = new List<RegisteredPostingAllocation>(fragments.Length * 2);
        var sequence = 1;

        foreach (var fragment in fragments)
        {
            var returnedUnits = Math.Min(fragment.Amount.Units, remainingReturn);
            var feeFragmentUnits = checked(fragment.Amount.Units - returnedUnits);
            var (returnRanges, feeRanges) = PartitionRanges(fragment, returnedUnits);

            if (returnedUnits > 0)
            {
                AddPair(
                    lines,
                    allocations,
                    ref sequence,
                    fragment,
                    returnedUnits,
                    returnRanges,
                    EscrowFor(escrow.Amount.Currency),
                    LiabilityFor(escrow.Amount.Currency, fragment.Provenance),
                    request.PosterWalletId,
                    fragment.Provenance);
                remainingReturn -= returnedUnits;
            }

            if (feeFragmentUnits > 0)
            {
                AddPair(
                    lines,
                    allocations,
                    ref sequence,
                    fragment,
                    feeFragmentUnits,
                    feeRanges,
                    EscrowFor(escrow.Amount.Currency),
                    FeeDestinationFor(escrow.Amount.Currency),
                    null,
                    null);
            }
        }

        EnsurePartitionComplete(
            remainingReturn,
            lines.Count,
            allocations.Sum(item => item.AmountUnits),
            escrow.Amount.Units);

        var posting = new PostingRequest(
            DeterministicPostingId(escrow.Id, request.IdempotencyKey),
            new PostingTemplate(PostingTemplateKind.BountyReclaim, PostingTemplate.CurrentVersion),
            request.IdempotencyKey,
            PostingAuthority.EscrowCoordinator,
            request.ReserveVersion,
            request.PolicyVersion,
            null,
            request.ReclaimedAt,
            lines);
        PostingMatrix.EnsureValid(posting);
        return new RegisteredPostingRequest(request.Authority, posting, allocations, request.DispatchSnapshotHash);
    }

    private static void AddPair(
        ICollection<PostingLine> lines,
        ICollection<RegisteredPostingAllocation> allocations,
        ref int sequence,
        PersistedBountyEscrowFragment fragment,
        long units,
        IReadOnlyList<RootTraceRange> ranges,
        EconomyAccountCode debitAccount,
        EconomyAccountCode creditAccount,
        WalletId? creditWalletId,
        ProvenanceKind? creditProvenance)
    {
        var amount = new CoinAmount(fragment.Amount.Currency, units);
        var debitSequence = sequence++;
        lines.Add(new PostingLine(
            debitSequence,
            EntrySide.Debit,
            debitAccount,
            amount,
            null,
            null,
            null));
        lines.Add(new PostingLine(
            sequence++,
            EntrySide.Credit,
            creditAccount,
            amount,
            creditWalletId,
            null,
            creditProvenance));
        allocations.Add(new RegisteredPostingAllocation(
            debitSequence,
            fragment.EscrowLotId!.Value,
            units,
            ranges));
    }

    private static (IReadOnlyList<RootTraceRange> Returned, IReadOnlyList<RootTraceRange> Fee) PartitionRanges(
        PersistedBountyEscrowFragment fragment,
        long returnedUnits)
    {
        var expectedTraceUnits = checked(fragment.Amount.Units * fragment.TraceUnitsPerCoinUnit);
        var remainingReturnedTraceUnits = checked(returnedUnits * fragment.TraceUnitsPerCoinUnit);
        var returned = new List<RootTraceRange>();
        var fee = new List<RootTraceRange>();
        long observedTraceUnits = 0;

        foreach (var range in fragment.SelectedRanges
                     .OrderBy(item => item.Root.Value.ToString("N"), StringComparer.Ordinal)
                     .ThenBy(item => item.Start))
        {
            observedTraceUnits = checked(observedTraceUnits + range.Length);
            if (remainingReturnedTraceUnits == 0)
            {
                fee.Add(range);
                continue;
            }

            if (remainingReturnedTraceUnits >= range.Length)
            {
                returned.Add(range);
                remainingReturnedTraceUnits -= range.Length;
                continue;
            }

            var split = range.Take(remainingReturnedTraceUnits);
            returned.Add(split.Selected);
            fee.Add(split.Remaining!.Value);
            remainingReturnedTraceUnits = 0;
        }

        if (observedTraceUnits != expectedTraceUnits || remainingReturnedTraceUnits != 0 ||
            (returnedUnits > 0 && returned.Count == 0) ||
            (returnedUnits < fragment.Amount.Units && fee.Count == 0))
            throw new InvalidOperationException("Bounty reclaim root ranges do not conserve the escrow fragment.");

        return (returned, fee);
    }

    private static void EnsurePartitionComplete(
        long remainingReturn,
        int lineCount,
        long allocatedUnits,
        long escrowUnits)
    {
        if (remainingReturn != 0 || lineCount == 0 || allocatedUnits != escrowUnits)
            throw new InvalidOperationException("Bounty reclaim fragment partition is incomplete.");
    }

    private static PostingId DeterministicPostingId(BountyId bountyId, IdempotencyKey idempotencyKey)
    {
        var payload = Encoding.UTF8.GetBytes($"{bountyId.Value:N}:reclaim:{idempotencyKey.Value}");
        return new PostingId(new Guid(SHA256.HashData(payload).AsSpan(0, 16)));
    }

    // PostgreSQL sorts UUIDs by their textual/network byte order, unlike Guid.CompareTo().
    // Use the normalized textual form on both sides of the specialized writer.
    private static string EscrowLotSortKey(PersistedBountyEscrowFragment fragment) =>
        fragment.EscrowLotId is { } escrowLotId
            ? escrowLotId.Value.ToString("N")
            : string.Empty;

    private static EconomyAccountCode EscrowFor(CurrencyCode currency) => currency switch
    {
        CurrencyCode.HardCoin => EconomyAccountCode.HardCoinEscrow,
        CurrencyCode.SoftCoin => EconomyAccountCode.SoftCoinEscrow,
        _ => throw new ArgumentOutOfRangeException(nameof(currency))
    };

    private static EconomyAccountCode LiabilityFor(CurrencyCode currency, ProvenanceKind provenance) => currency switch
    {
        CurrencyCode.HardCoin when provenance == ProvenanceKind.EarnedHard => EconomyAccountCode.EarnedHardLiability,
        CurrencyCode.HardCoin when provenance == ProvenanceKind.PurchasedHard => EconomyAccountCode.PurchasedHardLiability,
        CurrencyCode.SoftCoin => EconomyAccountCode.SoftCoinLiability,
        _ => throw new ArgumentOutOfRangeException(nameof(provenance))
    };

    private static EconomyAccountCode FeeDestinationFor(CurrencyCode currency) => currency switch
    {
        CurrencyCode.HardCoin => EconomyAccountCode.FeeRevenueHard,
        CurrencyCode.SoftCoin => EconomyAccountCode.SoftCoinReserve,
        _ => throw new ArgumentOutOfRangeException(nameof(currency))
    };
}
