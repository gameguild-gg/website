using System.ComponentModel.DataAnnotations;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Configuration settings for Billing module.
///     Contains all payment provider settings and shared configuration logic.
///     Implements IValidatableObject for proper ASP.NET Core options validation.
/// </summary>
public class BillingConfiguration : IValidatableObject
{
    /// <summary>
    ///     Configuration section name
    /// </summary>
    public const string SectionName = "Billing";

    /// <summary>
    ///     Stripe configuration settings
    /// </summary>
    public StripeSettings Stripe { get; set; } = new StripeSettings();

    /// <summary>
    ///     PayPal configuration settings
    /// </summary>
    public PayPalSettings PayPal { get; set; } = new PayPalSettings();

    /// <summary>
    ///     Apple Pay configuration settings
    /// </summary>
    public ApplePaySettings ApplePay { get; set; } = new ApplePaySettings();

    /// <summary>
    ///     Google Pay webhook verification settings
    /// </summary>
    public GooglePaySettings GooglePay { get; set; } = new GooglePaySettings();

    /// <summary>
    ///     Webhook configuration settings
    /// </summary>
    public WebhookSettings Webhook { get; set; } = new WebhookSettings();

    /// <summary>
    ///     Gets the list of enabled payment providers based on configuration.
    /// </summary>
    public IEnumerable<string> GetEnabledProviders()
    {
        if (!string.IsNullOrEmpty(Stripe.SecretKey))
        {
            yield return PaymentProviders.Stripe;
        }

        if (!string.IsNullOrEmpty(PayPal.ClientId))
        {
            yield return PaymentProviders.PayPal;
        }

        if (!string.IsNullOrEmpty(ApplePay.BundleId))
        {
            yield return PaymentProviders.AppleAppStore;
        }
    }

    /// <summary>
    ///     Checks if a specific payment provider is configured.
    /// </summary>
    public bool IsProviderEnabled(string provider)
    {
        return provider switch
        {
            PaymentProviders.Stripe => !string.IsNullOrEmpty(Stripe.SecretKey),
            PaymentProviders.PayPal => !string.IsNullOrEmpty(PayPal.ClientId),
            PaymentProviders.AppleAppStore => !string.IsNullOrEmpty(ApplePay.BundleId),
            _ => false
        };
    }

    /// <summary>
    ///     Gets the webhook secret for a specific provider.
    /// </summary>
    public string? GetWebhookSecret(string provider)
    {
        return provider switch
        {
            PaymentProviders.Stripe => Stripe.WebhookSecret,
            PaymentProviders.PayPal => PayPal.WebhookId, // PayPal uses webhook ID for verification
            _ => null
        };
    }

    /// <summary>
    ///     Implements IValidatableObject for ASP.NET Core Options validation.
    ///     Called automatically during options binding when using ValidateDataAnnotations().
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Stripe validation: PublishableKey required when SecretKey is set
        if (!string.IsNullOrEmpty(Stripe.SecretKey) && string.IsNullOrEmpty(Stripe.PublishableKey))
        {
            yield return new ValidationResult(
                "PublishableKey is required when SecretKey is set",
                new[] { $"{nameof(Stripe)}.{nameof(Stripe.PublishableKey)}" });
        }

        if (!string.IsNullOrWhiteSpace(Stripe.WebhookSecret))
        {
            if (string.IsNullOrWhiteSpace(Stripe.WebhookEndpointId))
            {
                yield return new ValidationResult(
                    "WebhookEndpointId is required when Stripe webhook verification is enabled",
                    new[] { $"{nameof(Stripe)}.{nameof(Stripe.WebhookEndpointId)}" });
            }

            if (string.IsNullOrWhiteSpace(Stripe.ApiVersion))
            {
                yield return new ValidationResult(
                    "ApiVersion is required when Stripe webhook verification is enabled",
                    new[] { $"{nameof(Stripe)}.{nameof(Stripe.ApiVersion)}" });
            }
        }

        if (Stripe.WebhookToleranceSeconds is < 1 or > 900)
        {
            yield return new ValidationResult(
                "WebhookToleranceSeconds must be between 1 and 900 seconds",
                new[] { $"{nameof(Stripe)}.{nameof(Stripe.WebhookToleranceSeconds)}" });
        }

        // PayPal validation: ClientSecret required when ClientId is set
        if (!string.IsNullOrEmpty(PayPal.ClientId) && string.IsNullOrEmpty(PayPal.ClientSecret))
        {
            yield return new ValidationResult(
                "ClientSecret is required when ClientId is set",
                new[] { $"{nameof(PayPal)}.{nameof(PayPal.ClientSecret)}" });
        }

        // ApplePay validation: SharedSecret required when BundleId is set
        if (!string.IsNullOrEmpty(ApplePay.BundleId) && string.IsNullOrEmpty(ApplePay.SharedSecret))
        {
            yield return new ValidationResult(
                "SharedSecret is required when BundleId is set",
                new[] { $"{nameof(ApplePay)}.{nameof(ApplePay.SharedSecret)}" });
        }

