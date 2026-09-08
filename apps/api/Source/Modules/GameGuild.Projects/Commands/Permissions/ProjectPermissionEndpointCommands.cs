using GameGuild.CQRS;
using PermissionType = GameGuild.Identity.Authorization.PermissionType;

namespace GameGuild.Projects;

public sealed record AddProjectPermissionCollaboratorCommand(
    Guid ProjectId,
    InviteUserRequest Request,
    Guid ActorId) : ICommand<InvitationResult>;

public sealed record UpdateProjectPermissionCollaboratorCommand(
    Guid ProjectId,
    Guid CollaboratorUserId,
    PermissionType[] Permissions,
    Guid ActorId,
    DateTime? ExpiresAt) : ICommand<PermissionUpdateResult>;

public sealed record RemoveProjectPermissionCollaboratorCommand(
    Guid ProjectId,
    Guid CollaboratorUserId,
    Guid ActorId) : ICommand<PermissionUpdateResult>;

public sealed record ShareProjectWithRoleCommand(
    Guid ProjectId,
    ShareResourceRequest Request,
    Guid ActorId) : ICommand<ShareResult>;

public sealed class ProjectPermissionEndpointCommandHandler(IResourcePermissionService resourcePermissionService) :
    ICommandHandler<AddProjectPermissionCollaboratorCommand, InvitationResult>,
    ICommandHandler<UpdateProjectPermissionCollaboratorCommand, PermissionUpdateResult>,
    ICommandHandler<RemoveProjectPermissionCollaboratorCommand, PermissionUpdateResult>,
    ICommandHandler<ShareProjectWithRoleCommand, ShareResult>
{
    public Task<InvitationResult> Handle(AddProjectPermissionCollaboratorCommand request, CancellationToken cancellationToken) =>
        resourcePermissionService.InviteUserToResourceAsync("projects", request.ProjectId, request.Request, request.ActorId);

    public Task<PermissionUpdateResult> Handle(UpdateProjectPermissionCollaboratorCommand request, CancellationToken cancellationToken) =>
        resourcePermissionService.UpdateUserPermissionsAsync(
            "projects",
            request.ProjectId,
            request.CollaboratorUserId,
            request.Permissions,
            request.ActorId,
            request.ExpiresAt);

    public Task<PermissionUpdateResult> Handle(RemoveProjectPermissionCollaboratorCommand request, CancellationToken cancellationToken) =>
        resourcePermissionService.RemoveUserAccessAsync("projects", request.ProjectId, request.CollaboratorUserId, request.ActorId);

    public Task<ShareResult> Handle(ShareProjectWithRoleCommand request, CancellationToken cancellationToken) =>
        resourcePermissionService.ShareResourceAsync("projects", request.ProjectId, request.Request, request.ActorId);
}
