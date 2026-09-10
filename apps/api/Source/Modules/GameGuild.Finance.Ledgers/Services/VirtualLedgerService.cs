using System.Text.Json;
using GameGuild.Finance.Ledgers.Abstractions;
using GameGuild.Finance.Ledgers.Entities;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Models;

namespace GameGuild.Finance.Ledgers.Services;

/// <summary>
/// Service for virtual ledger (filtered view) operations.
/// Virtual ledgers provide filtered views of entries without duplicating data.
/// </summary>
public class VirtualLedgerService : IVirtualLedgerService
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly ILedgerEntryRepository _entryRepository;
    private readonly ILedgerClosureRepository _closureRepository;

    public VirtualLedgerService(
        ILedgerRepository ledgerRepository,
        ILedgerEntryRepository entryRepository,
        ILedgerClosureRepository closureRepository)
    {
        _ledgerRepository = ledgerRepository;
        _entryRepository = entryRepository;
        _closureRepository = closureRepository;
    }

    /// <inheritdoc />
    public async Task<Ledger> CreateVirtualLedgerAsync(
        Guid tenantId,
        string code,
        string name,
        VirtualLedgerFilter filter,
        Guid createdByUserId,
        string? description = null,
        CancellationToken ct = default)
    {
        // Validate code uniqueness
        if (await _ledgerRepository.ExistsAsync(tenantId, code, ct))
            throw new InvalidOperationException($"Ledger code '{code}' already exists.");

        var filterSpec = JsonSerializer.Serialize(filter);
        var sourceLedgerIds = filter.SourceLedgerIds?.ToList() ?? [];

        var virtualLedger = Ledger.CreateVirtual(
            tenantId,
            code,
            name,
            filterSpec,
            sourceLedgerIds,
            createdByUserId,
            description);

        await _ledgerRepository.AddAsync(virtualLedger, ct);

        return virtualLedger;
    }

    /// <inheritdoc />
    public async Task UpdateFilterAsync(
        Guid virtualLedgerId,
        VirtualLedgerFilter filter,
        Guid updatedByUserId,
        CancellationToken ct = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(virtualLedgerId, ct)
            ?? throw new InvalidOperationException($"Virtual ledger '{virtualLedgerId}' not found.");

        if (ledger.Type != LedgerType.Virtual)
            throw new InvalidOperationException("Can only update filter on virtual ledgers.");

        // In a real implementation, we'd have a method on Ledger to update virtual filter
        await _ledgerRepository.UpdateAsync(ledger, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LedgerEntry>> GetVirtualEntriesAsync(
        Guid virtualLedgerId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(virtualLedgerId, ct)
            ?? throw new InvalidOperationException($"Virtual ledger '{virtualLedgerId}' not found.");

        if (ledger.Type != LedgerType.Virtual)
            throw new InvalidOperationException("Not a virtual ledger.");

        var filter = ParseFilter(ledger.VirtualFilterSpec);

        // Get entries from source ledgers
        var allEntries = new List<LedgerEntry>();

        var sourceLedgerIds = filter.SourceLedgerIds ?? ledger.VirtualSourceLedgerIds ?? [];

        foreach (var sourceLedgerId in sourceLedgerIds)
        {
            DateTimeOffset? fromDateTime = fromDate.HasValue
                ? new DateTimeOffset(fromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                : null;
            DateTimeOffset? toDateTime = toDate.HasValue
                ? new DateTimeOffset(toDate.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)
                : null;

            IReadOnlyList<LedgerEntry> entries;

            if (filter.IncludeDescendants)
            {
                entries = await _entryRepository.GetByLedgerIncludingDescendantsAsync(
                    sourceLedgerId, fromDateTime, toDateTime, ct);
            }
            else
            {
                entries = await _entryRepository.GetByLedgerAsync(
                    sourceLedgerId, fromDateTime, toDateTime, ct);
            }

            allEntries.AddRange(entries);
        }

        // Apply additional filters
        var filteredEntries = allEntries.AsEnumerable();

        if (filter.Categories?.Any() == true)
        {
            filteredEntries = filteredEntries.Where(e => filter.Categories.Contains(e.Category));
        }

        if (filter.MinAmount.HasValue)
        {
            filteredEntries = filteredEntries.Where(e => e.Amount >= filter.MinAmount.Value);
        }

        if (filter.MaxAmount.HasValue)
        {
            filteredEntries = filteredEntries.Where(e => e.Amount <= filter.MaxAmount.Value);
        }

        if (filter.Tags?.Any() == true)
        {
            filteredEntries = filteredEntries.Where(e => e.Tags.Any(t => filter.Tags.Contains(t)));
        }

        return filteredEntries
            .OrderByDescending(e => e.TransactionDate)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<LedgerBalance> GetVirtualBalanceAsync(
        Guid virtualLedgerId,
        DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(virtualLedgerId, ct)
            ?? throw new InvalidOperationException($"Virtual ledger '{virtualLedgerId}' not found.");

        if (ledger.Type != LedgerType.Virtual)
            throw new InvalidOperationException("Not a virtual ledger.");

        // Get all entries and calculate totals
        var entries = await GetVirtualEntriesAsync(virtualLedgerId, null, asOfDate, 0, int.MaxValue, ct);

        var totalDebit = entries.Where(e => e.Type == EntryType.Debit).Sum(e => e.Amount);
        var totalCredit = entries.Where(e => e.Type == EntryType.Credit).Sum(e => e.Amount);

        return new LedgerBalance(
            LedgerId: virtualLedgerId,
            LedgerCode: ledger.Code,
            LedgerName: ledger.Name,
            TotalDebit: totalDebit,
            TotalCredit: totalCredit,
            NetBalance: totalCredit - totalDebit,
            EntryCount: entries.Count,
            CurrencyCode: ledger.CurrencyCode,
            CalculatedAt: DateTime.UtcNow,
            IncludesDescendants: true);
    }

    private static VirtualLedgerFilter ParseFilter(string? filterSpec)
    {
        if (string.IsNullOrEmpty(filterSpec))
        {
            return new VirtualLedgerFilter();
        }

        try
        {
            return JsonSerializer.Deserialize<VirtualLedgerFilter>(filterSpec)
                ?? new VirtualLedgerFilter();
        }
        catch
        {
            return new VirtualLedgerFilter();
        }
    }
}
