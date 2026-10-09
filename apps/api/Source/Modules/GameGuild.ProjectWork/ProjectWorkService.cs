using System.Text.Json;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Projects;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.ProjectWork;

public sealed record CreateProjectWorkTask(
    Guid ColumnId,
    string Title,
    string? Description,
    ProjectWorkTaskPriority Priority,
    Guid? AssigneeUserId,
    Guid? MilestoneId,
    DateTime? DueAt);

public sealed record UpdateProjectWorkTask(
    string Title,
    string? Description,
    ProjectWorkTaskPriority Priority,
    Guid? AssigneeUserId,
    Guid? MilestoneId,
    DateTime? DueAt);

public sealed record ProjectWorkTaskDetails(
    ProjectWorkTask Task,
    IReadOnlyList<ProjectTaskChecklistItem> Checklist,
    IReadOnlyList<ProjectTaskComment> Comments,
    IReadOnlyList<ProjectTaskDependency> Dependencies,
    IReadOnlyList<ProjectTaskLabel> Labels);

public interface IProjectWorkService
{
    Task<Result<ProjectBoard>> GetBoardAsync(Guid projectId, bool createIfMissing, CancellationToken cancellationToken = default);
    Task<Result<ProjectWorkColumn>> ConfigureColumnAsync(Guid projectId, Guid? columnId, string name, ProjectWorkColumnKind kind, int position, int? workInProgressLimit, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteColumnAsync(Guid projectId, Guid columnId, CancellationToken cancellationToken = default);
    Task<Result<ProjectMilestone>> CreateMilestoneAsync(Guid projectId, string name, string? description, DateTime? dueAt, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectMilestone>>> GetMilestonesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectMilestone>> UpdateMilestoneAsync(Guid projectId, Guid milestoneId, string name, string? description, DateTime? dueAt, DateTime? completedAt, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteMilestoneAsync(Guid projectId, Guid milestoneId, CancellationToken cancellationToken = default);
    Task<Result<ProjectWorkTask>> CreateTaskAsync(Guid projectId, CreateProjectWorkTask request, CancellationToken cancellationToken = default);
    Task<Result<ProjectWorkTaskDetails>> GetTaskDetailsAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);
    Task<Result<ProjectWorkTask>> UpdateTaskAsync(Guid projectId, Guid taskId, UpdateProjectWorkTask request, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);
    Task<Result<ProjectWorkTask>> MoveTaskAsync(Guid projectId, Guid taskId, Guid columnId, int position, CancellationToken cancellationToken = default);
    Task<Result<ProjectTaskDependency>> AddDependencyAsync(Guid projectId, Guid taskId, Guid dependsOnTaskId, CancellationToken cancellationToken = default);
    Task<Result<bool>> RemoveDependencyAsync(Guid projectId, Guid taskId, Guid dependencyId, CancellationToken cancellationToken = default);
    Task<Result<ProjectTaskLabel>> CreateLabelAsync(Guid projectId, string name, string color, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectTaskLabel>>> GetLabelsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteLabelAsync(Guid projectId, Guid labelId, CancellationToken cancellationToken = default);
    Task<Result<ProjectTaskLabelAssignment>> AssignLabelAsync(Guid projectId, Guid taskId, Guid labelId, CancellationToken cancellationToken = default);
    Task<Result<bool>> UnassignLabelAsync(Guid projectId, Guid taskId, Guid labelId, CancellationToken cancellationToken = default);
    Task<Result<ProjectTaskComment>> AddCommentAsync(Guid projectId, Guid taskId, string body, CancellationToken cancellationToken = default);
    Task<Result<ProjectTaskComment>> UpdateCommentAsync(Guid projectId, Guid taskId, Guid commentId, string body, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteCommentAsync(Guid projectId, Guid taskId, Guid commentId, CancellationToken cancellationToken = default);
    Task<Result<ProjectTaskChecklistItem>> AddChecklistItemAsync(Guid projectId, Guid taskId, string text, CancellationToken cancellationToken = default);
    Task<Result<ProjectTaskChecklistItem>> SetChecklistCompletionAsync(Guid projectId, Guid taskId, Guid itemId, bool completed, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteChecklistItemAsync(Guid projectId, Guid taskId, Guid itemId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectWorkHistory>>> GetHistoryAsync(Guid projectId, int take, CancellationToken cancellationToken = default);
}

public sealed class ProjectWorkService(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    IProjectAuthorizationService projectAuthorizationService) : IProjectWorkService
{
    public async Task<Result<ProjectWorkTaskDetails>> GetTaskDetailsAsync(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Read, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectWorkTaskDetails>(NotFoundProject());
        }

        var task = await context.Set<ProjectWorkTask>().AsNoTracking()
            .Include(candidate => candidate.Checklist.Where(item => item.DeletedAt == null))
            .Include(candidate => candidate.Comments.Where(comment => comment.DeletedAt == null))
            .SingleOrDefaultAsync(candidate => candidate.Id == taskId && candidate.ProjectId == projectId && candidate.DeletedAt == null,
                cancellationToken).ConfigureAwait(false);
        if (task == null)
        {
            return Result.Failure<ProjectWorkTaskDetails>(Error.NotFound("ProjectWork.TaskNotFound", "Task not found"));
        }

        var dependencies = await context.Set<ProjectTaskDependency>().AsNoTracking()
            .Where(edge => edge.TaskId == taskId && edge.DeletedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var labels = await (
            from assignment in context.Set<ProjectTaskLabelAssignment>().AsNoTracking()
            join label in context.Set<ProjectTaskLabel>().AsNoTracking() on assignment.LabelId equals label.Id
            where assignment.TaskId == taskId && assignment.DeletedAt == null && label.DeletedAt == null
            orderby label.Name
            select label).ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new ProjectWorkTaskDetails(
            task,
            task.Checklist.OrderBy(item => item.Position).ToArray(),
            task.Comments.OrderBy(item => item.CreatedAt).ToArray(),
            dependencies,
            labels));
    }

    public async Task<Result<ProjectWorkColumn>> ConfigureColumnAsync(
        Guid projectId,
        Guid? columnId,
        string name,
        ProjectWorkColumnKind kind,
        int position,
        int? workInProgressLimit,
        CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectWorkColumn>(NotFoundProject());
        }

        if (string.IsNullOrWhiteSpace(name) || workInProgressLimit is <= 0)
        {
            return Result.Failure<ProjectWorkColumn>(Error.Validation("ProjectWork.ColumnInvalid", "Column name is required and WIP limit must be positive."));
        }

        var boardResult = await GetBoardAsync(projectId, true, cancellationToken).ConfigureAwait(false);
        if (boardResult.IsFailure)
        {
            return Result.Failure<ProjectWorkColumn>(boardResult.Error);
        }

        var board = boardResult.Value;
        var orderedColumns = board.Columns
            .Where(candidate => candidate.DeletedAt == null)
            .OrderBy(candidate => candidate.Position)
            .ThenBy(candidate => candidate.Id)
            .ToList();
        ProjectWorkColumn column;
        if (columnId.HasValue)
        {
            column = orderedColumns.SingleOrDefault(candidate => candidate.Id == columnId)!;
            if (column == null)
            {
                return Result.Failure<ProjectWorkColumn>(Error.NotFound("ProjectWork.ColumnNotFound", "Column not found"));
            }

            column.Name = name.Trim();
            column.Kind = kind;
            column.WorkInProgressLimit = workInProgressLimit;
            column.Touch();
            orderedColumns.Remove(column);
            orderedColumns.Insert(Math.Clamp(position, 0, orderedColumns.Count), column);
        }
        else
        {
            column = new ProjectWorkColumn
            {
                TenantId = board.TenantId,
                BoardId = board.Id,
                Name = name.Trim(),
                Kind = kind,
                Position = int.MinValue,
                WorkInProgressLimit = workInProgressLimit,
            };
            orderedColumns.Insert(Math.Clamp(position, 0, orderedColumns.Count), column);
        }

        // The database enforces a unique (BoardId, Position) index. Move persisted
        // rows through unique temporary positions before assigning the final order,
        // otherwise swapping two columns can violate the index mid-update.
        var persistedColumns = orderedColumns.Where(candidate => candidate.Id != column.Id || columnId.HasValue).ToArray();
        for (var index = 0; index < persistedColumns.Length; index++)
        {
            persistedColumns[index].Position = -(index + 1);
        }

        if (persistedColumns.Length > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        for (var index = 0; index < orderedColumns.Count; index++)
        {
            orderedColumns[index].Position = index;
        }

        if (!columnId.HasValue)
        {
            board.Columns.Add(column);
            context.Set<ProjectWorkColumn>().Add(column);
            AddHistory(projectId, null, "ColumnCreated", new { column.Id, column.Name, column.Position });
        }
        else
        {
            AddHistory(projectId, null, "ColumnUpdated", new { column.Id, column.Name, column.Position });
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(column);
    }

    public async Task<Result<bool>> DeleteColumnAsync(Guid projectId, Guid columnId, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(NotFoundProject());
        }

        var column = await context.Set<ProjectWorkColumn>().Include(candidate => candidate.Tasks)
            .SingleOrDefaultAsync(candidate => candidate.Id == columnId && candidate.Board.ProjectId == projectId && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (column == null)
        {
            return Result.Failure<bool>(Error.NotFound("ProjectWork.ColumnNotFound", "Column not found"));
        }

        if (column.Tasks.Any(task => task.DeletedAt == null))
        {
            return Result.Failure<bool>(Error.Conflict("ProjectWork.ColumnNotEmpty", "Move or remove all tasks before deleting the column."));
        }

        context.Set<ProjectWorkColumn>().Remove(column);
        AddHistory(projectId, null, "ColumnDeleted", new { column.Id, column.Name });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<ProjectMilestone>> CreateMilestoneAsync(
        Guid projectId, string name, string? description, DateTime? dueAt, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectMilestone>(NotFoundProject());
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ProjectMilestone>(Error.Validation("ProjectWork.MilestoneNameRequired", "Milestone name is required."));
        }

        var tenantId = await ProjectTenantAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!tenantId.HasValue)
        {
            return Result.Failure<ProjectMilestone>(NotFoundProject());
        }

        var milestone = new ProjectMilestone
        {
            TenantId = tenantId,
            ProjectId = projectId,
            Name = name.Trim(),
            Description = description?.Trim(),
            DueAt = dueAt,
        };
        context.Set<ProjectMilestone>().Add(milestone);
        AddHistory(projectId, null, "MilestoneCreated", new { milestone.Id, milestone.Name });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(milestone);
    }

    public async Task<Result<IReadOnlyList<ProjectMilestone>>> GetMilestonesAsync(
        Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Read, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<IReadOnlyList<ProjectMilestone>>(NotFoundProject());
        }

        var milestones = await context.Set<ProjectMilestone>().AsNoTracking()
            .Where(milestone => milestone.ProjectId == projectId && milestone.DeletedAt == null)
            .OrderBy(milestone => milestone.DueAt)
            .ThenBy(milestone => milestone.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ProjectMilestone>>(milestones);
    }

    public async Task<Result<ProjectMilestone>> UpdateMilestoneAsync(
        Guid projectId, Guid milestoneId, string name, string? description, DateTime? dueAt, DateTime? completedAt, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectMilestone>(NotFoundProject());
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ProjectMilestone>(Error.Validation("ProjectWork.MilestoneNameRequired", "Milestone name is required."));
        }

        var milestone = await context.Set<ProjectMilestone>().SingleOrDefaultAsync(candidate =>
            candidate.Id == milestoneId && candidate.ProjectId == projectId && candidate.DeletedAt == null, cancellationToken).ConfigureAwait(false);
        if (milestone == null)
        {
            return Result.Failure<ProjectMilestone>(Error.NotFound("ProjectWork.MilestoneNotFound", "Milestone not found"));
        }

        milestone.Name = name.Trim();
        milestone.Description = description?.Trim();
        milestone.DueAt = dueAt;
        milestone.CompletedAt = completedAt;
        milestone.Touch();
        AddHistory(projectId, null, "MilestoneUpdated", new { milestone.Id, milestone.Name, milestone.CompletedAt });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(milestone);
    }

    public async Task<Result<bool>> DeleteMilestoneAsync(Guid projectId, Guid milestoneId, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(NotFoundProject());
        }

        var milestone = await context.Set<ProjectMilestone>().SingleOrDefaultAsync(candidate =>
            candidate.Id == milestoneId && candidate.ProjectId == projectId && candidate.DeletedAt == null, cancellationToken).ConfigureAwait(false);
        if (milestone == null)
        {
            return Result.Failure<bool>(Error.NotFound("ProjectWork.MilestoneNotFound", "Milestone not found"));
        }

        foreach (var task in await context.Set<ProjectWorkTask>().Where(task => task.ProjectId == projectId && task.MilestoneId == milestoneId).ToListAsync(cancellationToken).ConfigureAwait(false))
        {
            task.MilestoneId = null;
        }

        context.Set<ProjectMilestone>().Remove(milestone);
        AddHistory(projectId, null, "MilestoneDeleted", new { milestone.Id, milestone.Name });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<ProjectBoard>> GetBoardAsync(
        Guid projectId,
        bool createIfMissing,
        CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Read, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectBoard>(Error.NotFound("ProjectWork.ProjectNotFound", "Project not found"));
        }

        var board = await context.Set<ProjectBoard>()
            .Include(candidate => candidate.Columns.OrderBy(column => column.Position))
            .ThenInclude(column => column.Tasks.OrderBy(task => task.Position))
            .SingleOrDefaultAsync(candidate => candidate.ProjectId == projectId && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (board != null || !createIfMissing)
        {
            return board == null
            ? Result.Failure<ProjectBoard>(Error.NotFound("ProjectWork.BoardNotFound", "Project board not found"))
            : Result.Success(board);
        }

        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectBoard>(Error.Forbidden("ProjectWork.EditRequired", "Project edit access is required"));
        }

        var tenantId = await context.Set<Project>().Where(project => project.Id == projectId)
            .Select(project => project.TenantId).SingleAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == null)
        {
            return Result.Failure<ProjectBoard>(Error.Conflict("ProjectWork.TenantRequired", "Project must belong to a tenant"));
        }

        board = ProjectBoard.Create(tenantId.Value, projectId);
        context.Set<ProjectBoard>().Add(board);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(board);
    }

    public async Task<Result<ProjectWorkTask>> CreateTaskAsync(
        Guid projectId,
        CreateProjectWorkTask request,
        CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectWorkTask>(Error.NotFound("ProjectWork.ProjectNotFound", "Project not found"));
        }

        var boardResult = await GetBoardAsync(projectId, true, cancellationToken).ConfigureAwait(false);
        if (boardResult.IsFailure)
        {
            return Result.Failure<ProjectWorkTask>(boardResult.Error);
        }

        var column = boardResult.Value.Columns.SingleOrDefault(candidate => candidate.Id == request.ColumnId);
        if (column == null)
        {
            return Result.Failure<ProjectWorkTask>(Error.Validation("ProjectWork.ColumnInvalid", "Column does not belong to this board"));
        }

        if (request.AssigneeUserId is { } assignee &&
            !await ProjectWorkAssignmentPolicy.IsEligibleAsync(context, projectId, assignee, SystemClock.UtcNow, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectWorkTask>(Error.Conflict("ProjectWork.AssigneeIneligible", "Assignee must be an active allocated project member"));
        }

        if (column.WorkInProgressLimit is { } limit && column.Tasks.Count(task => task.DeletedAt == null) >= limit)
        {
            return Result.Failure<ProjectWorkTask>(Error.Conflict("ProjectWork.WipLimit", "Column work-in-progress limit reached"));
        }

        var task = new ProjectWorkTask
        {
            TenantId = boardResult.Value.TenantId,
            ProjectId = projectId,
            BoardId = boardResult.Value.Id,
            ColumnId = column.Id,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = request.Priority,
            AssigneeUserId = request.AssigneeUserId,
            MilestoneId = request.MilestoneId,
            DueAt = request.DueAt,
            Position = column.Tasks.Count,
            Status = StatusFor(column.Kind),
            CreatedByUserId = actorContextAccessor.ActorContext.SubjectIdAsGuid!.Value
        };
        context.Set<ProjectWorkTask>().Add(task);
        AddHistory(projectId, task.Id, "TaskCreated", new { task.Title, task.AssigneeUserId });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(task);
    }

    public async Task<Result<ProjectWorkTask>> UpdateTaskAsync(
        Guid projectId, Guid taskId, UpdateProjectWorkTask request, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectWorkTask>(NotFoundProject());
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure<ProjectWorkTask>(Error.Validation("ProjectWork.TaskTitleRequired", "Task title is required."));
        }

        if (request.AssigneeUserId is { } assignee &&
            !await ProjectWorkAssignmentPolicy.IsEligibleAsync(context, projectId, assignee, SystemClock.UtcNow, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectWorkTask>(Error.Conflict("ProjectWork.AssigneeIneligible", "Assignee must be an active allocated project member"));
        }

        if (request.MilestoneId is { } milestoneId && !await context.Set<ProjectMilestone>().AnyAsync(milestone =>
                milestone.Id == milestoneId && milestone.ProjectId == projectId && milestone.DeletedAt == null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectWorkTask>(Error.NotFound("ProjectWork.MilestoneNotFound", "Milestone not found"));
        }

        var task = await context.Set<ProjectWorkTask>().SingleOrDefaultAsync(candidate =>
            candidate.Id == taskId && candidate.ProjectId == projectId && candidate.DeletedAt == null, cancellationToken).ConfigureAwait(false);
        if (task == null)
        {
            return Result.Failure<ProjectWorkTask>(Error.NotFound("ProjectWork.TaskNotFound", "Task not found"));
        }

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.Priority = request.Priority;
        task.AssigneeUserId = request.AssigneeUserId;
        task.MilestoneId = request.MilestoneId;
        task.DueAt = request.DueAt;
        task.Touch();
        AddHistory(projectId, task.Id, "TaskUpdated", new { task.Title, task.Priority, task.AssigneeUserId, task.MilestoneId });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(task);
    }

    public async Task<Result<bool>> DeleteTaskAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(NotFoundProject());
        }

        var task = await context.Set<ProjectWorkTask>().SingleOrDefaultAsync(candidate =>
            candidate.Id == taskId && candidate.ProjectId == projectId && candidate.DeletedAt == null, cancellationToken).ConfigureAwait(false);
        if (task == null)
        {
            return Result.Failure<bool>(Error.NotFound("ProjectWork.TaskNotFound", "Task not found"));
        }

        var dependencies = await context.Set<ProjectTaskDependency>()
            .Where(candidate => candidate.TaskId == taskId || candidate.DependsOnTaskId == taskId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        context.Set<ProjectTaskDependency>().RemoveRange(dependencies);
        context.Set<ProjectWorkTask>().Remove(task);
        AddHistory(projectId, task.Id, "TaskDeleted", new { task.Title });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<ProjectWorkTask>> MoveTaskAsync(
        Guid projectId,
        Guid taskId,
        Guid columnId,
        int position,
        CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectWorkTask>(Error.NotFound("ProjectWork.ProjectNotFound", "Project not found"));
        }

        var task = await context.Set<ProjectWorkTask>().SingleOrDefaultAsync(candidate =>
            candidate.Id == taskId && candidate.ProjectId == projectId && candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (task == null)
        {
            return Result.Failure<ProjectWorkTask>(Error.NotFound("ProjectWork.TaskNotFound", "Task not found"));
        }

        var column = await context.Set<ProjectWorkColumn>().SingleOrDefaultAsync(candidate =>
            candidate.Id == columnId && candidate.BoardId == task.BoardId && candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (column == null)
        {
            return Result.Failure<ProjectWorkTask>(Error.NotFound("ProjectWork.ColumnNotFound", "Target column not found"));
        }

        if (task.ColumnId != column.Id &&
            column.WorkInProgressLimit is { } limit &&
            await context.Set<ProjectWorkTask>().CountAsync(candidate =>
                candidate.ColumnId == column.Id && candidate.DeletedAt == null,
                cancellationToken).ConfigureAwait(false) >= limit)
        {
            return Result.Failure<ProjectWorkTask>(Error.Conflict("ProjectWork.WipLimit", "Column work-in-progress limit reached"));
        }

        if (column.Kind == ProjectWorkColumnKind.Done)
        {
            var dependencyIds = await context.Set<ProjectTaskDependency>().Where(edge => edge.TaskId == task.Id && edge.DeletedAt == null)
                .Select(edge => edge.DependsOnTaskId).ToListAsync(cancellationToken).ConfigureAwait(false);
            var dependencies = await context.Set<ProjectWorkTask>().Where(candidate => dependencyIds.Contains(candidate.Id))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            try { task.Complete(dependencies); }
            catch (InvalidOperationException exception)
            {
                return Result.Failure<ProjectWorkTask>(Error.Conflict("ProjectWork.TaskBlocked", exception.Message));
            }
        }
        else
        {
            task.Status = StatusFor(column.Kind);
            task.CompletedAt = null;
        }
        task.ColumnId = column.Id;
        task.Position = Math.Max(0, position);
        task.Touch();
        AddHistory(projectId, task.Id, "TaskMoved", new { task.ColumnId, task.Position, task.Status });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(task);
    }

    public async Task<Result<ProjectTaskDependency>> AddDependencyAsync(
        Guid projectId,
        Guid taskId,
        Guid dependsOnTaskId,
        CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectTaskDependency>(Error.NotFound("ProjectWork.ProjectNotFound", "Project not found"));
        }

        var validTasks = await context.Set<ProjectWorkTask>().CountAsync(task =>
            (task.Id == taskId || task.Id == dependsOnTaskId) && task.ProjectId == projectId && task.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (validTasks != 2)
        {
            return Result.Failure<ProjectTaskDependency>(Error.NotFound("ProjectWork.TaskNotFound", "Both tasks must belong to the project"));
        }

        var existing = await context.Set<ProjectTaskDependency>().Where(edge => edge.DeletedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        try { ProjectDependencyGraph.EnsureCanAdd(existing, taskId, dependsOnTaskId); }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<ProjectTaskDependency>(Error.Conflict("ProjectWork.CyclicDependency", exception.Message));
        }
        var tenantId = await context.Set<Project>().Where(project => project.Id == projectId).Select(project => project.TenantId)
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        var dependency = new ProjectTaskDependency
        {
            TenantId = tenantId,
            TaskId = taskId,
            DependsOnTaskId = dependsOnTaskId
        };
        context.Set<ProjectTaskDependency>().Add(dependency);
        AddHistory(projectId, taskId, "DependencyAdded", new { dependsOnTaskId });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(dependency);
    }

    public async Task<Result<bool>> RemoveDependencyAsync(
        Guid projectId, Guid taskId, Guid dependencyId, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(NotFoundProject());
        }

        var dependency = await context.Set<ProjectTaskDependency>().SingleOrDefaultAsync(candidate =>
            candidate.Id == dependencyId && candidate.TaskId == taskId &&
            context.Set<ProjectWorkTask>().Any(task => task.Id == taskId && task.ProjectId == projectId), cancellationToken).ConfigureAwait(false);
        if (dependency == null)
        {
            return Result.Failure<bool>(Error.NotFound("ProjectWork.DependencyNotFound", "Dependency not found"));
        }

        context.Set<ProjectTaskDependency>().Remove(dependency);
        AddHistory(projectId, taskId, "DependencyRemoved", new { dependency.DependsOnTaskId });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<ProjectTaskLabel>> CreateLabelAsync(
        Guid projectId, string name, string color, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectTaskLabel>(NotFoundProject());
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(color))
        {
            return Result.Failure<ProjectTaskLabel>(Error.Validation("ProjectWork.LabelInvalid", "Label name and color are required."));
        }

        if (await context.Set<ProjectTaskLabel>().AnyAsync(label => label.ProjectId == projectId && label.Name == name.Trim() && label.DeletedAt == null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectTaskLabel>(Error.Conflict("ProjectWork.LabelExists", "A label with this name already exists."));
        }

        var tenantId = await ProjectTenantAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!tenantId.HasValue)
        {
            return Result.Failure<ProjectTaskLabel>(NotFoundProject());
        }

        var label = new ProjectTaskLabel { TenantId = tenantId, ProjectId = projectId, Name = name.Trim(), Color = color.Trim() };
        context.Set<ProjectTaskLabel>().Add(label);
        AddHistory(projectId, null, "LabelCreated", new { label.Id, label.Name, label.Color });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(label);
    }

    public async Task<Result<IReadOnlyList<ProjectTaskLabel>>> GetLabelsAsync(
        Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Read, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<IReadOnlyList<ProjectTaskLabel>>(NotFoundProject());
        }

        var labels = await context.Set<ProjectTaskLabel>().AsNoTracking()
            .Where(label => label.ProjectId == projectId && label.DeletedAt == null)
            .OrderBy(label => label.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ProjectTaskLabel>>(labels);
    }

    public async Task<Result<bool>> DeleteLabelAsync(Guid projectId, Guid labelId, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(NotFoundProject());
        }

        var label = await context.Set<ProjectTaskLabel>().SingleOrDefaultAsync(candidate => candidate.Id == labelId && candidate.ProjectId == projectId, cancellationToken).ConfigureAwait(false);
        if (label == null)
        {
            return Result.Failure<bool>(Error.NotFound("ProjectWork.LabelNotFound", "Label not found"));
        }

        context.Set<ProjectTaskLabel>().Remove(label);
        AddHistory(projectId, null, "LabelDeleted", new { label.Id, label.Name });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<ProjectTaskLabelAssignment>> AssignLabelAsync(
        Guid projectId, Guid taskId, Guid labelId, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectTaskLabelAssignment>(NotFoundProject());
        }

        var task = await context.Set<ProjectWorkTask>().SingleOrDefaultAsync(candidate => candidate.Id == taskId && candidate.ProjectId == projectId && candidate.DeletedAt == null, cancellationToken).ConfigureAwait(false);
        var label = await context.Set<ProjectTaskLabel>().SingleOrDefaultAsync(candidate => candidate.Id == labelId && candidate.ProjectId == projectId && candidate.DeletedAt == null, cancellationToken).ConfigureAwait(false);
        if (task == null || label == null)
        {
            return Result.Failure<ProjectTaskLabelAssignment>(Error.NotFound("ProjectWork.TaskOrLabelNotFound", "Task or label not found"));
        }

        var existing = await context.Set<ProjectTaskLabelAssignment>().SingleOrDefaultAsync(candidate => candidate.TaskId == taskId && candidate.LabelId == labelId, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            return Result.Success(existing);
        }

        var assignment = new ProjectTaskLabelAssignment { TenantId = task.TenantId, TaskId = taskId, LabelId = labelId };
        context.Set<ProjectTaskLabelAssignment>().Add(assignment);
        AddHistory(projectId, taskId, "LabelAssigned", new { labelId });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(assignment);
    }

    public async Task<Result<bool>> UnassignLabelAsync(Guid projectId, Guid taskId, Guid labelId, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(NotFoundProject());
        }

        var assignment = await context.Set<ProjectTaskLabelAssignment>().SingleOrDefaultAsync(candidate =>
            candidate.TaskId == taskId && candidate.LabelId == labelId &&
            context.Set<ProjectWorkTask>().Any(task => task.Id == taskId && task.ProjectId == projectId), cancellationToken).ConfigureAwait(false);
        if (assignment == null)
        {
            return Result.Failure<bool>(Error.NotFound("ProjectWork.LabelAssignmentNotFound", "Label assignment not found"));
        }

        context.Set<ProjectTaskLabelAssignment>().Remove(assignment);
        AddHistory(projectId, taskId, "LabelUnassigned", new { labelId });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<ProjectTaskComment>> AddCommentAsync(
        Guid projectId,
        Guid taskId,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Read, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectTaskComment>(Error.NotFound("ProjectWork.ProjectNotFound", "Project not found"));
        }

        var task = await context.Set<ProjectWorkTask>().SingleOrDefaultAsync(candidate => candidate.Id == taskId && candidate.ProjectId == projectId, cancellationToken).ConfigureAwait(false);
        if (task == null)
        {
            return Result.Failure<ProjectTaskComment>(Error.NotFound("ProjectWork.TaskNotFound", "Task not found"));
        }

        var comment = new ProjectTaskComment
        {
            TenantId = task.TenantId,
            TaskId = task.Id,
            AuthorUserId = actorContextAccessor.ActorContext.SubjectIdAsGuid!.Value,
            Body = body.Trim()
        };
        context.Set<ProjectTaskComment>().Add(comment);
        AddHistory(projectId, taskId, "CommentAdded", null);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(comment);
    }

    public async Task<Result<ProjectTaskComment>> UpdateCommentAsync(
        Guid projectId, Guid taskId, Guid commentId, string body, CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Read, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectTaskComment>(NotFoundProject());
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure<ProjectTaskComment>(Error.Validation("ProjectWork.CommentBodyRequired", "Comment body is required."));
        }

        var comment = await context.Set<ProjectTaskComment>().SingleOrDefaultAsync(candidate =>
            candidate.Id == commentId && candidate.TaskId == taskId &&
            context.Set<ProjectWorkTask>().Any(task => task.Id == taskId && task.ProjectId == projectId), cancellationToken).ConfigureAwait(false);
        if (comment == null)
        {
            return Result.Failure<ProjectTaskComment>(Error.NotFound("ProjectWork.CommentNotFound", "Comment not found"));
        }

        var actorId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (comment.AuthorUserId != actorId && !await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectTaskComment>(Error.Forbidden("ProjectWork.CommentAuthorRequired", "Only the author or a project editor may edit the comment."));
        }

        comment.Body = body.Trim();
        comment.EditedAt = SystemClock.UtcNow;
        comment.Touch();
        AddHistory(projectId, taskId, "CommentUpdated", new { comment.Id });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(comment);
    }