        // Webhook settings validation
        if (Webhook.MaxRetryAttempts < 0)
        {
            yield return new ValidationResult(
                "MaxRetryAttempts cannot be negative",
                new[] { $"{nameof(Webhook)}.{nameof(Webhook.MaxRetryAttempts)}" });
        }

        if (Webhook.ProcessingTimeoutSeconds < 1)
        {
            yield return new ValidationResult(
                "ProcessingTimeoutSeconds must be at least 1 second",
                new[] { $"{nameof(Webhook)}.{nameof(Webhook.ProcessingTimeoutSeconds)}" });
        }

        if (Webhook.RetryPolicy.InitialDelaySeconds < 1)
        {
            yield return new ValidationResult(
                "RetryPolicy.InitialDelaySeconds must be at least 1 second",
                new[] { $"{nameof(Webhook)}.{nameof(Webhook.RetryPolicy)}.{nameof(Webhook.RetryPolicy.InitialDelaySeconds)}" });
        }

        if (Webhook.RetryPolicy.BackoffMultiplier < 1.0)
        {
            yield return new ValidationResult(
                "RetryPolicy.BackoffMultiplier must be at least 1.0",
                new[] { $"{nameof(Webhook)}.{nameof(Webhook.RetryPolicy)}.{nameof(Webhook.RetryPolicy.BackoffMultiplier)}" });
        }

        // Webhook source security: allowlist entries must be valid CIDR networks.
        foreach (var entry in Webhook.Security.SourceIpAllowlist)
        {
            if (!WebhookIpNetwork.TryParse(entry, out _))
            {
                yield return new ValidationResult(
                    $"SourceIpAllowlist entry '{entry}' is not a valid IPv4/IPv6 CIDR network",
                    new[] { $"{nameof(Webhook)}.{nameof(Webhook.Security)}.{nameof(Webhook.Security.SourceIpAllowlist)}" });
            }
        }

        // Webhook source security: suspicious-activity thresholds must be positive when enabled.
        if (Webhook.Security.SuspiciousActivity.Enabled)
        {
            if (Webhook.Security.SuspiciousActivity.FailureThreshold < 1)
            {
                yield return new ValidationResult(
                    "SuspiciousActivity.FailureThreshold must be at least 1",
                    new[] { $"{nameof(Webhook)}.{nameof(Webhook.Security)}.SuspiciousActivity.FailureThreshold" });
            }

            if (Webhook.Security.SuspiciousActivity.WindowSeconds < 1)
            {
                yield return new ValidationResult(
                    "SuspiciousActivity.WindowSeconds must be at least 1 second",
                    new[] { $"{nameof(Webhook)}.{nameof(Webhook.Security)}.SuspiciousActivity.WindowSeconds" });
            }

            if (Webhook.Security.SuspiciousActivity.BlockDurationSeconds < 1)
            {
                yield return new ValidationResult(
                    "SuspiciousActivity.BlockDurationSeconds must be at least 1 second",
                    new[] { $"{nameof(Webhook)}.{nameof(Webhook.Security)}.SuspiciousActivity.BlockDurationSeconds" });
            }
        }

        // Google Pay verification: configured keys must be importable RSA public keys.
        foreach (var key in GooglePay.VerificationKeys)
        {
            if (!GooglePayWebhookVerificationService.IsValidVerificationKey(key))
            {
                yield return new ValidationResult(
                    "GooglePay.VerificationKeys contains an entry that is not a valid PEM or base64 SPKI RSA public key",
                    new[] { $"{nameof(GooglePay)}.{nameof(GooglePay.VerificationKeys)}" });
            }
        }

        if (GooglePay.TimestampToleranceSeconds is < 1 or > 900)
        {
            yield return new ValidationResult(
                "GooglePay.TimestampToleranceSeconds must be between 1 and 900 seconds",
                new[] { $"{nameof(GooglePay)}.{nameof(GooglePay.TimestampToleranceSeconds)}" });
        }
    }

    /// <summary>
    ///     Validates that required configuration is present for the specified provider.
    ///     Use for runtime validation of provider-specific configuration.
    /// </summary>
    public BillingConfigurationValidationResult ValidateProvider(string? provider = null)
    {
        var errors = new List<string>();

        if (provider == null || provider == PaymentProviders.Stripe)
        {
            if (!string.IsNullOrEmpty(Stripe.SecretKey) && string.IsNullOrEmpty(Stripe.PublishableKey))
            {
                errors.Add("Stripe: PublishableKey is required when SecretKey is set");
            }
        }

        if (provider == null || provider == PaymentProviders.PayPal)
        {
            if (!string.IsNullOrEmpty(PayPal.ClientId) && string.IsNullOrEmpty(PayPal.ClientSecret))
            {
                errors.Add("PayPal: ClientSecret is required when ClientId is set");
            }
        }

        if (provider == null || provider == PaymentProviders.AppleAppStore)
        {
            if (!string.IsNullOrEmpty(ApplePay.BundleId) && string.IsNullOrEmpty(ApplePay.SharedSecret))
            {
                errors.Add("ApplePay: SharedSecret is required when BundleId is set");
            }
        }

        return new BillingConfigurationValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
///     Result of billing configuration validation.
/// </summary>
public sealed record BillingConfigurationValidationResult(bool IsValid, IReadOnlyList<string> Errors);
