using GameGuild.Finance.Ledgers.Entities;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Models;

namespace GameGuild.Finance.Ledgers.Abstractions;

/// <summary>
/// Service for managing ledger hierarchy operations.
/// Orchestrates Ledger and LedgerClosure interactions.
/// </summary>
public interface ILedgerHierarchyService
{
    /// <summary>
    /// Creates a root ledger with self-referencing closure.
    /// </summary>
    Task<Ledger> CreateRootLedgerAsync(
        Guid tenantId,
        string code,
        string name,
        string currencyCode,
        Guid createdByUserId,
        string? description = null,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a child ledger with proper closure entries.
    /// </summary>
    Task<Ledger> CreateChildLedgerAsync(
        Guid parentLedgerId,
        LedgerType type,
        string code,
        string name,
        Guid createdByUserId,
        string? description = null,
        string? currencyCode = null,
        CancellationToken ct = default);

    /// <summary>
    /// Moves a ledger (and its subtree) to a new parent.
    /// Recalculates all closure entries.
    /// </summary>
    Task MoveLedgerAsync(
        Guid ledgerId,
        Guid newParentLedgerId,
        Guid movedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the full hierarchy tree starting from a root.
    /// </summary>
    Task<LedgerTreeNode> GetHierarchyTreeAsync(
        Guid rootLedgerId,
        int? maxDepth = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets ancestors path from root to ledger.
    /// </summary>
    Task<IReadOnlyList<Ledger>> GetAncestorPathAsync(
        Guid ledgerId,
        CancellationToken ct = default);

    /// <summary>
    /// Validates that a ledger can be moved to a new parent.
    /// </summary>
    Task<bool> CanMoveToParentAsync(
        Guid ledgerId,
        Guid newParentLedgerId,
        CancellationToken ct = default);
}

/// <summary>
/// Service for posting and managing ledger entries.
/// </summary>
public interface ILedgerEntryService
{
    /// <summary>
    /// Posts a new entry to a ledger (with ancestor tracking).
    /// </summary>
    Task<LedgerEntry> PostEntryAsync(
        Guid ledgerId,
        EntryType type,
        EntryCategory category,
        decimal amount,
        DateOnly transactionDate,
        string description,
        Guid createdByUserId,
        string? externalReferenceId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Posts a transfer between two ledgers (creates paired entries).
    /// </summary>
    Task<(LedgerEntry Source, LedgerEntry Destination)> PostTransferAsync(
        Guid sourceLedgerId,
        Guid destinationLedgerId,
        decimal amount,
        DateOnly transactionDate,
        string description,
        Guid createdByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Reverses/voids an entry.
    /// </summary>
    Task<LedgerEntry> ReverseEntryAsync(
        Guid entryId,
        string reason,
        Guid reversedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates a mutable ledger entry.
    /// </summary>
    Task<LedgerEntry> UpdateEntryAsync(
        Guid entryId,
        string description,
        string? notes,
        Guid updatedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a mutable ledger entry.
    /// </summary>
    Task DeleteEntryAsync(
        Guid entryId,
        Guid deletedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Posts multiple entries in a batch (atomic).
    /// </summary>
    Task<IReadOnlyList<LedgerEntry>> PostBatchAsync(
        IEnumerable<CreateEntryRequest> requests,
        Guid createdByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Reconciles an entry with external reference.
    /// </summary>
    Task ReconcileEntryAsync(
        Guid entryId,
        Guid reconciledByUserId,
        string? externalReferenceId = null,
        CancellationToken ct = default);
}

/// <summary>
/// Service for rollup calculations and aggregations.
/// </summary>
public interface ILedgerRollupService
{
    /// <summary>
    /// Calculates consolidated balance for a ledger (direct only).
    /// </summary>
    Task<LedgerBalance> GetBalanceAsync(
        Guid ledgerId,
        DateOnly? asOfDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Calculates consolidated balance including all descendants.
    /// </summary>
    Task<LedgerBalance> GetConsolidatedBalanceAsync(
        Guid ledgerId,
        DateOnly? asOfDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets hierarchical rollup for a subtree.
    /// Each node includes its own balance + sum of children.
    /// </summary>
    Task<LedgerRollupNode> GetHierarchicalRollupAsync(
        Guid rootLedgerId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets rollup by category across hierarchy.
    /// </summary>
    Task<IReadOnlyList<CategoryRollup>> GetCategoryRollupAsync(
        Guid ledgerId,
        bool includeDescendants,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets time-series rollup for trend analysis.
    /// </summary>
    Task<IReadOnlyList<PeriodRollup>> GetPeriodRollupAsync(
        Guid ledgerId,
        bool includeDescendants,
        DateOnly fromDate,
        DateOnly toDate,
        PeriodGranularity granularity,
        CancellationToken ct = default);

    /// <summary>
    /// Recalculates and caches ledger statistics.
    /// </summary>
    Task RefreshLedgerStatsAsync(
        Guid ledgerId,
        CancellationToken ct = default);

    /// <summary>
    /// Refreshes stats for entire subtree.
    /// </summary>
    Task RefreshSubtreeStatsAsync(
        Guid rootLedgerId,
        CancellationToken ct = default);
}

/// <summary>
/// Service for virtual ledger (filtered view) operations.
/// </summary>
public interface IVirtualLedgerService
{
    /// <summary>
    /// Creates a virtual ledger with filter specification.
    /// </summary>
    Task<Ledger> CreateVirtualLedgerAsync(
        Guid tenantId,
        string code,
        string name,
        VirtualLedgerFilter filter,
        Guid createdByUserId,
        string? description = null,
        CancellationToken ct = default);

    /// <summary>
    /// Updates virtual ledger filter.
    /// </summary>
    Task UpdateFilterAsync(
        Guid virtualLedgerId,
        VirtualLedgerFilter filter,
        Guid updatedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets entries matching virtual ledger filter.
    /// </summary>
    Task<IReadOnlyList<LedgerEntry>> GetVirtualEntriesAsync(
        Guid virtualLedgerId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Gets balance for virtual ledger.
    /// </summary>
    Task<LedgerBalance> GetVirtualBalanceAsync(
        Guid virtualLedgerId,
        DateOnly? asOfDate = null,
        CancellationToken ct = default);
}
