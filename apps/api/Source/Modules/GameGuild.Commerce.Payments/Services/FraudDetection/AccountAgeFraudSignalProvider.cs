using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Account-age fraud signal provider (issue #409): flags high-value charges on a
///     brand-new customer relationship. The Payments module has no user-registration
///     data, so the subscription's billing cycle number is the customer-tenure proxy:
///     <c>BillingCycleNumber &lt;= 1</c> is the first-ever charge of the relationship.
/// </summary>
public sealed class AccountAgeFraudSignalProvider(
    ILogger<AccountAgeFraudSignalProvider> logger) : IFraudSignalProvider
{
    private const string SignalType = "AccountAge.NewCustomerHighValue";

    /// <inheritdoc />
    public string Name => "AccountAge";

    /// <inheritdoc />
    public Task<IReadOnlyList<FraudSignal>> EvaluateAsync(
        FraudSignalContext signalContext,
        FraudAssessmentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signalContext);
        ArgumentNullException.ThrowIfNull(options);

        cancellationToken.ThrowIfCancellationRequested();

        var signals = new List<FraudSignal>();

        if (signalContext.BillingCycleNumber <= 1
            && signalContext.Amount >= options.NewCustomerHighValueAmountThreshold)
        {
            signals.Add(new FraudSignal(
                SignalType,
                Math.Min(options.NewCustomerHighValueWeight, options.MaxSignalScore),
                JsonSerializer.Serialize(new
                {
                    billingCycleNumber = signalContext.BillingCycleNumber,
                    amount = signalContext.Amount,
                    highValueThreshold = options.NewCustomerHighValueAmountThreshold,
                    currency = signalContext.Currency
                })));

            logger.LogDebug(
                "Fraud account-age evaluation for tenant {TenantId} payment {PaymentId}: first-cycle high-value charge {Amount} {Currency}",
                signalContext.TenantId,
                signalContext.PaymentId,
                signalContext.Amount,
                signalContext.Currency);
        }

        return Task.FromResult<IReadOnlyList<FraudSignal>>(signals);
    }
}
