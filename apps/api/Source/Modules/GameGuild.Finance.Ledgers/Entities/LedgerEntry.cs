using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Finance.Ledgers.Enums;

namespace GameGuild.Finance.Ledgers.Entities;

/// <summary>
/// Financial entry (transaction) posted to a ledger.
/// Follows double-entry principles where debits and credits must balance.
/// Tracks full ancestry via ParentLedgerIds for efficient rollup queries.
/// </summary>
public class LedgerEntry : IHasIntegrationEvents
{
    private readonly List<IDurableIntegrationEvent> _integrationEvents = [];

    [NotMapped]
    public IReadOnlyList<IDurableIntegrationEvent> IntegrationEvents => _integrationEvents.AsReadOnly();

    public void AddIntegrationEvent(IDurableIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _integrationEvents.Add(integrationEvent);
    }

    public void ClearIntegrationEvents() => _integrationEvents.Clear();

    // ========================================================================
    // Identity
    // ========================================================================

    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Tenant discriminator for multi-tenancy.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>User-friendly reference number (e.g., "TXN-2025-001234").</summary>
    public string ReferenceNumber { get; private set; } = string.Empty;

    /// <summary>External reference ID (e.g., bank transaction ID).</summary>
    public string? ExternalReferenceId { get; private set; }

    // ========================================================================
    // Ledger Association
    // ========================================================================

    /// <summary>Direct ledger this entry is posted to.</summary>
    public Guid LedgerId { get; private set; }

    /// <summary>Navigation to the ledger.</summary>
    public Ledger Ledger { get; private set; } = null!;

    /// <summary>
    /// All ancestor ledger IDs for this entry (denormalized from closure table).
    /// Enables efficient filtering: "all entries under department X".
    /// Stored as JSON array for query flexibility.
    /// </summary>
    public List<Guid> ParentLedgerIds { get; private set; } = [];

    /// <summary>Full hierarchy path at time of posting (for display/audit).</summary>
    public string LedgerPath { get; private set; } = string.Empty;

    // ========================================================================
    // Entry Details
    // ========================================================================

    /// <summary>Debit or credit.</summary>
    public EntryType Type { get; private set; }

    /// <summary>Entry status.</summary>
    public EntryStatus Status { get; private set; }

    /// <summary>Category classification.</summary>
    public EntryCategory Category { get; private set; }

    /// <summary>Amount in ledger's base currency (always positive).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Currency code (ISO 4217).</summary>
    public string CurrencyCode { get; private set; } = "USD";

    /// <summary>Original amount if different currency.</summary>
    public decimal? OriginalAmount { get; private set; }

    /// <summary>Original currency code if converted.</summary>
    public string? OriginalCurrencyCode { get; private set; }

    /// <summary>Exchange rate used for conversion.</summary>
    public decimal? ExchangeRate { get; private set; }

    /// <summary>Date the transaction occurred.</summary>
    public DateOnly TransactionDate { get; private set; }

    /// <summary>Date the entry was posted to ledger.</summary>
    public DateOnly PostingDate { get; private set; }

    /// <summary>Description/memo for the entry.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Detailed notes.</summary>
    public string? Notes { get; private set; }

    /// <summary>Tags for categorization.</summary>
    public List<string> Tags { get; private set; } = [];

    // ========================================================================
    // Counterparty
    // ========================================================================

    /// <summary>Counterparty name (who paid/received).</summary>
    public string? CounterpartyName { get; private set; }

    /// <summary>Counterparty account/reference.</summary>
    public string? CounterpartyReference { get; private set; }

    // ========================================================================
    // Transfer Linking
    // ========================================================================

    /// <summary>For transfers: the paired entry ID in destination ledger.</summary>
    public Guid? TransferPairEntryId { get; private set; }

    /// <summary>Navigation to paired entry.</summary>
    public LedgerEntry? TransferPairEntry { get; private set; }

    /// <summary>For transfers: the destination ledger ID.</summary>
    public Guid? TransferDestinationLedgerId { get; private set; }

    // ========================================================================
    // Budget Integration
    // ========================================================================

    /// <summary>Associated budget ID.</summary>
    public Guid? BudgetId { get; private set; }

