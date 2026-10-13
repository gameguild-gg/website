using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Report-only wallet reconciliation query (issue #406): verifies, per wallet, that
///     <c>Balance</c> equals the latest transaction's <c>BalanceAfter</c> and equals the signed
///     sum of completed transactions (Credit + TransferIn − Debit − TransferOut). The query
///     never writes, corrects or locks anything — discrepancies are surfaced for operators
///     through the reconciliation report surface.
/// </summary>
[AuthorizeRequest(WalletsPermission.Keys.Admin)]
public sealed record GetWalletReconciliationReportQuery(Guid? WalletId = null) : IQuery<WalletReconciliationReportResponse>;

/// <summary>Reconciliation outcome for a single wallet.</summary>
/// <param name="WalletId">Wallet identifier</param>
/// <param name="UserId">Owning user identifier</param>
/// <param name="Currency">Wallet currency code</param>
/// <param name="Balance">Persisted wallet balance</param>
/// <param name="LatestBalanceAfter">BalanceAfter of the newest transaction, null when the wallet has none</param>
/// <param name="CompletedTransactionSum">Σ(Credit + TransferIn − Debit − TransferOut) over Completed transactions</param>
/// <param name="BalanceMatchesLatest">Whether Balance equals LatestBalanceAfter (or the wallet has no transactions and a zero balance)</param>
/// <param name="BalanceMatchesTransactionSum">Whether Balance equals CompletedTransactionSum</param>
public sealed record WalletReconciliationEntry(
    Guid WalletId,
    Guid UserId,
    string Currency,
    decimal Balance,
    decimal? LatestBalanceAfter,
    decimal CompletedTransactionSum,
    bool BalanceMatchesLatest,
    bool BalanceMatchesTransactionSum);

/// <summary>Aggregated wallet reconciliation report (report-only).</summary>
/// <param name="GeneratedAtUtc">Report generation timestamp</param>
/// <param name="WalletsChecked">Number of wallets evaluated</param>
/// <param name="DiscrepanciesFound">Number of wallets failing either invariant</param>
/// <param name="Items">Per-wallet reconciliation results</param>
public sealed record WalletReconciliationReportResponse(
    DateTime GeneratedAtUtc,
    int WalletsChecked,
    int DiscrepanciesFound,
    IReadOnlyList<WalletReconciliationEntry> Items);
