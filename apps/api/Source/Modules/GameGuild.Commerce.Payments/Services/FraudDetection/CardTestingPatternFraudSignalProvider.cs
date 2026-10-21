using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Card-testing pattern fraud signal provider (issue #409): detects the small rapid
///     transaction bursts bots use to probe stolen card numbers. A micro-amount attempt
///     (at or below <see cref="FraudAssessmentOptions.CardTestingAmountThreshold"/>) fires
///     when the same tenant + customer already produced more micro-amount attempts than
///     the configured threshold inside the velocity window.
/// </summary>
/// <remarks>
///     <para>
///         The look-back query mirrors the velocity provider's scoping: tenant + provider
///         customer id (subscription fallback for unbound attempts), excluding the attempt
///         being assessed, excluding cancelled and previously fraud-rejected rows.
///     </para>
/// </remarks>
public sealed class CardTestingPatternFraudSignalProvider(
    IApplicationDbContext context,
    ILogger<CardTestingPatternFraudSignalProvider> logger) : IFraudSignalProvider
{
    private const string SignalType = "Pattern.CardTesting";

    /// <inheritdoc />
    public string Name => "CardTestingPattern";

    /// <inheritdoc />
    public async Task<IReadOnlyList<FraudSignal>> EvaluateAsync(
        FraudSignalContext signalContext,
        FraudAssessmentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signalContext);
        ArgumentNullException.ThrowIfNull(options);

        var signals = new List<FraudSignal>();
        if (signalContext.Amount > options.CardTestingAmountThreshold)
        {
            // Card testing probes with micro-amounts; a normal-sized charge is not part of
            // the pattern on its own.
            return signals;
        }

        var windowStart = SystemClock.UtcNow - options.VelocityWindow;

        var priorAttemptsQuery = context.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.TenantId == signalContext.TenantId
                              && payment.Id != signalContext.PaymentId
                              && payment.CreatedAt >= windowStart
                              && payment.Status != PaymentStatus.Cancelled
                              && payment.Amount <= options.CardTestingAmountThreshold
                              && (payment.FraudDecision == null || payment.FraudDecision == FraudDecision.Allow));

        priorAttemptsQuery = signalContext.ExternalCustomerId is null
            ? priorAttemptsQuery.Where(payment => payment.SubscriptionId == signalContext.SubscriptionId)
            : priorAttemptsQuery.Where(payment => payment.ExternalCustomerId == signalContext.ExternalCustomerId);

        var priorMicroAttempts = await priorAttemptsQuery
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        if (priorMicroAttempts > options.CardTestingPriorSmallAttemptsThreshold)
        {
            signals.Add(new FraudSignal(
                SignalType,
                Math.Min(options.CardTestingWeight, options.MaxSignalScore),
                JsonSerializer.Serialize(new
                {
                    amount = signalContext.Amount,
                    priorMicroAttempts,
                    threshold = options.CardTestingPriorSmallAttemptsThreshold,
                    microAmountCeiling = options.CardTestingAmountThreshold,
                    windowMinutes = options.VelocityWindowMinutes
                })));
        }

        logger.LogDebug(
            "Fraud card-testing evaluation for tenant {TenantId} payment {PaymentId}: {PriorMicroAttempts} prior micro-amount attempt(s)",
            signalContext.TenantId,
            signalContext.PaymentId,
            priorMicroAttempts);

        return signals;
    }
}
