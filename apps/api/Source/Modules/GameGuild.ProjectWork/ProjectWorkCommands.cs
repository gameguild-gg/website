using GameGuild.CQRS;

namespace GameGuild.ProjectWork;

public sealed record CreateProjectWorkColumnCommand(Guid ProjectId, string Name, ProjectWorkColumnKind Kind, int Position, int? WorkInProgressLimit) : ICommand<Result<ProjectWorkColumn>>;
public sealed record UpdateProjectWorkColumnCommand(Guid ProjectId, Guid ColumnId, string Name, ProjectWorkColumnKind Kind, int Position, int? WorkInProgressLimit) : ICommand<Result<ProjectWorkColumn>>;
public sealed record DeleteProjectWorkColumnCommand(Guid ProjectId, Guid ColumnId) : ICommand<Result<bool>>;
public sealed record CreateProjectWorkMilestoneCommand(Guid ProjectId, string Name, string? Description, DateTime? DueAt) : ICommand<Result<ProjectMilestone>>;
public sealed record UpdateProjectWorkMilestoneCommand(Guid ProjectId, Guid MilestoneId, string Name, string? Description, DateTime? DueAt, DateTime? CompletedAt) : ICommand<Result<ProjectMilestone>>;
public sealed record DeleteProjectWorkMilestoneCommand(Guid ProjectId, Guid MilestoneId) : ICommand<Result<bool>>;
public sealed record CreateProjectWorkTaskCommand(Guid ProjectId, CreateProjectWorkTask Task) : ICommand<Result<ProjectWorkTask>>;
public sealed record UpdateProjectWorkTaskCommand(Guid ProjectId, Guid TaskId, UpdateProjectWorkTask Task) : ICommand<Result<ProjectWorkTask>>;
public sealed record DeleteProjectWorkTaskCommand(Guid ProjectId, Guid TaskId) : ICommand<Result<bool>>;
public sealed record MoveProjectWorkTaskCommand(Guid ProjectId, Guid TaskId, Guid ColumnId, int Position) : ICommand<Result<ProjectWorkTask>>;
public sealed record AddProjectWorkDependencyCommand(Guid ProjectId, Guid TaskId, Guid DependsOnTaskId) : ICommand<Result<ProjectTaskDependency>>;
public sealed record RemoveProjectWorkDependencyCommand(Guid ProjectId, Guid TaskId, Guid DependencyId) : ICommand<Result<bool>>;
public sealed record CreateProjectWorkLabelCommand(Guid ProjectId, string Name, string Color) : ICommand<Result<ProjectTaskLabel>>;
public sealed record DeleteProjectWorkLabelCommand(Guid ProjectId, Guid LabelId) : ICommand<Result<bool>>;
public sealed record AssignProjectWorkLabelCommand(Guid ProjectId, Guid TaskId, Guid LabelId) : ICommand<Result<ProjectTaskLabelAssignment>>;
public sealed record UnassignProjectWorkLabelCommand(Guid ProjectId, Guid TaskId, Guid LabelId) : ICommand<Result<bool>>;
public sealed record AddProjectWorkCommentCommand(Guid ProjectId, Guid TaskId, string Body) : ICommand<Result<ProjectTaskComment>>;
public sealed record UpdateProjectWorkCommentCommand(Guid ProjectId, Guid TaskId, Guid CommentId, string Body) : ICommand<Result<ProjectTaskComment>>;
public sealed record DeleteProjectWorkCommentCommand(Guid ProjectId, Guid TaskId, Guid CommentId) : ICommand<Result<bool>>;
public sealed record AddProjectWorkChecklistItemCommand(Guid ProjectId, Guid TaskId, string Text) : ICommand<Result<ProjectTaskChecklistItem>>;
public sealed record UpdateProjectWorkChecklistItemCommand(Guid ProjectId, Guid TaskId, Guid ItemId, bool IsCompleted) : ICommand<Result<ProjectTaskChecklistItem>>;
public sealed record DeleteProjectWorkChecklistItemCommand(Guid ProjectId, Guid TaskId, Guid ItemId) : ICommand<Result<bool>>;

