using GameGuild;
using GameGuild.API.Controllers;

[assembly: UseCaseEventContract(typeof(ProposeEconomyPolicyEndpointCommand), "economy.control-plane.policies.propose", NoDomainEventReason = "Policy proposals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ApproveEconomyPolicyEndpointCommand), "economy.control-plane.policies.approve", NoDomainEventReason = "Policy approvals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ActivateEconomyKillSwitchEndpointCommand), "economy.control-plane.kill-switches.activate", NoDomainEventReason = "Kill-switch activation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ProposeEconomyKillSwitchReleaseEndpointCommand), "economy.control-plane.kill-switches.release.propose", NoDomainEventReason = "Kill-switch release proposals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ApproveEconomyKillSwitchReleaseEndpointCommand), "economy.control-plane.kill-switches.release.approve", NoDomainEventReason = "Kill-switch release approvals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ReleaseEconomyKillSwitchEndpointCommand), "economy.control-plane.kill-switches.release", NoDomainEventReason = "Kill-switch releases are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(VerifyEconomyJournalEndpointCommand), "economy.control-plane.ledger.verify", NoDomainEventReason = "Ledger verification runs are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(PublishEconomyAnchorEndpointCommand), "economy.control-plane.ledger.anchors.publish", NoDomainEventReason = "Anchor publication is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(VerifyEconomyAnchorsEndpointCommand), "economy.control-plane.ledger.anchors.verify", NoDomainEventReason = "Anchor verification runs are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RebuildEconomyProjectionsEndpointCommand), "economy.control-plane.projections.rebuild", NoDomainEventReason = "Projection rebuilds are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ApproveEconomyProjectionEndpointCommand), "economy.control-plane.projections.approve", NoDomainEventReason = "Projection approvals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(IngestEconomyCustodyObservationEndpointCommand), "economy.control-plane.custody.ingest", NoDomainEventReason = "Custody observation ingestion is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ProposeEconomyReserveEndpointCommand), "economy.control-plane.reserves.propose", NoDomainEventReason = "Reserve proposals are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ApproveEconomyReserveEndpointCommand), "economy.control-plane.reserves.approve", NoDomainEventReason = "Reserve approvals are observed through the durable generic operation event.")]
