using Microsoft.EntityFrameworkCore;

namespace GameGuild.Projects;

public sealed class ProjectChannelAvailabilityService(IApplicationDbContext context) : IProjectChannelAvailabilityService
{
    public async Task<ProjectChannelAvailability> GetAsync(
        Guid projectId,
        ProjectChannel channel,
        Guid? tenantId,
        bool requirePublicVisibility = false,
        CancellationToken cancellationToken = default)
    {
        var project = await context.Set<Project>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken)
            .ConfigureAwait(false);

        if (project == null)
        {
            return Unavailable(projectId, channel, ProjectChannelReasonCodes.ProjectNotFound);
        }

        if (project.DeletedAt != null)
        {
            return Unavailable(projectId, channel, ProjectChannelReasonCodes.ProjectSoftDeleted);
        }

        if (project.TenantId != tenantId)
        {
            return Unavailable(projectId, channel, ProjectChannelReasonCodes.TenantMismatch);
        }

        if (channel is ProjectChannel.TestingLab or ProjectChannel.LaunchPad &&
            project.Status is ContentStatus.Archived or ContentStatus.Deleted)
        {
            return Unavailable(projectId, channel, ProjectChannelReasonCodes.LifecycleUnavailable);
        }

        if (channel == ProjectChannel.Store || channel == ProjectChannel.Projects && requirePublicVisibility)
        {
            if (project.Status != ContentStatus.Published)
            {
                return Unavailable(projectId, channel, ProjectChannelReasonCodes.NotPublished);
            }

            if (project.Visibility != ContentVisibility.Public)
            {
                return Unavailable(projectId, channel, ProjectChannelReasonCodes.NotPublic);
            }
        }

        return new ProjectChannelAvailability(projectId, channel, true, ProjectChannelReasonCodes.Available);
    }

    private static ProjectChannelAvailability Unavailable(Guid projectId, ProjectChannel channel, string reason)
        => new(projectId, channel, false, reason);
}
