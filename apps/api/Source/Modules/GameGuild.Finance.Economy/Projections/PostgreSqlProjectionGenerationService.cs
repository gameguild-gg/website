using System.Data;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Projections;

public sealed record ProjectionGenerationState(
    long Generation,
    long ToSequence,
    string JournalHash,
    string ProjectionHash,
    int MismatchCount,
    string State,
    bool IsActive,
    Guid ProposedBy,
    IReadOnlyList<Guid> ApprovedBy);

public interface IEconomyProjectionGenerationService
{
    ValueTask<ProjectionGenerationState> RebuildAsync(
        Guid proposedBy,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken);

    ValueTask<ProjectionGenerationState> ApproveAndTryActivateAsync(
        long generation,
        Guid actorId,
        string reauthenticationHash,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlProjectionGenerationService : IEconomyProjectionGenerationService
{
    private readonly DbContext _db;

    public PostgreSqlProjectionGenerationService(IApplicationDbContext context) =>
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);

    public async ValueTask<ProjectionGenerationState> RebuildAsync(
        Guid proposedBy,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        if (proposedBy == Guid.Empty) throw new ArgumentException("A proposer is required.", nameof(proposedBy));
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var head = await _db.Set<EconomyChainHeadRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == 1, cancellationToken)
            ?? throw new ProjectionGenerationException("Projection rebuild requires a journal chain head.");
        var verified = await _db.Set<EconomyJournalVerificationCheckpointRow>().AsNoTracking()
            .AnyAsync(row => row.IsValid && row.ToSequence == head.Sequence && row.CurrentHash == head.Hash,
                cancellationToken);
        if (!verified)
            throw new ProjectionGenerationException("Projection rebuild requires a verified journal head.");
        var generation = (await _db.Set<EconomyProjectionGenerationRow>()
            .Select(row => (long?)row.Generation).MaxAsync(cancellationToken) ?? 0) + 1;

        var rebuilt = await RebuildWalletsAsync(generation, head, startedAt, cancellationToken);
        var mismatchCount = rebuilt.Count(row => !row.MatchesLive);
        var projectionHash = Hash(string.Join('\n', rebuilt.OrderBy(row => row.WalletId)
            .Select(row => $"{row.WalletId:N}|{row.ProjectionHash}")));
        var generationRow = new EconomyProjectionGenerationRow
        {
            Id = Guid.NewGuid(),
            Generation = generation,
            FromSequence = 0,
            ToSequence = head.Sequence,
            ProjectionHash = projectionHash,
            JournalHash = head.Hash,
            MismatchCount = mismatchCount,
            State = mismatchCount == 0 ? "AwaitingApproval" : "MismatchAwaitingApproval",
            IsActive = false,
            ProposedBy = proposedBy,
            StartedAt = startedAt,
            CompletedAt = startedAt
        };
        _db.Set<EconomyProjectionGenerationRow>().Add(generationRow);
        _db.Set<EconomyWalletProjectionGenerationRow>().AddRange(rebuilt);
        await ApplyConservativeLimitsAsync(rebuilt, startedAt, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(generationRow, []);
        }, cancellationToken);
    }

    public async ValueTask<ProjectionGenerationState> ApproveAndTryActivateAsync(
        long generation,
        Guid actorId,
        string reauthenticationHash,
        DateTimeOffset approvedAt,
        CancellationToken cancellationToken)
    {
        if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
        if (actorId == Guid.Empty) throw new ArgumentException("An approver is required.", nameof(actorId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reauthenticationHash);
        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var row = await _db.Set<EconomyProjectionGenerationRow>()
            .SingleOrDefaultAsync(item => item.Generation == generation, cancellationToken)
            ?? throw new KeyNotFoundException("Projection generation not found.");
        if (row.IsActive) throw new ProjectionGenerationException("The projection generation is already active.");
        if (row.ProposedBy == actorId)
            throw new ProjectionGenerationException("The projection proposer cannot approve the cutover.");
        if (approvedAt < row.StartedAt)
            throw new ArgumentException("Approval cannot predate the rebuild.", nameof(approvedAt));
        var approvals = await _db.Set<EconomyProjectionGenerationApprovalRow>()
            .Where(item => item.Generation == generation)
            .OrderBy(item => item.ApprovedAt)
            .ToArrayAsync(cancellationToken);
        if (approvals.Any(item => item.ActorId == actorId))
            throw new ProjectionGenerationException("An administrator cannot approve the same cutover twice.");
        if (approvals.Length >= 2)
            throw new ProjectionGenerationException("The projection cutover already has two approvals.");
        var approval = new EconomyProjectionGenerationApprovalRow
        {
            Id = Guid.NewGuid(), Generation = generation, ActorId = actorId,
            ReauthenticationHash = reauthenticationHash.Trim(), ApprovedAt = approvedAt
        };
        _db.Set<EconomyProjectionGenerationApprovalRow>().Add(approval);
        if (approvals.Length == 0)
        {
            row.ApprovedBy = actorId;
            row.State = "AwaitingSecondApproval";
        }
        else
        {
            row.SecondApprovedBy = actorId;
            await ActivateAsync(row, approvedAt, cancellationToken);
        }
        await _db.SaveChangesAsync(cancellationToken);
        var actors = approvals.Select(item => item.ActorId).Append(actorId).ToArray();
        return Map(row, actors);
        }, cancellationToken);
    }

    private async ValueTask<EconomyWalletProjectionGenerationRow[]> RebuildWalletsAsync(
        long generation,
        EconomyChainHeadRow head,
        DateTimeOffset rebuiltAt,
        CancellationToken cancellationToken)
    {
        var wallets = await _db.Set<EconomyWalletRow>().AsNoTracking().ToArrayAsync(cancellationToken);
        var accounts = await _db.Set<EconomyAccountRow>().AsNoTracking()
            .Where(row => row.WalletId != null).ToDictionaryAsync(row => row.Id, cancellationToken);
        var accountIds = accounts.Keys.ToArray();
        var entryIds = await _db.Set<EconomyJournalEntryRow>().AsNoTracking()
            .Where(row => row.Sequence <= head.Sequence).Select(row => row.Id).ToArrayAsync(cancellationToken);
        var lines = await _db.Set<EconomyJournalLineRow>().AsNoTracking()
            .Where(row => entryIds.Contains(row.JournalEntryId) &&
                          (row.WalletId != null || accountIds.Contains(row.AccountId)))
            .ToArrayAsync(cancellationToken);
        var claims = await _db.Set<EconomyFundingClaimRow>().AsNoTracking().ToArrayAsync(cancellationToken);
        var holds = await _db.Set<EconomyHoldRow>().AsNoTracking()
            .Where(row => row.Status == HoldStatus.Active).ToArrayAsync(cancellationToken);
        var lots = await _db.Set<EconomyCreditLotRow>().AsNoTracking().ToArrayAsync(cancellationToken);
        var lotIds = lots.Select(row => row.Id).ToArray();
        var allocations = await _db.Set<EconomyEntryAllocationRow>().AsNoTracking()
            .Where(row => lotIds.Contains(row.ParentLotId)).ToArrayAsync(cancellationToken);
        var live = await _db.Set<EconomyWalletBalanceProjectionRow>().ToDictionaryAsync(row => row.WalletId, cancellationToken);

        return wallets.Select(wallet =>
        {
            long purchased = 0, earned = 0, restricted = 0, soft = 0;
            foreach (var line in lines.Where(line => (line.WalletId ?? accounts.GetValueOrDefault(line.AccountId)?.WalletId) == wallet.Id))
            {
                var delta = line.Side == EntrySide.Credit ? line.AmountUnits : -line.AmountUnits;
                if (line.Currency == CurrencyCode.SoftCoin)
                {
                    soft = checked(soft + delta);
                    continue;
                }
                var provenance = line.Provenance ?? accounts.GetValueOrDefault(line.AccountId)?.Provenance;
                if (provenance == ProvenanceKind.PurchasedHard) purchased = checked(purchased + delta);
                else if (provenance == ProvenanceKind.EarnedHard) earned = checked(earned + delta);
                else restricted = checked(restricted + delta);
            }
            if (purchased < 0 || earned < 0 || restricted < 0 || soft < 0)
                throw new ProjectionGenerationException("Journal recomputation produced a negative wallet component.");
            var pendingHard = claims.Where(row => row.WalletId == wallet.Id && row.State == SourceConfirmationState.Observed)
                .Aggregate(0L, (total, row) => checked(total + row.AuthoritativeUsdMinorUnits));
            var heldHard = holds.Where(row => row.WalletId == wallet.Id && row.Currency == CurrencyCode.HardCoin)
                .Aggregate(0L, (total, row) => checked(total + row.AmountUnits));
            var heldSoft = holds.Where(row => row.WalletId == wallet.Id && row.Currency == CurrencyCode.SoftCoin)
                .Aggregate(0L, (total, row) => checked(total + row.AmountUnits));
            var immature = lots.Where(row => row.WalletId == wallet.Id && row.Currency == CurrencyCode.HardCoin &&
                                              row.Provenance == ProvenanceKind.EarnedHard &&
                                              row.State is not (CreditLotState.Consumed or CreditLotState.Reversed) &&
                                              row.OriginalMaturesAt > rebuiltAt)
                .Aggregate(0L, (total, lot) => checked(total + Math.Max(0,
                    lot.AmountUnits - allocations.Where(item => item.ParentLotId == lot.Id).Sum(item => item.AmountUnits))));
            immature = Math.Min(earned, immature);
            heldHard = Math.Min(checked(purchased + earned + restricted), heldHard);
            heldSoft = Math.Min(soft, heldSoft);
            var availableHard = Math.Max(0, checked(purchased + earned + restricted - heldHard));
            var availableSoft = Math.Max(0, soft - heldSoft);
            var withdrawable = Math.Max(0, earned - immature - heldHard);
            var hash = ProjectionHash(wallet.Id, pendingHard, purchased, earned, restricted, soft,
                immature, heldHard, heldSoft, availableHard, availableSoft, withdrawable, head.Sequence);
            var matches = live.TryGetValue(wallet.Id, out var current) && Matches(current, pendingHard, purchased,
                earned, restricted, soft, immature, heldHard, heldSoft, availableHard, availableSoft,
                withdrawable, head.Sequence, hash);
            return new EconomyWalletProjectionGenerationRow
            {
                Generation = generation,
                WalletId = wallet.Id,
                PendingHard = pendingHard,
                PendingSoft = 0,
                PurchasedHard = purchased,
                EarnedHard = earned,
                RestrictedHard = restricted,
                Soft = soft,
                ImmatureEarnedHard = immature,
                HeldHard = heldHard,
                HeldSoft = heldSoft,
                AvailableHardToSpend = availableHard,
                AvailableSoftToSpend = availableSoft,
                WithdrawableHard = withdrawable,
                SourceJournalSequence = head.Sequence,
                ProjectionHash = hash,
                MatchesLive = matches,
                RebuiltAt = rebuiltAt
            };
        }).ToArray();
    }

    private async ValueTask ApplyConservativeLimitsAsync(
        IReadOnlyCollection<EconomyWalletProjectionGenerationRow> rebuilt,
        DateTimeOffset detectedAt,
        CancellationToken cancellationToken)
    {
        var live = await _db.Set<EconomyWalletBalanceProjectionRow>().ToDictionaryAsync(row => row.WalletId, cancellationToken);
        foreach (var snapshot in rebuilt.Where(row => !row.MatchesLive))
        {
            if (!live.TryGetValue(snapshot.WalletId, out var current)) continue;
            _db.Set<EconomyProjectionReconciliationEventRow>().Add(new EconomyProjectionReconciliationEventRow
            {
                Id = Guid.NewGuid(), WalletId = snapshot.WalletId, PreviousHash = current.ProjectionHash,
                RebuiltHash = snapshot.ProjectionHash, SourceJournalSequence = snapshot.SourceJournalSequence,
                DetectedAt = detectedAt
            });
            current.AvailableHardToSpend = Math.Min(current.AvailableHardToSpend, snapshot.AvailableHardToSpend);
            current.AvailableSoftToSpend = Math.Min(current.AvailableSoftToSpend, snapshot.AvailableSoftToSpend);
            current.WithdrawableHard = Math.Min(current.WithdrawableHard, snapshot.WithdrawableHard);
            current.ReviewState = WalletReviewState.ReviewRequired;
        }
    }

    private async ValueTask ActivateAsync(
        EconomyProjectionGenerationRow generation,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken)
    {
        var head = await _db.Set<EconomyChainHeadRow>().AsNoTracking().SingleAsync(row => row.Id == 1, cancellationToken);
        if (head.Sequence != generation.ToSequence || head.Hash != generation.JournalHash)
            throw new ProjectionGenerationException("The journal advanced after the projection rebuild; rebuild again before cutover.");
        var snapshots = await _db.Set<EconomyWalletProjectionGenerationRow>()
            .Where(row => row.Generation == generation.Generation).ToArrayAsync(cancellationToken);
        var live = await _db.Set<EconomyWalletBalanceProjectionRow>().ToDictionaryAsync(row => row.WalletId, cancellationToken);
        foreach (var snapshot in snapshots)
        {
            if (!live.TryGetValue(snapshot.WalletId, out var row))
            {
                row = new EconomyWalletBalanceProjectionRow { WalletId = snapshot.WalletId };
                _db.Set<EconomyWalletBalanceProjectionRow>().Add(row);
            }
            Copy(snapshot, row);
        }
        var previous = await _db.Set<EconomyProjectionGenerationRow>()
            .Where(row => row.IsActive).ToArrayAsync(cancellationToken);
        foreach (var row in previous)
        {
            row.IsActive = false;
            row.State = "Superseded";
        }
        generation.IsActive = true;
        generation.State = "Active";
        generation.ActivatedAt = activatedAt;
    }

    private static void Copy(EconomyWalletProjectionGenerationRow source, EconomyWalletBalanceProjectionRow target)
    {
        target.PendingHard = source.PendingHard;
        target.PendingSoft = source.PendingSoft;
        target.PurchasedHard = source.PurchasedHard;
        target.EarnedHard = source.EarnedHard;
        target.RestrictedHard = source.RestrictedHard;
        target.Soft = source.Soft;
        target.ImmatureEarnedHard = source.ImmatureEarnedHard;
        target.HeldHard = source.HeldHard;
        target.HeldSoft = source.HeldSoft;
        target.AvailableHardToSpend = source.AvailableHardToSpend;
        target.AvailableSoftToSpend = source.AvailableSoftToSpend;
        target.WithdrawableHard = source.WithdrawableHard;
        target.ReviewState = WalletReviewState.Healthy;
        target.SourceJournalSequence = source.SourceJournalSequence;
        target.ProjectionHash = source.ProjectionHash;
        target.RebuiltAt = source.RebuiltAt;
    }

    private static bool Matches(
        EconomyWalletBalanceProjectionRow row,
        long pendingHard,
        long purchased,
        long earned,
        long restricted,
        long soft,
        long immature,
        long heldHard,
        long heldSoft,
        long availableHard,
        long availableSoft,
        long withdrawable,
        long sequence,
        string hash) =>
        row.PendingHard == pendingHard && row.PendingSoft == 0 && row.PurchasedHard == purchased &&
        row.EarnedHard == earned && row.RestrictedHard == restricted && row.Soft == soft &&
        row.ImmatureEarnedHard == immature && row.HeldHard == heldHard && row.HeldSoft == heldSoft &&
        row.AvailableHardToSpend == availableHard && row.AvailableSoftToSpend == availableSoft &&
        row.WithdrawableHard == withdrawable && row.SourceJournalSequence == sequence && row.ProjectionHash == hash;

    private static string ProjectionHash(
        Guid walletId,
        params object[] values) => Hash(string.Join('|', new object[] { walletId.ToString("N") }.Concat(values)));

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static ProjectionGenerationState Map(
        EconomyProjectionGenerationRow row,
        IReadOnlyList<Guid> approvals) => new(
        row.Generation, row.ToSequence, row.JournalHash, row.ProjectionHash, row.MismatchCount,
        row.State, row.IsActive, row.ProposedBy, approvals);
}

public sealed class ProjectionGenerationException(string message) : InvalidOperationException(message);
