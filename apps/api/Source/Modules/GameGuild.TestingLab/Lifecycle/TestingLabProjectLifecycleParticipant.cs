using GameGuild.Projects;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.TestingLab;

public sealed class TestingLabProjectLifecycleParticipant(IApplicationDbContext context)
    : IProjectLifecycleParticipant
{
    public async Task CloseAsync(
        Guid projectId,
        DateTime closedAt,
        CancellationToken cancellationToken = default)
    {
        var projectVersionIds = await context.Set<ProjectVersion>()
            .IgnoreQueryFilters()
            .Where(version => version.ProjectId == projectId)
            .Select(version => version.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var testingRequests = await context.Set<TestingRequest>()
            .IgnoreQueryFilters()
            .Where(request =>
                request.ProjectVersionId.HasValue &&
                projectVersionIds.Contains(request.ProjectVersionId.Value) &&
                request.DeletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var request in testingRequests)
        {
            request.DeletedAt = closedAt;
            request.Touch();
        }

        var projectLinks = await context.Set<SessionProject>()
            .Where(link =>
                link.ProjectId == projectId &&
                link.IsActive &&
                link.DeletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (projectLinks.Count == 0)
        {
            return;
        }

        var sessionIds = projectLinks.Select(link => link.SessionId).Distinct().ToArray();
        var remainingCounts = await context.Set<SessionProject>()
            .Where(link =>
                sessionIds.Contains(link.SessionId) &&
                link.ProjectId != projectId &&
                link.IsActive &&
                link.DeletedAt == null)
            .GroupBy(link => link.SessionId)
            .Select(group => new { SessionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.SessionId, entry => entry.Count, cancellationToken)
            .ConfigureAwait(false);
        var sessions = await context.Set<TestingSession>()
            .Where(session => sessionIds.Contains(session.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var link in projectLinks)
        {
            link.IsActive = false;
            link.DeletedAt = closedAt;
            link.Touch();
        }

        foreach (var session in sessions)
        {
            session.RegisteredProjectCount = remainingCounts.GetValueOrDefault(session.Id);
            session.Touch();
        }
    }

    public async Task RemoveAsync(
        Guid projectId,
        DateTime removedAt,
        CancellationToken cancellationToken = default)
    {
        var projectLinks = await context.Set<SessionProject>()
            .IgnoreQueryFilters()
            .Where(link => link.ProjectId == projectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (projectLinks.Count == 0)
        {
            return;
        }

        var sessionIds = projectLinks.Select(link => link.SessionId).Distinct().ToArray();
        var remainingCounts = await context.Set<SessionProject>()
            .IgnoreQueryFilters()
            .Where(link =>
                sessionIds.Contains(link.SessionId) &&
                link.ProjectId != projectId &&
                link.IsActive &&
                link.DeletedAt == null)
            .GroupBy(link => link.SessionId)
            .Select(group => new { SessionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.SessionId, entry => entry.Count, cancellationToken)
            .ConfigureAwait(false);
        var sessions = await context.Set<TestingSession>()
            .Where(session => sessionIds.Contains(session.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        context.Set<SessionProject>().RemoveRange(projectLinks);
        foreach (var session in sessions)
        {
            session.RegisteredProjectCount = remainingCounts.GetValueOrDefault(session.Id);
            session.Touch();
        }
    }
}
