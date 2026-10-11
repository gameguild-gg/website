namespace GameGuild.Commerce.Billing;

/// <summary>
///     Read-only management and health snapshot of one configured external
///     billing provider (issue #397 provider-management API).
/// </summary>
public sealed record ExternalBillingProviderStatusDto
{
    /// <summary>
    ///     Provider key from <see cref="PaymentProviders"/> (stripe, paypal, ...).
    /// </summary>
    public required string ProviderKey { get; init; }

    /// <summary>
    ///     Whether the provider has its API credentials configured.
    /// </summary>
    public required bool Configured { get; init; }

    /// <summary>
    ///     Whether the provider's configuration passes validation
    ///     (<see cref="BillingConfiguration.ValidateProvider"/> plus provider-specific
    ///     credential completeness). Validation errors are surfaced read-only in
    ///     <see cref="ConfigurationErrors"/>.
    /// </summary>
    public required bool ConfigValid { get; init; }

    /// <summary>
    ///     Whether the provider's webhook verification material is configured so the
    ///     callback endpoint can authenticate provider events (signing secret / webhook
    ///     id / shared secret / verification keys).
    /// </summary>
    public required bool WebhookEndpointConfigured { get; init; }

    /// <summary>
    ///     Whether the provider is enabled at runtime. Defaults to true; an explicit
    ///     administrator disable persists a management state row.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    ///     Derived health summary: "healthy" (configured, valid config, webhook endpoint
    ///     ready), "degraded" (configured but incomplete — webhook verification or config
    ///     validation issues) or "not-configured" (no credentials).
    /// </summary>
    public required string Health { get; init; }

    /// <summary>
    ///     Provider configuration validation errors (read-only, never contains secrets).
    /// </summary>
    public IReadOnlyList<string> ConfigurationErrors { get; init; } = [];

    /// <summary>
    ///     When the enabled state was last changed by an administrator (null when the
    ///     provider keeps its default enabled disposition).
    /// </summary>
    public DateTimeOffset? EnabledStateChangedAt { get; init; }
}
