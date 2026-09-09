using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.Risk;

public sealed class EconomyRiskCompositionOptions
{
    public const string SectionName = "Modules:Economy.Risk";

    public bool ValueMovingDecisionsEnabled { get; set; }

    public string[] EnabledCapabilities { get; set; } = [];

    /// <summary>
    /// Legal jurisdictions that may use value-moving Economy capabilities. An empty list is a
    /// deliberate fail-closed rollout state, not a wildcard.
    /// </summary>
    public string[] AllowedJurisdictions { get; set; } = [];
}

public enum EconomyValueMovementCapability
{
    ConfirmHardCoinFunding = 1,
    ConvertHardToSoft = 2,
    ReverseProviderFunding = 3,
    Transfer = 4,
    IssueAdReward = 5,
    BountyEscrow = 6,
    BountyClaim = 7,
    MarketplaceSettlement = 8,
    PayoutExecution = 9,
    AdminWithdrawalExecution = 10,
    MarketplaceRefund = 11,
    BountyReclaim = 12,
    LegacyBalanceBackfill = 13
}

public interface IEconomyValueMovementDecisionGate
{
    bool IsEnabled { get; }

    bool IsCapabilityEnabled(EconomyValueMovementCapability capability);

    void EnsureEnabled();

    void EnsureEnabled(EconomyValueMovementCapability capability);
}

internal sealed class EconomyValueMovementDecisionGate(
    IOptions<EconomyRiskCompositionOptions> options) : IEconomyValueMovementDecisionGate
{
    public bool IsEnabled => options.Value.ValueMovingDecisionsEnabled &&
                             EconomyValueMovementCapabilities.HasAllowedJurisdiction(options.Value.AllowedJurisdictions);

    public bool IsCapabilityEnabled(EconomyValueMovementCapability capability) =>
        IsEnabled && EconomyValueMovementCapabilities.Parse(options.Value.EnabledCapabilities).Contains(capability);

    public void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            if (options.Value.ValueMovingDecisionsEnabled &&
                !EconomyValueMovementCapabilities.HasAllowedJurisdiction(options.Value.AllowedJurisdictions))
            {
                throw new EconomyValueMovementDisabledException(
                    "Economy value-moving decisions are disabled because the global jurisdiction allowlist is empty.");
            }

            throw new EconomyValueMovementDisabledException(
                "Economy value-moving decisions are disabled until an explicit capability rollout is configured.");
        }
    }

    public void EnsureEnabled(EconomyValueMovementCapability capability)
    {
        EnsureEnabled();
        if (!IsCapabilityEnabled(capability))
            throw new EconomyValueMovementDisabledException(
                "Economy capability " + capability + " is disabled until explicitly enabled for rollout.");
    }
}

public static class EconomyValueMovementCapabilities
{
    public static IReadOnlySet<EconomyValueMovementCapability> Parse(IEnumerable<string>? configuredCapabilities)
    {
        var capabilities = new HashSet<EconomyValueMovementCapability>();
        foreach (var configuredCapability in configuredCapabilities ?? [])
        {
            if (!Enum.TryParse<EconomyValueMovementCapability>(configuredCapability, ignoreCase: true, out var capability) ||
                !Enum.IsDefined(capability))
                throw new EconomyCapabilityConfigurationException(
                    "Unknown Economy value-moving capability " + configuredCapability + ".");
            capabilities.Add(capability);
        }

        return capabilities;
    }

    public static void Validate(EconomyRiskCompositionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var enabledCapabilities = Parse(options.EnabledCapabilities);
        if (options.ValueMovingDecisionsEnabled && enabledCapabilities.Count == 0)
            throw new EconomyCapabilityConfigurationException(
                "ValueMovingDecisionsEnabled requires at least one explicitly enabled Economy capability.");
    }

    public static bool HasAllowedJurisdiction(IEnumerable<string>? configuredJurisdictions) =>
        configuredJurisdictions?.Any(value => !string.IsNullOrWhiteSpace(value)) == true;
}

public static class EconomyRiskCompositionExtensions
{
    public static IServiceCollection AddEconomyRiskComposition(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<EconomyRiskCompositionOptions>()
            .Bind(configuration.GetSection(EconomyRiskCompositionOptions.SectionName))
            .Validate(options =>
            {
                try
                {
                    EconomyValueMovementCapabilities.Validate(options);
                    return true;
                }
                catch (EconomyCapabilityConfigurationException)
                {
                    return false;
                }
            }, "Invalid Economy capability configuration.");

        // Economy value movement is opt-in and all operation paths fail closed. Invalid
        // rollout configuration must not make safe reads or unrelated modules unavailable.
        services.TryAddSingleton<IEconomyValueMovementDecisionGate, EconomyValueMovementDecisionGate>();
        return services;
    }
}

public sealed class EconomyValueMovementDisabledException(string message) : InvalidOperationException(message);
public sealed class EconomyCapabilityConfigurationException(string message) : InvalidOperationException(message);
