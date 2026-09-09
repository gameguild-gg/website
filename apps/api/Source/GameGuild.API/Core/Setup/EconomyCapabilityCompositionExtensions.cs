using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Payments;
using GameGuild.Compliance.FinancialCrime;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TrustSafety;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Setup;

public static class EconomyCapabilityCompositionExtensions
{
    public static IServiceCollection AddEconomyCapabilityComposition(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(configuration);
        services.AddEconomyRiskComposition(configuration);
        services.AddFinancialCrimeComposition();
        services.AddTrustSafetyComposition();
        services.TryAddSingleton<IEconomyProviderCapabilityReadiness, EconomyProviderCapabilityReadiness>();
        return services;
    }
}

public enum EconomyCapabilityReadinessState
{
    Disabled = 1,
    Ready = 2,
    ProviderNotReady = 3,
    InvalidConfiguration = 4
}

public sealed record EconomyCapabilityReadinessResult(
    EconomyValueMovementCapability Capability,
    EconomyCapabilityReadinessState State,
    IReadOnlyList<string> Diagnostics)
{
    public bool IsReady => State == EconomyCapabilityReadinessState.Ready;
}

/// <summary>
/// Evaluates a value-moving capability at operation time. It intentionally does not run during
/// application startup: optional providers must not prevent safe reads or unrelated modules from running.
/// </summary>
public interface IEconomyProviderCapabilityReadiness
{
    EconomyCapabilityReadinessResult Assess(EconomyValueMovementCapability capability);

    void EnsureReady(EconomyValueMovementCapability capability);
}

public sealed class EconomyProviderCapabilityReadiness(
    IOptions<EconomyRiskCompositionOptions> economyOptions,
    IOptions<StripeGatewayOptions> gatewayOptions,
    IOptions<BillingConfiguration> billingOptions,
    IHostEnvironment hostEnvironment,
    IConfiguration configuration) : IEconomyProviderCapabilityReadiness
{
    public EconomyCapabilityReadinessResult Assess(EconomyValueMovementCapability capability)
    {
        ArgumentNullException.ThrowIfNull(economyOptions);
        ArgumentNullException.ThrowIfNull(gatewayOptions);
        ArgumentNullException.ThrowIfNull(billingOptions);
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        EconomyRiskCompositionOptions economy;
        IReadOnlySet<EconomyValueMovementCapability> enabledCapabilities;
        try
        {
            economy = economyOptions.Value;
            EconomyValueMovementCapabilities.Validate(economy);
            enabledCapabilities = EconomyValueMovementCapabilities.Parse(economy.EnabledCapabilities);
        }
        catch (Exception exception) when (exception is EconomyCapabilityConfigurationException or OptionsValidationException)
        {
            return new EconomyCapabilityReadinessResult(
                capability,
                EconomyCapabilityReadinessState.InvalidConfiguration,
                [exception.Message]);
        }

        if (!economy.ValueMovingDecisionsEnabled || !enabledCapabilities.Contains(capability))
            return new EconomyCapabilityReadinessResult(
                capability,
                EconomyCapabilityReadinessState.Disabled,
                ["The capability is not enabled for rollout."]);

        if (!EconomyValueMovementCapabilities.HasAllowedJurisdiction(economy.AllowedJurisdictions))
            return new EconomyCapabilityReadinessResult(
                capability,
                EconomyCapabilityReadinessState.Disabled,
                ["The global Economy jurisdiction allowlist is empty."]);

        // Payout status reads are always available, but payout writes are composed only when the
        // durable reservation and settlement workflows have been explicitly enabled.
        if (capability == EconomyValueMovementCapability.PayoutExecution &&
            !configuration.GetValue<bool>("Modules:Economy.Payouts:WriteWorkflowEnabled"))
            return new EconomyCapabilityReadinessResult(
                capability,
                EconomyCapabilityReadinessState.Disabled,
                ["The durable payout write workflow is not enabled for rollout."]);

        if (!EconomyProviderCapabilityGuard.IsProviderBacked(capability))
            return new EconomyCapabilityReadinessResult(
                capability,
                EconomyCapabilityReadinessState.Ready,
                []);

        var failures = EconomyProviderCapabilityGuard.EvaluateProviderConfiguration(
            gatewayOptions.Value,
            billingOptions.Value,
            hostEnvironment.EnvironmentName);
        return failures.Count == 0
            ? new EconomyCapabilityReadinessResult(capability, EconomyCapabilityReadinessState.Ready, [])
            : new EconomyCapabilityReadinessResult(
                capability,
                EconomyCapabilityReadinessState.ProviderNotReady,
                failures);
    }

    public void EnsureReady(EconomyValueMovementCapability capability)
    {
        var readiness = Assess(capability);
        if (readiness.IsReady)
            return;

        var message = string.Join(" ", readiness.Diagnostics);
        if (readiness.State == EconomyCapabilityReadinessState.Disabled)
            throw new EconomyValueMovementDisabledException(message);
        throw new EconomyProviderConfigurationException(message);
    }
}

