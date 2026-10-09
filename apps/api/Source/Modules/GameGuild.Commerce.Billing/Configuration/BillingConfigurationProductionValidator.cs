using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>Validates Stripe webhook/provider configuration on startup, logging warnings if unconfigured outside local environments.</summary>
public sealed class BillingConfigurationProductionValidator(
    IHostEnvironment environment,
    ILogger<BillingConfigurationProductionValidator>? logger = null)
    : IValidateOptions<BillingConfiguration>
{
    public ValidateOptionsResult Validate(string? name, BillingConfiguration options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!RequiresProviderConfiguration(environment.EnvironmentName))
        {
            return ValidateOptionsResult.Success;
        }

        var warnings = new List<string>();
        var stripe = options.Stripe;
        if (string.IsNullOrWhiteSpace(stripe.WebhookSecret))
        {
            warnings.Add($"Stripe.{nameof(StripeSettings.WebhookSecret)} is not set.");
        }

        if (string.IsNullOrWhiteSpace(stripe.WebhookEndpointId))
        {
            warnings.Add($"Stripe.{nameof(StripeSettings.WebhookEndpointId)} is not set.");
        }

        if (string.IsNullOrWhiteSpace(stripe.AccountId))
        {
            warnings.Add($"Stripe.{nameof(StripeSettings.AccountId)} is not set.");
        }

        if (string.IsNullOrWhiteSpace(stripe.ApiVersion))
        {
            warnings.Add($"Stripe.{nameof(StripeSettings.ApiVersion)} is not set.");
        }

        if (!options.Webhook.VerifySignatures)
        {
            warnings.Add($"Webhook.{nameof(WebhookSettings.VerifySignatures)} signature verification is disabled.");
        }

        if (string.Equals(environment.EnvironmentName, Environments.Production, StringComparison.OrdinalIgnoreCase) && !stripe.LiveMode)
        {
            warnings.Add($"Stripe.{nameof(StripeSettings.LiveMode)} is disabled in Production.");
        }

        if (warnings.Count != 0)
        {
            logger?.LogWarning("Stripe Billing configuration warnings: {Warnings}", string.Join("; ", warnings));
        }

        // Always return Success so missing/pending Stripe billing keys do not block application startup
        return ValidateOptionsResult.Success;
    }

    private static bool RequiresProviderConfiguration(string environmentName) =>
        !string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
}
