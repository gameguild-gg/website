using GameGuild.CQRS;

namespace GameGuild.Learning.Courses;

public sealed record SubmitProgramContentEndpointCommand(
    Guid ProgramId,
    Guid UserId,
    Guid ContentId,
    string SubmissionData) : ICommand<ContentInteraction?>;
public sealed record CreateProgramContentEndpointCommand(ProgramContent Content) : ICommand<ProgramContent>;
public sealed record UpdateProgramContentEndpointCommand(ProgramContent Content) : ICommand<ProgramContent>;
public sealed record DeleteProgramContentEndpointCommand(Guid ContentId) : ICommand<bool>;
public sealed record ReorderProgramContentEndpointCommand(
    Guid ProgramId,
    List<(Guid contentId, int sortOrder)> NewOrder) : ICommand<bool>;
public sealed record MoveProgramContentEndpointCommand(
    Guid ContentId,
    Guid? NewParentId,
    int NewSortOrder) : ICommand<bool>;
public sealed record PutCodingAssignmentEndpointCommand(
    Guid ProgramId,
    Guid ContentId,
    CodingAssignmentContent Content,
    Guid ActorId) : ICommand<Result<CodingAssignmentContent>>;
public sealed record SearchProgramContentEndpointQuery(Guid ProgramId, string SearchTerm) : IQuery<IEnumerable<ProgramContent>>;

public sealed class ProgramContentEndpointCommandHandler(
    IProgramContentService contentService,
    IProgramCrudService programService,
    ICodingAssignmentContentService codingAssignmentService) :
    ICommandHandler<SubmitProgramContentEndpointCommand, ContentInteraction?>,
    ICommandHandler<CreateProgramContentEndpointCommand, ProgramContent>,
    ICommandHandler<UpdateProgramContentEndpointCommand, ProgramContent>,
    ICommandHandler<DeleteProgramContentEndpointCommand, bool>,
    ICommandHandler<ReorderProgramContentEndpointCommand, bool>,
    ICommandHandler<MoveProgramContentEndpointCommand, bool>,
    ICommandHandler<PutCodingAssignmentEndpointCommand, Result<CodingAssignmentContent>>,
    IQueryHandler<SearchProgramContentEndpointQuery, IEnumerable<ProgramContent>>
{
    public Task<ContentInteraction?> Handle(SubmitProgramContentEndpointCommand request, CancellationToken cancellationToken) =>
        programService.SubmitUserContentAsync(request.ProgramId, request.UserId, request.ContentId, request.SubmissionData);

    public Task<ProgramContent> Handle(CreateProgramContentEndpointCommand request, CancellationToken cancellationToken) =>
        contentService.CreateContentAsync(request.Content);

    public Task<ProgramContent> Handle(UpdateProgramContentEndpointCommand request, CancellationToken cancellationToken) =>
        contentService.UpdateContentAsync(request.Content);

    public Task<bool> Handle(DeleteProgramContentEndpointCommand request, CancellationToken cancellationToken) =>
        contentService.DeleteContentAsync(request.ContentId);

    public Task<bool> Handle(ReorderProgramContentEndpointCommand request, CancellationToken cancellationToken) =>
        contentService.ReorderContentAsync(request.ProgramId, request.NewOrder);

    public Task<bool> Handle(MoveProgramContentEndpointCommand request, CancellationToken cancellationToken) =>
        contentService.MoveContentAsync(request.ContentId, request.NewParentId, request.NewSortOrder);

    public Task<Result<CodingAssignmentContent>> Handle(PutCodingAssignmentEndpointCommand request, CancellationToken cancellationToken) =>
        codingAssignmentService.UpsertAsync(request.ProgramId, request.ContentId, request.Content, request.ActorId, cancellationToken);

    public Task<IEnumerable<ProgramContent>> Handle(SearchProgramContentEndpointQuery request, CancellationToken cancellationToken) =>
        contentService.SearchContentAsync(request.ProgramId, request.SearchTerm);
}
