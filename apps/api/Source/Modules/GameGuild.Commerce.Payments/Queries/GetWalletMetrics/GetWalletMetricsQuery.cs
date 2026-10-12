using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Wallet balance and transaction analytics (issue #406): total balances by currency,
///     active/frozen wallet counts and completed transaction volume by type and by day over a
///     trailing window. Follows the ProductMetrics permission precedent
///     (<c>MonetizationPermission.ViewAnalytics</c>) — surfaced here under the wallet
///     administration permission that gates the rest of the wallets read surface.
/// </summary>
/// <param name="Days">Trailing window in days for transaction volume (clamped to 1..365, default 30)</param>
[AuthorizeRequest(WalletsPermission.Keys.Admin)]
public sealed record GetWalletMetricsQuery(int Days = 30) : IQuery<WalletMetricsResponse>;

/// <summary>Total balance aggregated per currency.</summary>
/// <param name="Currency">ISO-4217 currency code</param>
/// <param name="TotalBalance">Sum of wallet balances in the currency</param>
/// <param name="WalletCount">Number of wallets in the currency</param>
public sealed record WalletCurrencyBalance(string Currency, decimal TotalBalance, int WalletCount);

/// <summary>Completed transaction volume for one transaction type.</summary>
/// <param name="Type">Transaction type name</param>
/// <param name="Count">Number of completed transactions</param>
/// <param name="TotalAmount">Sum of transaction amounts</param>
public sealed record WalletTransactionTypeVolume(string Type, int Count, decimal TotalAmount);

/// <summary>Completed transaction volume for one day.</summary>
/// <param name="Date">UTC day</param>
/// <param name="CreditTotal">Sum of Credit + TransferIn amounts</param>
/// <param name="DebitTotal">Sum of Debit + TransferOut amounts</param>
/// <param name="Count">Number of completed transactions</param>
public sealed record WalletDailyVolume(DateOnly Date, decimal CreditTotal, decimal DebitTotal, int Count);

/// <summary>Aggregated wallet metrics response (issue #406).</summary>
/// <param name="GeneratedAtUtc">Report generation timestamp</param>
/// <param name="TotalWallets">All non-deleted wallets</param>
/// <param name="ActiveWallets">Wallets with IsActive = true</param>
/// <param name="FrozenWallets">Wallets with IsLocked = true</param>
/// <param name="BalancesByCurrency">Total balances grouped by currency</param>
/// <param name="TransactionVolumeByType">Completed transaction volume grouped by type over the window</param>
/// <param name="DailyTransactionVolume">Completed transaction volume grouped by day over the window</param>
/// <param name="VolumeFromUtc">Inclusive window start</param>
/// <param name="VolumeToUtc">Inclusive window end</param>
public sealed record WalletMetricsResponse(
    DateTime GeneratedAtUtc,
    int TotalWallets,
    int ActiveWallets,
    int FrozenWallets,
    IReadOnlyList<WalletCurrencyBalance> BalancesByCurrency,
    IReadOnlyList<WalletTransactionTypeVolume> TransactionVolumeByType,
    IReadOnlyList<WalletDailyVolume> DailyTransactionVolume,
    DateTime VolumeFromUtc,
    DateTime VolumeToUtc);
