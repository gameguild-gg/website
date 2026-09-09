using GameGuild.CQRS;
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

public sealed class TeamEndpointCommandHandler(IApplicationDbContext context) :
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
        return SaveAsync(ct);
    }

    public Task<Unit> Handle(UpdateTeamEndpointCommand request, CancellationToken ct) => SaveAsync(ct);
    public Task<Unit> Handle(ArchiveTeamEndpointCommand request, CancellationToken ct) => SaveAsync(ct);
    public Task<Unit> Handle(RestoreTeamEndpointCommand request, CancellationToken ct) => SaveAsync(ct);

    public Task<Unit> Handle(AddTeamMemberEndpointCommand request, CancellationToken ct)
    {
        if (request.IsNew) context.Set<TeamMember>().Add(request.Member);
        return SaveAsync(ct);
    }

    public Task<Unit> Handle(ChangeTeamMemberEndpointCommand request, CancellationToken ct) => SaveAsync(ct);
    public Task<Unit> Handle(RemoveTeamMemberEndpointCommand request, CancellationToken ct) => SaveAsync(ct);

    public Task<Unit> Handle(CreateTeamInvitationEndpointCommand request, CancellationToken ct)
    {
        context.Set<TeamInvitation>().Add(request.Invitation);
        return SaveAsync(ct);
    }

    public Task<Unit> Handle(RevokeTeamInvitationEndpointCommand request, CancellationToken ct) => SaveAsync(ct);

    public Task<Unit> Handle(AcceptTeamInvitationEndpointCommand request, CancellationToken ct)
    {
        if (request.IsNewMember) context.Set<TeamMember>().Add(request.Member);
        return SaveAsync(ct);
    }

    public Task<Unit> Handle(AcceptAuthenticatedTeamInvitationEndpointCommand request, CancellationToken ct)
    {
        if (request.IsNewMember) context.Set<TeamMember>().Add(request.Member);
        return SaveAsync(ct);
    }

    private async Task<Unit> SaveAsync(CancellationToken ct)
    {
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return Unit.Value;
    }
}