    public async Task<Result<bool>> DeleteCommentAsync(
        Guid projectId, Guid taskId, Guid commentId, CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Read, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(NotFoundProject());
        }

        var comment = await context.Set<ProjectTaskComment>().SingleOrDefaultAsync(candidate =>
            candidate.Id == commentId && candidate.TaskId == taskId &&
            context.Set<ProjectWorkTask>().Any(task => task.Id == taskId && task.ProjectId == projectId), cancellationToken).ConfigureAwait(false);
        if (comment == null)
        {
            return Result.Failure<bool>(Error.NotFound("ProjectWork.CommentNotFound", "Comment not found"));
        }

        var actorId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (comment.AuthorUserId != actorId && !await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(Error.Forbidden("ProjectWork.CommentAuthorRequired", "Only the author or a project editor may delete the comment."));
        }

        context.Set<ProjectTaskComment>().Remove(comment);
        AddHistory(projectId, taskId, "CommentDeleted", new { comment.Id });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<ProjectTaskChecklistItem>> AddChecklistItemAsync(
        Guid projectId,
        Guid taskId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectTaskChecklistItem>(Error.NotFound("ProjectWork.ProjectNotFound", "Project not found"));
        }

        var task = await context.Set<ProjectWorkTask>().Include(candidate => candidate.Checklist)
            .SingleOrDefaultAsync(candidate => candidate.Id == taskId && candidate.ProjectId == projectId, cancellationToken).ConfigureAwait(false);
        if (task == null)
        {
            return Result.Failure<ProjectTaskChecklistItem>(Error.NotFound("ProjectWork.TaskNotFound", "Task not found"));
        }

