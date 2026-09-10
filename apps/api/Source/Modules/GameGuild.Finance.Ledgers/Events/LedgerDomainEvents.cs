namespace GameGuild.Finance.Ledgers.Events;

/// <summary>
/// Base class for all ledger domain events.
/// </summary>
public abstract record LedgerDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public Guid TenantId { get; init; }
}

// ============================================================================
// Ledger Events
// ============================================================================

/// <summary>
/// Raised when a new ledger is created.
/// </summary>
public record LedgerCreatedEvent : LedgerDomainEvent
{
    public required Guid LedgerId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string LedgerType { get; init; }
    public Guid? ParentLedgerId { get; init; }
    public required Guid CreatedByUserId { get; init; }
}

/// <summary>
/// Raised when a ledger is moved to a new parent.
/// </summary>
public record LedgerMovedEvent : LedgerDomainEvent
{
    public required Guid LedgerId { get; init; }
    public Guid? OldParentLedgerId { get; init; }
    public required Guid NewParentLedgerId { get; init; }
    public required string OldHierarchyPath { get; init; }
    public required string NewHierarchyPath { get; init; }
    public required Guid MovedByUserId { get; init; }
}

/// <summary>
/// Raised when a ledger's status changes.
/// </summary>
public record LedgerStatusChangedEvent : LedgerDomainEvent
{
    public required Guid LedgerId { get; init; }
    public required string OldStatus { get; init; }
    public required string NewStatus { get; init; }
    public required Guid ChangedByUserId { get; init; }
}

/// <summary>
/// Raised when a ledger is archived.
/// </summary>
public record LedgerArchivedEvent : LedgerDomainEvent
{
    public required Guid LedgerId { get; init; }
    public required Guid ArchivedByUserId { get; init; }
}

/// <summary>
/// Raised when a ledger's cached statistics are refreshed.
/// </summary>
public record LedgerStatsRefreshedEvent : LedgerDomainEvent
{
    public required Guid LedgerId { get; init; }
    public required decimal NewNetBalance { get; init; }
    public required int NewEntryCount { get; init; }
    public required DateTimeOffset CalculatedAt { get; init; }
}

// ============================================================================
// Entry Events
// ============================================================================

/// <summary>
/// Raised when a new entry is posted to a ledger.
/// </summary>
public record EntryPostedEvent : LedgerDomainEvent
{
    public required Guid EntryId { get; init; }
    public required Guid LedgerId { get; init; }
    public required string EntryType { get; init; }
    public required decimal Amount { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset TransactionDate { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public string? Category { get; init; }
    public string? Reference { get; init; }
    public string? ExternalId { get; init; }
    public IReadOnlyList<Guid> AffectedLedgerIds { get; init; } = Array.Empty<Guid>();
}

/// <summary>
/// Raised when a transfer is completed between two ledgers.
/// </summary>
public record TransferCompletedEvent : LedgerDomainEvent
{
    public required Guid DebitEntryId { get; init; }
    public required Guid CreditEntryId { get; init; }
    public required Guid FromLedgerId { get; init; }
    public required Guid ToLedgerId { get; init; }
    public required decimal Amount { get; init; }
    public required string Description { get; init; }
    public required DateTimeOffset TransactionDate { get; init; }
    public required Guid CreatedByUserId { get; init; }
}

/// <summary>
/// Raised when an entry is reversed.
/// </summary>
public record EntryReversedEvent : LedgerDomainEvent
{
    public required Guid OriginalEntryId { get; init; }
    public required Guid ReversalEntryId { get; init; }
    public required Guid LedgerId { get; init; }
    public required decimal Amount { get; init; }
    public required string ReversalReason { get; init; }
    public required Guid ReversedByUserId { get; init; }
}

/// <summary>
/// Raised when an entry's status changes.
/// </summary>
public record EntryStatusChangedEvent : LedgerDomainEvent
{
    public required Guid EntryId { get; init; }
    public required Guid LedgerId { get; init; }
    public required string OldStatus { get; init; }
    public required string NewStatus { get; init; }
    public required Guid ChangedByUserId { get; init; }
}

// ============================================================================
// Virtual Ledger Events
// ============================================================================

/// <summary>
/// Raised when a virtual ledger is created.
/// </summary>
public record VirtualLedgerCreatedEvent : LedgerDomainEvent
{
    public required Guid VirtualLedgerId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string FilterSpec { get; init; }
    public required Guid CreatedByUserId { get; init; }
}

/// <summary>
/// Raised when a virtual ledger's filter is updated.
/// </summary>
public record VirtualLedgerFilterUpdatedEvent : LedgerDomainEvent
{
    public required Guid VirtualLedgerId { get; init; }
    public required string OldFilterSpec { get; init; }
    public required string NewFilterSpec { get; init; }
    public required Guid UpdatedByUserId { get; init; }
}

/// <summary>
/// Raised when a virtual ledger is deleted.
/// </summary>
public record VirtualLedgerDeletedEvent : LedgerDomainEvent
{
    public required Guid VirtualLedgerId { get; init; }
    public required string Code { get; init; }
    public required Guid DeletedByUserId { get; init; }
}
