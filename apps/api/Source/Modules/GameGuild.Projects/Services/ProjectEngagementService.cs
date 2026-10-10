using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Teams;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Projects;

/// <summary>
/// Service implementation for project engagement: teams, followers, feedback, jams, and analytics.
/// </summary>
public class ProjectEngagementService(
    IApplicationDbContext context,
    IProjectAuthorizationService authorizationService,
    IActorContextAccessor actorContextAccessor) : IProjectEngagementService
{
    #region Team Integration

    public async Task<ProjectTeam> AddTeamToProjectAsync(Guid projectId, Guid teamId, string role, string? permissions = null)
    {
        await RequireProjectPermissionAsync(projectId, PermissionType.Manage).ConfigureAwait(false);
        var projectTenantId = await context.Set<Project>().Where(project => project.Id == projectId && project.DeletedAt == null)
            .Select(project => project.TenantId).SingleAsync().ConfigureAwait(false);
        if (!await context.Set<Team>().AnyAsync(team => team.Id == teamId && team.TenantId == projectTenantId && team.IsActive && team.DeletedAt == null).ConfigureAwait(false))
        {
            throw new ArgumentException("Team not found in the Project tenant.", nameof(teamId));
        }

        var projectTeam = new ProjectTeam
        {
            ProjectId = projectId,
            TeamId = teamId,
            Role = Enum.TryParse<ProjectTeamRole>(role, true, out var parsedRole)
                ? parsedRole
                : throw new ArgumentException("Unknown project team role.", nameof(role)),
            Permissions = permissions,
            AssignedAt = SystemClock.UtcNow,
            IsActive = true
        };

        context.Set<ProjectTeam>().Add(projectTeam);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return projectTeam;
    }

    public async Task<bool> RemoveTeamFromProjectAsync(Guid projectId, Guid teamId)
    {
        await RequireProjectPermissionAsync(projectId, PermissionType.Manage).ConfigureAwait(false);
        var projectTeam = await context.Set<ProjectTeam>()
            .FirstOrDefaultAsync(pt => pt.ProjectId == projectId && pt.TeamId == teamId);

        if (projectTeam == null)
        {
            return false;
        }

        projectTeam.IsActive = false;
        projectTeam.EndedAt = SystemClock.UtcNow;
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<ProjectTeam>> GetProjectTeamsAsync(Guid projectId)
    {
        await RequireProjectPermissionAsync(projectId, PermissionType.Read).ConfigureAwait(false);
        return await context.Set<ProjectTeam>()
            .Include(pt => pt.Team!)
            .ThenInclude(t => t.Members)
            .Where(pt => pt.ProjectId == projectId && pt.IsActive)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetProjectsByTeamAsync(Guid teamId)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(project => project.CreatedBy)
            .Include(project => project.Category)
            .Where(project => project.Teams.Any(team => team.TeamId == teamId && team.IsActive && team.DeletedAt == null && team.EndedAt == null))
            .ToListAsync();
    }

    #endregion

    #region Follower Integration

    public async Task<ProjectFollower> FollowProjectAsync(Guid projectId, Guid userId, bool emailNotifications = true, bool pushNotifications = true)
    {
        await RequireActorAsync(userId).ConfigureAwait(false);
        await RequireProjectPermissionAsync(projectId, PermissionType.Read).ConfigureAwait(false);
        var existing = await context.Set<ProjectFollower>()
            .FirstOrDefaultAsync(pf => pf.ProjectId == projectId && pf.UserId == userId);

        if (existing != null)
        {
            return existing;
        }

        var follower = new ProjectFollower
        {
            ProjectId = projectId,
            UserId = userId,
            FollowedAt = SystemClock.UtcNow,
            EmailNotifications = emailNotifications,
            PushNotifications = pushNotifications
        };

        context.Set<ProjectFollower>().Add(follower);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return follower;
    }

    public async Task<bool> UnfollowProjectAsync(Guid projectId, Guid userId)
    {
        await RequireActorAsync(userId).ConfigureAwait(false);
        await RequireProjectPermissionAsync(projectId, PermissionType.Read).ConfigureAwait(false);
        var follower = await context.Set<ProjectFollower>()
            .FirstOrDefaultAsync(pf => pf.ProjectId == projectId && pf.UserId == userId);

        if (follower == null)
        {
            return false;
        }

        context.Set<ProjectFollower>().Remove(follower);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<bool> IsUserFollowingProjectAsync(Guid projectId, Guid userId)
    {
        if (actorContextAccessor.ActorContext.SubjectIdAsGuid != userId ||
            !await authorizationService.HasPermissionAsync(projectId, PermissionType.Read).ConfigureAwait(false))
        {
            return false;
        }

        return await context.Set<ProjectFollower>()
            .AnyAsync(pf => pf.ProjectId == projectId && pf.UserId == userId);
    }

    public async Task<IEnumerable<ProjectFollower>> GetProjectFollowersAsync(Guid projectId)
    {
        await RequireProjectPermissionAsync(projectId, PermissionType.Read).ConfigureAwait(false);
        return await context.Set<ProjectFollower>()
            .Include(pf => pf.User)
            .Where(pf => pf.ProjectId == projectId)
            .OrderBy(pf => pf.FollowedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetProjectsFollowedByUserAsync(Guid userId)
    {
        await RequireActorAsync(userId).ConfigureAwait(false);
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(project => project.CreatedBy)
            .Include(project => project.Category)
            .Where(project => project.Followers.Any(follower => follower.UserId == userId))
            .ToListAsync();
    }

    #endregion

    #region Feedback Integration

    public async Task<ProjectFeedback> AddProjectFeedbackAsync(Guid projectId, Guid userId, int rating, string title, string? content = null)
    {
        await RequireActorAsync(userId).ConfigureAwait(false);
        await RequireProjectPermissionAsync(projectId, PermissionType.Read).ConfigureAwait(false);
        var existing = await context.Set<ProjectFeedback>()
            .FirstOrDefaultAsync(pf => pf.ProjectId == projectId && pf.UserId == userId);

        if (existing != null)
        {
            existing.Rating = rating;
            existing.Title = title;
            existing.Content = content;
            existing.Touch();
            await context.SaveChangesAsync().ConfigureAwait(false);

            return existing;
        }

        var feedback = new ProjectFeedback
        {
            ProjectId = projectId,
            UserId = userId,
            Rating = rating,
            Title = title,
            Content = content,
            Status = ContentStatus.Published
        };

        context.Set<ProjectFeedback>().Add(feedback);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return feedback;
    }

    public async Task<ProjectFeedback> UpdateProjectFeedbackAsync(Guid feedbackId, int rating, string title, string? content = null)
    {
        var feedback = await context.Set<ProjectFeedback>().FirstOrDefaultAsync(candidate => candidate.Id == feedbackId).ConfigureAwait(false);

        if (feedback == null)
        {
            throw new ArgumentException("Feedback not found", nameof(feedbackId));
        }

        var actorId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (actorId != feedback.UserId &&
            !await authorizationService.HasPermissionAsync(feedback.ProjectId, PermissionType.Edit).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException("Only the feedback author or a Project editor may update feedback.");
        }

        feedback.Rating = rating;
        feedback.Title = title;
        feedback.Content = content;
        feedback.Touch();

        await context.SaveChangesAsync().ConfigureAwait(false);

        return feedback;
    }

    public async Task<bool> DeleteProjectFeedbackAsync(Guid feedbackId)
    {
        var feedback = await context.Set<ProjectFeedback>().FirstOrDefaultAsync(candidate => candidate.Id == feedbackId).ConfigureAwait(false);

        if (feedback == null)
        {
            return false;
        }

        var actorId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (actorId != feedback.UserId &&
            !await authorizationService.HasPermissionAsync(feedback.ProjectId, PermissionType.Edit).ConfigureAwait(false))
        {
            return false;
        }

        context.Set<ProjectFeedback>().Remove(feedback);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<ProjectFeedback>> GetProjectFeedbackAsync(Guid projectId, int skip = 0, int take = 50)
    {
        await RequireProjectPermissionAsync(projectId, PermissionType.Read).ConfigureAwait(false);
        return await context.Set<ProjectFeedback>()
            .Include(pf => pf.User)
            .Where(pf => pf.ProjectId == projectId && pf.Status == ContentStatus.Published)
            .OrderByDescending(pf => pf.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<ProjectFeedback?> GetUserFeedbackForProjectAsync(Guid projectId, Guid userId)
    {
        if (actorContextAccessor.ActorContext.SubjectIdAsGuid != userId &&
            !await authorizationService.HasPermissionAsync(projectId, PermissionType.Edit).ConfigureAwait(false))
        {
            return null;
        }

        await RequireProjectPermissionAsync(projectId, PermissionType.Read).ConfigureAwait(false);
        return await context.Set<ProjectFeedback>()
            .Include(pf => pf.User)
            .FirstOrDefaultAsync(pf => pf.ProjectId == projectId && pf.UserId == userId);
    }

    #endregion

    #region Jam Integration

    public async Task<ProjectJamSubmission> SubmitProjectToJamAsync(Guid projectId, Guid jamId, string? submissionNotes = null)
    {
        await RequireProjectPermissionAsync(projectId, PermissionType.Edit).ConfigureAwait(false);
        var existing = await context.Set<ProjectJamSubmission>()
            .FirstOrDefaultAsync(pjs => pjs.ProjectId == projectId && pjs.JamId == jamId);

        if (existing != null)
        {
            return existing;
        }

        var submission = new ProjectJamSubmission
        {
            ProjectId = projectId,
            JamId = jamId,
            SubmittedAt = SystemClock.UtcNow,
            SubmissionNotes = submissionNotes,
            IsEligible = true
        };

        context.Set<ProjectJamSubmission>().Add(submission);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return submission;
    }

    public async Task<bool> RemoveProjectFromJamAsync(Guid projectId, Guid jamId)
    {
        if (!await authorizationService.HasPermissionAsync(projectId, PermissionType.Edit).ConfigureAwait(false))
        {
            return false;
        }

        var submission = await context.Set<ProjectJamSubmission>()
            .FirstOrDefaultAsync(pjs => pjs.ProjectId == projectId && pjs.JamId == jamId);

        if (submission == null)
        {
            return false;
        }

        context.Set<ProjectJamSubmission>().Remove(submission);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<IEnumerable<ProjectJamSubmission>> GetProjectJamSubmissionsAsync(Guid projectId)
    {
        await RequireProjectPermissionAsync(projectId, PermissionType.Read).ConfigureAwait(false);
        return await context.Set<ProjectJamSubmission>()
            .Include(pjs => pjs.Jam)
            .Include(pjs => pjs.Scores)
            .Where(pjs => pjs.ProjectId == projectId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetProjectsByJamAsync(Guid jamId)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(project => project.CreatedBy)
            .Include(project => project.Category)
            .Where(project => project.JamSubmissions.Any(submission => submission.JamId == jamId))
            .ToListAsync();
    }

    #endregion

    #region Analytics and Statistics

    public async Task<ProjectStatistics> GetProjectStatisticsAsync(Guid projectId)
    {
        await RequireProjectPermissionAsync(projectId, PermissionType.Read).ConfigureAwait(false);
        var project = await context.Set<Project>()
            .Include(p => p.Followers)
            .Include(p => p.Feedbacks)
            .Include(p => p.Teams)
            .Include(p => p.Collaborators)
            .Include(p => p.Releases)
            .Include(p => p.JamSubmissions)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            throw new ArgumentException("Project not found", nameof(projectId));
        }

        var thirtyDaysAgo = SystemClock.UtcNow.AddDays(-30);

        return new ProjectStatistics
        {
            ProjectId = projectId,
            FollowerCount = project.Followers.Count,
            FeedbackCount = project.Feedbacks.Count(f => f.Status == ContentStatus.Published),
            AverageRating = project.Feedbacks.Any(f => f.Status == ContentStatus.Published)
                ? (decimal?)project.Feedbacks.Where(f => f.Status == ContentStatus.Published).Average(f => f.Rating)
                : null,
            TotalDownloads = project.Releases.Sum(r => r.DownloadCount),
            ActiveTeamCount = project.Teams.Count(t => t.IsActive),
            CollaboratorCount = project.Collaborators.Count,
            ReleaseCount = project.Releases.Count,
            JamSubmissionCount = project.JamSubmissions.Count,
            AwardCount = project.JamSubmissions.Count(js => js.HasAward),
            NewFollowersLast30Days = project.Followers.Count(f => f.FollowedAt >= thirtyDaysAgo),
            CalculatedAt = SystemClock.UtcNow,
            TrendingScore = CalculateTrendingScore(project, thirtyDaysAgo),
        };
    }

    public async Task<IEnumerable<Project>> GetTrendingProjectsAsync(int take = 10, TimeSpan? timeWindow = null)
    {
        var cutoffDate = SystemClock.UtcNow.Subtract(timeWindow ?? TimeSpan.FromDays(7));

        var projects = await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Include(p => p.Followers)
            .Include(p => p.Feedbacks)
            .Include(p => p.Releases)
            .Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published)
            .ToListAsync();

        return projects
            .OrderByDescending(p => CalculateTrendingScore(p, cutoffDate))
            .Take(take)
            .ToList();
    }

    public async Task<IEnumerable<Project>> GetPopularProjectsAsync(int take = 10)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Include(p => p.Releases)
            .Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published)
            .OrderByDescending(p => p.Releases.Sum(r => r.DownloadCount))
            .ThenByDescending(p => p.Followers.Count)
            .Take(take)
            .ToListAsync();
    }

    private async Task RequireProjectPermissionAsync(Guid projectId, PermissionType permission)
    {
        if (!await authorizationService.HasPermissionAsync(projectId, permission).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException($"Project {permission} permission is required.");
        }
    }

    private async Task RequireActorAsync(Guid userId)
    {
        if (actorContextAccessor.ActorContext.SubjectIdAsGuid != userId ||
            !await authorizationService.IsActorActiveTenantMemberAsync().ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException("The authenticated active tenant member must match the requested user.");
        }
    }

    private static decimal CalculateTrendingScore(Project project, DateTime cutoffDate)
    {
        var recentFollowers = project.Followers.Count(f => f.FollowedAt >= cutoffDate);
        var recentFeedback = project.Feedbacks.Count(f => f.CreatedAt >= cutoffDate);
        var totalDownloads = project.Releases.Sum(r => r.DownloadCount);
        var averageRating = project.Feedbacks.Count != 0
            ? project.Feedbacks.Where(f => f.Status == ContentStatus.Published).Average(f => f.Rating)
            : 0;

        return (decimal)(recentFollowers * 2.0 + recentFeedback * 1.5 + totalDownloads * 0.001 + averageRating * 0.5);
    }

    #endregion
}