        var item = new ProjectTaskChecklistItem
        {
            TenantId = task.TenantId,
            TaskId = task.Id,
            Text = text.Trim(),
            Position = task.Checklist.Count
        };
        context.Set<ProjectTaskChecklistItem>().Add(item);
        AddHistory(projectId, taskId, "ChecklistItemAdded", null);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(item);
    }

    public async Task<Result<ProjectTaskChecklistItem>> SetChecklistCompletionAsync(
        Guid projectId, Guid taskId, Guid itemId, bool completed, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ProjectTaskChecklistItem>(NotFoundProject());
        }

        var item = await context.Set<ProjectTaskChecklistItem>().SingleOrDefaultAsync(candidate =>
            candidate.Id == itemId && candidate.TaskId == taskId &&
            context.Set<ProjectWorkTask>().Any(task => task.Id == taskId && task.ProjectId == projectId), cancellationToken).ConfigureAwait(false);
        if (item == null)
        {
            return Result.Failure<ProjectTaskChecklistItem>(Error.NotFound("ProjectWork.ChecklistItemNotFound", "Checklist item not found"));
        }

        item.IsCompleted = completed;
        item.Touch();
        AddHistory(projectId, taskId, "ChecklistItemUpdated", new { item.Id, item.IsCompleted });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(item);
    }

    public async Task<Result<bool>> DeleteChecklistItemAsync(
        Guid projectId, Guid taskId, Guid itemId, CancellationToken cancellationToken = default)
    {
        if (!await CanEditAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(NotFoundProject());
        }

        var item = await context.Set<ProjectTaskChecklistItem>().SingleOrDefaultAsync(candidate =>
            candidate.Id == itemId && candidate.TaskId == taskId &&
            context.Set<ProjectWorkTask>().Any(task => task.Id == taskId && task.ProjectId == projectId), cancellationToken).ConfigureAwait(false);
        if (item == null)
        {
            return Result.Failure<bool>(Error.NotFound("ProjectWork.ChecklistItemNotFound", "Checklist item not found"));
        }

        context.Set<ProjectTaskChecklistItem>().Remove(item);
        AddHistory(projectId, taskId, "ChecklistItemDeleted", new { item.Id });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }

    public async Task<Result<IReadOnlyList<ProjectWorkHistory>>> GetHistoryAsync(
        Guid projectId, int take, CancellationToken cancellationToken = default)
    {
        if (!await projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Read, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<IReadOnlyList<ProjectWorkHistory>>(NotFoundProject());
        }

        var history = await context.Set<ProjectWorkHistory>().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.DeletedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<ProjectWorkHistory>>(history);
    }

    private void AddHistory(Guid projectId, Guid? taskId, string action, object? changes)
    {
        var actor = actorContextAccessor.ActorContext;
        context.Set<ProjectWorkHistory>().Add(new ProjectWorkHistory
        {
            TenantId = actor.TenantId,
            ProjectId = projectId,
            TaskId = taskId,
            ActorUserId = actor.SubjectIdAsGuid!.Value,
            Action = action,
            ChangesJson = changes == null ? null : JsonSerializer.Serialize(changes)
        });
    }

    private Task<bool> CanEditAsync(Guid projectId, CancellationToken cancellationToken) =>
        projectAuthorizationService.HasPermissionAsync(projectId, PermissionType.Edit, cancellationToken);

    private Task<Guid?> ProjectTenantAsync(Guid projectId, CancellationToken cancellationToken) => context.Set<Project>()
        .Where(project => project.Id == projectId && project.DeletedAt == null)
        .Select(project => project.TenantId)
        .SingleOrDefaultAsync(cancellationToken);

    private static Error NotFoundProject() => Error.NotFound("ProjectWork.ProjectNotFound", "Project not found");

    private static ProjectWorkTaskStatus StatusFor(ProjectWorkColumnKind kind) => kind switch
    {
        ProjectWorkColumnKind.Backlog => ProjectWorkTaskStatus.Backlog,
        ProjectWorkColumnKind.Ready => ProjectWorkTaskStatus.Ready,
        ProjectWorkColumnKind.InProgress => ProjectWorkTaskStatus.InProgress,
        ProjectWorkColumnKind.InReview => ProjectWorkTaskStatus.InReview,
        ProjectWorkColumnKind.Done => ProjectWorkTaskStatus.Done,
        _ => ProjectWorkTaskStatus.Backlog
    };
}
