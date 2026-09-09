using GameGuild;
using GameGuild.API.Controllers;

[assembly: UseCaseEventContract(typeof(StartAdRewardSessionEndpointCommand), "economy.ad-rewards.sessions.start", NoDomainEventReason = "The durable generic operation event records ad-reward session creation.")]
[assembly: UseCaseEventContract(typeof(CompleteAdRewardSessionEndpointCommand), "economy.ad-rewards.sessions.complete", NoDomainEventReason = "The durable generic operation event records ad-reward completion.")]
[assembly: UseCaseEventContract(typeof(ImportAdRewardReportEndpointCommand), "economy.ad-rewards.reports.import", NoDomainEventReason = "The durable generic operation event records provider report import.")]
[assembly: UseCaseEventContract(typeof(CreateBountyEndpointCommand), "economy.bounties.create", NoDomainEventReason = "The durable generic operation event records bounty creation.")]
[assembly: UseCaseEventContract(typeof(ClaimBountyEndpointCommand), "economy.bounties.claim", NoDomainEventReason = "The durable generic operation event records bounty claiming.")]
[assembly: UseCaseEventContract(typeof(ReclaimBountyEndpointCommand), "economy.bounties.reclaim", NoDomainEventReason = "The durable generic operation event records bounty reclaiming.")]
[assembly: UseCaseEventContract(typeof(SettleMarketplaceOrderEndpointCommand), "economy.marketplace.orders.settle", NoDomainEventReason = "The durable generic operation event records marketplace settlement.")]
[assembly: UseCaseEventContract(typeof(RefundMarketplaceOrderEndpointCommand), "economy.marketplace.refunds.self-service", NoDomainEventReason = "The durable generic operation event records self-service marketplace refunds.")]
[assembly: UseCaseEventContract(typeof(RefundMarketplaceOrderAdministrationEndpointCommand), "economy.marketplace.refunds.admin", NoDomainEventReason = "The durable generic operation event records administrative marketplace refunds.")]
