using GameGuild.API.Authorization;
using GameGuild.API.Controllers;
using GameGuild.Compliance.FinancialCrime;
using GameGuild.Compliance.KYC;
using GameGuild.Finance.Economy.AdRewards;
using GameGuild.Finance.Economy.Bounties;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Marketplace;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Payouts;
using GameGuild.Finance.Economy.Projections;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Treasury;
using GameGuild.TrustSafety;
using Moq;

namespace GameGuild.API.UnitTests;

internal static class EconomyHandlerSenders
{
    public static DirectHandlerSender Public(
        IDurableAdRewardSessionService? adRewardSessions = null,
        IDurableAdRewardCompletionService? adRewardCompletions = null,
        IDurableAdRewardReportService? adRewardReports = null,
        IDurableBountyApplicationService? bounties = null,
        IDurableMarketplaceSettlementService? marketplaceSettlements = null,
        IDurableMarketplaceRefundService? marketplaceRefunds = null) =>
        new(new EconomyPublicEndpointCommandHandler(
            adRewardSessions ?? Mock.Of<IDurableAdRewardSessionService>(),
            adRewardCompletions ?? Mock.Of<IDurableAdRewardCompletionService>(),
            adRewardReports ?? Mock.Of<IDurableAdRewardReportService>(),
            bounties ?? Mock.Of<IDurableBountyApplicationService>(),
            marketplaceSettlements ?? Mock.Of<IDurableMarketplaceSettlementService>(),
            marketplaceRefunds ?? Mock.Of<IDurableMarketplaceRefundService>()));

    public static DirectHandlerSender Compliance(
        IFinancialCrimeControlPlane? financialCrime = null,
        ITrustSafetyControlPlane? trustSafety = null,
        IComplianceHoldAdministrationStore? holds = null,
        IEconomyStepUpExecutor? stepUp = null,
        IRiskReviewStore? riskReviews = null,
        IKycAmlOrchestrator? kyc = null) =>
        new(new EconomyComplianceEndpointCommandHandler(
            financialCrime ?? Mock.Of<IFinancialCrimeControlPlane>(),
            trustSafety ?? Mock.Of<ITrustSafetyControlPlane>(),
            holds ?? Mock.Of<IComplianceHoldAdministrationStore>(),
            stepUp ?? Mock.Of<IEconomyStepUpExecutor>(),
            riskReviews ?? Mock.Of<IRiskReviewStore>(),
            kyc ?? Mock.Of<IKycAmlOrchestrator>()));

    public static DirectHandlerSender Funds(
        IDurableAdminWithdrawalApplicationService? withdrawals = null,
        ILegacyEconomyShadowMigration? legacyMigration = null,
        IDurablePayoutApplicationService? payouts = null,
        IEconomyStepUpExecutor? stepUp = null,
        TimeProvider? timeProvider = null) =>
        new(new EconomyFundsEndpointCommandHandler(
            withdrawals ?? Mock.Of<IDurableAdminWithdrawalApplicationService>(),
            legacyMigration ?? Mock.Of<ILegacyEconomyShadowMigration>(),
            payouts ?? Mock.Of<IDurablePayoutApplicationService>(),
            stepUp ?? Mock.Of<IEconomyStepUpExecutor>(),
            timeProvider ?? TimeProvider.System));

    public static DirectHandlerSender ControlPlane(
        IEconomyCapabilityPolicyStore? policies = null,
        IEconomyKillSwitchStore? killSwitches = null,
        IJournalIntegrityService? journal = null,
        IEconomyAnchorPublisher? anchors = null,
        IEconomyAnchorVerificationService? anchorVerification = null,
        IEconomyProjectionGenerationService? projections = null,
        IEconomyReserveCustodyControlPlane? reserves = null,
        IEconomyStepUpExecutor? stepUp = null,
        TimeProvider? timeProvider = null) =>
        new(new EconomyControlPlaneEndpointCommandHandler(
            policies ?? Mock.Of<IEconomyCapabilityPolicyStore>(),
            killSwitches ?? Mock.Of<IEconomyKillSwitchStore>(),
            journal ?? Mock.Of<IJournalIntegrityService>(),
            anchors ?? Mock.Of<IEconomyAnchorPublisher>(),
            anchorVerification ?? Mock.Of<IEconomyAnchorVerificationService>(),
            projections ?? Mock.Of<IEconomyProjectionGenerationService>(),
            reserves ?? Mock.Of<IEconomyReserveCustodyControlPlane>(),
            stepUp ?? Mock.Of<IEconomyStepUpExecutor>(),
            timeProvider ?? TimeProvider.System));
}
