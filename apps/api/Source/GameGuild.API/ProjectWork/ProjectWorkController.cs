using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.ProjectWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.ProjectWork;

public sealed record ProjectWorkTaskDto(
    Guid Id,
    Guid ColumnId,
    string Title,
    string? Description,
    ProjectWorkTaskStatus Status,
    ProjectWorkTaskPriority Priority,
    Guid? AssigneeUserId,
    Guid? MilestoneId,
    DateTime? DueAt,
    DateTime? CompletedAt,
    int Position);

public sealed record ProjectWorkColumnDto(
    Guid Id,
    string Name,
    ProjectWorkColumnKind Kind,
    int Position,
    int? WorkInProgressLimit,
    IReadOnlyList<ProjectWorkTaskDto> Tasks);

public sealed record ProjectBoardDto(Guid Id, Guid ProjectId, string Name, IReadOnlyList<ProjectWorkColumnDto> Columns);
public sealed record ProjectMilestoneDto(Guid Id, string Name, string? Description, DateTime? DueAt, DateTime? CompletedAt);
public sealed record ProjectTaskLabelDto(Guid Id, string Name, string Color);
public sealed record ProjectChecklistItemDto(Guid Id, string Text, bool IsCompleted, int Position);
public sealed record ProjectTaskCommentDto(Guid Id, Guid AuthorUserId, string Body, DateTime? EditedAt, DateTime CreatedAt);
public sealed record ProjectWorkHistoryDto(Guid Id, Guid? TaskId, Guid ActorUserId, string Action, string? ChangesJson, DateTime CreatedAt);
public sealed record ProjectTaskDependencyDto(Guid Id, Guid DependsOnTaskId);
public sealed record ProjectWorkTaskDetailsDto(
    ProjectWorkTaskDto Task,
    IReadOnlyList<ProjectChecklistItemDto> Checklist,
    IReadOnlyList<ProjectTaskCommentDto> Comments,
    IReadOnlyList<ProjectTaskDependencyDto> Dependencies,
    IReadOnlyList<ProjectTaskLabelDto> Labels);
