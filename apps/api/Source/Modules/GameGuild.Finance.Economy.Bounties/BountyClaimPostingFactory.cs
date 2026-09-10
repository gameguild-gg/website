using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Posting;

namespace GameGuild.Finance.Economy.Bounties;

/// <summary>
/// Builds a claim posting exclusively from the durable escrow lots created when the bounty was
/// posted. This keeps the terminal debit bound to the escrowed value and its root ranges.
/// </summary>
public static class BountyClaimPostingFactory
{
    public static RegisteredPostingRequest Create(
        PersistedBountyEscrow escrow,
        DurableBountyClaimRequest request)
    {
        ArgumentNullException.ThrowIfNull(escrow);
        ArgumentNullException.ThrowIfNull(request);
        if (escrow.Id != request.BountyId)
            throw new ArgumentException("The bounty claim must target the persisted escrow.", nameof(request));
        if (escrow.Status != BountyStatus.Open)
            throw new BountyTerminalConflictException("Only an open bounty can be claimed.");
        if (request.ClaimantId == escrow.PosterId ||
            request.ClaimantWalletId == escrow.PosterWalletId ||
            request.ClaimantWalletId == escrow.EscrowWalletId)
            throw new BountyClaimIneligibleException("A poster cannot claim their own bounty.");
        if (request.Authority.ActorId != request.ClaimantId)
            throw new ArgumentException("The bounty claim authority must be the claimant.", nameof(request));

        var fragments = escrow.Fragments.OrderBy(fragment => fragment.EscrowLotId?.Value).ToArray();
        if (fragments.Length == 0 || fragments.Any(fragment => fragment.EscrowLotId is null))
            throw new InvalidOperationException("A bounty claim requires every materialized escrow lot.");
        if (fragments.Any(fragment => fragment.Amount.Currency != escrow.Amount.Currency) ||
            fragments.Sum(fragment => fragment.Amount.Units) != escrow.Amount.Units)
            throw new InvalidOperationException("Bounty escrow fragments do not conserve the claim amount.");

        var outputProvenance = escrow.Amount.Currency == CurrencyCode.HardCoin
            ? ProvenanceKind.EarnedHard
            : ProvenanceKind.EscrowReturn;
        var posting = new PostingRequest(
            DeterministicPostingId(escrow.Id, request.IdempotencyKey),
            new PostingTemplate(PostingTemplateKind.BountyClaim, PostingTemplate.CurrentVersion),
            request.IdempotencyKey,
            PostingAuthority.EscrowCoordinator,
            request.ReserveVersion,
            request.PolicyVersion,
            null,
            request.ClaimedAt,
            [
                new PostingLine(
                    1,
                    EntrySide.Debit,
                    EscrowFor(escrow.Amount.Currency),
                    escrow.Amount,
                    null,
                    null,
                    null),
                new PostingLine(
                    2,
                    EntrySide.Credit,
                    LiabilityFor(escrow.Amount.Currency, outputProvenance),
                    escrow.Amount,
                    request.ClaimantWalletId,
                    null,
                    outputProvenance)
            ]);
        PostingMatrix.EnsureValid(posting);

        var allocations = fragments.Select(fragment => new RegisteredPostingAllocation(
            1,
            fragment.EscrowLotId!.Value,
            fragment.Amount.Units,
            fragment.SelectedRanges)).ToArray();
        return new RegisteredPostingRequest(request.Authority, posting, allocations, request.DispatchSnapshotHash);
    }

    private static PostingId DeterministicPostingId(BountyId bountyId, IdempotencyKey idempotencyKey)
    {
        var payload = Encoding.UTF8.GetBytes($"{bountyId.Value:N}:claim:{idempotencyKey.Value}");
        return new PostingId(new Guid(SHA256.HashData(payload).AsSpan(0, 16)));
    }

    private static EconomyAccountCode EscrowFor(CurrencyCode currency) => currency switch
    {
        CurrencyCode.HardCoin => EconomyAccountCode.HardCoinEscrow,
        CurrencyCode.SoftCoin => EconomyAccountCode.SoftCoinEscrow,
        _ => throw new ArgumentOutOfRangeException(nameof(currency))
    };

    private static EconomyAccountCode LiabilityFor(CurrencyCode currency, ProvenanceKind provenance) => currency switch
    {
        CurrencyCode.HardCoin when provenance == ProvenanceKind.EarnedHard => EconomyAccountCode.EarnedHardLiability,
        CurrencyCode.SoftCoin when provenance == ProvenanceKind.EscrowReturn => EconomyAccountCode.SoftCoinLiability,
        _ => throw new ArgumentOutOfRangeException(nameof(provenance))
    };
}
