using GameGuild.CQRS;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Applies the calling user's account credits to one of their invoices (issue #410 v1,
///     manual multi-tender settlement). Requires an idempotency key (from the
///     Idempotency-Key header); an optional <paramref name="maxAmount" /> caps the credit
///     tender. Credits are consumed FIFO by expiry in the same SaveChanges batch as the
///     invoice update and the settlement row, so the settlement is atomic.
/// </summary>
[GameGuild.Identity.Authorization.AuthorizeRequest("")]
public sealed record ApplyInvoiceAccountCreditsCommand(
    Guid InvoiceId,
    string IdempotencyKey,
    decimal? MaxAmount = null) : ICommand<Result<InvoiceAccountCreditSettlementResult>>;

/// <summary>Outcome of the credit tender application.</summary>
/// <param name="InvoiceId">Settled invoice.</param>
/// <param name="InvoiceNumber">Human-readable invoice number.</param>
/// <param name="InvoiceStatus">Invoice status after the credit tender.</param>
/// <param name="AmountApplied">Credit tender amount applied.</param>
/// <param name="Currency">Shared currency of invoice and consumed grants.</param>
/// <param name="AmountRemaining">Amount still owed to provider tenders.</param>
/// <param name="ConsumedGrants">Per-grant FIFO consumption breakdown.</param>
/// <param name="IdempotentReplay">True when the idempotency key matched an existing settlement.</param>
public sealed record InvoiceAccountCreditSettlementResult(
    Guid InvoiceId,
    string InvoiceNumber,
    InvoiceStatus InvoiceStatus,
    decimal AmountApplied,
    string Currency,
    decimal AmountRemaining,
    IReadOnlyList<GameGuild.Commerce.Payments.AccountCreditConsumption> ConsumedGrants,
    bool IdempotentReplay);
