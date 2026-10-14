using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Teams;

namespace GameGuild.API.Teams;

public sealed record CreateTeamEndpointCommand(Team Team) : ICommand;
public sealed record UpdateTeamEndpointCommand(Team Team) : ICommand;
public sealed record ArchiveTeamEndpointCommand(Team Team) : ICommand;
public sealed record RestoreTeamEndpointCommand(Team Team) : ICommand;
public sealed record AddTeamMemberEndpointCommand(TeamMember Member, bool IsNew) : ICommand;
public sealed record ChangeTeamMemberEndpointCommand(TeamMember Member) : ICommand;
public sealed record RemoveTeamMemberEndpointCommand(Team Team) : ICommand;
public sealed record CreateTeamInvitationEndpointCommand(TeamInvitation Invitation) : ICommand;
public sealed record RevokeTeamInvitationEndpointCommand(TeamInvitation Invitation) : ICommand;
public sealed record AcceptTeamInvitationEndpointCommand(Team Team, TeamMember Member, bool IsNewMember) : ICommand;
public sealed record AcceptAuthenticatedTeamInvitationEndpointCommand(Team Team, TeamMember Member, bool IsNewMember) : ICommand;

public sealed class TeamEndpointCommandHandler(
    IApplicationDbContext context,
    ITeamsAuditEventSink auditSink,
    IActorContextAccessor actorContextAccessor) :
    ICommandHandler<CreateTeamEndpointCommand>,
    ICommandHandler<UpdateTeamEndpointCommand>,
    ICommandHandler<ArchiveTeamEndpointCommand>,
    ICommandHandler<RestoreTeamEndpointCommand>,
    ICommandHandler<AddTeamMemberEndpointCommand>,
    ICommandHandler<ChangeTeamMemberEndpointCommand>,
    ICommandHandler<RemoveTeamMemberEndpointCommand>,
    ICommandHandler<CreateTeamInvitationEndpointCommand>,
    ICommandHandler<RevokeTeamInvitationEndpointCommand>,
    ICommandHandler<AcceptTeamInvitationEndpointCommand>,
    ICommandHandler<AcceptAuthenticatedTeamInvitationEndpointCommand>
{
    public Task<Unit> Handle(CreateTeamEndpointCommand request, CancellationToken ct)
    {
        context.Set<Team>().Add(request.Team);
        return SaveWithAuditAsync(
            "Teams.TeamCreated", "Team", request.Team.Id, request.Team.TenantId,
            $"Team '{request.Team.Name}' created",
            new { request.Team.Slug, request.Team.Visibility.ToString(), request.Team.IsPersonal, MemberCount = request.Team.Members.Count }, ct);
    }

    public Task<Unit> Handle(UpdateTeamEndpointCommand request, CancellationToken ct) => SaveWithAuditAsync(
        "Teams.TeamUpdated", "Team", request.Team.Id, request.Team.TenantId,
        $"Team '{request.Team.Name}' updated", new { request.Team.Slug, request.Team.Visibility.ToString() }, ct);

    public Task<Unit> Handle(ArchiveTeamEndpointCommand request, CancellationToken ct) => SaveWithAuditAsync(
        "Teams.TeamArchived", "Team", request.Team.Id, request.Team.TenantId,
        $"Team '{request.Team.Name}' archived", new { request.Team.Status.ToString() }, ct);

    public Task<Unit> Handle(RestoreTeamEndpointCommand request, CancellationToken ct) => SaveWithAuditAsync(
        "Teams.TeamRestored", "Team", request.Team.Id, request.Team.TenantId,
        $"Team '{request.Team.Name}' restored", new { request.Team.Status.ToString() }, ct);

    public Task<Unit> Handle(AddTeamMemberEndpointCommand request, CancellationToken ct)
    {
        if (request.IsNew) context.Set<TeamMember>().Add(request.Member);
        return SaveWithAuditAsync(
            "Teams.TeamMemberAdded", "TeamMember", request.Member.TeamId, request.Member.TenantId,
            $"Member added to team {request.Member.TeamId}",
            new { request.Member.TeamId, request.Member.UserId, Authority = request.Member.Authority.ToString() }, ct);
    }

    public Task<Unit> Handle(ChangeTeamMemberEndpointCommand request, CancellationToken ct) => SaveWithAuditAsync(
        "Teams.TeamMemberChanged", "TeamMember", request.Member.TeamId, request.Member.TenantId,
        $"Member changed on team {request.Member.TeamId}",
        new { request.Member.TeamId, request.Member.UserId, Authority = request.Member.Authority.ToString() }, ct);

    public Task<Unit> Handle(RemoveTeamMemberEndpointCommand request, CancellationToken ct) => SaveWithAuditAsync(
        "Teams.TeamMemberRemoved", "Team", request.Team.Id, request.Team.TenantId,
        $"Member removed from team '{request.Team.Name}'", new { request.Team.Members.Count }, ct);

    public Task<Unit> Handle(CreateTeamInvitationEndpointCommand request, CancellationToken ct)
    {
        context.Set<TeamInvitation>().Add(request.Invitation);
        // Invitation tokens grant membership on redemption and must never reach the audit trail.
        return SaveWithAuditAsync(
            "Teams.TeamInvitationCreated", "TeamInvitation", request.Invitation.Id, request.Invitation.TenantId,
            "Team invitation created",
            new { request.Invitation.TeamId, request.Invitation.InvitedUserId, Authority = request.Invitation.Authority.ToString() }, ct);
    }

    public Task<Unit> Handle(RevokeTeamInvitationEndpointCommand request, CancellationToken ct) => SaveWithAuditAsync(
        "Teams.TeamInvitationRevoked", "TeamInvitation", request.Invitation.Id, request.Invitation.TenantId,
        "Team invitation revoked", new { request.Invitation.TeamId }, ct);

    public Task<Unit> Handle(AcceptTeamInvitationEndpointCommand request, CancellationToken ct)
    {
        if (request.IsNewMember) context.Set<TeamMember>().Add(request.Member);
        return SaveWithAuditAsync(
            "Teams.TeamInvitationAccepted", "Team", request.Team.Id, request.Team.TenantId,
            $"Invitation to team '{request.Team.Name}' accepted",
            new { Member = request.Member.UserId, Authority = request.Member.Authority.ToString() }, ct);
    }

    public Task<Unit> Handle(AcceptAuthenticatedTeamInvitationEndpointCommand request, CancellationToken ct)
    {
        if (request.IsNewMember) context.Set<TeamMember>().Add(request.Member);
        return SaveWithAuditAsync(
            "Teams.TeamInvitationAccepted", "Team", request.Team.Id, request.Team.TenantId,
            $"Invitation to team '{request.Team.Name}' accepted by an authenticated member",
            new { Member = request.Member.UserId, Authority = request.Member.Authority.ToString() }, ct);
    }

    /// <summary>
    ///     Persists the tracked team mutation together with its required audit event in one
    ///     transaction: when the audit emission fails the transaction never commits, so the
    ///     mutation rolls back (fail closed).
    /// </summary>
    private async Task<Unit> SaveWithAuditAsync(
        string actionType, string resourceType, Guid resourceId, Guid? tenantId, string description, object metadata, CancellationToken ct)
    {
        var actor = actorContextAccessor.ActorContext;
        await context.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new TeamsAuditEvent(
                actionType, resourceType, resourceId.ToString(), actor.SubjectIdAsGuid, tenantId,
                description, Metadata: metadata), token),
            ct).ConfigureAwait(false);
        return Unit.Value;
    }
}