public sealed class ProjectWorkCommandHandler(IProjectWorkService service) :
    ICommandHandler<CreateProjectWorkColumnCommand, Result<ProjectWorkColumn>>,
    ICommandHandler<UpdateProjectWorkColumnCommand, Result<ProjectWorkColumn>>,
    ICommandHandler<DeleteProjectWorkColumnCommand, Result<bool>>,
    ICommandHandler<CreateProjectWorkMilestoneCommand, Result<ProjectMilestone>>,
    ICommandHandler<UpdateProjectWorkMilestoneCommand, Result<ProjectMilestone>>,
    ICommandHandler<DeleteProjectWorkMilestoneCommand, Result<bool>>,
    ICommandHandler<CreateProjectWorkTaskCommand, Result<ProjectWorkTask>>,
    ICommandHandler<UpdateProjectWorkTaskCommand, Result<ProjectWorkTask>>,
    ICommandHandler<DeleteProjectWorkTaskCommand, Result<bool>>,
    ICommandHandler<MoveProjectWorkTaskCommand, Result<ProjectWorkTask>>,
    ICommandHandler<AddProjectWorkDependencyCommand, Result<ProjectTaskDependency>>,
    ICommandHandler<RemoveProjectWorkDependencyCommand, Result<bool>>,
    ICommandHandler<CreateProjectWorkLabelCommand, Result<ProjectTaskLabel>>,
    ICommandHandler<DeleteProjectWorkLabelCommand, Result<bool>>,
    ICommandHandler<AssignProjectWorkLabelCommand, Result<ProjectTaskLabelAssignment>>,
    ICommandHandler<UnassignProjectWorkLabelCommand, Result<bool>>,
    ICommandHandler<AddProjectWorkCommentCommand, Result<ProjectTaskComment>>,
    ICommandHandler<UpdateProjectWorkCommentCommand, Result<ProjectTaskComment>>,
    ICommandHandler<DeleteProjectWorkCommentCommand, Result<bool>>,
    ICommandHandler<AddProjectWorkChecklistItemCommand, Result<ProjectTaskChecklistItem>>,
    ICommandHandler<UpdateProjectWorkChecklistItemCommand, Result<ProjectTaskChecklistItem>>,
    ICommandHandler<DeleteProjectWorkChecklistItemCommand, Result<bool>>
{
    public Task<Result<ProjectWorkColumn>> Handle(CreateProjectWorkColumnCommand request, CancellationToken ct) =>
        service.ConfigureColumnAsync(request.ProjectId, null, request.Name, request.Kind, request.Position, request.WorkInProgressLimit, ct);

    public Task<Result<ProjectWorkColumn>> Handle(UpdateProjectWorkColumnCommand request, CancellationToken ct) =>
        service.ConfigureColumnAsync(request.ProjectId, request.ColumnId, request.Name, request.Kind, request.Position, request.WorkInProgressLimit, ct);

    public Task<Result<bool>> Handle(DeleteProjectWorkColumnCommand request, CancellationToken ct) =>
        service.DeleteColumnAsync(request.ProjectId, request.ColumnId, ct);

    public Task<Result<ProjectMilestone>> Handle(CreateProjectWorkMilestoneCommand request, CancellationToken ct) =>
        service.CreateMilestoneAsync(request.ProjectId, request.Name, request.Description, request.DueAt, ct);

    public Task<Result<ProjectMilestone>> Handle(UpdateProjectWorkMilestoneCommand request, CancellationToken ct) =>
        service.UpdateMilestoneAsync(request.ProjectId, request.MilestoneId, request.Name, request.Description, request.DueAt, request.CompletedAt, ct);

    public Task<Result<bool>> Handle(DeleteProjectWorkMilestoneCommand request, CancellationToken ct) =>
        service.DeleteMilestoneAsync(request.ProjectId, request.MilestoneId, ct);

    public Task<Result<ProjectWorkTask>> Handle(CreateProjectWorkTaskCommand request, CancellationToken ct) =>
        service.CreateTaskAsync(request.ProjectId, request.Task, ct);

    public Task<Result<ProjectWorkTask>> Handle(UpdateProjectWorkTaskCommand request, CancellationToken ct) =>
        service.UpdateTaskAsync(request.ProjectId, request.TaskId, request.Task, ct);

    public Task<Result<bool>> Handle(DeleteProjectWorkTaskCommand request, CancellationToken ct) =>
        service.DeleteTaskAsync(request.ProjectId, request.TaskId, ct);

    public Task<Result<ProjectWorkTask>> Handle(MoveProjectWorkTaskCommand request, CancellationToken ct) =>
        service.MoveTaskAsync(request.ProjectId, request.TaskId, request.ColumnId, request.Position, ct);

    public Task<Result<ProjectTaskDependency>> Handle(AddProjectWorkDependencyCommand request, CancellationToken ct) =>
        service.AddDependencyAsync(request.ProjectId, request.TaskId, request.DependsOnTaskId, ct);

    public Task<Result<bool>> Handle(RemoveProjectWorkDependencyCommand request, CancellationToken ct) =>
        service.RemoveDependencyAsync(request.ProjectId, request.TaskId, request.DependencyId, ct);

    public Task<Result<ProjectTaskLabel>> Handle(CreateProjectWorkLabelCommand request, CancellationToken ct) =>
        service.CreateLabelAsync(request.ProjectId, request.Name, request.Color, ct);

    public Task<Result<bool>> Handle(DeleteProjectWorkLabelCommand request, CancellationToken ct) =>
        service.DeleteLabelAsync(request.ProjectId, request.LabelId, ct);

    public Task<Result<ProjectTaskLabelAssignment>> Handle(AssignProjectWorkLabelCommand request, CancellationToken ct) =>
        service.AssignLabelAsync(request.ProjectId, request.TaskId, request.LabelId, ct);

    public Task<Result<bool>> Handle(UnassignProjectWorkLabelCommand request, CancellationToken ct) =>
        service.UnassignLabelAsync(request.ProjectId, request.TaskId, request.LabelId, ct);

    public Task<Result<ProjectTaskComment>> Handle(AddProjectWorkCommentCommand request, CancellationToken ct) =>
        service.AddCommentAsync(request.ProjectId, request.TaskId, request.Body, ct);

    public Task<Result<ProjectTaskComment>> Handle(UpdateProjectWorkCommentCommand request, CancellationToken ct) =>
        service.UpdateCommentAsync(request.ProjectId, request.TaskId, request.CommentId, request.Body, ct);

    public Task<Result<bool>> Handle(DeleteProjectWorkCommentCommand request, CancellationToken ct) =>
        service.DeleteCommentAsync(request.ProjectId, request.TaskId, request.CommentId, ct);

    public Task<Result<ProjectTaskChecklistItem>> Handle(AddProjectWorkChecklistItemCommand request, CancellationToken ct) =>
        service.AddChecklistItemAsync(request.ProjectId, request.TaskId, request.Text, ct);

    public Task<Result<ProjectTaskChecklistItem>> Handle(UpdateProjectWorkChecklistItemCommand request, CancellationToken ct) =>
        service.SetChecklistCompletionAsync(request.ProjectId, request.TaskId, request.ItemId, request.IsCompleted, ct);

    public Task<Result<bool>> Handle(DeleteProjectWorkChecklistItemCommand request, CancellationToken ct) =>
        service.DeleteChecklistItemAsync(request.ProjectId, request.TaskId, request.ItemId, ct);
}
