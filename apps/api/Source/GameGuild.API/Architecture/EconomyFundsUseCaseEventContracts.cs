using GameGuild;
using GameGuild.API.Controllers;

[assembly: UseCaseEventContract(typeof(ProposeTreasuryWithdrawalEndpointCommand), "economy.treasury.withdrawals.propose", NoDomainEventReason = "Treasury proposals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ApproveTreasuryWithdrawalEndpointCommand), "economy.treasury.withdrawals.approve", NoDomainEventReason = "Treasury approvals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(DispatchTreasuryWithdrawalEndpointCommand), "economy.treasury.withdrawals.dispatch", NoDomainEventReason = "Treasury dispatch is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ReconcileTreasuryWithdrawalEndpointCommand), "economy.treasury.withdrawals.reconcile", NoDomainEventReason = "Treasury reconciliation is observed through the durable generic operation event.")]

[assembly: UseCaseEventContract(typeof(CaptureLegacyEconomyEndpointCommand), "economy.legacy-migration.capture", NoDomainEventReason = "Legacy capture is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(BackfillLegacyEconomyEndpointCommand), "economy.legacy-migration.backfill", NoDomainEventReason = "Legacy backfill is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ReconcileLegacyEconomyEndpointCommand), "economy.legacy-migration.reconcile", NoDomainEventReason = "Legacy reconciliation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ProposeLegacyEconomyCutoverEndpointCommand), "economy.legacy-migration.cutover.propose", NoDomainEventReason = "Legacy cutover proposals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ApproveLegacyEconomyCutoverEndpointCommand), "economy.legacy-migration.cutover.approve", NoDomainEventReason = "Legacy cutover approvals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RollbackLegacyEconomyCutoverEndpointCommand), "economy.legacy-migration.cutover.rollback", NoDomainEventReason = "Legacy cutover rollback is observed through the durable generic operation event.")]

[assembly: UseCaseEventContract(typeof(CreateOrRefreshPayoutOnboardingEndpointCommand), "economy.payouts.onboarding.create-or-refresh", NoDomainEventReason = "Payout onboarding is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ReservePayoutExecutionEndpointCommand), "economy.payouts.execution.reserve", NoDomainEventReason = "Payout reservation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(DispatchPayoutExecutionEndpointCommand), "economy.payouts.execution.dispatch", NoDomainEventReason = "Payout dispatch is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ReconcilePayoutExecutionEndpointCommand), "economy.payouts.execution.reconcile", NoDomainEventReason = "Payout reconciliation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ApplyStripePayoutProviderEventEndpointCommand), "economy.payouts.stripe-connect.ingest", NoDomainEventReason = "Payout provider-event ingestion is observed through the durable generic operation event.")]
