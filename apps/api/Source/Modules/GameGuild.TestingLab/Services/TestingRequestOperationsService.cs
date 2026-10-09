using GameGuild.Projects;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using ProjectEntity = GameGuild.Projects.Project;
using ProjectReleaseEntity = GameGuild.Projects.ProjectRelease;

namespace GameGuild.TestingLab;

/// <summary>
/// Service implementation for testing request operations.
/// Extracted from the monolithic TestService for focused responsibility.
/// </summary>
public class TestingRequestOperationsService(
    IApplicationDbContext context,
    IProjectChannelAvailabilityService availabilityService,
    IProjectAuthorizationService authorizationService,
    IActorContextAccessor actorContextAccessor,
    IProjectLifecycleLock? lifecycleLock = null) : ITestingRequestOperations
{
    private readonly IProjectLifecycleLock _lifecycleLock = lifecycleLock ?? new ProjectLifecycleLock(context);

    public async Task<IEnumerable<TestingRequest>> GetAllTestingRequestsAsync()
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        return await context.Set<TestingRequest>()
            .Where(tr => tr.TenantId == tenantId && tr.DeletedAt == null)
            .Include(tr => tr.ProjectVersion)
            .ThenInclude(pv => pv!.Project)
            .Include(tr => tr.CreatedBy)
            .OrderByDescending(tr => tr.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingRequest>> GetTestingRequestsAsync(
        int skip = 0,
        int take = 50,
        bool includeArchived = false)
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        return await context.Set<TestingRequest>()
            .IgnoreQueryFilters()
            .Where(tr =>
                tr.TenantId == tenantId &&
                (includeArchived || tr.DeletedAt == null))
            .Include(tr => tr.ProjectVersion)
            .ThenInclude(pv => pv!.Project)
            .Include(tr => tr.CreatedBy)
            .OrderByDescending(tr => tr.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<TestingRequest?> GetTestingRequestByIdAsync(Guid id)
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        return await context.Set<TestingRequest>()
            .Where(tr => tr.Id == id && tr.TenantId == tenantId && tr.DeletedAt == null)
            .Include(tr => tr.ProjectVersion)
            .ThenInclude(pv => pv!.Project)
            .Include(tr => tr.CreatedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<TestingRequest?> GetTestingRequestByIdWithDetailsAsync(Guid id)
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        return await context.Set<TestingRequest>()
            .Where(tr => tr.Id == id && tr.TenantId == tenantId && tr.DeletedAt == null)
            .Include(tr => tr.ProjectVersion)
            .ThenInclude(pv => pv!.Project)
            .Include(tr => tr.CreatedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<TestingRequest> CreateTestingRequestAsync(TestingRequest testingRequest)
    {
        var actor = RequireTenantActor();
        IProjectLifecycleLockHandle? lockHandle = null;
        if (testingRequest.ProjectVersionId.HasValue)
        {
            lockHandle = await AcquireProjectVersionLockAsync(
                    testingRequest.ProjectVersionId.Value,
                    actor.TenantId!.Value)
                .ConfigureAwait(false);
        }

        await using var lockScope = lockHandle;
        testingRequest.Id = Guid.NewGuid();
        testingRequest.CreatedById = actor.SubjectIdAsGuid!.Value;
        testingRequest.TenantId = actor.TenantId;
        testingRequest.Touch();

        context.Set<TestingRequest>().Add(testingRequest);
        await context.SaveChangesAsync().ConfigureAwait(false);
        if (lockHandle != null)
        {
            await lockHandle.CommitAsync().ConfigureAwait(false);
        }

        return (await GetTestingRequestByIdAsync(testingRequest.Id).ConfigureAwait(false)) ?? testingRequest;
    }

    public async Task<TestingRequest> UpdateTestingRequestAsync(TestingRequest testingRequest)
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        var existingRequest = await context.Set<TestingRequest>()
            .FirstOrDefaultAsync(request => request.Id == testingRequest.Id && request.TenantId == tenantId)
            .ConfigureAwait(false);

        if (existingRequest == null)
        {
            throw new InvalidOperationException($"Testing request with ID {testingRequest.Id} not found.");
        }

        existingRequest.Title = testingRequest.Title;
        existingRequest.Description = testingRequest.Description;
        existingRequest.InstructionsType = testingRequest.InstructionsType;
        existingRequest.InstructionsContent = testingRequest.InstructionsContent;
        existingRequest.InstructionsUrl = testingRequest.InstructionsUrl;
        existingRequest.InstructionsFileId = testingRequest.InstructionsFileId;
        existingRequest.MaxTesters = testingRequest.MaxTesters;
        existingRequest.StartDate = testingRequest.StartDate;
        existingRequest.EndDate = testingRequest.EndDate;
        existingRequest.Status = testingRequest.Status;
        existingRequest.Touch();

        await context.SaveChangesAsync().ConfigureAwait(false);

        return (await GetTestingRequestByIdAsync(existingRequest.Id).ConfigureAwait(false)) ?? existingRequest;
    }

    public async Task<bool> DeleteTestingRequestAsync(Guid id)
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        var testingRequest = await context.Set<TestingRequest>()
            .FirstOrDefaultAsync(request => request.Id == id && request.TenantId == tenantId)
            .ConfigureAwait(false);

        if (testingRequest == null)
        {
            return false;
        }

        testingRequest.SoftDelete();
        await context.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }

    public async Task<bool> RestoreTestingRequestAsync(Guid id)
    {
        var actor = RequireTenantActor();
        var testingRequest = await context.Set<TestingRequest>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(tr => tr.Id == id && tr.TenantId == actor.TenantId);

        if (testingRequest == null)
        {
            return false;
        }

        IProjectLifecycleLockHandle? lockHandle = null;
        if (testingRequest.ProjectVersionId.HasValue)
        {
            lockHandle = await AcquireProjectVersionLockAsync(
                    testingRequest.ProjectVersionId.Value,
                    actor.TenantId!.Value)
                .ConfigureAwait(false);
        }

        await using var lockScope = lockHandle;
        testingRequest.Restore();
        testingRequest.Touch();
        await context.SaveChangesAsync().ConfigureAwait(false);
        if (lockHandle != null)
        {
            await lockHandle.CommitAsync().ConfigureAwait(false);
        }

        return true;
    }

    public async Task<IEnumerable<TestingRequest>> GetTestingRequestsByProjectVersionAsync(Guid projectVersionId)
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        return await context.Set<TestingRequest>()
            .Where(tr => tr.ProjectVersionId == projectVersionId && tr.TenantId == tenantId && tr.DeletedAt == null)
            .Include(tr => tr.ProjectVersion)
            .ThenInclude(pv => pv!.Project)
            .Include(tr => tr.CreatedBy)
            .OrderByDescending(tr => tr.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingRequest>> GetTestingRequestsByCreatorAsync(Guid creatorId)
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        return await context.Set<TestingRequest>()
            .Where(tr => tr.CreatedById == creatorId && tr.TenantId == tenantId && tr.DeletedAt == null)
            .Include(tr => tr.ProjectVersion)
            .ThenInclude(pv => pv!.Project)
            .Include(tr => tr.CreatedBy)
            .OrderByDescending(tr => tr.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingRequest>> GetTestingRequestsByStatusAsync(TestingRequestStatus status)
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        return await context.Set<TestingRequest>()
            .Where(tr => tr.Status == status && tr.TenantId == tenantId && tr.DeletedAt == null)
            .Include(tr => tr.ProjectVersion)
            .ThenInclude(pv => pv!.Project)
            .Include(tr => tr.CreatedBy)
            .OrderByDescending(tr => tr.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingRequest>> SearchTestingRequestsAsync(string searchTerm)
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        var lowerSearchTerm = searchTerm.ToLower();

        return await context.Set<TestingRequest>()
            .Where(tr => tr.TenantId == tenantId && tr.DeletedAt == null &&
                (tr.Title.ToLower().Contains(lowerSearchTerm) ||
                 tr.Description != null && tr.Description.ToLower().Contains(lowerSearchTerm)))
            .Include(tr => tr.ProjectVersion)
            .ThenInclude(pv => pv!.Project)
            .Include(tr => tr.CreatedBy)
            .OrderByDescending(tr => tr.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingRequest>> GetActiveTestingRequestsAsync()
    {
        var tenantId = RequireTenantActor().TenantId!.Value;
        return await context.Set<TestingRequest>()
            .Where(tr => tr.TenantId == tenantId && tr.DeletedAt == null && tr.Status == TestingRequestStatus.Open)
            .Include(tr => tr.ProjectVersion)
            .ThenInclude(pv => pv!.Project)
            .Include(tr => tr.CreatedBy)
            .OrderByDescending(tr => tr.CreatedAt)
            .ToListAsync();
    }

    public async Task<TestingRequest> CreateSimpleTestingRequestAsync(CreateSimpleTestingRequestDto requestDto, Guid userId)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid != userId || actor.TenantId == null)
        {
            throw new UnauthorizedAccessException("An authenticated tenant actor matching the submission user is required.");
        }

        ProjectEntity? existingProject;
        if (requestDto.ProjectId.HasValue)
        {
            existingProject = await context.Set<ProjectEntity>()
                .FirstOrDefaultAsync(project =>
                    project.Id == requestDto.ProjectId.Value &&
                    project.TenantId == actor.TenantId &&
                    project.DeletedAt == null)
                .ConfigureAwait(false);
        }
        else
        {
            var matchingProjects = await context.Set<ProjectEntity>()
                .Where(project =>
                    project.Title == requestDto.TeamIdentifier &&
                    project.TenantId == actor.TenantId &&
                    project.DeletedAt == null)
                .Take(2)
                .ToListAsync()
                .ConfigureAwait(false);
            if (matchingProjects.Count > 1)
            {
                throw new InvalidOperationException("Multiple active projects match the legacy team identifier.");
            }

            existingProject = matchingProjects.SingleOrDefault();
        }

        if (existingProject == null)
        {
            throw new InvalidOperationException("Testing Lab submissions must be linked to an existing project.");
        }

        var projectId = existingProject.Id;
        if (!await authorizationService.HasPermissionAsync(projectId, PermissionType.Edit).ConfigureAwait(false))
        {
            throw new KeyNotFoundException("Project not found.");
        }

        await using var lockHandle = await _lifecycleLock.AcquireAsync(projectId).ConfigureAwait(false);
        var availability = await availabilityService
            .GetAsync(projectId, ProjectChannel.TestingLab, actor.TenantId)
            .ConfigureAwait(false);
        if (!availability.IsAvailable)
        {
            throw new InvalidOperationException(availability.Reason);
        }

        var projectVersion = await context.Set<GameGuild.Projects.ProjectVersion>()
            .FirstOrDefaultAsync(version =>
                version.ProjectId == projectId &&
                version.TenantId == actor.TenantId &&
                version.DeletedAt == null &&
                version.VersionNumber == requestDto.VersionNumber)
            .ConfigureAwait(false);

        if (projectVersion == null)
        {
            projectVersion = GameGuild.Projects.ProjectVersion.Create(
                projectId,
                requestDto.VersionNumber,
                requestDto.Description,
                userId,
                existingProject.TenantId);
            projectVersion.MarkReadyForTesting();

            context.Set<GameGuild.Projects.ProjectVersion>().Add(projectVersion);
        }

        var projectRelease = await context.Set<GameGuild.Projects.ProjectRelease>()
            .FirstOrDefaultAsync(release =>
                release.ProjectId == projectId &&
                release.DeletedAt == null &&
                release.ReleaseVersion == requestDto.VersionNumber)
            .ConfigureAwait(false);

        if (projectRelease == null)
        {
            projectRelease = new ProjectReleaseEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = $"{existingProject.Title} {requestDto.VersionNumber}",
                ReleaseVersion = requestDto.VersionNumber,
                ReleaseNotes = requestDto.Description ?? "",
                DownloadUrl = requestDto.DownloadUrl,
                IsPrerelease = true,
                ReleaseType = "testing",
                ReleasedAt = SystemClock.UtcNow,
                TenantId = existingProject.TenantId,
            };

            context.Set<GameGuild.Projects.ProjectRelease>().Add(projectRelease);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        var testingRequest = new TestingRequest
        {
            Id = Guid.NewGuid(),
            ProjectVersionId = projectVersion.Id,
            Title = requestDto.Title,
            Description = requestDto.Description,
            DownloadUrl = requestDto.DownloadUrl,
            InstructionsType = requestDto.InstructionsType,
            InstructionsContent = requestDto.InstructionsContent,
            InstructionsUrl = requestDto.InstructionsUrl,
            FeedbackFormContent = requestDto.FeedbackFormContent,
            MaxTesters = requestDto.MaxTesters,
            StartDate = requestDto.StartDate ?? SystemClock.UtcNow,
            EndDate = requestDto.EndDate ?? SystemClock.UtcNow.AddDays(30),
            Status = TestingRequestStatus.Draft,
            CreatedById = userId,
            TenantId = existingProject.TenantId,
        };

        context.Set<TestingRequest>().Add(testingRequest);
        await context.SaveChangesAsync().ConfigureAwait(false);
        await lockHandle.CommitAsync().ConfigureAwait(false);

        return (await GetTestingRequestByIdAsync(testingRequest.Id).ConfigureAwait(false)) ?? testingRequest;
    }

    private ActorContext RequireTenantActor()
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid == null || actor.TenantId == null)
        {
            throw new UnauthorizedAccessException("An authenticated tenant actor is required.");
        }

        return actor;
    }

    private async Task<IProjectLifecycleLockHandle> AcquireProjectVersionLockAsync(
        Guid projectVersionId,
        Guid tenantId)
    {
        var projectId = await context.Set<GameGuild.Projects.ProjectVersion>()
            .IgnoreQueryFilters()
            .Where(version =>
                version.Id == projectVersionId &&
                version.TenantId == tenantId &&
                version.DeletedAt == null)
            .Select(version => version.ProjectId)
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);
        if (projectId == Guid.Empty)
        {
            throw new InvalidOperationException("Testing requests must reference an active project version in the current tenant.");
        }

        var lockHandle = await _lifecycleLock.AcquireAsync(projectId).ConfigureAwait(false);
        try
        {
            var versionIsActive = await context.Set<GameGuild.Projects.ProjectVersion>()
                .IgnoreQueryFilters()
                .AnyAsync(version =>
                    version.Id == projectVersionId &&
                    version.ProjectId == projectId &&
                    version.TenantId == tenantId &&
                    version.DeletedAt == null)
                .ConfigureAwait(false);
            if (!versionIsActive)
            {
                throw new InvalidOperationException("Testing requests must reference an active project version in the current tenant.");
            }

            if (!await authorizationService.HasPermissionAsync(projectId, PermissionType.Edit).ConfigureAwait(false))
            {
                throw new KeyNotFoundException("Project not found.");
            }

            var availability = await availabilityService
                .GetAsync(projectId, ProjectChannel.TestingLab, tenantId)
                .ConfigureAwait(false);
            if (!availability.IsAvailable)
            {
                throw new InvalidOperationException(availability.Reason);
            }

            return lockHandle;
        }
        catch
        {
            await lockHandle.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
