using GameGuild.Finance.Ledgers.Abstractions;
using GameGuild.Finance.Ledgers.Entities;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Models;
using Microsoft.Extensions.Logging;

namespace GameGuild.Finance.Ledgers.Services;

/// <summary>
/// Service for posting and managing ledger entries.
/// Handles entry creation with automatic ancestor tracking for rollup queries.
/// </summary>
public class LedgerEntryService : ILedgerEntryService
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly ILedgerClosureRepository _closureRepository;
    private readonly ILedgerEntryRepository _entryRepository;
    private readonly ILogger<LedgerEntryService> _logger;

    public LedgerEntryService(
        ILedgerRepository ledgerRepository,
        ILedgerClosureRepository closureRepository,
        ILedgerEntryRepository entryRepository,
        ILogger<LedgerEntryService> logger)
    {
        _ledgerRepository = ledgerRepository;
        _closureRepository = closureRepository;
        _entryRepository = entryRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LedgerEntry> PostEntryAsync(
        Guid ledgerId,
        EntryType type,
        EntryCategory category,
        decimal amount,
        DateOnly transactionDate,
        string description,
        Guid createdByUserId,
        string? externalReferenceId = null,
        CancellationToken ct = default)
    {
        // Get ledger and validate
        var ledger = await _ledgerRepository.GetByIdAsync(ledgerId, ct)
            ?? throw new InvalidOperationException($"Ledger '{ledgerId}' not found.");

        if (!ledger.CanAcceptEntry())
            throw new InvalidOperationException($"Ledger '{ledger.Code}' cannot accept entries.");

        // Get ancestor IDs for rollup tracking
        var ancestorIds = await _closureRepository.GetAncestorIdsAsync(ledgerId, ct);

        // Create entry
        var entry = LedgerEntry.Create(
            ledger,
            ancestorIds.Where(id => id != ledgerId), // Exclude self
            type,
            category,
            amount,
            transactionDate,
            description,
            createdByUserId,
            externalReferenceId);

        // Post immediately
        entry.Post(createdByUserId);

        await _entryRepository.AddAsync(entry, ct);
        LogFinanceAudit("LedgerEntryPosted", entry.Id, ledgerId, createdByUserId, amount, externalReferenceId);

        return entry;
    }

    /// <inheritdoc />
    public async Task<(LedgerEntry Source, LedgerEntry Destination)> PostTransferAsync(
        Guid sourceLedgerId,
        Guid destinationLedgerId,
        decimal amount,
        DateOnly transactionDate,
        string description,
        Guid createdByUserId,
        CancellationToken ct = default)
    {
        // Validate ledgers
        var sourceLedger = await _ledgerRepository.GetByIdAsync(sourceLedgerId, ct)
            ?? throw new InvalidOperationException($"Source ledger '{sourceLedgerId}' not found.");

        var destLedger = await _ledgerRepository.GetByIdAsync(destinationLedgerId, ct)
            ?? throw new InvalidOperationException($"Destination ledger '{destinationLedgerId}' not found.");

        if (!sourceLedger.CanAcceptEntry())
            throw new InvalidOperationException($"Source ledger '{sourceLedger.Code}' cannot accept entries.");

        if (!destLedger.CanAcceptEntry())
            throw new InvalidOperationException($"Destination ledger '{destLedger.Code}' cannot accept entries.");

        // Get ancestor IDs for both ledgers
        var sourceAncestorIds = await _closureRepository.GetAncestorIdsAsync(sourceLedgerId, ct);
        var destAncestorIds = await _closureRepository.GetAncestorIdsAsync(destinationLedgerId, ct);

        // Create paired transfer entries
        var (sourceEntry, destEntry) = LedgerEntry.CreateTransfer(
            sourceLedger,
            sourceAncestorIds.Where(id => id != sourceLedgerId),
            destLedger,
            destAncestorIds.Where(id => id != destinationLedgerId),
            amount,
            transactionDate,
            description,
            createdByUserId);

        // Post both entries
        sourceEntry.Post(createdByUserId);
        destEntry.Post(createdByUserId);

        await _entryRepository.AddRangeAsync([sourceEntry, destEntry], ct);
        LogFinanceAudit("LedgerTransferPosted", sourceEntry.Id, sourceLedgerId, createdByUserId, amount, null);
        LogFinanceAudit("LedgerTransferPosted", destEntry.Id, destinationLedgerId, createdByUserId, amount, null);

        return (sourceEntry, destEntry);
    }

    /// <inheritdoc />
    public async Task<LedgerEntry> ReverseEntryAsync(
        Guid entryId,
        string reason,
        Guid reversedByUserId,
        CancellationToken ct = default)
    {
        var entry = await _entryRepository.GetByIdAsync(entryId, ct)
            ?? throw new InvalidOperationException($"Entry '{entryId}' not found.");

        // Create reversal
        var reversal = entry.CreateReversal(reversedByUserId, reason);

        // Update original and add reversal
        await _entryRepository.UpdateAsync(entry, ct);
        await _entryRepository.AddAsync(reversal, ct);
        LogFinanceAudit("LedgerEntryReversed", reversal.Id, reversal.LedgerId, reversedByUserId, reversal.Amount, entryId.ToString());

        // Handle transfer pair reversal
        if (entry.TransferPairEntryId.HasValue)
        {
            var pairEntry = await _entryRepository.GetByIdAsync(entry.TransferPairEntryId.Value, ct);
            if (pairEntry != null && pairEntry.Status != EntryStatus.Voided)
            {
                var pairReversal = pairEntry.CreateReversal(reversedByUserId, reason);
                await _entryRepository.UpdateAsync(pairEntry, ct);
                await _entryRepository.AddAsync(pairReversal, ct);
                LogFinanceAudit("LedgerEntryReversed", pairReversal.Id, pairReversal.LedgerId, reversedByUserId, pairReversal.Amount, pairEntry.Id.ToString());
            }
        }

        return reversal;
    }

    /// <inheritdoc />
    public async Task<LedgerEntry> UpdateEntryAsync(
        Guid entryId,
        string description,
        string? notes,
        Guid updatedByUserId,
        CancellationToken ct = default)
    {
        var entry = await _entryRepository.GetByIdAsync(entryId, ct)
            ?? throw new InvalidOperationException($"Entry '{entryId}' not found.");

        entry.UpdateDescription(description, notes, updatedByUserId);
        await _entryRepository.UpdateAsync(entry, ct);
        LogFinanceAudit("LedgerEntryUpdated", entry.Id, entry.LedgerId, updatedByUserId, entry.Amount, entry.ExternalReferenceId);

        return entry;
    }

    /// <inheritdoc />
    public async Task DeleteEntryAsync(
        Guid entryId,
        Guid deletedByUserId,
        CancellationToken ct = default)
    {
        var entry = await _entryRepository.GetByIdAsync(entryId, ct)
            ?? throw new InvalidOperationException($"Entry '{entryId}' not found.");

        entry.EnsureCanDelete();
        await _entryRepository.DeleteAsync(entry, ct);
        LogFinanceAudit("LedgerEntryDeleted", entry.Id, entry.LedgerId, deletedByUserId, entry.Amount, entry.ExternalReferenceId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LedgerEntry>> PostBatchAsync(
        IEnumerable<CreateEntryRequest> requests,
        Guid createdByUserId,
        CancellationToken ct = default)
    {
        var entries = new List<LedgerEntry>();
        var requestList = requests.ToList();

        // Pre-fetch all ledgers needed (one by one since we don't have GetByIdsAsync)
        var ledgerIds = requestList.Select(r => r.LedgerId).Distinct();
        var ledgerMap = new Dictionary<Guid, Ledger>();
        foreach (var ledgerId in ledgerIds)
        {
            var ledger = await _ledgerRepository.GetByIdAsync(ledgerId, ct);
            if (ledger != null)
                ledgerMap[ledgerId] = ledger;
        }

        // Pre-fetch all ancestor IDs
        var ancestorMap = new Dictionary<Guid, IReadOnlyList<Guid>>();
        foreach (var ledgerId in ledgerIds)
        {
            ancestorMap[ledgerId] = await _closureRepository.GetAncestorIdsAsync(ledgerId, ct);
        }

        // Create entries
        foreach (var request in requestList)
        {
            if (!ledgerMap.TryGetValue(request.LedgerId, out var ledger))
                throw new InvalidOperationException($"Ledger '{request.LedgerId}' not found.");

            if (!ledger.CanAcceptEntry())
                throw new InvalidOperationException($"Ledger '{ledger.Code}' cannot accept entries.");

            var ancestorIds = ancestorMap[request.LedgerId].Where(id => id != request.LedgerId);

            var entry = LedgerEntry.Create(
                ledger,
                ancestorIds,
                request.Type,
                request.Category,
                request.Amount,
                request.TransactionDate,
                request.Description,
                createdByUserId,
                request.ExternalReferenceId);

            // Set optional fields
            if (request.BudgetId.HasValue && request.BudgetCategoryCode != null)
            {
                entry.AssociateBudget(request.BudgetId.Value, request.BudgetCategoryCode, string.Empty);
            }

            entry.Post(createdByUserId);
            entries.Add(entry);
        }

        await _entryRepository.AddRangeAsync(entries, ct);
        foreach (var entry in entries)
        {
            LogFinanceAudit("LedgerEntryBatchPosted", entry.Id, entry.LedgerId, createdByUserId, entry.Amount, entry.ExternalReferenceId);
        }

        return entries;
    }

    /// <inheritdoc />
    public async Task ReconcileEntryAsync(
        Guid entryId,
        Guid reconciledByUserId,
        string? externalReferenceId = null,
        CancellationToken ct = default)
    {
        var entry = await _entryRepository.GetByIdAsync(entryId, ct)
            ?? throw new InvalidOperationException($"Entry '{entryId}' not found.");

        entry.Reconcile(reconciledByUserId, externalReferenceId);

        await _entryRepository.UpdateAsync(entry, ct);
        LogFinanceAudit("LedgerEntryReconciled", entry.Id, entry.LedgerId, reconciledByUserId, entry.Amount, externalReferenceId ?? entry.ExternalReferenceId);
    }

    private void LogFinanceAudit(
        string eventType,
        Guid entryId,
        Guid ledgerId,
        Guid actorUserId,
        decimal amount,
        string? externalReferenceId)
    {
        _logger.LogInformation(
            "Finance audit event {AuditEventType}: EntryId={EntryId}, LedgerId={LedgerId}, ActorUserId={ActorUserId}, Amount={Amount}, ExternalReferenceId={ExternalReferenceId}, AuditEvent={AuditEvent}, AuditCategory={AuditCategory}",
            eventType,
            entryId,
            ledgerId,
            actorUserId,
            amount,
            externalReferenceId ?? string.Empty,
            true,
            "Finance");
    }
}
