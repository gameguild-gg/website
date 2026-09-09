using GameGuild;
using GameGuild.API.Projects;

[assembly: UseCaseEventContract(typeof(AddProjectTeamOwnershipEndpointCommand), "projects.ownership.teams.add", NoDomainEventReason = "Project-team ownership changes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UpdateProjectTeamOwnershipEndpointCommand), "projects.ownership.teams.update", NoDomainEventReason = "Project-team ownership changes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RemoveProjectTeamOwnershipEndpointCommand), "projects.ownership.teams.remove", NoDomainEventReason = "Project-team ownership changes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(TransferProjectOwnerTeamEndpointCommand), "projects.ownership.owner-team.transfer", NoDomainEventReason = "Owner-team transfers are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(CreateProjectAllocationEndpointCommand), "projects.ownership.allocations.create", NoDomainEventReason = "Project allocations are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UpdateProjectAllocationEndpointCommand), "projects.ownership.allocations.update", NoDomainEventReason = "Project allocations are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RemoveProjectAllocationEndpointCommand), "projects.ownership.allocations.remove", NoDomainEventReason = "Project allocations are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(CreateProjectTeamAgreementEndpointCommand), "projects.ownership.agreements.create", NoDomainEventReason = "Project-team agreements are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(CounterProjectTeamAgreementEndpointCommand), "projects.ownership.agreements.counter", NoDomainEventReason = "Project-team agreements are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(AcceptProjectTeamAgreementEndpointCommand), "projects.ownership.agreements.accept", NoDomainEventReason = "Project-team agreements are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(CancelProjectTeamAgreementEndpointCommand), "projects.ownership.agreements.cancel", NoDomainEventReason = "Project-team agreements are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(CompleteProjectTeamAgreementEndpointCommand), "projects.ownership.agreements.complete", NoDomainEventReason = "Project-team agreements are observed through the durable generic operation event.")]
