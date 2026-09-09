using GameGuild.CQRS;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Models;

namespace GameGuild.Finance.Ledgers.Commands;

// ============================================================================
// Ledger Commands
// ============================================================================

/// <summary>
/// Creates a new root ledger for a tenant.
/// </summary>
public record CreateRootLedgerCommand(
    Guid TenantId,
    string Code,
    string Name,
    string CurrencyCode,
    Guid CreatedByUserId,
    string? Description = null,
    IEnumerable<string>? Tags = null) : ICommand<LedgerDto>;

/// <summary>
/// Creates a child ledger under a parent.
/// </summary>
public record CreateChildLedgerCommand(
    Guid ParentLedgerId,
    LedgerType Type,
    string Code,
    string Name,
    Guid CreatedByUserId,
    string? Description = null,
    string? CurrencyCode = null,
    bool IsShared = false,
    decimal? BudgetLimit = null,
    DateOnly? ProjectStartDate = null,
    DateOnly? ProjectEndDate = null,
    IEnumerable<string>? Tags = null) : ICommand<LedgerDto>;

/// <summary>
/// Creates a virtual (filtered) ledger.
/// </summary>
public record CreateVirtualLedgerCommand(
    Guid TenantId,
    string Code,
    string Name,
    VirtualLedgerFilter Filter,
    Guid CreatedByUserId,
    string? Description = null) : IRequest<LedgerDto>;

/// <summary>
/// Updates ledger metadata.
/// </summary>
public record UpdateLedgerCommand(
    Guid LedgerId,
    string Name,
    Guid UpdatedByUserId,
    string? Description = null,
    IEnumerable<string>? Tags = null) : IRequest<LedgerDto>;

/// <summary>
/// Changes ledger status.
/// </summary>
public record ChangeLedgerStatusCommand(
    Guid LedgerId,
    LedgerStatus NewStatus,
    Guid UpdatedByUserId) : IRequest<LedgerDto>;

/// <summary>
/// Moves a ledger to a new parent.
/// </summary>
public record MoveLedgerCommand(
    Guid LedgerId,
    Guid NewParentLedgerId,
    Guid MovedByUserId) : IRequest<LedgerDto>;

/// <summary>
/// Sets budget limit on a ledger.
/// </summary>
public record SetLedgerBudgetCommand(
    Guid LedgerId,
    decimal? BudgetLimit,
    Guid UpdatedByUserId) : IRequest<LedgerDto>;

/// <summary>
/// Soft deletes a ledger.
/// </summary>
public record DeleteLedgerCommand(
    Guid LedgerId,
    Guid DeletedByUserId) : IRequest<bool>;

// ============================================================================
// Entry Commands
// ============================================================================

/// <summary>
/// Posts a new entry to a ledger.
/// </summary>
public record PostEntryCommand(
    Guid LedgerId,
    EntryType Type,
    EntryCategory Category,
    decimal Amount,
    DateOnly TransactionDate,
    string Description,
    Guid CreatedByUserId,
    string? ExternalReferenceId = null,
    string? CounterpartyName = null,
    Guid? BudgetId = null,
    string? BudgetCategoryCode = null,
    IEnumerable<string>? Tags = null) : ICommand<LedgerEntryDto>;

/// <summary>
/// Posts a transfer between two ledgers.
/// </summary>
public record PostTransferCommand(
    Guid SourceLedgerId,
    Guid DestinationLedgerId,
    decimal Amount,
    DateOnly TransactionDate,
    string Description,
    Guid CreatedByUserId) : ICommand<TransferResult>;

/// <summary>
/// Posts multiple entries in a batch.
/// </summary>
public record PostBatchEntriesCommand(
    IEnumerable<CreateEntryRequest> Entries,
    Guid CreatedByUserId) : IRequest<IReadOnlyList<LedgerEntryDto>>;

/// <summary>
/// Reverses/voids an entry.
/// </summary>
public record ReverseEntryCommand(
    Guid EntryId,
    string Reason,
    Guid ReversedByUserId) : ICommand<LedgerEntryDto>;

/// <summary>
/// Reconciles an entry with external reference.
/// </summary>
public record ReconcileEntryCommand(
    Guid EntryId,
    Guid ReconciledByUserId,
    string? ExternalReferenceId = null) : IRequest<LedgerEntryDto>;

/// <summary>
/// Updates entry description/notes.
/// </summary>
public record UpdateEntryCommand(
    Guid EntryId,
    string Description,
    Guid UpdatedByUserId,
    string? Notes = null) : ICommand<LedgerEntryDto>;

/// <summary>
/// Deletes a mutable entry.
/// </summary>
public record DeleteEntryCommand(
    Guid EntryId,
    Guid DeletedByUserId) : ICommand<bool>;

// ============================================================================
// Stats Commands
// ============================================================================

/// <summary>
/// Refreshes cached statistics for a ledger.
/// </summary>
public record RefreshLedgerStatsCommand(
    Guid LedgerId) : IRequest<bool>;

/// <summary>
/// Refreshes cached statistics for an entire subtree.
/// </summary>
public record RefreshSubtreeStatsCommand(
    Guid RootLedgerId) : IRequest<bool>;

/// <summary>
/// Changes entry status (post/void/reconcile).
/// </summary>
public record ChangeEntryStatusCommand(
    Guid EntryId,
    EntryStatus NewStatus,
    Guid UpdatedByUserId,
    string? Reason = null) : IRequest<LedgerEntryDto>;

/// <summary>
/// Updates virtual ledger filter specification.
/// </summary>
public record UpdateVirtualLedgerFilterCommand(
    Guid VirtualLedgerId,
    VirtualLedgerFilter Filter,
    Guid UpdatedByUserId) : IRequest<bool>;

/// <summary>
/// Deletes a virtual ledger.
/// </summary>
public record DeleteVirtualLedgerCommand(
    Guid VirtualLedgerId,
    Guid DeletedByUserId) : IRequest<bool>;
