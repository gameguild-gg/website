namespace GameGuild.Finance.Contracts;

/// <summary>
/// A primitive-only representation of an immutable economy journal line.
/// Account and side are stable codes owned by the producing economy module.
/// </summary>
public sealed record FinancePostingLineV1(
    int Sequence,
    string Side,
    string AccountCode,
    string CurrencyCode,
    long Units,
    Guid? WalletId);

/// <summary>
/// Published atomically when an economy posting is accepted by its journal.
/// Consumers use <see cref="IIntegrationEvent.EventId"/> as their inbox idempotency key.
/// </summary>
public sealed record EconomyPostingAcceptedEventV1(
    Guid PostingId,
    string PostingHash,
    IReadOnlyList<FinancePostingLineV1> Lines) : DurableIntegrationEventBase
{
    public override string EventName => "finance.economy.posting-accepted.v1";
    public override string SourceModule => "Finance.Economy";
}

/// <summary>
/// Published when an entry reaches the posted state in the financial ledger.
/// </summary>
public sealed record FinanceLedgerEntryPostedEventV1(
    Guid LedgerId,
    Guid EntryId,
    string ReferenceNumber,
    string CurrencyCode,
    decimal Amount,
    string EntryType,
    DateOnly TransactionDate) : DurableIntegrationEventBase
{
    public override string EventName => "finance.ledger-entry.posted.v1";
    public override string SourceModule => "Finance.Ledgers";
}
