using GameGuild.CQRS;

namespace GameGuild.Projects;

public sealed record CreateProjectVersionEndpointCommand(ProjectVersion Version) : ICommand<ProjectVersion>;
public sealed record UpdateProjectVersionEndpointCommand(ProjectVersion Version) : ICommand<ProjectVersion>;
public sealed record TransitionProjectVersionEndpointCommand(ProjectVersion Version) : ICommand<ProjectVersion>;
public sealed record RestoreProjectEndpointCommand(Project Project) : ICommand<Project>;
public sealed record AcceptProjectInvitationEndpointCommand(ProjectInvitation Invitation) : ICommand<ProjectInvitation>;
public sealed record DeclineProjectInvitationEndpointCommand(ProjectInvitation Invitation) : ICommand<ProjectInvitation>;
public sealed record AddProjectCollaboratorEndpointCommand(ProjectCollaborator Collaborator) : ICommand<ProjectCollaborator>;
public sealed record UpdateProjectCollaboratorEndpointCommand(ProjectCollaborator Collaborator) : ICommand<ProjectCollaborator>;
public sealed record RemoveProjectCollaboratorEndpointCommand(ProjectCollaborator Collaborator) : ICommand;
public sealed record ShareProjectEndpointCommand(Guid ProjectId, Guid UserId) : ICommand;
public sealed record InviteProjectCollaboratorEndpointCommand(ProjectInvitation Invitation) : ICommand<ProjectInvitation>;

public sealed class ProjectEndpointMutationCommandHandler(IApplicationDbContext context) :
    ICommandHandler<CreateProjectVersionEndpointCommand, ProjectVersion>,
    ICommandHandler<UpdateProjectVersionEndpointCommand, ProjectVersion>,
    ICommandHandler<TransitionProjectVersionEndpointCommand, ProjectVersion>,
    ICommandHandler<RestoreProjectEndpointCommand, Project>,
    ICommandHandler<AcceptProjectInvitationEndpointCommand, ProjectInvitation>,
    ICommandHandler<DeclineProjectInvitationEndpointCommand, ProjectInvitation>,
    ICommandHandler<AddProjectCollaboratorEndpointCommand, ProjectCollaborator>,
    ICommandHandler<UpdateProjectCollaboratorEndpointCommand, ProjectCollaborator>,
    ICommandHandler<RemoveProjectCollaboratorEndpointCommand>,
    ICommandHandler<ShareProjectEndpointCommand>,
    ICommandHandler<InviteProjectCollaboratorEndpointCommand, ProjectInvitation>
{
    public async Task<ProjectVersion> Handle(CreateProjectVersionEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Version;
    }

    public async Task<ProjectVersion> Handle(UpdateProjectVersionEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Version;
    }

    public async Task<ProjectVersion> Handle(TransitionProjectVersionEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Version;
    }

    public async Task<Project> Handle(RestoreProjectEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Project;
    }

    public async Task<ProjectInvitation> Handle(AcceptProjectInvitationEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Invitation;
    }

    public async Task<ProjectInvitation> Handle(DeclineProjectInvitationEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Invitation;
    }

    public async Task<ProjectCollaborator> Handle(AddProjectCollaboratorEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Collaborator;
    }

    public async Task<ProjectCollaborator> Handle(UpdateProjectCollaboratorEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Collaborator;
    }

    public async Task<Unit> Handle(RemoveProjectCollaboratorEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ShareProjectEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<ProjectInvitation> Handle(InviteProjectCollaboratorEndpointCommand request, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return request.Invitation;
    }
}