    /// <summary>Budget category code.</summary>
    public string? BudgetCategoryCode { get; private set; }

    /// <summary>Budget period identifier.</summary>
    public string? BudgetPeriodId { get; private set; }

    // ========================================================================
    // Reversals & Adjustments
    // ========================================================================

    /// <summary>If this is a reversal, the original entry ID.</summary>
    public Guid? ReversesEntryId { get; private set; }

    /// <summary>Navigation to reversed entry.</summary>
    public LedgerEntry? ReversesEntry { get; private set; }

    /// <summary>If this entry was reversed, the reversal entry ID.</summary>
    public Guid? ReversedByEntryId { get; private set; }

    /// <summary>Navigation to reversal entry.</summary>
    public LedgerEntry? ReversedByEntry { get; private set; }

    // ========================================================================
    // Attachments & Metadata
    // ========================================================================

    /// <summary>JSON metadata for extensibility.</summary>
    public string? Metadata { get; private set; }

    /// <summary>Attachment/document IDs.</summary>
    public List<Guid> AttachmentIds { get; private set; } = [];

    // ========================================================================
    // Audit
    // ========================================================================

    /// <summary>User who created this entry.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Last modification timestamp (UTC).</summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>User who last modified.</summary>
    public Guid? UpdatedByUserId { get; private set; }

    /// <summary>Optimistic concurrency token.</summary>
    public uint RowVersion { get; private set; }

    // ========================================================================
    // Factory Methods
    // ========================================================================

    private LedgerEntry() { } // EF Core

    /// <summary>
    /// Creates a new ledger entry.
    /// </summary>
    public static LedgerEntry Create(
        Ledger ledger,
        IEnumerable<Guid> ancestorLedgerIds,
        EntryType type,
        EntryCategory category,
        decimal amount,
        DateOnly transactionDate,
        string description,
        Guid createdByUserId,
        string? externalReferenceId = null)
    {
        if (!ledger.CanAcceptEntry())
            throw new InvalidOperationException($"Ledger '{ledger.Code}' cannot accept entries in current state.");

        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        var entry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = ledger.TenantId,
            ReferenceNumber = GenerateReferenceNumber(),
            ExternalReferenceId = externalReferenceId,
            LedgerId = ledger.Id,
            Ledger = ledger,
            ParentLedgerIds = ancestorLedgerIds.ToList(),
            LedgerPath = ledger.HierarchyPath,
            Type = type,
            Status = EntryStatus.Pending,
            Category = category,
            Amount = amount,
            CurrencyCode = ledger.CurrencyCode,
            TransactionDate = transactionDate,
            PostingDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = description,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        return entry;
    }

    /// <summary>
    /// Creates a transfer entry pair between two ledgers.
    /// </summary>
    public static (LedgerEntry Source, LedgerEntry Destination) CreateTransfer(
        Ledger sourceLedger,
        IEnumerable<Guid> sourceAncestorIds,
        Ledger destinationLedger,
        IEnumerable<Guid> destinationAncestorIds,
        decimal amount,
        DateOnly transactionDate,
        string description,
        Guid createdByUserId)
    {
        // Source: Credit (money leaving)
        var sourceEntry = Create(
            sourceLedger,
            sourceAncestorIds,
            EntryType.Credit,
            EntryCategory.Transfer,
            amount,
            transactionDate,
            $"Transfer to {destinationLedger.Name}: {description}",
            createdByUserId);

        // Destination: Debit (money arriving)
        var destEntry = Create(
            destinationLedger,
            destinationAncestorIds,
            EntryType.Debit,
            EntryCategory.Transfer,
            amount,
            transactionDate,
            $"Transfer from {sourceLedger.Name}: {description}",
            createdByUserId);

        // Link them
        sourceEntry.TransferPairEntryId = destEntry.Id;
        sourceEntry.TransferPairEntry = destEntry;
        sourceEntry.TransferDestinationLedgerId = destinationLedger.Id;

        destEntry.TransferPairEntryId = sourceEntry.Id;
        destEntry.TransferPairEntry = sourceEntry;
        destEntry.TransferDestinationLedgerId = sourceLedger.Id;

        return (sourceEntry, destEntry);
    }

