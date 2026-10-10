using GameGuild.CQRS;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Query for failed payments that are due for a retry right now: retries remaining and the
///     scheduled next-retry time has passed (issue #403). This is the candidate set an automated
///     retry queue (#415) would consume.
/// </summary>
/// <param name="TenantId">Optional tenant scope; null spans all tenants (system-administrators only).</param>
/// <param name="Take">Maximum number of candidates to return (1–100, default 50).</param>
public sealed record GetRetryingPaymentCandidatesQuery(
    Guid? TenantId = null,
    int Take = 50) : IQuery<IReadOnlyList<PaymentResult>>;
