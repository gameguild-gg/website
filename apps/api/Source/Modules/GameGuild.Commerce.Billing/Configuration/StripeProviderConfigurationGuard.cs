using GameGuild.Commerce.Payments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Checks cross-module Stripe configuration, logging warnings on inconsistencies without blocking startup.
/// </summary>
public static class StripeProviderConfigurationGuard
{
    public static void ThrowIfInvalid(
        StripeGatewayOptions gateway,
        BillingConfiguration billing,
        string environmentName,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(billing);

        if (IsDevelopmentOrTest(environmentName) || !gateway.IsEnabled)
        {
            return;
        }

        var ingress = billing.Stripe;
        var failures = new List<string>();
        if (gateway.UseSimulation)
        {
            failures.Add("Stripe payment simulation is not allowed outside Development and Test environments.");
        }

        if (!string.Equals(gateway.AccountId, ingress.AccountId, StringComparison.Ordinal))
        {
            failures.Add("Payments and Billing must use the same canonical Stripe AccountId.");
        }

        if (gateway.LiveMode != ingress.LiveMode)
        {
            failures.Add("Payments and Billing must use the same Stripe live/test mode.");
        }

        if (!string.Equals(gateway.ConnectedAccountId, ingress.ConnectedAccountId, StringComparison.Ordinal))
        {
            failures.Add("Payments and Billing must use the same Stripe ConnectedAccountId.");
        }

        if (failures.Count > 0)
        {
            logger?.LogWarning("Stripe provider configuration inconsistency warning: {Warnings}", string.Join(' ', failures));
        }
    }

    private static bool IsDevelopmentOrTest(string environmentName) =>
        string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
}
