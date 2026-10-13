namespace GameGuild.Commerce.Payments;

/// <summary>Per-currency spendable credit balance of a user.</summary>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="Available">Sum of remaining balances of Active grants.</param>
/// <param name="GrantCount">Number of Active grants backing the balance.</param>
/// <param name="EarliestExpiry">Earliest expiry among the backing grants, when any expire.</param>
public sealed record AccountCreditBalance(string Currency, decimal Available, int GrantCount, DateTime? EarliestExpiry);

/// <summary>Result of applying account credits to an invoice (multi-tender: credits are one tender).</summary>
/// <param name="InvoiceId">Settled invoice.</param>
/// <param name="AmountApplied">Credit tender amount actually applied.</param>
/// <param name="Currency">Shared currency of the invoice and the consumed grants.</param>
/// <param name="AmountRemaining">Invoice amount still owed to provider tenders after the credit tender.</param>
/// <param name="ConsumedGrants">Per-grant consumption breakdown (FIFO by expiry).</param>
public sealed record AccountCreditApplicationResult(
    Guid InvoiceId,
    decimal AmountApplied,
    string Currency,
    decimal AmountRemaining,
    IReadOnlyList<AccountCreditConsumption> ConsumedGrants);

/// <summary>One grant's share of a consumption.</summary>
/// <param name="GrantId">Consumed grant.</param>
/// <param name="Amount">Amount taken from the grant.</param>
/// <param name="RemainingAfter">Grant remaining balance after the consumption.</param>
public sealed record AccountCreditConsumption(Guid GrantId, decimal Amount, decimal RemainingAfter);

/// <summary>Outcome of one bounded expiry pass.</summary>
/// <param name="ExpiredGrants">Number of Active grants marked Expired.</param>
/// <param name="ExpiredGrantAmount">Remaining balance wasted by expiry.</param>
/// <param name="ExpiredGifts">Number of unredeemed gifts marked Expired.</param>
public sealed record AccountCreditExpiryResult(int ExpiredGrants, decimal ExpiredGrantAmount, int ExpiredGifts);

/// <summary>
///     Account credit ledger service (issue #410 v1): idempotent grants, per-currency
///     balances, paged history, FIFO-by-expiry consumption, revocation with reason and a
///     bounded daily expiry pass.
/// </summary>
public interface IAccountCreditService
{
    /// <summary>Grants credit idempotently per (tenant, idempotency key); returns the existing grant on replay.</summary>
    Task<Result<AccountCreditGrant>> GrantAsync(AccountCreditGrantRequest request, CancellationToken cancellationToken = default);

    /// <summary>Spendable per-currency balances of the user (Active grants only).</summary>
    Task<IReadOnlyList<AccountCreditBalance>> GetBalancesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Paged credit ledger rows for the user, newest first.</summary>
    Task<PagedResult<AccountCreditTransaction>> GetHistoryAsync(Guid tenantId, Guid userId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Consumes credits FIFO by expiry in a single SaveChanges batch (one atomic
    ///     transaction with the caller's invoice settlement), appending one Consume ledger
    ///     row per touched grant. <paramref name="invoiceKind" /> restricts which credit
    ///     types are eligible.
    /// </summary>
    Task<Result<AccountCreditApplicationResult>> ConsumeAsync(
        Guid tenantId,
        Guid userId,
        string currency,
        decimal amount,
        Guid invoiceId,
        string invoiceKind,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes the remaining balance of a grant with an auditable reason.</summary>
    Task<Result> RevokeAsync(Guid tenantId, Guid grantId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Bounded pass expiring due grants and unredeemed gifts (daily worker path).</summary>
    Task<AccountCreditExpiryResult> ExpireDueAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>Parameters of a credit grant.</summary>
/// <param name="TenantId">Tenant of the owner.</param>
/// <param name="UserId">Owner of the credit.</param>
/// <param name="Amount">Positive amount to grant.</param>
/// <param name="Currency">ISO 4217 currency code (validated against the ISO catalog).</param>
/// <param name="Source">Where the credit came from.</param>
/// <param name="CreditType">What the credit may be applied to.</param>
/// <param name="Description">Human-readable description captured at issuance.</param>
/// <param name="SourceReferenceId">Optional originating entity reference.</param>
/// <param name="ExpiresAt">Optional expiry; null never expires.</param>
/// <param name="IdempotencyKey">Optional idempotency key unique per tenant.</param>
public sealed record AccountCreditGrantRequest(
    Guid TenantId,
    Guid UserId,
    decimal Amount,
    string Currency,
    AccountCreditSource Source,
    AccountCreditType CreditType,
    string Description,
    Guid? SourceReferenceId = null,
    DateTime? ExpiresAt = null,
    string? IdempotencyKey = null);
