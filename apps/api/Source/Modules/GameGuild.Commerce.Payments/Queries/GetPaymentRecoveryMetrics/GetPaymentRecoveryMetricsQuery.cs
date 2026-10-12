using GameGuild.CQRS;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Query for payment success and failed-payment recovery metrics over an inclusive window (issue #403).
/// </summary>
/// <param name="FromUtc">Inclusive window start.</param>
/// <param name="ToUtc">Inclusive window end.</param>
/// <param name="TenantId">Optional tenant scope; null aggregates across tenants.</param>
public sealed record GetPaymentRecoveryMetricsQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? TenantId = null) : IQuery<PaymentRecoveryMetrics>;
