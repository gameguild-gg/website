using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Teams;
using GameGuild.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using PermissionType = GameGuild.Identity.Authorization.PermissionType;

namespace GameGuild.Projects;

/// <summary> Role constants for project collaborators </summary>
public static class ProjectRoles {
  public const string Owner = "Owner";

  public const string Editor = "Editor";

  public const string Viewer = "Viewer";
}

/// <summary> Command handlers for project operations </summary>
public sealed class ProjectCommandHandlers
  : ICommandHandler<CreateProjectCommand, Result<Project>>,
    ICommandHandler<UpdateProjectCommand, Result<Project>>,
    ICommandHandler<DeleteProjectCommand, Result<bool>>,
    ICommandHandler<PublishProjectCommand, Result<Project>>,
    ICommandHandler<UnpublishProjectCommand, Result<Project>>,
    ICommandHandler<ArchiveProjectCommand, Result<Project>> {
  private readonly IApplicationDbContext _context;

  private readonly ILogger<ProjectCommandHandlers> _logger;

  private readonly IActorContextAccessor _actorContextAccessor;

  private readonly IProjectLifecycleCoordinator _lifecycleCoordinator;

  private readonly IProjectAuthorizationService _authorizationService;

  private readonly IResourceQuotaEnforcer? _quotaEnforcer;
  private readonly GameGuild.CQRS.IMediator? _publicationMediator;

  public ProjectCommandHandlers(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    ILogger<ProjectCommandHandlers> logger,
    IProjectLifecycleCoordinator? lifecycleCoordinator = null,
    IProjectAuthorizationService? authorizationService = null,
    IResourceQuotaEnforcer? quotaEnforcer = null,
    GameGuild.CQRS.IMediator? publicationMediator = null) {
    _context = context;
    _actorContextAccessor = actorContextAccessor;
    _logger = logger;
    _lifecycleCoordinator = lifecycleCoordinator ?? new ProjectLifecycleCoordinator(
      context,
      [new ProjectStoreProductLifecycleParticipant(context)]);
    _authorizationService = authorizationService ?? new ProjectAuthorizationService(context, actorContextAccessor);
    _quotaEnforcer = quotaEnforcer;
    _publicationMediator = publicationMediator;
  }

  private ActorContext Actor => _actorContextAccessor.ActorContext;
  private Guid? UserId => Actor.SubjectIdAsGuid;
  private Guid? TenantId => Actor.TenantId;
  private bool IsAuthenticated => Actor.IsAuthenticated;

  public async Task<Result<Project>> Handle(CreateProjectCommand request, CancellationToken cancellationToken) {
    _logger.LogInformation("Creating project: {Title} by user {UserId}", request.Title, UserId);

    if (!IsAuthenticated || UserId == null) {
      return Result.Failure<Project>(Error.Unauthorized("Project.Unauthenticated", "User must be authenticated"));
    }

    if (TenantId == null || !await _authorizationService.IsActorActiveTenantMemberAsync(cancellationToken).ConfigureAwait(false)) {
      return Result.Failure<Project>(Error.Forbidden("Project.TenantMembershipRequired", "An active tenant membership is required"));
    }

    // Create project entity
    var project = new Project {
      Id = Guid.NewGuid(),
      Title = request.Title,
      Description = request.Description,
      ShortDescription = request.ShortDescription,
      ImageUrl = request.ImageUrl,
      RepositoryUrl = request.RepositoryUrl,
      WebsiteUrl = request.WebsiteUrl,
      DownloadUrl = request.DownloadUrl,
      Type = (GameGuild.Projects.ProjectType)request.Type,
      CategoryId = request.CategoryId,
      Visibility = request.Visibility,
      Status = request.Status
    };
    project.CreatedById = UserId.Value;

    // Set TenantId on the entity directly
    // Tenant authority always comes from the authenticated actor, never from the request body.
    var tenantId = TenantId.Value;
    project.SetTenantId(tenantId);

    // Generate slug from name
    project.Slug = GenerateSlug(request.Title);

    // Ensure slug is unique
    var existingSlugCount = await _context.Set<Project>()
      .Where(p => p.Slug.StartsWith(project.Slug) && p.DeletedAt == null)
      .CountAsync(cancellationToken).ConfigureAwait(false);

    if (existingSlugCount > 0) {
      project.Slug = $"{project.Slug}-{existingSlugCount + 1}";
    }

    Team ownerTeam;
    var teamQuotaConsumed = false;
    if (request.OwnerTeamId is { } ownerTeamId) {
      var canUseOwnerTeam = Actor.IsTenantAdmin || Actor.HasPermission(GameGuild.Identity.Authorization.ProjectPermission.Keys.Admin) || await _context.Set<TeamMember>()
        .AsNoTracking()
        .AnyAsync(member =>
          member.TeamId == ownerTeamId &&
          member.UserId == UserId.Value &&
          member.Authority >= TeamMemberAuthority.Manager &&
          member.IsActive &&
          member.LeftAt == null &&
          member.DeletedAt == null &&
          _context.Set<Team>().Any(team =>
            team.Id == ownerTeamId &&
            team.TenantId == tenantId &&
            team.IsActive &&
            team.DeletedAt == null),
          cancellationToken)
        .ConfigureAwait(false);
      if (!canUseOwnerTeam) {
        return Result.Failure<Project>(Error.Forbidden(
          "Project.OwnerTeamForbidden",
          "Team manager authority is required to create a project for this team"));
      }

      ownerTeam = await _context.Set<Team>().SingleAsync(team => team.Id == ownerTeamId, cancellationToken).ConfigureAwait(false);
    }
    else {
      if (_quotaEnforcer != null) {
        var (allowed, currentUsage, hardLimit) = await _quotaEnforcer.TryAtomicConsumeAsync(
          tenantId,
          ResourceUsageType.Teams,
          cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!allowed) {
          return Result.Failure<Project>(Error.Conflict(
            "Project.TeamQuotaExceeded",
            $"Team quota exceeded. Current usage: {currentUsage}; hard limit: {hardLimit}"));
        }
        teamQuotaConsumed = true;
      }
      var personalSlugBase = $"{project.Slug}-team";
      var personalSlug = personalSlugBase;
      var suffix = 2;
      while (await _context.Set<Team>().AnyAsync(team =>
               team.TenantId == tenantId && team.Slug == personalSlug && team.DeletedAt == null,
               cancellationToken).ConfigureAwait(false)) {
        personalSlug = $"{personalSlugBase}-{suffix++}";
      }
      ownerTeam = Team.Create(tenantId, $"{request.Title} Team", personalSlug, UserId.Value, isPersonal: true);
      _context.Set<Team>().Add(ownerTeam);
    }

    project.SetOwnerTeam(ownerTeam.Id);

    _context.Set<Project>().Add(project);

    // Add the creator as a collaborator with all permissions
    var creatorCollaborator = new ProjectCollaborator {
      Id = Guid.NewGuid(),
      ProjectId = project.Id,
      UserId = UserId!.Value,
      Role = ProjectRoles.Owner,
      Permissions = FormatOwnerPermissions(),
      IsActive = true,
      JoinedAt = SystemClock.UtcNow
    };
    _context.Set<ProjectCollaborator>().Add(creatorCollaborator);

    try {
      await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
    catch {
      if (teamQuotaConsumed && _quotaEnforcer != null) {
        await _quotaEnforcer.DecrementUsageAsync(
          tenantId,
          ResourceUsageType.Teams,
          cancellationToken: cancellationToken).ConfigureAwait(false);
      }
      throw;
    }

    _logger.LogInformation("Project created successfully: {ProjectId}", project.Id);

    return Result.Success(project);
  }

  public async Task<Result<Project>> Handle(UpdateProjectCommand request, CancellationToken cancellationToken) {
    _logger.LogInformation("Updating project: {ProjectId} by user {UserId}", request.ProjectId, UserId);

    var project = await _context.Set<Project>()
      .Include(p => p.Collaborators)
      .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.DeletedAt == null, cancellationToken)
      .ConfigureAwait(false);

    if (project == null) {
      return Result.Failure<Project>(Error.NotFound("Project.NotFound", $"Project with ID {request.ProjectId} was not found"));
    }

    if (!await _authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false)) {
      return Result.Failure<Project>(Error.NotFound("Project.NotFound", "Project not found"));
    }

    // Update fields
    if (request.Title != null)
        {
            project.Title = request.Title;
        }

        if (request.Description != null)
        {
            project.Description = request.Description;
        }

        if (request.ShortDescription != null)
        {
            project.ShortDescription = request.ShortDescription;
        }

        if (request.ImageUrl != null)
        {
            project.ImageUrl = request.ImageUrl;
        }

        if (request.RepositoryUrl != null)
        {
            project.RepositoryUrl = request.RepositoryUrl;
        }

        if (request.WebsiteUrl != null)
        {
            project.WebsiteUrl = request.WebsiteUrl;
        }

        if (request.DownloadUrl != null)
        {
            project.DownloadUrl = request.DownloadUrl;
        }

        if (request.Type.HasValue)
        {
            project.Type = (GameGuild.Projects.ProjectType)request.Type.Value;
        }

        if (request.CategoryId.HasValue)
        {
            project.CategoryId = request.CategoryId;
        }

        if (request.Visibility.HasValue)
        {
            project.Visibility = request.Visibility.Value;
        }

        if (request.Status.HasValue)
        {
            project.Status = request.Status.Value;
        }

        project.Touch();

    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    _logger.LogInformation("Project updated successfully: {ProjectId}", project.Id);

    return Result.Success(project);
  }

  public async Task<Result<bool>> Handle(DeleteProjectCommand request, CancellationToken cancellationToken) {
    _logger.LogInformation("Deleting project: {ProjectId} by user {UserId}", request.ProjectId, UserId);

    var project = await _context.Set<Project>()
      .Include(p => p.Collaborators)
      .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.DeletedAt == null, cancellationToken)
      .ConfigureAwait(false);

    if (project == null) {
      return Result.Failure<bool>(Error.NotFound("Project.NotFound", $"Project with ID {request.ProjectId} was not found"));
    }

    if (!await _authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Delete, cancellationToken).ConfigureAwait(false)) {
      return Result.Failure<bool>(Error.NotFound("Project.NotFound", "Project not found"));
    }

    if (!request.SoftDelete && !await HasSensitiveActionAssuranceAsync(cancellationToken).ConfigureAwait(false)) {
      return Result.Failure<bool>(Error.Forbidden(
        "Project.ReauthenticationRequired",
        "Recent authentication and verified MFA, when enabled, are required for permanent deletion"));
    }

    if (!await _lifecycleCoordinator.DeleteAsync(request.ProjectId, request.SoftDelete, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<bool>(Error.NotFound("Project.NotFound", $"Project with ID {request.ProjectId} was not found"));
        }

        _logger.LogInformation("Project deleted successfully: {ProjectId}", project.Id);

    return Result.Success(true);
  }

  public async Task<Result<Project>> Handle(PublishProjectCommand request, CancellationToken cancellationToken) {
    var project = await _context.Set<Project>()
      .Include(p => p.Collaborators)
      .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.DeletedAt == null, cancellationToken)
      .ConfigureAwait(false);

    if (project == null) {
      return Result.Failure<Project>(Error.NotFound("Project.NotFound", $"Project with ID {request.ProjectId} was not found"));
    }

    if (!await _authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Publish, cancellationToken).ConfigureAwait(false)) {
      return Result.Failure<Project>(Error.NotFound("Project.NotFound", "Project not found"));
    }

    project.Status = ContentStatus.Published;
    project.Touch();

    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    if (_publicationMediator is not null) {
      await _publicationMediator.Send(new GameGuild.Announcements.Contracts.AnnouncePublicationCommand {
        Kind = GameGuild.Announcements.Contracts.PublicationKind.ProjectPublished,
        ActorId = request.PublishedBy,
        Title = project.Title,
        EntityId = project.Id,
        Slug = project.Slug,
        TenantId = project.TenantId,
      }, cancellationToken).ConfigureAwait(false);
    }

    return Result.Success(project);
  }

  public async Task<Result<Project>> Handle(UnpublishProjectCommand request, CancellationToken cancellationToken) {
    var project = await _context.Set<Project>()
      .Include(p => p.Collaborators)
      .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.DeletedAt == null, cancellationToken)
      .ConfigureAwait(false);

    if (project == null) {
      return Result.Failure<Project>(Error.NotFound("Project.NotFound", $"Project with ID {request.ProjectId} was not found"));
    }

    if (!await _authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Unpublish, cancellationToken).ConfigureAwait(false)) {
      return Result.Failure<Project>(Error.NotFound("Project.NotFound", "Project not found"));
    }

    project.Status = ContentStatus.Draft;
    project.Touch();

    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    return Result.Success(project);
  }

  public async Task<Result<Project>> Handle(ArchiveProjectCommand request, CancellationToken cancellationToken) {
    var project = await _context.Set<Project>()
      .Include(p => p.Collaborators)
      .FirstOrDefaultAsync(p => p.Id == request.ProjectId && p.DeletedAt == null, cancellationToken)
      .ConfigureAwait(false);

    if (project == null) {
      return Result.Failure<Project>(Error.NotFound("Project.NotFound", $"Project with ID {request.ProjectId} was not found"));
    }

    if (!await _authorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Archive, cancellationToken).ConfigureAwait(false)) {
      return Result.Failure<Project>(Error.NotFound("Project.NotFound", "Project not found"));
    }

    project.Status = ContentStatus.Archived;
    project.Touch();

    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    return Result.Success(project);
  }

  private static string GenerateSlug(string name) { return Project.GenerateSlug(name); }

  private async Task<bool> HasSensitiveActionAssuranceAsync(CancellationToken cancellationToken) {
    if (Actor.TypedAttributes.AuthenticatedAt is not { } authenticatedAt ||
        authenticatedAt < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(15)) ||
        UserId is not { } actorId)
        {
            return false;
        }

        var hasMfa = await _context.Set<UserMfaConfiguration>().AsNoTracking().AnyAsync(configuration =>
      configuration.UserId == actorId && configuration.IsEnabled && configuration.IsSetupComplete,
      cancellationToken).ConfigureAwait(false);
    return !hasMfa || Actor.IsMfaVerified;
  }

  /// <summary> Format permissions for an owner collaborator </summary>
  private static string FormatOwnerPermissions() {
    var ownerPermissions = new[] {
      PermissionType.Read, PermissionType.Edit, PermissionType.Delete, PermissionType.Publish, PermissionType.Unpublish, PermissionType.Archive, PermissionType.Create, PermissionType.Approve, PermissionType.Manage,
    };

    return string.Join(",", ownerPermissions.Select(p => p.ToString()));
  }

  /// <summary> Format permissions for an editor collaborator </summary>
  private static string FormatEditorPermissions() {
    var editorPermissions = new[] { PermissionType.Read, PermissionType.Edit, PermissionType.Comment, PermissionType.Submit };

    return string.Join(",", editorPermissions.Select(p => p.ToString()));
  }
}
