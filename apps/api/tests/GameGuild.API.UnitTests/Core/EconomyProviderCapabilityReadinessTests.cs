using GameGuild.API.Setup;
using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Risk;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GameGuild.API.UnitTests.Core;

public sealed class EconomyProviderCapabilityReadinessTests
{
    [Fact]
    public void DisabledCapability_IsReportedAsDisabledWithoutRequiringStripe()
    {
        var readiness = CreateReadiness();

        readiness.Assess(EconomyValueMovementCapability.PayoutExecution)
            .State.Should().Be(EconomyCapabilityReadinessState.Disabled);
    }

    [Fact]
    public void EnabledNonProviderCapability_IsDisabledUntilJurisdictionIsExplicitlyAllowed()
    {
        var readiness = CreateReadiness(
            enabledCapabilities: [nameof(EconomyValueMovementCapability.ConvertHardToSoft)]);

        readiness.Assess(EconomyValueMovementCapability.ConvertHardToSoft)
            .State.Should().Be(EconomyCapabilityReadinessState.Disabled);
    }

    [Fact]
    public void EnabledNonProviderCapability_IsReadyWhenJurisdictionIsExplicitlyAllowed()
    {
        var readiness = CreateReadiness(
            enabledCapabilities: [nameof(EconomyValueMovementCapability.ConvertHardToSoft)],
            allowedJurisdictions: ["BR"]);

        readiness.Assess(EconomyValueMovementCapability.ConvertHardToSoft)
            .State.Should().Be(EconomyCapabilityReadinessState.Ready);
    }

    [Fact]
    public void EnabledPayoutCapability_IsNotReadyWhenStripeIsDisabled()
    {
        var readiness = CreateReadiness(
            enabledCapabilities: [nameof(EconomyValueMovementCapability.PayoutExecution)],
            payoutWriteWorkflowEnabled: true,
            allowedJurisdictions: ["BR"]);

        readiness.Assess(EconomyValueMovementCapability.PayoutExecution)
            .State.Should().Be(EconomyCapabilityReadinessState.ProviderNotReady);
    }

    [Fact]
    public void EnabledPayoutCapability_IsReadyWithReplaySafeStripeConfiguration()
    {
        var readiness = CreateReadiness(
            enabledCapabilities: [nameof(EconomyValueMovementCapability.PayoutExecution)],
            payoutWriteWorkflowEnabled: true,
            allowedJurisdictions: ["BR"],
            gateway: new StripeGatewayOptions
            {
                IsEnabled = true,
                UseSimulation = false,
                AccountId = "acct_platform",
                LiveMode = false
            },
            billing: CreateBilling());

        readiness.Assess(EconomyValueMovementCapability.PayoutExecution)
            .State.Should().Be(EconomyCapabilityReadinessState.Ready);
    }

    [Fact]
    public void EnabledPayoutCapability_IsReportedAsDisabledUntilTheDurableWriteWorkflowIsEnabled()
    {
        var readiness = CreateReadiness(
            enabledCapabilities: [nameof(EconomyValueMovementCapability.PayoutExecution)],
            gateway: new StripeGatewayOptions
            {
                IsEnabled = true,
                UseSimulation = false,
                AccountId = "acct_platform",
                LiveMode = false
            },
            billing: CreateBilling(),
            allowedJurisdictions: ["BR"]);

        var result = readiness.Assess(EconomyValueMovementCapability.PayoutExecution);

        result.State.Should().Be(EconomyCapabilityReadinessState.Disabled);
        result.Diagnostics.Should().ContainSingle()
            .Which.Should().Contain("durable payout write workflow");
    }

    [Fact]
    public void InvalidCapabilityConfiguration_IsReportedWithoutBlockingComposition()
    {
        var values = new Dictionary<string, string?>
        {
            [EconomyRiskCompositionOptions.SectionName + ":ValueMovingDecisionsEnabled"] = "true"
        };
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment("Staging"));
        services.AddEconomyCapabilityComposition(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        using var provider = services.BuildServiceProvider();
        var readiness = provider.GetRequiredService<IEconomyProviderCapabilityReadiness>();

        readiness.Assess(EconomyValueMovementCapability.ConvertHardToSoft)
            .State.Should().Be(EconomyCapabilityReadinessState.InvalidConfiguration);
    }

    private static EconomyProviderCapabilityReadiness CreateReadiness(
        string[]? enabledCapabilities = null,
        bool payoutWriteWorkflowEnabled = false,
        StripeGatewayOptions? gateway = null,
        BillingConfiguration? billing = null,
        string[]? allowedJurisdictions = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Modules:Economy.Payouts:WriteWorkflowEnabled"] = payoutWriteWorkflowEnabled.ToString()
            })
            .Build();

        return new EconomyProviderCapabilityReadiness(
            Options.Create(new EconomyRiskCompositionOptions
            {
                ValueMovingDecisionsEnabled = enabledCapabilities is not null,
                EnabledCapabilities = enabledCapabilities ?? [],
                AllowedJurisdictions = allowedJurisdictions ?? []
            }),
            Options.Create(gateway ?? new StripeGatewayOptions()),
            Options.Create(billing ?? new BillingConfiguration()),
            new TestHostEnvironment("Staging"),
            configuration);
    }

    private static BillingConfiguration CreateBilling() => new()
    {
        Stripe = new StripeSettings
        {
            AccountId = "acct_platform",
            LiveMode = false,
            WebhookSecret = "whsec_economy",
            WebhookEndpointId = "we_economy",
            ApiVersion = "2025-01-27.acacia",
            WebhookToleranceSeconds = 300
        },
        Webhook = new WebhookSettings
        {
            VerifySignatures = true,
            StorePayloads = true
        }
    };

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "GameGuild.API.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
