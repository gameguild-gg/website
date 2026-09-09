using GameGuild;
using GameGuild.API.Controllers;

[assembly: UseCaseEventContract(typeof(AssignFinancialCrimeCaseEndpointCommand), "economy.compliance.financial-crime.assign", NoDomainEventReason = "Financial-crime assignment is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(DecideFinancialCrimeCaseEndpointCommand), "economy.compliance.financial-crime.decide", NoDomainEventReason = "Financial-crime decisions are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RecordRegulatoryReferenceEndpointCommand), "economy.compliance.financial-crime.regulatory-reference.record", NoDomainEventReason = "Regulatory-reference recording is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(AssignTrustSafetyAppealEndpointCommand), "economy.compliance.trust-safety.assign", NoDomainEventReason = "Trust-safety assignment is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(DecideTrustSafetyAppealEndpointCommand), "economy.compliance.trust-safety.decide", NoDomainEventReason = "Trust-safety decisions are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ProposeComplianceHoldReleaseEndpointCommand), "economy.compliance.holds.release.propose", NoDomainEventReason = "Compliance-hold release proposals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ApproveComplianceHoldReleaseEndpointCommand), "economy.compliance.holds.release.approve", NoDomainEventReason = "Compliance-hold release approvals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ApproveEconomyRiskReviewEndpointCommand), "economy.risk-reviews.approve", NoDomainEventReason = "Risk-review approval is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RejectEconomyRiskReviewEndpointCommand), "economy.risk-reviews.reject", NoDomainEventReason = "Risk-review rejection is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(StartKycOnboardingEndpointCommand), "economy.kyc.onboarding.start", NoDomainEventReason = "KYC onboarding is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(CreateKycAccessTokenEndpointCommand), "economy.kyc.access-token.create", NoDomainEventReason = "KYC access-token creation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(IngestSumSubWebhookEndpointCommand), "economy.kyc.sumsub-webhook.ingest", NoDomainEventReason = "KYC webhook ingestion is observed through the durable generic operation event.")]
