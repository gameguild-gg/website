namespace GameGuild.Finance.Economy.Contracts;

/// <summary>
/// Read-only, actor-scoped view of a wallet projection. The individual source lots and
/// fragment lineage remain internal to the Economy writer and are never selected by clients.
/// </summary>
public sealed record EconomyWalletSummaryDto(
    Guid WalletId,
    WalletLifecycleState State,
    DateTimeOffset CreatedAt,
    long PendingHard,
    long PendingSoft,
    long PurchasedHard,
    long EarnedHard,
    long RestrictedHard,
    long Soft,
    long HeldHard,
    long HeldSoft,
    long AvailableHardToSpend,
    long AvailableSoftToSpend,
    long WithdrawableHard,
    long OutstandingHardDebt,
    DateTimeOffset ProjectionRebuiltAt,
    long SourceJournalSequence);

/// <summary>
/// A projected line from the immutable Economy journal. It is suitable for a wallet history,
/// but carries no provider references, source hashes, or fragment ranges.
/// </summary>
public sealed record EconomyWalletTransactionDto(
    Guid PostingGroupId,
    Guid JournalEntryId,
    long JournalSequence,
    PostingTemplateKind TemplateKind,
    PostingStatus Status,
    DateTimeOffset RecordedAt,
    EntrySide Side,
    CurrencyCode Currency,
    long AmountUnits,
    ProvenanceKind? Provenance);
