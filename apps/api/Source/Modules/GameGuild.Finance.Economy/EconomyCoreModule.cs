using Amazon.KeyManagementService;
using Amazon.S3;
using GameGuild.Commerce.Billing;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Integrations;
using GameGuild.Finance.Economy.Integrations.AI;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Transfers;
using GameGuild.Finance.Economy.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Finance.Economy;

public sealed class EconomyCoreModule : ModuleBase
{
    public override string Name => "Finance.Economy";
    public override bool EnabledByDefault => true;

    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddEconomyRiskComposition(configuration);
        services.AddScoped<IRiskDecisionAuthorizer, PostgreSqlRiskDecisionAuthorizer>();
        services.AddScoped<IHardToSoftConversionRiskEvidenceVerifier, HardToSoftConversionRiskEvidenceVerifier>();
        services.AddScoped<IHardToSoftConversionRiskDecisionIssuer, PostgreSqlHardToSoftConversionRiskDecisionIssuer>();
        services.AddScoped<CoreProtectedPostingGate>();
        services.AddScoped<IRegisteredPostingGateway, PostgreSqlRegisteredPostingGateway>();
        services.AddScoped<IRegisteredPostingCapabilityResolver, PostgreSqlRegisteredPostingCapabilityResolver>();
        services.AddScoped<IProviderEvidencePostingAuthorityIssuer, PostgreSqlProviderEvidencePostingAuthorityIssuer>();
        services.AddScoped<IEconomyWalletDirectory, PostgreSqlEconomyWalletDirectory>();
        services.AddScoped<IEconomyWalletProvisioner, PostgreSqlEconomyWalletProvisioner>();
        services.AddScoped<ILegacyBalanceBackfillGateway, PostgreSqlLegacyBalanceBackfillGateway>();
        services.AddScoped<IHardCoinFundingGateway, PostgreSqlHardCoinFundingGateway>();
        services.AddScoped<PostgreSqlEconomyTopUpIntentStore>();
        services.AddScoped<IEconomyTopUpIntentStore>(provider =>
            provider.GetRequiredService<PostgreSqlEconomyTopUpIntentStore>());
        services.AddScoped<IEconomyTopUpReader>(provider =>
            provider.GetRequiredService<PostgreSqlEconomyTopUpIntentStore>());
        services.AddScoped<IEconomyTopUpSettlementStore>(provider =>
            provider.GetRequiredService<PostgreSqlEconomyTopUpIntentStore>());
        services.AddScoped<IHardCoinTopUpPolicyResolver, HardCoinTopUpPolicyResolver>();
        services.AddScoped<IEconomyTopUpProvider, StripeEconomyTopUpProvider>();
        services.AddScoped<ISelfServiceHardCoinTopUpService, SelfServiceHardCoinTopUpService>();
        services.AddScoped<IStripeVerifiedEventConsumer, StripeEconomyTopUpEventConsumer>();
        services.AddScoped<IAdRewardIssuanceGateway, PostgreSqlAdRewardIssuanceGateway>();
        services.AddScoped<IHardToSoftConversionGateway, PostgreSqlHardToSoftConversionGateway>();
        services.AddScoped<IHardToSoftConversionPolicyResolver, HardToSoftConversionPolicyResolver>();
        services.AddScoped<IHardToSoftConversionWorkflow, PostgreSqlHardToSoftConversionWorkflow>();
        services.AddScoped<PostgreSqlFifoFragmentReservationGateway>();
        services.AddScoped<IFifoFragmentReservationGateway>(provider =>
            provider.GetRequiredService<PostgreSqlFifoFragmentReservationGateway>());
        services.AddScoped<IFifoFragmentReservationReader>(provider =>
            provider.GetRequiredService<PostgreSqlFifoFragmentReservationGateway>());
        services.AddScoped<PostgreSqlMarketplaceLedgerGateway>();
        services.AddScoped<IMarketplaceFifoReservationGateway>(provider =>
            provider.GetRequiredService<PostgreSqlMarketplaceLedgerGateway>());
        services.AddScoped<IMarketplaceSettlementLedgerGateway>(provider =>
            provider.GetRequiredService<PostgreSqlMarketplaceLedgerGateway>());
        services.AddScoped<IMarketplaceRefundLedgerGateway>(provider =>
            provider.GetRequiredService<PostgreSqlMarketplaceLedgerGateway>());
        services.AddScoped<IProviderReversalGateway, PostgreSqlProviderReversalGateway>();
        services.AddScoped<IFifoTransferGateway, PostgreSqlFifoTransferGateway>();
        services.AddScoped<ISelfServiceEconomyTransferIntentStore, PostgreSqlSelfServiceEconomyTransferIntentStore>();
        services.AddScoped<ISelfServiceEconomyTransferSourceRootPlanner,
            PostgreSqlSelfServiceEconomyTransferSourceRootPlanner>();
        services.AddScoped<ISelfServiceEconomyTransferService, SelfServiceEconomyTransferService>();
        services.AddScoped<IEconomyCapabilityControlPlaneStore, PostgreSqlEconomyCapabilityControlPlaneStore>();
        services.AddScoped<IEconomyCapabilityEvaluator, EconomyCapabilityEvaluator>();
        services.AddScoped<IEconomyCapabilityReadinessInspector, EconomyCapabilityReadinessInspector>();
        services.AddScoped<IEconomyCapabilityAuthorizationService, EconomyCapabilityAuthorizationService>();
        services.AddScoped<IEconomyProtectedOperationTransaction, EconomyProtectedOperationTransaction>();
        services.AddScoped<IEconomyProtectedOperationRiskDecisionIssuer,
            PostgreSqlEconomyProtectedOperationRiskDecisionIssuer>();
        services.AddScoped<IEconomyTrustedProtectedOperationAuthorizer,
            EconomyTrustedProtectedOperationAuthorizer>();
        services.AddScoped<IEconomyProtectedOperationOrchestrator, EconomyProtectedOperationOrchestrator>();
        services.AddScoped<PostgreSqlComplianceEvidenceStore>();
        services.AddScoped<IComplianceEvidenceStore>(provider =>
            provider.GetRequiredService<PostgreSqlComplianceEvidenceStore>());
        services.AddScoped<IComplianceEvidenceReader>(provider =>
            provider.GetRequiredService<PostgreSqlComplianceEvidenceStore>());
        services.AddScoped<IEconomyJurisdictionResolver, EconomyJurisdictionResolver>();
        services.AddScoped<IComplianceHoldStore, PostgreSqlComplianceHoldStore>();
        services.AddScoped<IComplianceHoldReleasePolicyResolver, PostgreSqlComplianceHoldReleasePolicyResolver>();
        services.AddScoped<IComplianceHoldAdministrationStore, PostgreSqlComplianceHoldAdministrationStore>();
        services.AddScoped<IJournalIntegrityVerifier, JournalIntegrityVerifier>();
        services.AddScoped<IJournalIntegrityService, PostgreSqlJournalIntegrityService>();
        services.AddScoped<IEntityRiskGraphStore, PostgreSqlEntityRiskGraphStore>();
        services.AddScoped<IAggregateRiskCounterStore, PostgreSqlAggregateRiskCounterStore>();
        services.AddScoped<IProtectedChangeCooldownStore, PostgreSqlProtectedChangeCooldownStore>();
        services.AddScoped<IRiskReviewStore, PostgreSqlRiskReviewStore>();
        services.AddScoped<IEconomyCapabilityPolicyStore, PostgreSqlEconomyCapabilityPolicyStore>();
        services.AddScoped<IEconomyKillSwitchStore, PostgreSqlEconomyKillSwitchStore>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IKillSwitchReleaseReadinessGate, PostgreSqlKillSwitchReleaseReadinessGate>();
        services.AddScoped<IEconomyAnchorPublisher, PostgreSqlEconomyAnchorPublisher>();
        services.AddScoped<IEconomyAnchorVerificationService, PostgreSqlAnchorVerificationService>();
        services.AddScoped<IEconomyReserveCustodyControlPlane, PostgreSqlReserveCustodyControlPlane>();
        services.AddScoped<IEconomyProjectionGenerationService, PostgreSqlProjectionGenerationService>();
        services.AddScoped<IEconomyOperationsReader, PostgreSqlEconomyOperationsReader>();
        services.AddScoped<IEconomyPolicyQueryReader, PostgreSqlEconomyPolicyQueryReader>();
        services.AddScoped<IEconomyReserveQueryReader, PostgreSqlEconomyReserveQueryReader>();
        services.AddScoped<IEconomyLedgerQueryReader, PostgreSqlEconomyLedgerQueryReader>();
        services.AddScoped<ILegacyEconomyShadowMigration, PostgreSqlLegacyEconomyShadowMigration>();
        services.AddScoped<ILegacyEconomyQueryReader, PostgreSqlLegacyEconomyQueryReader>();
        services.AddOptions<EconomyKmsOptions>().Bind(configuration.GetSection(EconomyKmsOptions.SectionName));
        if (configuration.GetValue<bool>($"{EconomyKmsOptions.SectionName}:Enabled"))
        {
            services.TryAddSingleton<IAmazonKeyManagementService>(_ => new AmazonKeyManagementServiceClient());
            services.TryAddSingleton<AwsKmsEconomyCryptography>();
            services.TryAddSingleton<ICapabilityReceiptSigner>(provider => provider.GetRequiredService<AwsKmsEconomyCryptography>());
            services.TryAddSingleton<ICapabilityPolicySigner>(provider => provider.GetRequiredService<AwsKmsEconomyCryptography>());
            services.TryAddSingleton<ICapabilityPolicySignatureVerifier>(provider => provider.GetRequiredService<AwsKmsEconomyCryptography>());
        }
        else
        {
            services.TryAddSingleton<UnavailableEconomyCapabilityCryptography>();
            services.TryAddSingleton<ICapabilityReceiptSigner>(provider =>
                provider.GetRequiredService<UnavailableEconomyCapabilityCryptography>());
            services.TryAddSingleton<ICapabilityPolicySigner>(provider =>
                provider.GetRequiredService<UnavailableEconomyCapabilityCryptography>());
            services.TryAddSingleton<ICapabilityPolicySignatureVerifier>(provider =>
                provider.GetRequiredService<UnavailableEconomyCapabilityCryptography>());
        }
        services.AddOptions<EconomyWormOptions>().Bind(configuration.GetSection(EconomyWormOptions.SectionName));
        if (configuration.GetValue<bool>($"{EconomyWormOptions.SectionName}:Enabled"))
        {
            services.TryAddSingleton<IAmazonS3>(_ => new AmazonS3Client());
            services.TryAddSingleton<S3ObjectLockWormAnchorStore>();
            services.TryAddSingleton<IWormAnchorStore>(provider =>
                provider.GetRequiredService<S3ObjectLockWormAnchorStore>());
            services.TryAddSingleton<IWormAnchorVerifier>(provider =>
                provider.GetRequiredService<S3ObjectLockWormAnchorStore>());
        }
        else
        {
            services.TryAddSingleton<UnavailableWormAnchorStore>();
            services.TryAddSingleton<IWormAnchorStore>(provider =>
                provider.GetRequiredService<UnavailableWormAnchorStore>());
            services.TryAddSingleton<IWormAnchorVerifier>(provider =>
                provider.GetRequiredService<UnavailableWormAnchorStore>());
        }
        services.AddSingleton<IStripeEconomyFundingAdapter, StripeEconomyFundingAdapter>();
        services.AddScoped<IAiProviderCostFactStore, EfAiProviderCostFactStore>();
        return services;
    }
}

public static class EconomyCoreCompositionExtensions
{
    public static IServiceCollection AddEconomyCoreComposition(
        this IServiceCollection services,
        IConfiguration configuration) => new EconomyCoreModule().ConfigureServices(services, configuration);
}
