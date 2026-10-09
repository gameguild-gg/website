using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Projects;

/// <summary>
/// Service implementation for core Project CRUD operations, queries, and tenant management.
/// </summary>
public class ProjectCrudService(
    IApplicationDbContext context,
    IProjectAuthorizationService authorizationService,
    IActorContextAccessor actorContextAccessor,
    IProjectLifecycleCoordinator? lifecycleCoordinator = null) : IProjectCrudService
{
    private readonly IProjectLifecycleCoordinator _lifecycleCoordinator = lifecycleCoordinator ??
        new ProjectLifecycleCoordinator(context, [new ProjectStoreProductLifecycleParticipant(context)]);

    #region Deleted Projects

    public async Task<IEnumerable<Project>> GetDeletedProjectsAsync()
    {
        return await authorizationService.ApplyWorkspaceAccess(context.Set<Project>().IgnoreQueryFilters(), includeDeleted: true)
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.DeletedAt != null)
            .OrderByDescending(p => p.DeletedAt)
            .ToListAsync();
    }

    #endregion

    #region Basic CRUD Operations

    public async Task<Project?> GetProjectByIdAsync(Guid id)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.DeletedAt == null)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project?> GetProjectByIdWithDetailsAsync(Guid id)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy!)
            .Include(p => p.Category)
            .Include(p => p.Collaborators)
            .ThenInclude(c => c.User)
            .Include(p => p.Releases)
            .Include(p => p.Versions)
            .Include(p => p.ProjectMetadata)
            .Include(p => p.Teams)
            .ThenInclude(t => t.Team!)
            .ThenInclude(t => t.Members)
            .Include(p => p.Followers)
            .ThenInclude(f => f.User)
            .Include(p => p.Feedbacks.Where(f => f.Status == ContentStatus.Published))
            .ThenInclude(f => f.User)
            .Include(p => p.JamSubmissions)
            .ThenInclude(js => js.Jam)
            .Where(p => p.DeletedAt == null)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project?> GetProjectBySlugAsync(string slug)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.DeletedAt == null)
            .FirstOrDefaultAsync(p => p.Slug == slug);
    }

    public async Task<IEnumerable<Project>> GetProjectsAsync(int skip = 0, int take = 50)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetProjectsOptimizedAsync(int skip = 0, int take = 50)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Where(p => p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetAllProjectsAsync()
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    #endregion

    #region Filtered Queries

    public async Task<IEnumerable<Project>> GetProjectsByCategoryAsync(Guid categoryId)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.CategoryId == categoryId && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetProjectsByCreatorAsync(Guid creatorId)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.CreatedById == creatorId && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetProjectsByStatusAsync(ContentStatus status)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.Status == status && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetProjectsByTypeAsync(ProjectType type)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.Type == type && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetProjectsByDevelopmentStatusAsync(DevelopmentStatus status)
    {
        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.DevelopmentStatus == status && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> GetPublicProjectsAsync(int skip = 0, int take = 50)
    {
        return await context.Set<Project>()
            .IgnoreQueryFilters()
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.Status == ContentStatus.Published &&
                        p.Visibility == ContentVisibility.Public &&
                        p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IEnumerable<Project>> SearchProjectsAsync(string searchTerm, int skip = 0, int take = 50)
    {
        var lowerSearchTerm = searchTerm.ToLower();

        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.DeletedAt == null &&
                        (p.Title.ToLower().Contains(lowerSearchTerm) ||
                         p.Description != null && p.Description.ToLower().Contains(lowerSearchTerm) ||
                         p.ShortDescription != null && p.ShortDescription.ToLower().Contains(lowerSearchTerm)))
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    #endregion

    #region Create, Update, Delete

    public async Task<Project> CreateProjectAsync(Project project)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!await authorizationService.IsActorActiveTenantMemberAsync().ConfigureAwait(false) ||
            actor.SubjectIdAsGuid is not { } actorId || actor.TenantId is not { } tenantId ||
            project.TenantId != tenantId || project.CreatedById != actorId)
        {
            throw new UnauthorizedAccessException("An active tenant member may only create their own Project in the selected tenant.");
        }

        project.Touch();

        if (string.IsNullOrEmpty(project.Slug))
        {
            project.Slug = Project.GenerateSlug(project.Title);
        }

        if (project.Status == default(ContentStatus))
        {
            project.Status = ContentStatus.Draft;
        }

        if (project.Visibility == default(ContentVisibility))
        {
            project.Visibility = ContentVisibility.Private;
        }

        context.Set<Project>().Add(project);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return project;
    }

    public async Task<Project> UpdateProjectAsync(Project project)
    {
        if (!await authorizationService.HasPermissionAsync(project.Id, PermissionType.Edit).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException("Project edit permission is required.");
        }

        var existingProject = await GetProjectByIdAsync(project.Id).ConfigureAwait(false);

        if (existingProject == null)
        {
            throw new InvalidOperationException($"Project with ID {project.Id} not found");
        }

        existingProject.Title = project.Title;
        existingProject.Description = project.Description;
        existingProject.ShortDescription = project.ShortDescription;
        existingProject.ImageUrl = project.ImageUrl;
        existingProject.Type = project.Type;
        existingProject.DevelopmentStatus = project.DevelopmentStatus;
        existingProject.CategoryId = project.CategoryId;
        existingProject.WebsiteUrl = project.WebsiteUrl;
        existingProject.RepositoryUrl = project.RepositoryUrl;
        existingProject.DownloadUrl = project.DownloadUrl;
        existingProject.SocialLinks = project.SocialLinks;
        existingProject.Tags = project.Tags;
        existingProject.Status = project.Status;
        existingProject.Visibility = project.Visibility;
        existingProject.Touch();

        await context.SaveChangesAsync().ConfigureAwait(false);

        return existingProject;
    }

    public async Task<bool> DeleteProjectAsync(Guid id)
    {
        if (!await authorizationService.HasPermissionAsync(id, PermissionType.Delete).ConfigureAwait(false))
        {
            return false;
        }

        var project = await GetProjectByIdAsync(id).ConfigureAwait(false);

        if (project == null)
        {
            return false;
        }

        return await _lifecycleCoordinator.DeleteAsync(id, softDelete: true).ConfigureAwait(false);
    }

    public async Task<bool> RestoreProjectAsync(Guid id)
    {
        if (!await authorizationService.HasPermissionIncludingDeletedAsync(id, PermissionType.Restore).ConfigureAwait(false))
        {
            return false;
        }

        var project = await context.Set<Project>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && p.DeletedAt != null);

        if (project == null)
        {
            return false;
        }

        project.Restore();
        project.Touch();

        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    #endregion

    #region Tenant Integration

    public async Task<IEnumerable<Project>> GetProjectsByTenantAsync(Guid tenantId, int skip = 0, int take = 50)
    {
        if (actorContextAccessor.ActorContext.TenantId != tenantId)
        {
            return [];
        }

        return await authorizationService.ApplyReadAccess(context.Set<Project>())
            .Include(p => p.CreatedBy)
            .Include(p => p.Category)
            .Where(p => p.TenantId == tenantId && p.DeletedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<bool> MoveProjectToTenantAsync(Guid projectId, Guid? tenantId)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!tenantId.HasValue || actor.TenantId != tenantId ||
            !await authorizationService.HasPermissionAsync(projectId, PermissionType.Manage).ConfigureAwait(false))
        {
            return false;
        }

        var project = await context.Set<Project>().FirstOrDefaultAsync(candidate => candidate.Id == projectId && candidate.DeletedAt == null).ConfigureAwait(false);

        if (project == null)
        {
            return false;
        }

        project.SetTenantId(tenantId.Value);
        project.Touch();
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    #endregion
}
