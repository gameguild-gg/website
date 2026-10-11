using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Velocity fraud signal provider (issue #409). Computes tenant-scoped velocity signals
///     from the existing payment history of the customer being charged:
///     per-user payment frequency, failed-attempt bursts, first-seen payment methods
///     ("new device / new method") and prior disputed payments.
/// </summary>
/// <remarks>
///     <para>
///         The look-back query matches the partial index
///         <c>ix_payments_fraud_velocity_lookup</c> (non-cancelled, non-fraud-rejected rows).
///         Fraud-rejected rows are excluded so a previously rejected attempt is reassessed
///         without its own history inflating the velocity counts.
///     </para>
///     <para>
///         Customer scope: prior payments are matched on the external (provider) customer id;
///         attempts that are not yet bound to a provider customer fall back to the
///         subscription being charged.
///     </para>
/// </remarks>
public sealed class VelocityFraudSignalProvider(
    IApplicationDbContext context,
    ILogger<VelocityFraudSignalProvider> logger) : IFraudSignalProvider
{
    private const string FrequencySignalType = "Velocity.PaymentFrequency";
    private const string FailedAttemptsSignalType = "Velocity.FailedAttempts";
    private const string NewPaymentMethodSignalType = "Velocity.NewPaymentMethod";
    private const string DisputedPaymentsSignalType = "Velocity.DisputedPayments";

    /// <inheritdoc />
    public string Name => "Velocity";

    /// <inheritdoc />
    public async Task<IReadOnlyList<FraudSignal>> EvaluateAsync(
        FraudSignalContext signalContext,
        FraudAssessmentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signalContext);
        ArgumentNullException.ThrowIfNull(options);

        var windowStart = SystemClock.UtcNow - options.VelocityWindow;

        var priorPaymentsQuery = context.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.TenantId == signalContext.TenantId
                              && payment.Id != signalContext.PaymentId
                              && payment.CreatedAt >= windowStart
                              && payment.Status != PaymentStatus.Cancelled
                              && (payment.FraudDecision == null || payment.FraudDecision == FraudDecision.Allow));

        priorPaymentsQuery = signalContext.ExternalCustomerId is null
            ? priorPaymentsQuery.Where(payment => payment.SubscriptionId == signalContext.SubscriptionId)
            : priorPaymentsQuery.Where(payment => payment.ExternalCustomerId == signalContext.ExternalCustomerId);

        var priorPayments = await priorPaymentsQuery
            .Select(payment => new { payment.Status, payment.PaymentMethodId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var signals = new List<FraudSignal>();

        var totalAttempts = priorPayments.Count;
        if (totalAttempts > options.MaxPaymentsInWindow)
        {
            var weight = Math.Min(
                (totalAttempts - options.MaxPaymentsInWindow) * options.PaymentFrequencyWeight,
                options.MaxSignalScore);
            signals.Add(new FraudSignal(
                FrequencySignalType,
                weight,
                JsonSerializer.Serialize(new
                {
                    attempts = totalAttempts,
                    threshold = options.MaxPaymentsInWindow,
                    windowMinutes = options.VelocityWindowMinutes
                })));
        }

        var failedAttempts = priorPayments.Count(payment => payment.Status == PaymentStatus.Failed);
        if (failedAttempts > options.MaxFailedAttemptsInWindow)
        {
            var weight = Math.Min(
                (failedAttempts - options.MaxFailedAttemptsInWindow) * options.FailedAttemptWeight,
                options.MaxSignalScore);
            signals.Add(new FraudSignal(
                FailedAttemptsSignalType,
                weight,
                JsonSerializer.Serialize(new
                {
                    failedAttempts,
                    threshold = options.MaxFailedAttemptsInWindow,
                    windowMinutes = options.VelocityWindowMinutes
                })));
        }

        if (signalContext.PaymentMethodId is not null
            && priorPayments.All(payment => payment.PaymentMethodId != signalContext.PaymentMethodId))
        {
            signals.Add(new FraudSignal(
                NewPaymentMethodSignalType,
                options.NewPaymentMethodWeight,
                JsonSerializer.Serialize(new { firstSeenPaymentMethod = true })));
        }

        var disputedPayments = priorPayments.Count(payment => payment.Status == PaymentStatus.Disputed);
        if (disputedPayments > 0)
        {
            var weight = Math.Min(disputedPayments * options.DisputedPaymentWeight, options.MaxSignalScore);
            signals.Add(new FraudSignal(
                DisputedPaymentsSignalType,
                weight,
                JsonSerializer.Serialize(new
                {
                    disputedPayments,
                    windowMinutes = options.VelocityWindowMinutes
                })));
        }

        logger.LogDebug(
            "Fraud velocity evaluation for tenant {TenantId} payment {PaymentId}: {SignalCount} signal(s) from {AttemptCount} prior attempt(s)",
            signalContext.TenantId,
            signalContext.PaymentId,
            signals.Count,
            totalAttempts);

        return signals;
    }
}
