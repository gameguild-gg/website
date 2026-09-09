using FluentAssertions;
using Amazon.KeyManagementService;
using Amazon.S3;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.UnitTests;

public sealed class EconomyCoreModuleTests
{
    [Fact]
    public void CoreModuleComposesRuntimeWhileValueCapabilitiesRemainFailClosed()
    {
        var services = new ServiceCollection();
        var module = new EconomyCoreModule();

        module.EnabledByDefault.Should().BeTrue();
        module.Name.Should().Be("Finance.Economy");
        module.ConfigureServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IRegisteredPostingGateway) &&
            descriptor.ImplementationType == typeof(PostgreSqlRegisteredPostingGateway));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHardCoinFundingGateway) &&
            descriptor.ImplementationType == typeof(PostgreSqlHardCoinFundingGateway));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IRiskDecisionAuthorizer) &&
            descriptor.ImplementationType == typeof(PostgreSqlRiskDecisionAuthorizer));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHardToSoftConversionWorkflow) &&
            descriptor.ImplementationType == typeof(PostgreSqlHardToSoftConversionWorkflow));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHardToSoftConversionRiskEvidenceVerifier) &&
            descriptor.ImplementationType == typeof(HardToSoftConversionRiskEvidenceVerifier));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHardToSoftConversionRiskDecisionIssuer) &&
            descriptor.ImplementationType == typeof(PostgreSqlHardToSoftConversionRiskDecisionIssuer));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEconomyCapabilityControlPlaneStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlEconomyCapabilityControlPlaneStore));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEconomyCapabilityEvaluator) &&
            descriptor.ImplementationType == typeof(EconomyCapabilityEvaluator));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEconomyCapabilityReadinessInspector) &&
            descriptor.ImplementationType == typeof(EconomyCapabilityReadinessInspector));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IComplianceEvidenceStore) &&
            descriptor.ImplementationFactory != null);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEntityRiskGraphStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlEntityRiskGraphStore));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAggregateRiskCounterStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlAggregateRiskCounterStore));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IProtectedChangeCooldownStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlProtectedChangeCooldownStore));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IRiskReviewStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlRiskReviewStore));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEconomyProtectedOperationTransaction) &&
            descriptor.ImplementationType == typeof(EconomyProtectedOperationTransaction));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEconomyProtectedOperationRiskDecisionIssuer) &&
            descriptor.ImplementationType == typeof(PostgreSqlEconomyProtectedOperationRiskDecisionIssuer));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEconomyProtectedOperationOrchestrator) &&
            descriptor.ImplementationType == typeof(EconomyProtectedOperationOrchestrator));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEconomyCapabilityPolicyStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlEconomyCapabilityPolicyStore));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEconomyKillSwitchStore) &&
            descriptor.ImplementationType == typeof(PostgreSqlEconomyKillSwitchStore));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IKillSwitchReleaseReadinessGate) &&
            descriptor.ImplementationType == typeof(PostgreSqlKillSwitchReleaseReadinessGate));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IEconomyOperationsReader) &&
            descriptor.ImplementationType == typeof(PostgreSqlEconomyOperationsReader));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IWormAnchorStore) &&
            descriptor.ImplementationFactory != null);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IJournalIntegrityVerifier) &&
            descriptor.ImplementationType == typeof(JournalIntegrityVerifier));
        services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(InMemoryLedgerKernelStore) ||
            descriptor.ImplementationType == typeof(RiskDecisionAuthorizer));
    }

    [Fact]
    public async Task MissingKmsCryptographyRemainsFailClosed()
    {
        var unavailable = new UnavailableEconomyCapabilityCryptography();

        (await unavailable.VerifyAsync("payload", "key", "signature", CancellationToken.None))
            .Should().BeFalse();
        await FluentActions.Awaiting(() => unavailable.SignAsync(
                "payload", CancellationToken.None).AsTask())
            .Should().ThrowAsync<EconomyCryptographyUnavailableException>();
    }

    [Fact]
    public void CompositionExtensionRegistersTheCoreModule()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddEconomyCoreComposition(configuration);

        result.Should().BeSameAs(services);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHardToSoftConversionGateway) &&
            descriptor.ImplementationType == typeof(PostgreSqlHardToSoftConversionGateway));
    }

    [Fact]
    public void CoreModuleRegistersAwsAdaptersOnlyWhenExplicitlyEnabled()
    {
        var disabledServices = new ServiceCollection();
        new EconomyCoreModule().ConfigureServices(disabledServices, new ConfigurationBuilder().Build());
        disabledServices.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(UnavailableEconomyCapabilityCryptography));
        disabledServices.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(UnavailableWormAnchorStore));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{EconomyKmsOptions.SectionName}:Enabled"] = "true",
                [$"{EconomyWormOptions.SectionName}:Enabled"] = "true"
            })
            .Build();
        var services = new ServiceCollection();

        new EconomyCoreModule().ConfigureServices(services, configuration);

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAmazonKeyManagementService) &&
            descriptor.ImplementationFactory != null);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(AwsKmsEconomyCryptography));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(ICapabilityReceiptSigner) &&
            descriptor.ImplementationFactory != null);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(ICapabilityPolicySigner) &&
            descriptor.ImplementationFactory != null);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(ICapabilityPolicySignatureVerifier) &&
            descriptor.ImplementationFactory != null);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAmazonS3) &&
            descriptor.ImplementationFactory != null);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(S3ObjectLockWormAnchorStore));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IWormAnchorStore) &&
            descriptor.ImplementationFactory != null);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IWormAnchorVerifier) &&
            descriptor.ImplementationFactory != null);
        services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(UnavailableEconomyCapabilityCryptography) ||
            descriptor.ServiceType == typeof(UnavailableWormAnchorStore));
    }
    [Fact]
    public void ValueMovementCapabilities_AreExplicitAndFailClosed()
    {
        EconomyValueMovementCapabilities.Parse(null).Should().BeEmpty();
        EconomyValueMovementCapabilities.Parse(["convertHardToSoft"])
            .Should().ContainSingle().Which.Should().Be(EconomyValueMovementCapability.ConvertHardToSoft);

        var disabled = new EconomyValueMovementDecisionGate(Options.Create(new EconomyRiskCompositionOptions
        {
            EnabledCapabilities = ["ConvertHardToSoft"]
        }));
        disabled.IsEnabled.Should().BeFalse();
        disabled.IsCapabilityEnabled(EconomyValueMovementCapability.ConvertHardToSoft).Should().BeFalse();
        Action disabledAction = disabled.EnsureEnabled;
        disabledAction.Should().Throw<EconomyValueMovementDisabledException>();

        var enabled = new EconomyValueMovementDecisionGate(Options.Create(new EconomyRiskCompositionOptions
        {
            ValueMovingDecisionsEnabled = true,
            EnabledCapabilities = ["ConvertHardToSoft"],
            AllowedJurisdictions = ["BR"]
        }));
        enabled.EnsureEnabled();
        enabled.IsCapabilityEnabled(EconomyValueMovementCapability.ConvertHardToSoft).Should().BeTrue();
        enabled.IsCapabilityEnabled(EconomyValueMovementCapability.Transfer).Should().BeFalse();
        Action missingCapability = () => enabled.EnsureEnabled(EconomyValueMovementCapability.Transfer);
        missingCapability.Should().Throw<EconomyValueMovementDisabledException>();

        Action missingOptions = () => EconomyValueMovementCapabilities.Validate(new EconomyRiskCompositionOptions
        {
            ValueMovingDecisionsEnabled = true
        });
        Action unknownCapability = () => EconomyValueMovementCapabilities.Parse(["not-a-capability"]);
        Action nullOptions = () => EconomyValueMovementCapabilities.Validate(null!);

        missingOptions.Should().Throw<EconomyCapabilityConfigurationException>();
        unknownCapability.Should().Throw<EconomyCapabilityConfigurationException>();
        nullOptions.Should().Throw<ArgumentNullException>();
    }
}
