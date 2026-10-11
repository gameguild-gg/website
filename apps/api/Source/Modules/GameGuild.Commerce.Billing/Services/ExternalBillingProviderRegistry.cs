using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Read-only management and health view over the configured external billing
///     providers (issue #397). Fail-closed: only providers listed in
/// <see cref="PaymentProviders.All"/> are addressable; unknown keys are rejected.
/// </summary>
public interface IExternalBillingProviderRegistry
{
    /// <summary>
    ///     Lists every supported provider with its configuration health and runtime
    ///     enabled state.
    /// </summary>
    Task<IReadOnlyList<ExternalBillingProviderStatusDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the status of a single provider. Returns null for unknown provider keys.
    /// </summary>
    Task<ExternalBillingProviderStatusDto?> GetAsync(string providerKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persists an explicit enable/disable decision for a provider and returns the
    ///     resulting status. Fails closed with <see cref="KeyNotFoundException"/> for
    ///     unknown provider keys (no state row is ever created for them).
    /// </summary>
    Task<ExternalBillingProviderStatusDto> SetEnabledAsync(
        string providerKey,
        bool enabled,
        Guid changedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Default <see cref="IExternalBillingProviderRegistry"/> implementation: derives
///     per-provider health from <see cref="BillingConfiguration"/> and merges the
///     persisted <see cref="BillingProviderState"/> overrides.
/// </summary>
public sealed class ExternalBillingProviderRegistry(
    IBillingProviderStateRepository stateRepository,
    IOptions<BillingConfiguration> billingConfiguration) : IExternalBillingProviderRegistry
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ExternalBillingProviderStatusDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var states = await stateRepository.GetAllStatesAsync(cancellationToken).ConfigureAwait(false);
        var statesByKey = states.ToDictionary(state => state.ProviderKey, StringComparer.Ordinal);

        return PaymentProviders.All
            .Select(providerKey => BuildStatus(providerKey, statesByKey.GetValueOrDefault(providerKey)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ExternalBillingProviderStatusDto?> GetAsync(string providerKey, CancellationToken cancellationToken = default)
    {
        if (!IsKnownProvider(providerKey, out var normalizedKey))
        {
            return null;
        }

        var state = await stateRepository.GetByProviderKeyAsync(normalizedKey, cancellationToken).ConfigureAwait(false);

        return BuildStatus(normalizedKey, state);
    }

    /// <inheritdoc />
    public async Task<ExternalBillingProviderStatusDto> SetEnabledAsync(
        string providerKey,
        bool enabled,
        Guid changedByUserId,
        CancellationToken cancellationToken = default)
    {
        // Fail closed: an unknown provider key must never materialize a state row.
        if (!IsKnownProvider(providerKey, out var normalizedKey))
        {
            throw new KeyNotFoundException($"Unknown external billing provider '{providerKey}'.");
        }

        var state = await stateRepository.GetByProviderKeyAsync(normalizedKey, cancellationToken).ConfigureAwait(false);

        if (state is null)
        {
            state = new BillingProviderState(normalizedKey, enabled, changedByUserId);
            await stateRepository.CreateAsync(state, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            state.SetEnabled(enabled, changedByUserId);
            await stateRepository.UpdateAsync(state, cancellationToken).ConfigureAwait(false);
        }

        return BuildStatus(normalizedKey, state);
    }

    private ExternalBillingProviderStatusDto BuildStatus(string providerKey, BillingProviderState? state)
    {
        var configuration = billingConfiguration.Value;
        var (configured, configValid, webhookEndpointConfigured, configurationErrors) = EvaluateHealth(providerKey, configuration);

        var health = !configured
            ? "not-configured"
            : configValid && webhookEndpointConfigured
                ? "healthy"
                : "degraded";

        return new ExternalBillingProviderStatusDto
        {
            ProviderKey = providerKey,
            Configured = configured,
            ConfigValid = configValid,
            WebhookEndpointConfigured = webhookEndpointConfigured,
            Enabled = state?.IsEnabled ?? true,
            Health = health,
            ConfigurationErrors = configurationErrors,
            EnabledStateChangedAt = state is null ? null : new DateTimeOffset(state.UpdatedAt, TimeSpan.Zero)
        };
    }

    /// <summary>
    ///     Derives the read-only health signals of a provider from the billing
    ///     configuration: credentials configured, configuration valid, and webhook
    ///     endpoint verification material present. Fail-closed: unknown providers are
    ///     reported as not configured.
    /// </summary>
    public static (bool Configured, bool ConfigValid, bool WebhookEndpointConfigured, IReadOnlyList<string> Errors) EvaluateHealth(
        string providerKey,
        BillingConfiguration configuration)
    {
        var errors = new List<string>();

        bool configured;
        bool webhookEndpointConfigured;

        switch (providerKey)
        {
            case PaymentProviders.Stripe:
                configured = !string.IsNullOrEmpty(configuration.Stripe.SecretKey);
                webhookEndpointConfigured =
                    !string.IsNullOrWhiteSpace(configuration.Stripe.WebhookSecret) &&
                    !string.IsNullOrWhiteSpace(configuration.Stripe.WebhookEndpointId);
                if (configured && string.IsNullOrEmpty(configuration.Stripe.PublishableKey))
                {
                    errors.Add("Stripe: PublishableKey is required when SecretKey is set");
                }

                break;

            case PaymentProviders.PayPal:
                configured = !string.IsNullOrEmpty(configuration.PayPal.ClientId);
                webhookEndpointConfigured = !string.IsNullOrWhiteSpace(configuration.PayPal.WebhookId);
                if (configured && string.IsNullOrEmpty(configuration.PayPal.ClientSecret))
                {
                    errors.Add("PayPal: ClientSecret is required when ClientId is set");
                }

                break;

            case PaymentProviders.ApplePay:
            case PaymentProviders.AppleAppStore:
                configured = !string.IsNullOrEmpty(configuration.ApplePay.BundleId);
                webhookEndpointConfigured = !string.IsNullOrWhiteSpace(configuration.ApplePay.SharedSecret);
                if (configured && string.IsNullOrWhiteSpace(configuration.ApplePay.SharedSecret))
                {
                    errors.Add("ApplePay: SharedSecret is required when BundleId is set");
                }

                break;

            case PaymentProviders.GooglePay:
            case PaymentProviders.GooglePlayStore:
                configured = configuration.GooglePay.VerificationKeys.Count > 0;
                webhookEndpointConfigured = configured;
                break;

            default:
                // Fail closed: unknown providers are reported as not configured rather
                // than being silently treated as healthy.
                configured = false;
                webhookEndpointConfigured = false;
                errors.Add($"Provider '{providerKey}' is not a supported external billing provider");
                break;
        }

        return (configured, errors.Count == 0, webhookEndpointConfigured, errors);
    }

    private static bool IsKnownProvider(string providerKey, out string normalizedKey)
    {
        normalizedKey = string.Empty;

        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return false;
        }

        normalizedKey = PaymentProviders.Normalize(providerKey);

        return PaymentProviders.All.Contains(normalizedKey);
    }
}
