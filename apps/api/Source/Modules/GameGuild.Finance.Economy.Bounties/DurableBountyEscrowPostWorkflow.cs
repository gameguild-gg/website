using System.Data;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Bounties;

public sealed record DurableBountyEscrowPostRequest(
    BountyId Id,
    Guid PosterId,
    WalletId PosterWalletId,
    WalletId EscrowWalletId,
    CoinAmount Amount,
    BountyEligibilityRequirements Eligibility,
    int ReclaimFeePpm,
    DateTimeOffset PostedAt,
    DateTimeOffset ExpiresAt,
    IdempotencyKey IdempotencyKey,
    string RequestHash,
    RegisteredPostingAuthority Authority,
    ReserveVersion ReserveVersion,
    PolicyVersion PolicyVersion,
    string? DispatchSnapshotHash = null);

public interface IDurableBountyEscrowPostWorkflow
{
    Task<PersistedBountyEscrow> PostAsync(
        DurableBountyEscrowPostRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reserves the exact server-read FIFO fragments and persists their escrow in one database
/// transaction. A UI/API caller never supplies the lots being spent.
/// </summary>
public sealed class PostgreSqlDurableBountyEscrowPostWorkflow(
    IApplicationDbContext dbContext,
    IBountyPostableLotReader lots,
    IFifoFragmentReservationGateway reservations,
    IBountyEscrowStore escrows,
    IRegisteredPostingGateway postings) : IDurableBountyEscrowPostWorkflow
{
    public async Task<PersistedBountyEscrow> PostAsync(
        DurableBountyEscrowPostRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var replay = escrows.FindPostReplay(request.Authority.TenantId, request.IdempotencyKey, request.RequestHash);
        if (replay is not null)
            return EnsureSameBounty(replay, request.Authority.TenantId, request.Id);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            dbContext, IsolationLevel.ReadCommitted, async _ =>
        {
            replay = escrows.FindPostReplay(request.Authority.TenantId, request.IdempotencyKey, request.RequestHash);
            if (replay is not null)
                return EnsureSameBounty(replay, request.Authority.TenantId, request.Id);

            var command = new PostBountyCommand(
                request.Id,
                request.PosterId,
                request.PosterWalletId,
                request.EscrowWalletId,
                request.Amount,
                lots.Read(request.PosterWalletId, request.Amount.Currency, request.PostedAt),
                request.Eligibility,
                request.ReclaimFeePpm,
                request.PostedAt,
                request.ExpiresAt,
                request.IdempotencyKey);
            var position = BountyEscrowPositionFactory.Create(command);
            var persistedReservations = position.EscrowFragments
                .GroupBy(fragment => fragment.ParentLot.Provenance)
                .SelectMany(group => reservations.Reserve(new FifoFragmentReservationRequest(
                    request.Id.Value,
                    request.PosterWalletId,
                    request.Amount.Currency,
                    group.Key,
                    new CoinAmount(request.Amount.Currency, group.Sum(fragment => fragment.Amount.Units)),
                    PersistedFragmentReservationPurpose.BountyEscrow,
                    request.PostedAt)))
                .ToArray();
            EnsureReservationsMatch(position, persistedReservations);
            var receipt = postings.Post(BountyEscrowPostingFactory.Create(
                position,
                PostingId.New(),
                request.Authority,
                request.ReserveVersion,
                request.PolicyVersion,
                request.DispatchSnapshotHash));

            var persisted = escrows.Create(new CreateBountyEscrowPersistenceCommand(
                position,
                request.Authority.TenantId,
                request.IdempotencyKey,
                request.RequestHash.Trim(),
                receipt.PostingId));
            EnsureSameBounty(persisted, request.Authority.TenantId, request.Id);
            var consumed = reservations.Transition(
                request.Id.Value,
                PersistedFragmentReservationStatus.Reserved,
                PersistedFragmentReservationStatus.Consumed,
                request.PostedAt);
            if (consumed != persistedReservations.Length)
                throw new RegisteredPostingRejectedException(
                    "Bounty FIFO reservations could not be consumed atomically with the escrow posting.");
            await Task.CompletedTask;
            return persisted;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static PersistedBountyEscrow EnsureSameBounty(
        PersistedBountyEscrow replay,
        Guid tenantId,
        BountyId requestedId)
    {
        if (replay.TenantId != tenantId || replay.Id != requestedId)
            throw new BountyIdempotencyConflictException(
                "Bounty post idempotency key is bound to another tenant or bounty.");
        return replay;
    }

    private static void EnsureReservationsMatch(
        BountyEscrowPosition position,
        IReadOnlyCollection<PersistedFragmentReservation> persisted)
    {
        var expected = position.EscrowFragments
            .SelectMany(fragment => fragment.SelectedRanges.Select(range => new ReservationRange(
                fragment.ParentLot.Id,
                range.Root,
                range.Start,
                range.EndExclusive,
                range.Epoch)))
            .OrderBy(range => range.ParentLotId.Value)
            .ThenBy(range => range.RootSourceStampId.Value)
            .ThenBy(range => range.StartInclusive)
            .ToArray();
        var actual = persisted
            .Select(reservation => new ReservationRange(
                reservation.ParentLotId,
                reservation.RootSourceStampId,
                reservation.Range.Start,
                reservation.Range.EndExclusive,
                reservation.ReversalEpoch))
            .OrderBy(range => range.ParentLotId.Value)
            .ThenBy(range => range.RootSourceStampId.Value)
            .ThenBy(range => range.StartInclusive)
            .ToArray();

        if (!expected.SequenceEqual(actual) || persisted.Sum(item => item.Amount.Units) != position.Amount.Units)
            throw new RegisteredPostingRejectedException(
                "Bounty FIFO reservations do not match the server-selected escrow fragments.");
    }

    private static void Validate(DurableBountyEscrowPostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Eligibility);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequestHash);
        if (request.RequestHash.Trim().Length > 128)
            throw new ArgumentException("Bounty request hashes cannot exceed 128 characters.", nameof(request));
        if (request.PosterId == Guid.Empty)
            throw new ArgumentException("Poster ID is required.", nameof(request));
        if (request.Authority.ActorId != request.PosterId)
            throw new ArgumentException("The bounty posting authority must be the poster.", nameof(request));
        if (request.Authority.TenantId == Guid.Empty)
            throw new ArgumentException("The bounty posting authority must be tenant scoped.", nameof(request));
        if (request.PosterWalletId == request.EscrowWalletId)
            throw new ArgumentException("Poster and escrow wallets must be distinct.", nameof(request));
        if (request.ExpiresAt <= request.PostedAt)
            throw new ArgumentException("Bounty expiry must follow posting.", nameof(request));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Amount.Units);
        _ = BountyFeePolicy.Calculate(request.Amount.Units, request.ReclaimFeePpm);
    }

    private readonly record struct ReservationRange(
        CreditLotId ParentLotId,
        SourceStampId RootSourceStampId,
        long StartInclusive,
        long EndExclusive,
        long ReversalEpoch);
}
