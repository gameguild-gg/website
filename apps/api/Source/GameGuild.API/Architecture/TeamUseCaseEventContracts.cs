using GameGuild;
using GameGuild.API.Teams;

[assembly: UseCaseEventContract(typeof(CreateTeamEndpointCommand), "teams.create", NoDomainEventReason = "Team creation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UpdateTeamEndpointCommand), "teams.update", NoDomainEventReason = "Team updates are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ArchiveTeamEndpointCommand), "teams.archive", NoDomainEventReason = "Team archival is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RestoreTeamEndpointCommand), "teams.restore", NoDomainEventReason = "Team restoration is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(AddTeamMemberEndpointCommand), "teams.members.add", NoDomainEventReason = "Team membership creation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(ChangeTeamMemberEndpointCommand), "teams.members.update", NoDomainEventReason = "Team membership updates are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RemoveTeamMemberEndpointCommand), "teams.members.remove", NoDomainEventReason = "Team membership removal is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(CreateTeamInvitationEndpointCommand), "teams.invitations.create", NoDomainEventReason = "Team invitations are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RevokeTeamInvitationEndpointCommand), "teams.invitations.revoke", NoDomainEventReason = "Team invitation revocation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(AcceptTeamInvitationEndpointCommand), "teams.invitations.accept", NoDomainEventReason = "Team invitation acceptance is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(AcceptAuthenticatedTeamInvitationEndpointCommand), "teams.invitations.accept-authenticated", NoDomainEventReason = "Authenticated team invitation acceptance is observed through the durable generic operation event.")]
