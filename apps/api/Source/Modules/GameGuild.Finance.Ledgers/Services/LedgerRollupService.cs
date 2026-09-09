using GameGuild.Finance.Ledgers.Abstractions;
using GameGuild.Finance.Ledgers.Entities;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Models;
using System.Globalization;

namespace GameGuild.Finance.Ledgers.Services;

/// <summary>
/// Service for rollup calculations and hierarchical aggregations.
/// Leverages closure table and ParentLedgerIds for efficient queries.
/// </summary>
public class LedgerRollupService : ILedgerRollupService
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly ILedgerClosureRepository _closureRepository;
    private readonly ILedgerEntryRepository _entryRepository;

    public LedgerRollupService(
        ILedgerRepository ledgerRepository,
        ILedgerClosureRepository closureRepository,
        ILedgerEntryRepository entryRepository)
    {
        _ledgerRepository = ledgerRepository;
        _closureRepository = closureRepository;
        _entryRepository = entryRepository;
    }

    /// <inheritdoc />
    public async Task<LedgerBalance> GetBalanceAsync(
        Guid ledgerId,
        DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(ledgerId, ct)
            ?? throw new InvalidOperationException($"Ledger '{ledgerId}' not found.");

        DateTimeOffset? asOfDateTime = asOfDate.HasValue
            ? new DateTimeOffset(asOfDate.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)
            : null;

        var (totalCredits, totalDebits, entryCount) =
            await _entryRepository.GetBalanceSummaryAsync(ledgerId, asOfDateTime, ct);

        return new LedgerBalance(
            LedgerId: ledgerId,
            LedgerCode: ledger.Code,
            LedgerName: ledger.Name,
            TotalDebit: totalDebits,
            TotalCredit: totalCredits,
            NetBalance: totalCredits - totalDebits,
            EntryCount: entryCount,
            CurrencyCode: ledger.CurrencyCode,
            CalculatedAt: DateTime.UtcNow,
            IncludesDescendants: false);
    }

    /// <inheritdoc />
    public async Task<LedgerBalance> GetConsolidatedBalanceAsync(
        Guid ledgerId,
        DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(ledgerId, ct)
            ?? throw new InvalidOperationException($"Ledger '{ledgerId}' not found.");

        DateTimeOffset? asOfDateTime = asOfDate.HasValue
            ? new DateTimeOffset(asOfDate.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)
            : null;

        var (totalCredits, totalDebits, entryCount) =
            await _entryRepository.GetBalanceSummaryIncludingDescendantsAsync(ledgerId, asOfDateTime, ct);

        return new LedgerBalance(
            LedgerId: ledgerId,
            LedgerCode: ledger.Code,
            LedgerName: ledger.Name,
            TotalDebit: totalDebits,
            TotalCredit: totalCredits,
            NetBalance: totalCredits - totalDebits,
            EntryCount: entryCount,
            CurrencyCode: ledger.CurrencyCode,
            CalculatedAt: DateTime.UtcNow,
            IncludesDescendants: true);
    }

    /// <inheritdoc />
    public async Task<LedgerRollupNode> GetHierarchicalRollupAsync(
        Guid rootLedgerId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(rootLedgerId, ct)
            ?? throw new InvalidOperationException($"Ledger '{rootLedgerId}' not found.");

        var ownEntries = await GetPostedEntriesAsync(rootLedgerId, false, fromDate, toDate, ct);
        var ownSummary = SummarizeEntries(ownEntries);

        var ownBalance = new LedgerBalance(
            LedgerId: rootLedgerId,
            LedgerCode: ledger.Code,
            LedgerName: ledger.Name,
            TotalDebit: ownSummary.TotalDebits,
            TotalCredit: ownSummary.TotalCredits,
            NetBalance: ownSummary.NetBalance,
            EntryCount: ownSummary.EntryCount,
            CurrencyCode: ledger.CurrencyCode,
            CalculatedAt: DateTime.UtcNow,
            IncludesDescendants: false);

        // Get children and recursively build tree
        var children = await _ledgerRepository.GetChildrenAsync(rootLedgerId, ct);
        var childNodes = new List<LedgerRollupNode>();

        decimal consolidatedDebit = ownSummary.TotalDebits;
        decimal consolidatedCredit = ownSummary.TotalCredits;
        int consolidatedEntryCount = ownSummary.EntryCount;

        foreach (var child in children)
        {
            var childNode = await GetHierarchicalRollupAsync(child.Id, fromDate, toDate, ct);
            childNodes.Add(childNode);
            consolidatedDebit += childNode.ConsolidatedBalance.TotalDebit;
            consolidatedCredit += childNode.ConsolidatedBalance.TotalCredit;
            consolidatedEntryCount += childNode.ConsolidatedBalance.EntryCount;
        }

        var consolidatedBalance = new LedgerBalance(
            LedgerId: rootLedgerId,
            LedgerCode: ledger.Code,
            LedgerName: ledger.Name,
            TotalDebit: consolidatedDebit,
            TotalCredit: consolidatedCredit,
            NetBalance: consolidatedCredit - consolidatedDebit,
            EntryCount: consolidatedEntryCount,
            CurrencyCode: ledger.CurrencyCode,
            CalculatedAt: DateTime.UtcNow,
            IncludesDescendants: true);

        return new LedgerRollupNode
        {
            Id = rootLedgerId,
            Code = ledger.Code,
            Name = ledger.Name,
            Type = ledger.Type,
            Depth = ledger.HierarchyDepth,
            OwnBalance = ownBalance,
            ConsolidatedBalance = consolidatedBalance,
            Children = childNodes
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryRollup>> GetCategoryRollupAsync(
        Guid ledgerId,
        bool includeDescendants,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var entries = await GetPostedEntriesAsync(ledgerId, includeDescendants, fromDate, toDate, ct);
        var totalMovement = entries.Sum(entry => entry.Amount);

        return entries
            .GroupBy(entry => entry.Category)
            .Select(group =>
            {
                var totalDebit = group
                    .Where(entry => entry.Type == EntryType.Debit)
                    .Sum(entry => entry.Amount);
                var totalCredit = group
                    .Where(entry => entry.Type == EntryType.Credit)
                    .Sum(entry => entry.Amount);
                var movement = totalDebit + totalCredit;

                return new CategoryRollup(
                    Category: group.Key,
                    TotalDebit: totalDebit,
                    TotalCredit: totalCredit,
                    NetAmount: totalCredit - totalDebit,
                    EntryCount: group.Count(),
                    PercentageOfTotal: totalMovement == 0m ? 0m : decimal.Round((movement / totalMovement) * 100m, 2));
            })
            .OrderByDescending(rollup => Math.Abs(rollup.NetAmount))
            .ThenBy(rollup => rollup.Category)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PeriodRollup>> GetPeriodRollupAsync(
        Guid ledgerId,
        bool includeDescendants,
        DateOnly fromDate,
        DateOnly toDate,
        PeriodGranularity granularity,
        CancellationToken ct = default)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentException("The end date must be on or after the start date.", nameof(toDate));
        }

        var entries = await GetPostedEntriesAsync(ledgerId, includeDescendants, fromDate, toDate, ct);
        var buckets = BuildPeriodBuckets(fromDate, toDate, granularity);
        var openingBalance = includeDescendants
            ? (await GetConsolidatedBalanceAsync(ledgerId, fromDate.AddDays(-1), ct)).NetBalance
            : (await GetBalanceAsync(ledgerId, fromDate.AddDays(-1), ct)).NetBalance;

        var runningBalance = openingBalance;
        var result = new List<PeriodRollup>(buckets.Count);

        foreach (var bucket in buckets)
        {
            var bucketEntries = entries
                .Where(entry => entry.TransactionDate >= bucket.Start && entry.TransactionDate <= bucket.End)
                .ToList();

            var summary = SummarizeEntries(bucketEntries);
            runningBalance += summary.NetBalance;

            result.Add(new PeriodRollup(
                PeriodStart: bucket.Start,
                PeriodEnd: bucket.End,
                PeriodLabel: bucket.Label,
                TotalDebit: summary.TotalDebits,
                TotalCredit: summary.TotalCredits,
                NetChange: summary.NetBalance,
                RunningBalance: runningBalance,
                EntryCount: summary.EntryCount));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task RefreshLedgerStatsAsync(Guid ledgerId, CancellationToken ct = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(ledgerId, ct)
            ?? throw new InvalidOperationException($"Ledger '{ledgerId}' not found.");

        var balance = await GetBalanceAsync(ledgerId, null, ct);

        ledger.UpdateCachedStats(
            totalDebit: balance.TotalDebit,
            totalCredit: balance.TotalCredit,
            entryCount: balance.EntryCount);

        await _ledgerRepository.UpdateAsync(ledger, ct);
    }

    /// <inheritdoc />
    public async Task RefreshSubtreeStatsAsync(Guid rootLedgerId, CancellationToken ct = default)
    {
        // Refresh stats for root and all descendants
        var descendantIds = await _closureRepository.GetDescendantIdsAsync(rootLedgerId, ct: ct);

        foreach (var ledgerId in descendantIds)
        {
            await RefreshLedgerStatsAsync(ledgerId, ct);
        }
    }

    private async Task<IReadOnlyList<LedgerEntry>> GetPostedEntriesAsync(
        Guid ledgerId,
        bool includeDescendants,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct)
    {
        DateTimeOffset? fromDateTime = fromDate.HasValue
            ? new DateTimeOffset(fromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
        DateTimeOffset? toDateTime = toDate.HasValue
            ? new DateTimeOffset(toDate.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)
            : null;

        var entries = includeDescendants
            ? await _entryRepository.GetByLedgerIncludingDescendantsAsync(ledgerId, fromDateTime, toDateTime, ct)
            : await _entryRepository.GetByLedgerAsync(ledgerId, fromDateTime, toDateTime, ct);

        return entries
            .Where(entry => entry.Status == EntryStatus.Posted && entry.ReversedByEntryId == null)
            .ToList();
    }

    private static (decimal TotalCredits, decimal TotalDebits, decimal NetBalance, int EntryCount) SummarizeEntries(
        IReadOnlyCollection<LedgerEntry> entries)
    {
        var totalCredits = entries
            .Where(entry => entry.Type == EntryType.Credit)
            .Sum(entry => entry.Amount);
        var totalDebits = entries
            .Where(entry => entry.Type == EntryType.Debit)
            .Sum(entry => entry.Amount);

        return (totalCredits, totalDebits, totalCredits - totalDebits, entries.Count);
    }

    private static List<(DateOnly Start, DateOnly End, string Label)> BuildPeriodBuckets(
        DateOnly fromDate,
        DateOnly toDate,
        PeriodGranularity granularity)
    {
        var buckets = new List<(DateOnly Start, DateOnly End, string Label)>();
        var cursor = AlignPeriodStart(fromDate, granularity);

        while (cursor <= toDate)
        {
            var naturalEnd = GetPeriodEnd(cursor, granularity);
            var bucketStart = cursor < fromDate ? fromDate : cursor;
            var bucketEnd = naturalEnd > toDate ? toDate : naturalEnd;

            buckets.Add((bucketStart, bucketEnd, FormatPeriodLabel(cursor, granularity)));
            cursor = naturalEnd.AddDays(1);
        }

        return buckets;
    }

    private static DateOnly AlignPeriodStart(DateOnly date, PeriodGranularity granularity) => granularity switch
    {
        PeriodGranularity.Day => date,
        PeriodGranularity.Week => date.AddDays(-((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7)),
        PeriodGranularity.Month => new DateOnly(date.Year, date.Month, 1),
        PeriodGranularity.Quarter => new DateOnly(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
        PeriodGranularity.Year => new DateOnly(date.Year, 1, 1),
        _ => date,
    };

    private static DateOnly GetPeriodEnd(DateOnly periodStart, PeriodGranularity granularity) => granularity switch
    {
        PeriodGranularity.Day => periodStart,
        PeriodGranularity.Week => periodStart.AddDays(6),
        PeriodGranularity.Month => periodStart.AddMonths(1).AddDays(-1),
        PeriodGranularity.Quarter => periodStart.AddMonths(3).AddDays(-1),
        PeriodGranularity.Year => periodStart.AddYears(1).AddDays(-1),
        _ => periodStart,
    };

    private static string FormatPeriodLabel(DateOnly periodStart, PeriodGranularity granularity) => granularity switch
    {
        PeriodGranularity.Day => periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        PeriodGranularity.Week => $"Week of {periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
        PeriodGranularity.Month => periodStart.ToString("MMM yyyy", CultureInfo.InvariantCulture),
        PeriodGranularity.Quarter => $"Q{((periodStart.Month - 1) / 3) + 1} {periodStart.Year}",
        PeriodGranularity.Year => periodStart.Year.ToString(CultureInfo.InvariantCulture),
        _ => periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };
}