public static class EconomyProviderCapabilityGuard
{
    private static readonly EconomyValueMovementCapability[] ProviderBackedCapabilities =
    [
        EconomyValueMovementCapability.ConfirmHardCoinFunding,
        EconomyValueMovementCapability.ReverseProviderFunding,
        EconomyValueMovementCapability.PayoutExecution,
        EconomyValueMovementCapability.AdminWithdrawalExecution
    ];

    public static void ThrowIfInvalid(
        EconomyRiskCompositionOptions economy,
        StripeGatewayOptions gateway,
        BillingConfiguration billing,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(billing);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        EconomyValueMovementCapabilities.Validate(economy);
        var enabledCapabilities = EconomyValueMovementCapabilities.Parse(economy.EnabledCapabilities);
        if (!economy.ValueMovingDecisionsEnabled ||
            !ProviderBackedCapabilities.Any(enabledCapabilities.Contains))
            return;

        var failures = EvaluateProviderConfiguration(gateway, billing, environmentName);
        if (failures.Count != 0)
            throw new EconomyProviderConfigurationException(string.Join(" ", failures));
    }

    public static bool IsProviderBacked(EconomyValueMovementCapability capability) =>
        ProviderBackedCapabilities.Contains(capability);

    public static IReadOnlyList<string> EvaluateProviderConfiguration(
        StripeGatewayOptions gateway,
        BillingConfiguration billing,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(billing);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        var failures = new List<string>();
        if (!gateway.IsEnabled)
            failures.Add("Payments:Stripe:IsEnabled must be true.");
        if (gateway.UseSimulation && !IsDevelopmentOrTest(environmentName))
            failures.Add("Payments:Stripe:UseSimulation must be false outside Development and Test.");
        if (!string.Equals(gateway.AccountId, billing.Stripe.AccountId, StringComparison.Ordinal))
            failures.Add("Payments and Billing must use the same canonical Stripe account.");
        if (gateway.LiveMode != billing.Stripe.LiveMode)
            failures.Add("Payments and Billing must use the same Stripe live/test mode.");
        if (!billing.Webhook.VerifySignatures)
            failures.Add("Billing:Webhook:VerifySignatures must be true.");
        if (!billing.Webhook.StorePayloads)
            failures.Add("Billing:Webhook:StorePayloads must be true for replay-safe ingress.");
        if (string.IsNullOrWhiteSpace(billing.Stripe.WebhookSecret))
            failures.Add("Billing:Stripe:WebhookSecret is required.");
        if (string.IsNullOrWhiteSpace(billing.Stripe.WebhookEndpointId))
            failures.Add("Billing:Stripe:WebhookEndpointId is required.");
        if (string.IsNullOrWhiteSpace(billing.Stripe.ApiVersion))
            failures.Add("Billing:Stripe:ApiVersion is required.");
        if (billing.Stripe.WebhookToleranceSeconds is < 1 or > 900)
            failures.Add("Billing:Stripe:WebhookToleranceSeconds must be between 1 and 900.");
        if (string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase) &&
            (!gateway.LiveMode || !billing.Stripe.LiveMode))
            failures.Add("Stripe live mode is required in Production.");

        return failures;
    }

    private static bool IsDevelopmentOrTest(string environmentName) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
}

public sealed class EconomyProviderConfigurationException(string message) : InvalidOperationException(message);
