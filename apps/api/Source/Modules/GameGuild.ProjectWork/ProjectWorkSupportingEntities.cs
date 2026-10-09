using System.ComponentModel.DataAnnotations;

namespace GameGuild.ProjectWork;

public sealed class ProjectMilestone : EntityBase
{
    public Guid ProjectId { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<ProjectWorkTask> Tasks { get; set; } = new List<ProjectWorkTask>();
}

public sealed class ProjectTaskDependency : EntityBase
{
    public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }
}

public sealed class ProjectTaskLabel : EntityBase
{
    public Guid ProjectId { get; set; }
    [Required, MaxLength(80)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Color { get; set; } = "#64748b";
}

public sealed class ProjectTaskLabelAssignment : EntityBase
{
    public Guid TaskId { get; set; }
    public Guid LabelId { get; set; }
}

public sealed class ProjectWorkHistory : EntityBase
{
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid ActorUserId { get; set; }
    [Required, MaxLength(100)] public string Action { get; set; } = string.Empty;
    [MaxLength(10000)] public string? ChangesJson { get; set; }
}

public static class ProjectDependencyGraph
{
    public static void EnsureCanAdd(
        IEnumerable<ProjectTaskDependency> existing,
        Guid taskId,
        Guid dependsOnTaskId)
    {
        if (taskId == dependsOnTaskId)
        {
            throw new InvalidOperationException("Task dependencies cannot be cyclic.");
        }

        var graph = existing.GroupBy(edge => edge.TaskId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.DependsOnTaskId).ToArray());
        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        pending.Push(dependsOnTaskId);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current == taskId)
            {
                throw new InvalidOperationException("Task dependencies cannot be cyclic.");
            }

            if (!graph.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var candidate in next)
            {
                pending.Push(candidate);
            }
        }
    }
}
