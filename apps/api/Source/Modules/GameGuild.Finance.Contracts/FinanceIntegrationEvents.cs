namespace GameGuild.Finance.Contracts;

/// <summary>
/// A primitive-only representation of an immutable economy journal line.
/// Account and side are stable codes owned by the producing economy module.
/// </summary>
public sealed record FinancePostingLineV1(
    [property: NonPersonalEventData] int Sequence,
    [property: NonPersonalEventData] string Side,
    [property: NonPersonalEventData] string AccountCode,
    [property: NonPersonalEventData] string CurrencyCode,
    [property: NonPersonalEventData] long Units,
    [property: NonPersonalEventData] Guid? WalletId);

/// <summary>
/// Published atomically when an economy posting is accepted by its journal.
/// Consumers use <see cref="IIntegrationEvent.EventId"/> as their inbox idempotency key.
/// </summary>
public sealed record EconomyPostingAcceptedEventV1(
    [property: NonPersonalEventData] Guid PostingId,
    [property: NonPersonalEventData] string PostingHash,
    [property: NonPersonalEventData] IReadOnlyList<FinancePostingLineV1> Lines) : DurableIntegrationEventBase
{
    public override string EventName => "economy.posting.accepted.v1";
    public override string SourceModule => "Finance.Economy";
}

/// <summary>
/// Published when an entry reaches the posted state in the financial ledger.
/// </summary>
public sealed record FinanceLedgerEntryPostedEventV1(
    [property: NonPersonalEventData] Guid LedgerId,
    [property: NonPersonalEventData] Guid EntryId,
    [property: NonPersonalEventData] string ReferenceNumber,
    [property: NonPersonalEventData] string CurrencyCode,
    [property: NonPersonalEventData] decimal Amount,
    [property: NonPersonalEventData] string EntryType,
    [property: NonPersonalEventData] DateOnly TransactionDate) : DurableIntegrationEventBase
{
    public override string EventName => "finance.ledger-entry.posted.v1";
    public override string SourceModule => "Finance.Ledgers";
}