public sealed record CreateProjectWorkTaskRequest(Guid ColumnId, string Title, string? Description, ProjectWorkTaskPriority Priority, Guid? AssigneeUserId, Guid? MilestoneId, DateTime? DueAt);
public sealed record UpdateProjectWorkTaskRequest(string Title, string? Description, ProjectWorkTaskPriority Priority, Guid? AssigneeUserId, Guid? MilestoneId, DateTime? DueAt);
public sealed record MoveProjectWorkTaskRequest(Guid ColumnId, int Position);
public sealed record ConfigureProjectWorkColumnRequest(string Name, ProjectWorkColumnKind Kind, int Position, int? WorkInProgressLimit);
public sealed record CreateProjectMilestoneRequest(string Name, string? Description, DateTime? DueAt);
public sealed record UpdateProjectMilestoneRequest(string Name, string? Description, DateTime? DueAt, DateTime? CompletedAt);
public sealed record AddProjectTaskDependencyRequest(Guid DependsOnTaskId);
public sealed record AddProjectTaskCommentRequest(string Body);
public sealed record UpdateProjectTaskCommentRequest(string Body);
public sealed record AddProjectTaskChecklistRequest(string Text);
public sealed record UpdateProjectTaskChecklistRequest(bool IsCompleted);
public sealed record CreateProjectTaskLabelRequest(string Name, string Color);

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("v{version:apiVersion}/projects/{projectId:guid}/work")]
public sealed class ProjectWorkController(IProjectWorkService service, ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProjectBoardDto>> Get(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await service.GetBoardAsync(projectId, true, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(Map(result.Value)) : Error(result.Error);
    }

    [HttpPost("columns")]
    public async Task<ActionResult<ProjectWorkColumnDto>> CreateColumn(
        Guid projectId,
        ConfigureProjectWorkColumnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateProjectWorkColumnCommand(
            projectId, request.Name, request.Kind, request.Position, request.WorkInProgressLimit), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Created(string.Empty, Map(result.Value)) : Error(result.Error);
    }

    [HttpPut("columns/{columnId:guid}")]
    public async Task<ActionResult<ProjectWorkColumnDto>> UpdateColumn(
        Guid projectId,
        Guid columnId,
        ConfigureProjectWorkColumnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateProjectWorkColumnCommand(
            projectId, columnId, request.Name, request.Kind, request.Position, request.WorkInProgressLimit), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(Map(result.Value)) : Error(result.Error);
    }

    [HttpDelete("columns/{columnId:guid}")]
    public async Task<IActionResult> DeleteColumn(Guid projectId, Guid columnId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteProjectWorkColumnCommand(projectId, columnId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : Error(result.Error);
    }

    [HttpGet("milestones")]
    public async Task<ActionResult<IReadOnlyList<ProjectMilestoneDto>>> GetMilestones(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await service.GetMilestonesAsync(projectId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value.Select(Map).ToArray()) : Error(result.Error);
    }

    [HttpPost("milestones")]
    public async Task<ActionResult<ProjectMilestoneDto>> CreateMilestone(
        Guid projectId, CreateProjectMilestoneRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateProjectWorkMilestoneCommand(
            projectId, request.Name, request.Description, request.DueAt), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Created(string.Empty, Map(result.Value)) : Error(result.Error);
    }

    [HttpPut("milestones/{milestoneId:guid}")]
    public async Task<ActionResult<ProjectMilestoneDto>> UpdateMilestone(
        Guid projectId, Guid milestoneId, UpdateProjectMilestoneRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateProjectWorkMilestoneCommand(
            projectId, milestoneId, request.Name, request.Description, request.DueAt, request.CompletedAt), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(Map(result.Value)) : Error(result.Error);
    }

    [HttpDelete("milestones/{milestoneId:guid}")]
    public async Task<IActionResult> DeleteMilestone(Guid projectId, Guid milestoneId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteProjectWorkMilestoneCommand(projectId, milestoneId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : Error(result.Error);
    }

    [HttpPost("tasks")]
    public async Task<ActionResult<ProjectWorkTaskDto>> CreateTask(
        Guid projectId,
        CreateProjectWorkTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateProjectWorkTaskCommand(projectId, new CreateProjectWorkTask(
            request.ColumnId,
            request.Title,
            request.Description,
            request.Priority,
            request.AssigneeUserId,
            request.MilestoneId,
            request.DueAt)), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Created($"/v1/projects/{projectId}/work/tasks/{result.Value.Id}", Map(result.Value)) : Error(result.Error);
    }

    [HttpGet("tasks/{taskId:guid}")]
    public async Task<ActionResult<ProjectWorkTaskDetailsDto>> GetTask(
        Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await service.GetTaskDetailsAsync(projectId, taskId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(Map(result.Value)) : Error(result.Error);
    }

    [HttpPut("tasks/{taskId:guid}")]
    public async Task<ActionResult<ProjectWorkTaskDto>> UpdateTask(
        Guid projectId,
        Guid taskId,
        UpdateProjectWorkTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateProjectWorkTaskCommand(projectId, taskId, new UpdateProjectWorkTask(
            request.Title,
            request.Description,
            request.Priority,
            request.AssigneeUserId,
            request.MilestoneId,
            request.DueAt)), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(Map(result.Value)) : Error(result.Error);
    }

    [HttpDelete("tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteProjectWorkTaskCommand(projectId, taskId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : Error(result.Error);
    }

    [HttpPut("tasks/{taskId:guid}/move")]
    public async Task<ActionResult<ProjectWorkTaskDto>> MoveTask(
        Guid projectId,
        Guid taskId,
        MoveProjectWorkTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new MoveProjectWorkTaskCommand(
            projectId, taskId, request.ColumnId, request.Position), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(Map(result.Value)) : Error(result.Error);
    }

    [HttpPost("tasks/{taskId:guid}/dependencies")]
    public async Task<IActionResult> AddDependency(
        Guid projectId,
        Guid taskId,
        AddProjectTaskDependencyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddProjectWorkDependencyCommand(
            projectId, taskId, request.DependsOnTaskId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Created(string.Empty, new { result.Value.Id, result.Value.TaskId, result.Value.DependsOnTaskId }) : Error(result.Error);
    }

    [HttpDelete("tasks/{taskId:guid}/dependencies/{dependencyId:guid}")]
    public async Task<IActionResult> RemoveDependency(
        Guid projectId,
        Guid taskId,
        Guid dependencyId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveProjectWorkDependencyCommand(
            projectId, taskId, dependencyId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : Error(result.Error);
    }

    [HttpGet("labels")]
    public async Task<ActionResult<IReadOnlyList<ProjectTaskLabelDto>>> GetLabels(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await service.GetLabelsAsync(projectId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value.Select(Map).ToArray()) : Error(result.Error);
    }

    [HttpPost("labels")]
    public async Task<ActionResult<ProjectTaskLabelDto>> CreateLabel(
        Guid projectId, CreateProjectTaskLabelRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateProjectWorkLabelCommand(
            projectId, request.Name, request.Color), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Created(string.Empty, Map(result.Value)) : Error(result.Error);
    }

    [HttpDelete("labels/{labelId:guid}")]
    public async Task<IActionResult> DeleteLabel(Guid projectId, Guid labelId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteProjectWorkLabelCommand(projectId, labelId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : Error(result.Error);
    }

    [HttpPost("tasks/{taskId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> AssignLabel(Guid projectId, Guid taskId, Guid labelId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AssignProjectWorkLabelCommand(
            projectId, taskId, labelId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Created(string.Empty, new { result.Value.Id, result.Value.TaskId, result.Value.LabelId }) : Error(result.Error);
    }

    [HttpDelete("tasks/{taskId:guid}/labels/{labelId:guid}")]
    public async Task<IActionResult> UnassignLabel(Guid projectId, Guid taskId, Guid labelId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UnassignProjectWorkLabelCommand(
            projectId, taskId, labelId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : Error(result.Error);
    }

    [HttpPost("tasks/{taskId:guid}/comments")]
    public async Task<IActionResult> AddComment(
        Guid projectId,
        Guid taskId,
        AddProjectTaskCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddProjectWorkCommentCommand(
            projectId, taskId, request.Body), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Created(string.Empty, new { result.Value.Id, result.Value.Body, result.Value.AuthorUserId, result.Value.CreatedAt }) : Error(result.Error);
    }

    [HttpPut("tasks/{taskId:guid}/comments/{commentId:guid}")]
    public async Task<ActionResult<ProjectTaskCommentDto>> UpdateComment(
        Guid projectId,
        Guid taskId,
        Guid commentId,
        UpdateProjectTaskCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateProjectWorkCommentCommand(
            projectId, taskId, commentId, request.Body), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(Map(result.Value)) : Error(result.Error);
    }

    [HttpDelete("tasks/{taskId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(
        Guid projectId, Guid taskId, Guid commentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteProjectWorkCommentCommand(
            projectId, taskId, commentId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : Error(result.Error);
    }

    [HttpPost("tasks/{taskId:guid}/checklist")]
    public async Task<IActionResult> AddChecklist(
        Guid projectId,
        Guid taskId,
        AddProjectTaskChecklistRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddProjectWorkChecklistItemCommand(
            projectId, taskId, request.Text), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Created(string.Empty, new { result.Value.Id, result.Value.Text, result.Value.Position }) : Error(result.Error);
    }

    [HttpPut("tasks/{taskId:guid}/checklist/{itemId:guid}")]
    public async Task<ActionResult<ProjectChecklistItemDto>> UpdateChecklist(
        Guid projectId,
        Guid taskId,
        Guid itemId,
        UpdateProjectTaskChecklistRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateProjectWorkChecklistItemCommand(
            projectId, taskId, itemId, request.IsCompleted), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(Map(result.Value)) : Error(result.Error);
    }

    [HttpDelete("tasks/{taskId:guid}/checklist/{itemId:guid}")]
    public async Task<IActionResult> DeleteChecklist(
        Guid projectId, Guid taskId, Guid itemId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteProjectWorkChecklistItemCommand(
            projectId, taskId, itemId), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : Error(result.Error);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<ProjectWorkHistoryDto>>> GetHistory(
        Guid projectId, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var result = await service.GetHistoryAsync(projectId, take, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value.Select(Map).ToArray()) : Error(result.Error);
    }

    private ObjectResult Error(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, new ProblemDetails { Title = error.Code, Detail = error.Description, Status = status });
    }

    private static ProjectBoardDto Map(ProjectBoard board) => new(
        board.Id,
        board.ProjectId,
        board.Name,
        board.Columns.OrderBy(column => column.Position).Select(column => new ProjectWorkColumnDto(
            column.Id,
            column.Name,
            column.Kind,
            column.Position,
            column.WorkInProgressLimit,
            column.Tasks.Where(task => task.DeletedAt == null).OrderBy(task => task.Position).Select(Map).ToArray())).ToArray());

    private static ProjectWorkColumnDto Map(ProjectWorkColumn column) => new(
        column.Id,
        column.Name,
        column.Kind,
        column.Position,
        column.WorkInProgressLimit,
        column.Tasks.Where(task => task.DeletedAt == null).OrderBy(task => task.Position).Select(Map).ToArray());

    private static ProjectWorkTaskDto Map(ProjectWorkTask task) => new(
        task.Id,
        task.ColumnId,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.AssigneeUserId,
        task.MilestoneId,
        task.DueAt,
        task.CompletedAt,
        task.Position);

    private static ProjectWorkTaskDetailsDto Map(ProjectWorkTaskDetails details) => new(
        Map(details.Task),
        details.Checklist.Select(Map).ToArray(),
        details.Comments.Select(Map).ToArray(),
        details.Dependencies.Select(edge => new ProjectTaskDependencyDto(edge.Id, edge.DependsOnTaskId)).ToArray(),
        details.Labels.Select(Map).ToArray());

    private static ProjectMilestoneDto Map(ProjectMilestone milestone) =>
        new(milestone.Id, milestone.Name, milestone.Description, milestone.DueAt, milestone.CompletedAt);

    private static ProjectTaskLabelDto Map(ProjectTaskLabel label) => new(label.Id, label.Name, label.Color);

    private static ProjectChecklistItemDto Map(ProjectTaskChecklistItem item) =>
        new(item.Id, item.Text, item.IsCompleted, item.Position);

    private static ProjectTaskCommentDto Map(ProjectTaskComment comment) =>
        new(comment.Id, comment.AuthorUserId, comment.Body, comment.EditedAt, comment.CreatedAt);

    private static ProjectWorkHistoryDto Map(ProjectWorkHistory history) =>
        new(history.Id, history.TaskId, history.ActorUserId, history.Action, history.ChangesJson, history.CreatedAt);
}