    // ========================================================================
    // Domain Methods
    // ========================================================================

    /// <summary>
    /// Posts the entry (finalizes it to affect balances).
    /// </summary>
    public void Post(Guid postedByUserId)
    {
        if (Status != EntryStatus.Pending)
            throw new InvalidOperationException($"Cannot post entry in {Status} status.");

        Status = EntryStatus.Posted;
        PostingDate = DateOnly.FromDateTime(DateTime.UtcNow);
        UpdatedByUserId = postedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reconciles the entry with external source.
    /// </summary>
    public void Reconcile(Guid reconciledByUserId, string? externalReferenceId = null)
    {
        if (Status != EntryStatus.Posted)
            throw new InvalidOperationException("Only posted entries can be reconciled.");

        Status = EntryStatus.Reconciled;
        if (externalReferenceId != null)
            ExternalReferenceId = externalReferenceId;
        UpdatedByUserId = reconciledByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a reversal entry for voiding.
    /// </summary>
    public LedgerEntry CreateReversal(Guid createdByUserId, string reason)
    {
        if (Status == EntryStatus.Voided)
            throw new InvalidOperationException("Entry is already voided.");

        if (ReversedByEntryId.HasValue)
            throw new InvalidOperationException("Entry has already been reversed.");

        var reversal = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ReferenceNumber = GenerateReferenceNumber(),
            LedgerId = LedgerId,
            ParentLedgerIds = ParentLedgerIds,
            LedgerPath = LedgerPath,
            Type = Type == EntryType.Debit ? EntryType.Credit : EntryType.Debit,
            Status = EntryStatus.Posted,
            Category = EntryCategory.Adjustment,
            Amount = Amount,
            CurrencyCode = CurrencyCode,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PostingDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = $"Reversal of {ReferenceNumber}: {reason}",
            ReversesEntryId = Id,
            ReversesEntry = this,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        // Mark original as voided
        Status = EntryStatus.Voided;
        ReversedByEntryId = reversal.Id;
        ReversedByEntry = reversal;
        UpdatedByUserId = createdByUserId;
        UpdatedAt = DateTime.UtcNow;

        return reversal;
    }

    /// <summary>
    /// Sets currency conversion information.
    /// </summary>
    public void SetCurrencyConversion(
        decimal originalAmount,
        string originalCurrencyCode,
        decimal exchangeRate)
    {
        OriginalAmount = originalAmount;
        OriginalCurrencyCode = originalCurrencyCode;
        ExchangeRate = exchangeRate;
    }

    /// <summary>
    /// Associates entry with a budget.
    /// </summary>
    public void AssociateBudget(Guid budgetId, string categoryCode, string periodId)
    {
        BudgetId = budgetId;
        BudgetCategoryCode = categoryCode;
        BudgetPeriodId = periodId;
    }

    /// <summary>
    /// Updates description and notes.
    /// </summary>
    public void UpdateDescription(string description, string? notes, Guid updatedByUserId)
    {
        if (Status == EntryStatus.Reconciled)
            throw new InvalidOperationException("Cannot modify reconciled entries.");

        if (Status == EntryStatus.Voided)
            throw new InvalidOperationException("Cannot modify voided entries.");

        Description = description;
        Notes = notes;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates whether the entry can be permanently deleted.
    /// </summary>
    public void EnsureCanDelete()
    {
        if (Status == EntryStatus.Reconciled)
            throw new InvalidOperationException("Cannot delete reconciled entries.");

        if (Status == EntryStatus.Voided)
            throw new InvalidOperationException("Cannot delete voided entries.");

        if (TransferPairEntryId.HasValue)
            throw new InvalidOperationException("Cannot delete transfer entries.");

        if (ReversesEntryId.HasValue || ReversedByEntryId.HasValue)
            throw new InvalidOperationException("Cannot delete entries that are part of a reversal chain.");
    }

    /// <summary>
    /// Calculates signed amount (positive for debit, negative for credit).
    /// </summary>
    public decimal GetSignedAmount() => Amount * (int)Type;

    // ========================================================================
    // Helpers
    // ========================================================================

    private static string GenerateReferenceNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"TXN-{timestamp}-{random}";
    }
}
