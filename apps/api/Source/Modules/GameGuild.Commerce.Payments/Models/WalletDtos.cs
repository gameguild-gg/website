namespace GameGuild.Commerce.Payments.Models;

/// <summary>
///     Request to update wallet settings
/// </summary>
public sealed record PatchWalletRequest(
    string? Currency = null,
    decimal? DailyLimit = null,
    decimal? MonthlyLimit = null);

/// <summary>
///     Request to freeze a wallet
/// </summary>
public sealed record FreezeWalletRequest(string Reason);

/// <summary>
///     Administrative balance adjustment against a wallet (issue #406).
/// </summary>
/// <param name="Direction">Either "credit" (add funds) or "debit" (deduct funds)</param>
/// <param name="Amount">Positive amount to adjust</param>
/// <param name="Reason">Human-readable adjustment reason (audited)</param>
/// <param name="ReferenceId">Optional idempotency reference; replays return the original transaction</param>
public sealed record AdjustWalletRequest(string Direction, decimal Amount, string Reason, string? ReferenceId = null);

/// <summary>
///     Wallet-to-wallet transfer request (issue #406). Both wallets must share the same currency.
/// </summary>
/// <param name="FromWalletId">Source wallet identifier</param>
/// <param name="ToWalletId">Destination wallet identifier</param>
/// <param name="Amount">Positive amount to transfer</param>
/// <param name="Description">Human-readable transfer description (audited)</param>
/// <param name="ReferenceId">Optional idempotency reference; replays return the original transactions</param>
public sealed record TransferWalletsRequest(Guid FromWalletId, Guid ToWalletId, decimal Amount, string Description, string? ReferenceId = null);

/// <summary>
///     Transfer result payload for the wallets:transfer endpoint (issue #406).
/// </summary>
public sealed record TransferWalletsResponse(WalletTransaction DebitTransaction, WalletTransaction CreditTransaction);

/// <summary>
///     Wallet audit log entry
/// </summary>
public record WalletAuditEntry(
    Guid Id,
    Guid WalletId,
    string Action,
    string? Details,
    decimal? Amount,
    decimal? BalanceAfter,
    DateTime Timestamp,
    string? PerformedBy);

/// <summary>
///     Paginated wallet audit log response
/// </summary>
public sealed record WalletAuditLogResponse(
    IReadOnlyList<WalletAuditEntry> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
///     Paginated list of wallets response
/// </summary>
public sealed record WalletListResponse(
    IReadOnlyList<WalletSummary> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
///     Summary view of a wallet for list responses
/// </summary>
public record WalletSummary(
    Guid Id,
    Guid UserId,
    string Currency,
    decimal Balance,
    bool IsFrozen,
    DateTime CreatedAt,
    DateTime? LastTransactionAt);
