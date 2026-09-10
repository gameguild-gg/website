using System.Data;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Writer;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public enum JournalIntegrityRunStatus
{
    Verified = 0,
    Failed = 1,
    LeaseUnavailable = 2
}

public sealed record JournalIntegrityRunResult(
    JournalIntegrityRunStatus Status,
    JournalIntegrityVerificationResult? Verification,
    long FencingToken);

public interface IJournalIntegrityService
{
    ValueTask<JournalIntegrityRunResult> RunIncrementAsync(
        string owner,
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlJournalIntegrityService : IJournalIntegrityService
{
    internal const string LeaseName = "economy-journal-integrity";
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly Guid SystemActorId = new("e8cf8c6d-6edf-4d15-a945-4bb3add04b3e");

    private readonly DbContext _db;
    private readonly IJournalIntegrityVerifier _verifier;
    private readonly IEconomyKillSwitchStore _killSwitches;

    public PostgreSqlJournalIntegrityService(
        IApplicationDbContext context,
        IJournalIntegrityVerifier verifier,
        IEconomyKillSwitchStore killSwitches)
    {
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(killSwitches);
        _verifier = verifier;
        _killSwitches = killSwitches;
    }

    public async ValueTask<JournalIntegrityRunResult> RunIncrementAsync(
        string owner,
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));

        var fencingToken = await TryAcquireLeaseAsync(owner.Trim(), now, cancellationToken);
        if (fencingToken is null)
            return new JournalIntegrityRunResult(JournalIntegrityRunStatus.LeaseUnavailable, null, 0);

        var latest = await _db.Set<EconomyJournalVerificationCheckpointRow>()
            .AsNoTracking()
            .Where(row => row.IsValid)
            .OrderByDescending(row => row.ToSequence)
            .ThenByDescending(row => row.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var expectedSequence = latest is null ? 1 : checked(latest.ToSequence + 1);
        var expectedHash = latest?.CurrentHash ?? JournalChain.GenesisHash;
        var rows = await _db.Set<EconomyJournalEntryRow>()
            .AsNoTracking()
            .Where(row => row.Sequence >= expectedSequence)
            .OrderBy(row => row.Sequence)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        if (rows.Length == 0)
        {
            var head = await _db.Set<EconomyChainHeadRow>().AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var verification = head is { Sequence: 0 } && expectedSequence == 1
                ? new JournalIntegrityVerificationResult(true, JournalIntegrityFailureCode.None, 0, 0, JournalChain.GenesisHash)
                : _verifier.Verify([], expectedSequence, expectedHash);
            await PersistCheckpointAsync(verification, expectedHash, fencingToken.Value, now, cancellationToken);
            return new JournalIntegrityRunResult(JournalIntegrityRunStatus.Verified, verification, fencingToken.Value);
        }

        var entries = await MaterializeEntriesAsync(rows, cancellationToken);
        var result = _verifier.Verify(entries, expectedSequence, expectedHash);
        await PersistCheckpointAsync(result, expectedHash, fencingToken.Value, now, cancellationToken);
        if (result.IsValid)
            return new JournalIntegrityRunResult(JournalIntegrityRunStatus.Verified, result, fencingToken.Value);

        await ActivateIntegrityKillSwitchAsync(result, now, cancellationToken);
        return new JournalIntegrityRunResult(JournalIntegrityRunStatus.Failed, result, fencingToken.Value);
    }

    private async ValueTask<long?> TryAcquireLeaseAsync(
        string owner,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await PostgreSqlTransactionExecutor.ExecuteAsync<long?>(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var lease = await _db.Set<EconomyWorkerLeaseRow>()
            .SingleOrDefaultAsync(row => row.Name == LeaseName, cancellationToken);
        if (lease is not null && lease.Owner != owner && lease.ExpiresAt > now)
            return null;

        if (lease is null)
        {
            lease = new EconomyWorkerLeaseRow
            {
                Name = LeaseName,
                Owner = owner,
                FencingToken = 1,
                AcquiredAt = now,
                ExpiresAt = now.Add(LeaseDuration)
            };
            _db.Set<EconomyWorkerLeaseRow>().Add(lease);
        }
        else
        {
            if (lease.Owner != owner || lease.ExpiresAt <= now)
                lease.FencingToken = checked(lease.FencingToken + 1);
            lease.Owner = owner;
            lease.AcquiredAt = now;
            lease.ExpiresAt = now.Add(LeaseDuration);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return lease.FencingToken;
        }, cancellationToken);
    }

    private async ValueTask<IReadOnlyCollection<JournalIntegrityEntry>> MaterializeEntriesAsync(
        IReadOnlyCollection<EconomyJournalEntryRow> rows,
        CancellationToken cancellationToken)
    {
        var entryIds = rows.Select(row => row.Id).ToArray();
        var postingIds = rows.Select(row => row.PostingGroupId).ToArray();
        var groups = await _db.Set<EconomyPostingGroupRow>().AsNoTracking()
            .Where(row => postingIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var lines = await _db.Set<EconomyJournalLineRow>().AsNoTracking()
            .Where(row => entryIds.Contains(row.JournalEntryId))
            .OrderBy(row => row.Sequence)
            .ToArrayAsync(cancellationToken);
        var lineIds = lines.Select(row => row.Id).ToArray();
        var allocations = await _db.Set<EconomyEntryAllocationRow>().AsNoTracking()
            .Where(row => lineIds.Contains(row.JournalLineId))
            .ToArrayAsync(cancellationToken);
        var relevantLotIds = allocations.Select(row => row.ParentLotId)
            .Concat(lines.Where(row => row.CreditLotId.HasValue).Select(row => row.CreditLotId!.Value))
            .Distinct().ToArray();
        var lots = await _db.Set<EconomyCreditLotRow>().AsNoTracking()
            .Where(row => relevantLotIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var sourceIds = groups.Values.Where(row => row.SourceStampId.HasValue)
            .Select(row => row.SourceStampId!.Value).Distinct().ToArray();
        var sources = await _db.Set<EconomySourceStampRow>().AsNoTracking()
            .Where(row => sourceIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        var lineageValid = await ValidateLineageAsync(cancellationToken);
        var rootRangesValid = await ValidateRootRangesAsync(cancellationToken);
        var cumulativeReversalsValid = await ValidateCumulativeReversalsAsync(cancellationToken);

        return rows.OrderBy(row => row.Sequence).Select(row =>
        {
            var entryLines = lines.Where(line => line.JournalEntryId == row.Id).ToArray();
            var groupFound = groups.TryGetValue(row.PostingGroupId, out var group);
            var registration = groupFound
                ? PostingTemplateCatalog.Find(group!.TemplateKind, group.TemplateVersion)
                : null;
            var postingValid = groupFound && registration is not null &&
                               group!.Status == PostingStatus.Accepted &&
                               group.Authority == registration.Authority &&
                               registration.AllowsLineCount(entryLines.Length);
            var sourceValid = groupFound && ValidateSource(group!, registration, sources);
            var allocationValid = ValidateAllocations(entryLines, allocations, lots);

            return new JournalIntegrityEntry(
                row.Sequence,
                row.PreviousHash,
                row.CanonicalPayloadHash ?? string.Empty,
                row.HashAlgorithmVersion,
                row.Hash,
                row.RecordedAt,
                postingValid,
                entryLines.Select(line => new JournalIntegrityLine(line.Side, line.Currency, line.AmountUnits)).ToArray())
            {
                PostingGroupId = row.PostingGroupId,
                SourceStampValid = sourceValid,
                AllocationsValid = allocationValid,
                LineageValid = lineageValid,
                RootRangesValid = rootRangesValid,
                CumulativeReversalsValid = cumulativeReversalsValid
            };
        }).ToArray();
    }

    private static bool ValidateSource(
        EconomyPostingGroupRow group,
        PostingTemplateRegistration? registration,
        IReadOnlyDictionary<Guid, EconomySourceStampRow> sources)
    {
        if (registration is null) return false;
        if (!group.SourceStampId.HasValue) return registration.RequiredSourceState is null;
        if (!sources.TryGetValue(group.SourceStampId.Value, out var source)) return false;
        return source.TenantId == group.TenantId &&
               (!registration.RequiredSourceState.HasValue || source.State == registration.RequiredSourceState.Value);
    }

    private static bool ValidateAllocations(
        IReadOnlyCollection<EconomyJournalLineRow> lines,
        IReadOnlyCollection<EconomyEntryAllocationRow> allocations,
        IReadOnlyDictionary<Guid, EconomyCreditLotRow> lots)
    {
        foreach (var line in lines)
        {
            var lineAllocations = allocations.Where(row => row.JournalLineId == line.Id).ToArray();
            if (lineAllocations.Any(row => row.AmountUnits <= 0) ||
                lineAllocations.Sum(row => row.AmountUnits) > line.AmountUnits)
                return false;
            if (lineAllocations.Any(row => !lots.TryGetValue(row.ParentLotId, out var lot) || lot.Currency != line.Currency))
                return false;
        }
        return true;
    }

    private async ValueTask<bool> ValidateLineageAsync(CancellationToken cancellationToken)
    {
        var edges = await _db.Set<EconomyLotLineageEdgeRow>().AsNoTracking().ToArrayAsync(cancellationToken);
        if (edges.Length == 0) return true;
        var ids = edges.SelectMany(row => new[] { row.ParentLotId, row.ChildLotId }).Distinct().ToArray();
        var lots = await _db.Set<EconomyCreditLotRow>().AsNoTracking()
            .Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        if (edges.Any(edge => edge.AmountUnits <= 0 || !lots.TryGetValue(edge.ParentLotId, out var parent) ||
                              !lots.TryGetValue(edge.ChildLotId, out var child) || parent.Currency != edge.Currency ||
                              child.Currency != edge.Currency))
            return false;
        if (edges.GroupBy(edge => edge.ParentLotId).Any(group => group.Sum(edge => edge.AmountUnits) > lots[group.Key].AmountUnits))
            return false;
        return edges.GroupBy(edge => edge.ChildLotId)
            .All(group => group.Sum(edge => edge.AmountUnits) == lots[group.Key].AmountUnits);
    }

    private async ValueTask<bool> ValidateRootRangesAsync(CancellationToken cancellationToken)
    {
        var ranges = await _db.Set<EconomyFragmentRootRangeRow>().AsNoTracking().ToArrayAsync(cancellationToken);
        if (ranges.Length == 0) return true;
        var lotIds = ranges.Where(row => row.CreditLotId.HasValue).Select(row => row.CreditLotId!.Value).Distinct().ToArray();
        var allocationIds = ranges.Where(row => row.EntryAllocationId.HasValue).Select(row => row.EntryAllocationId!.Value).Distinct().ToArray();
        var lots = await _db.Set<EconomyCreditLotRow>().AsNoTracking()
            .Where(row => lotIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        var allocations = await _db.Set<EconomyEntryAllocationRow>().AsNoTracking()
            .Where(row => allocationIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, cancellationToken);
        foreach (var range in ranges)
        {
            if (range.CreditLotId.HasValue == range.EntryAllocationId.HasValue || range.StartInclusive < 0 ||
                range.EndExclusive <= range.StartInclusive)
                return false;
            var ownerAmount = range.CreditLotId.HasValue
                ? lots.GetValueOrDefault(range.CreditLotId.Value)?.AmountUnits
                : allocations.GetValueOrDefault(range.EntryAllocationId!.Value)?.AmountUnits;
            if (!ownerAmount.HasValue || range.EndExclusive > ownerAmount.Value) return false;
        }
        return ranges.GroupBy(row => new { row.RootSourceStampId, row.CreditLotId, row.EntryAllocationId, row.ReversalEpoch })
            .All(group =>
            {
                var ordered = group.OrderBy(row => row.StartInclusive).ToArray();
                return ordered.Zip(ordered.Skip(1), (left, right) => left.EndExclusive <= right.StartInclusive).All(value => value);
            });
    }

    private async ValueTask<bool> ValidateCumulativeReversalsAsync(CancellationToken cancellationToken)
    {
        var reversalsValid = await _db.Set<EconomyRootReversalStateRow>().AsNoTracking()
            .AllAsync(row => row.CumulativeProviderUnits >= 0 && row.ReversedUnits >= 0 &&
                             row.ReversedUnits <= row.CumulativeProviderUnits, cancellationToken);
        if (!reversalsValid) return false;
        return await _db.Set<EconomyProviderFactAllocationRow>().AsNoTracking()
            .AllAsync(row => row.AllocatedUnits > 0 && row.CumulativeCreditedUnits >= row.AllocatedUnits &&
                             row.CumulativeCreditedUnits <= row.AuthoritativeUnits, cancellationToken);
    }

    private async ValueTask PersistCheckpointAsync(
        JournalIntegrityVerificationResult result,
        string previousHash,
        long fencingToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _db.Set<EconomyJournalVerificationCheckpointRow>().Add(new EconomyJournalVerificationCheckpointRow
        {
            Id = Guid.NewGuid(),
            FromSequence = result.FromSequence,
            ToSequence = result.IsValid ? result.ToSequence : Math.Max(result.FromSequence, result.ToSequence),
            PreviousHash = previousHash,
            CurrentHash = result.CurrentHash,
            IsValid = result.IsValid,
            FailureCode = result.IsValid ? null : result.FailureCode.ToString(),
            FencingToken = fencingToken,
            StartedAt = now,
            CompletedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async ValueTask ActivateIntegrityKillSwitchAsync(
        JournalIntegrityVerificationResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var seed = Encoding.UTF8.GetBytes($"journal-integrity|{result.FailureCode}|{result.FromSequence}|{result.ToSequence}|{result.CurrentHash}");
        var activationId = new Guid(SHA256.HashData(seed).AsSpan(0, 16));
        try
        {
            await _killSwitches.ActivateAsync(
                activationId,
                EconomyKillSwitchScope.Global,
                $"Journal integrity verification failed: {result.FailureCode}.",
                SystemActorId,
                now,
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Another verifier may have activated the same scope concurrently. Fail-closed is already satisfied.
        }
    }
}
