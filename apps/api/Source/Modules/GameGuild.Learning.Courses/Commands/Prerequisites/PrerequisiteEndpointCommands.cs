using GameGuild.CQRS;

namespace GameGuild.Learning.Courses;

public sealed record CreatePrerequisiteEndpointCommand(CreatePrerequisiteRequest Request) : ICommand<Result<CoursePrerequisite>>;
public sealed record UpdatePrerequisiteEndpointCommand(Guid PrerequisiteId, UpdatePrerequisiteRequest Request) : ICommand<Result<CoursePrerequisite>>;
public sealed record DeletePrerequisiteEndpointCommand(Guid PrerequisiteId) : ICommand<Result<bool>>;
public sealed record ReorderPrerequisitesEndpointCommand(Guid CourseId, IEnumerable<Guid> PrerequisiteIds) : ICommand<Result<bool>>;

public sealed class PrerequisiteEndpointCommandHandler(IPrerequisiteService prerequisiteService) :
    ICommandHandler<CreatePrerequisiteEndpointCommand, Result<CoursePrerequisite>>,
    ICommandHandler<UpdatePrerequisiteEndpointCommand, Result<CoursePrerequisite>>,
    ICommandHandler<DeletePrerequisiteEndpointCommand, Result<bool>>,
    ICommandHandler<ReorderPrerequisitesEndpointCommand, Result<bool>>
{
    public Task<Result<CoursePrerequisite>> Handle(CreatePrerequisiteEndpointCommand request, CancellationToken cancellationToken) =>
        prerequisiteService.CreatePrerequisiteAsync(request.Request);

    public Task<Result<CoursePrerequisite>> Handle(UpdatePrerequisiteEndpointCommand request, CancellationToken cancellationToken) =>
        prerequisiteService.UpdatePrerequisiteAsync(request.PrerequisiteId, request.Request);

    public Task<Result<bool>> Handle(DeletePrerequisiteEndpointCommand request, CancellationToken cancellationToken) =>
        prerequisiteService.DeletePrerequisiteAsync(request.PrerequisiteId);

    public Task<Result<bool>> Handle(ReorderPrerequisitesEndpointCommand request, CancellationToken cancellationToken) =>
        prerequisiteService.ReorderPrerequisitesAsync(request.CourseId, request.PrerequisiteIds);
}
