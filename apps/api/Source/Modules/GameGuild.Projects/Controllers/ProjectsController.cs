using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using PermissionType = GameGuild.Identity.Authorization.PermissionType;


namespace GameGuild.Projects;

/// <summary> REST API controller for managing projects using CQRS pattern </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/projects")]
[Authorize]
public class ProjectsController : BaseApiController {
  private static readonly TimeSpan RecentAuthenticationWindow = TimeSpan.FromMinutes(15);
  private readonly ILogger<ProjectsController> _logger;

  private readonly IMediator _mediator;

  private readonly IActorContextAccessor _actorContextAccessor;

  private readonly IApplicationDbContext _context;

  private readonly IProjectAuthorizationService _authorizationService;

  public ProjectsController(IMediator mediator, IActorContextAccessor actorContextAccessor, IApplicationDbContext context, IProjectAuthorizationService authorizationService, ILogger<ProjectsController> logger) {
    _mediator = mediator;
    _actorContextAccessor = actorContextAccessor;
    _context = context;
    _authorizationService = authorizationService;
    _logger = logger;
  }

  /// <summary> Get all projects with filtering and pagination </summary>
  /// <remarks>
  /// Use query parameters to filter results:
  /// - `featured=true` to get featured projects
  /// - `popular=true` to get popular projects (sorted by popularity score)
  /// - `recent=true` to get recently created/updated projects
  /// - `sortBy=CreatedAt` with `sortDirection=DESC` for manual sorting
  /// </remarks>
  [HttpGet]
  [AllowAnonymous]
  public async Task<ActionResult<IEnumerable<ProjectApiResponse>>> GetProjects(
    [FromQuery] ProjectType? type = null,
    [FromQuery] ContentStatus? status = null,
    [FromQuery] ContentVisibility? visibility = null,
    [FromQuery] Guid? creatorId = null,
    [FromQuery] Guid? categoryId = null,
    [FromQuery] string? searchTerm = null,
    [FromQuery] bool? featured = null,
    [FromQuery] bool? popular = null,
    [FromQuery] bool? recent = null,
    [FromQuery] bool currentTenantOnly = false,
    [FromQuery] bool includeArchived = false,
    [FromQuery] int skip = 0,
    [FromQuery] int take = 50,
    [FromQuery] string? sortBy = "CreatedAt",
    [FromQuery] string? sortDirection = "DESC"
  ) {
    var actor = _actorContextAccessor.ActorContext;
    var mayManageProjects = actor.IsTenantAdmin || actor.HasPermission(GameGuild.Identity.Authorization.ProjectPermission.Keys.Admin);
    var query = new GetAllProjectsQuery {
      Type = type,
      Status = status,
      Visibility = visibility,
      CreatorId = creatorId,
      CategoryId = categoryId,
      SearchTerm = searchTerm,
      Featured = featured,
      Popular = popular,
      Recent = recent,
      CurrentTenantOnly = currentTenantOnly,
      IncludeArchived = includeArchived && mayManageProjects,
      Skip = skip,
      Take = Math.Min(take, 100), // Limit max items
      SortBy = sortBy,
      SortDirection = sortDirection,
    };

    var projects = await _mediator.Send(query).ConfigureAwait(false);

    return projects.IsSuccess
      ? Ok(projects.Value.Select(ProjectApiResponse.FromProject))
      : ToActionResult(Result.Failure<IEnumerable<ProjectApiResponse>>(projects.Error));
  }

  /// <summary>
  /// Gets the Projects that belong to the authenticated user's actual workspace relationship.
  /// This scope intentionally does not expand for tenant or system administrators.
  /// </summary>
  [HttpGet("mine")]
  public async Task<ActionResult<IReadOnlyList<ProjectApiResponse>>> GetMyProjects(
    [FromQuery] bool includeArchived = false,
    [FromQuery] int skip = 0,
    [FromQuery] int take = 50,
    CancellationToken cancellationToken = default) {
    if (!_actorContextAccessor.ActorContext.IsAuthenticated)
      return Unauthorized();

    var source = _context.Set<Project>().AsNoTracking();
    if (!includeArchived)
      source = source.Where(project => project.Status != ContentStatus.Archived && project.Status != ContentStatus.Deleted);
    var projects = await _authorizationService.ApplyPersonalAccess(source)
      .Include(project => project.CreatedBy)
      .Include(project => project.Category)
      .Include(project => project.Versions)
      .Include(project => project.Collaborators)
      .Include(project => project.Releases)
      .Include(project => project.Teams)
        .ThenInclude(team => team.Team)
      .OrderByDescending(project => project.UpdatedAt)
      .Skip(Math.Max(skip, 0))
      .Take(Math.Clamp(take, 1, 100))
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);

    return Ok(projects.Select(ProjectApiResponse.FromProject).ToArray());
  }

  [HttpGet("accessible-versions")]
  public async Task<ActionResult<IReadOnlyList<ProjectVersionOptionProjection>>> GetAccessibleProjectVersions(
    [FromQuery] int take = 200) {
    if (!_actorContextAccessor.ActorContext.IsAuthenticated) return Unauthorized();
    var projects = _authorizationService.ApplyWorkspaceAccess(
      _context.Set<Project>().AsNoTracking().Where(project =>
        project.DeletedAt == null &&
        project.Status != ContentStatus.Archived &&
        project.Status != ContentStatus.Deleted));
    var accessibleProjects = await projects
      .Select(project => new { project.Id, project.Title })
      .ToListAsync()
      .ConfigureAwait(false);
    var projectIds = accessibleProjects.Select(project => project.Id).ToArray();
    var projectTitles = accessibleProjects.ToDictionary(project => project.Id, project => project.Title);
    var rows = await _context.Set<ProjectVersion>().AsNoTracking()
      .Where(version => projectIds.Contains(version.ProjectId) && version.DeletedAt == null)
      .OrderByDescending(version => version.UpdatedAt)
      .Take(Math.Clamp(take, 1, 500))
      .ToListAsync()
      .ConfigureAwait(false);
    var versions = rows.Select(version => new ProjectVersionOptionProjection(
      version.Id,
      version.ProjectId,
      projectTitles.GetValueOrDefault(version.ProjectId, string.Empty),
      version.VersionNumber,
      version.Status,
      version.UpdatedAt)).ToList();
    return Ok(versions);
  }

  /// <summary> Get project by ID </summary>
  [HttpGet("{id:guid}")]
  [AllowAnonymous]
  public async Task<ActionResult<ProjectApiResponse>> GetProject(Guid id, [FromQuery] bool includeTeam = true, [FromQuery] bool includeReleases = true, [FromQuery] bool includeCollaborators = true, [FromQuery] bool includeStatistics = false) {
    var query = new GetProjectByIdQuery { ProjectId = id, IncludeTeam = includeTeam, IncludeReleases = includeReleases, IncludeCollaborators = includeCollaborators, IncludeStatistics = includeStatistics };

    var project = await _mediator.Send(query).ConfigureAwait(false);

    if (project.IsFailure) return ToActionResult(Result.Failure<ProjectApiResponse>(project.Error));
    if (project.Value == null) { return NotFound(); }

    return Ok(ProjectApiResponse.FromProject(project.Value));
  }

  [HttpGet("{id:guid}/versions")]
  public async Task<ActionResult<IReadOnlyList<ProjectVersionApiResponse>>> GetProjectVersions(Guid id) {
    if (!await _authorizationService.HasPermissionAsync(id, PermissionType.Read).ConfigureAwait(false)) return NotFound();
    var versions = await _context.Set<ProjectVersion>()
      .AsNoTracking()
      .Where(version => version.ProjectId == id && version.DeletedAt == null)
      .OrderByDescending(version => version.CreatedAt)
      .ToListAsync()
      .ConfigureAwait(false);
    return Ok(versions.Select(ProjectVersionApiResponse.FromEntity).ToArray());
  }

  [HttpPost("{id:guid}/versions")]
  public async Task<ActionResult<ProjectVersionApiResponse>> CreateProjectVersion(Guid id, [FromBody] CreateProjectVersionRequest request) {
    if (!await _authorizationService.HasPermissionAsync(id, PermissionType.Edit).ConfigureAwait(false)) return NotFound();
    if (string.IsNullOrWhiteSpace(request.VersionNumber))
      return UnprocessableEntity(new { code = "Projects.VersionNumberRequired" });
    var project = await _context.Set<Project>().AsNoTracking()
      .Where(candidate => candidate.Id == id && candidate.DeletedAt == null)
      .Select(candidate => new { candidate.Id, candidate.TenantId })
      .SingleOrDefaultAsync()
      .ConfigureAwait(false);
    if (project == null) return NotFound();
    var normalizedVersion = request.VersionNumber.Trim();
    if (await _context.Set<ProjectVersion>().AnyAsync(version =>
        version.ProjectId == id && version.VersionNumber == normalizedVersion && version.DeletedAt == null).ConfigureAwait(false))
      return Conflict(new { code = "Projects.VersionExists" });
    if (_actorContextAccessor.ActorContext.SubjectIdAsGuid is not { } actorId) return Unauthorized();
    if (request.Status is not null and not ProjectVersionStatus.Draft)
      return UnprocessableEntity(new { code = "Projects.VersionMustStartAsDraft" });
    var version = ProjectVersion.Create(
      id,
      normalizedVersion,
      request.ReleaseNotes,
      actorId,
      project.TenantId);
    _context.Set<ProjectVersion>().Add(version);
    version = await _mediator.Send(new CreateProjectVersionEndpointCommand(version)).ConfigureAwait(false);
    return CreatedAtAction(nameof(GetProjectVersions), new { id }, ProjectVersionApiResponse.FromEntity(version));
  }

  [HttpPut("{id:guid}/versions/{versionId:guid}")]
  public async Task<ActionResult<ProjectVersionApiResponse>> UpdateProjectVersion(
    Guid id,
    Guid versionId,
    [FromBody] UpdateProjectVersionRequest request) {
    if (!await _authorizationService.HasPermissionAsync(id, PermissionType.Edit).ConfigureAwait(false)) return NotFound();
    if (string.IsNullOrWhiteSpace(request.VersionNumber))
      return UnprocessableEntity(new { code = "Projects.VersionNumberRequired" });
    var version = await _context.Set<ProjectVersion>()
      .SingleOrDefaultAsync(candidate => candidate.Id == versionId && candidate.ProjectId == id && candidate.DeletedAt == null)
      .ConfigureAwait(false);
    if (version == null) return NotFound();
    var normalizedVersion = request.VersionNumber.Trim();
    if (await _context.Set<ProjectVersion>().AnyAsync(candidate =>
        candidate.Id != versionId &&
        candidate.ProjectId == id &&
        candidate.VersionNumber == normalizedVersion &&
        candidate.DeletedAt == null).ConfigureAwait(false))
      return Conflict(new { code = "Projects.VersionExists" });

    try {
      version.UpdateDraft(normalizedVersion, request.ReleaseNotes);
      version = await _mediator.Send(new UpdateProjectVersionEndpointCommand(version)).ConfigureAwait(false);
      return Ok(ProjectVersionApiResponse.FromEntity(version));
    }
    catch (InvalidOperationException) {
      return Conflict(new { code = "Projects.VersionImmutable" });
    }
  }

  [HttpPost("{id:guid}/versions/{versionId:guid}:ready")]
  public async Task<ActionResult<ProjectVersionApiResponse>> MarkProjectVersionReady(Guid id, Guid versionId) =>
    await TransitionProjectVersion(id, versionId, PermissionType.Edit, static version => version.MarkReadyForTesting()).ConfigureAwait(false);

  [HttpPost("{id:guid}/versions/{versionId:guid}:release")]
  public async Task<ActionResult<ProjectVersionApiResponse>> ReleaseProjectVersion(Guid id, Guid versionId) =>
    await TransitionProjectVersion(id, versionId, PermissionType.Publish, static version => version.Release()).ConfigureAwait(false);

  [HttpPost("{id:guid}/versions/{versionId:guid}:archive")]
  public async Task<ActionResult<ProjectVersionApiResponse>> ArchiveProjectVersion(Guid id, Guid versionId) =>
    await TransitionProjectVersion(id, versionId, PermissionType.Archive, static version => version.Archive()).ConfigureAwait(false);

  private async Task<ActionResult<ProjectVersionApiResponse>> TransitionProjectVersion(
    Guid projectId,
    Guid versionId,
    PermissionType permission,
    Action<ProjectVersion> transition) {
    if (!await _authorizationService.HasPermissionAsync(projectId, permission).ConfigureAwait(false)) return NotFound();
    var version = await _context.Set<ProjectVersion>()
      .SingleOrDefaultAsync(candidate => candidate.Id == versionId && candidate.ProjectId == projectId && candidate.DeletedAt == null)
      .ConfigureAwait(false);
    if (version == null) return NotFound();

    try {
      transition(version);
      version = await _mediator.Send(new TransitionProjectVersionEndpointCommand(version)).ConfigureAwait(false);
      return Ok(ProjectVersionApiResponse.FromEntity(version));
    }
    catch (InvalidOperationException) {
      return Conflict(new { code = "Projects.InvalidVersionTransition" });
    }
  }

  /// <summary> Get project by slug </summary>
  [HttpGet("slug/{slug}")]
  [AllowAnonymous]
  public async Task<ActionResult<ProjectApiResponse>> GetProjectBySlug(string slug, [FromQuery] bool includeTeam = true, [FromQuery] bool includeReleases = true, [FromQuery] bool includeCollaborators = true) {
    var query = new GetProjectBySlugQuery { Slug = slug, IncludeTeam = includeTeam, IncludeReleases = includeReleases, IncludeCollaborators = includeCollaborators };

    var project = await _mediator.Send(query).ConfigureAwait(false);

    if (project.IsFailure) return ToActionResult(Result.Failure<ProjectApiResponse>(project.Error));
    if (project.Value == null) { return NotFound(); }

    return Ok(ProjectApiResponse.FromProject(project.Value));
  }

  /// <summary> Create a new project </summary>
  [HttpPost]
  public async Task<ActionResult<ProjectApiResponse>> CreateProject([FromBody] CreateProjectRequest request) {
    if (!ModelState.IsValid) { return BadRequest(ModelState); }

    var command = new CreateProjectCommand {
      Title = request.Title,
      Description = request.Description,
      ShortDescription = request.ShortDescription,
      ImageUrl = request.ImageUrl,
      RepositoryUrl = request.RepositoryUrl,
      WebsiteUrl = request.WebsiteUrl,
      DownloadUrl = request.DownloadUrl,
      Type = (GameGuild.ProjectType)request.Type,
      CreatedById = _actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty,
      CategoryId = request.CategoryId,
      Visibility = request.Visibility,
      Status = request.Status,
      Tags = request.Tags,
      TenantId = _actorContextAccessor.ActorContext.TenantId,
      OwnerTeamId = request.OwnerTeamId,
    };

    var result = await _mediator.Send(command).ConfigureAwait(false);

    if (result.IsSuccess)
      return CreatedAtAction(nameof(GetProject), new { id = result.Value.Id }, ProjectApiResponse.FromProject(result.Value));

    return ToActionResult(Result.Failure<ProjectApiResponse>(result.Error));
  }

  /// <summary> Update an existing project </summary>
  [HttpPut("{id:guid}")]
  public async Task<ActionResult<ProjectApiResponse>> UpdateProject(Guid id, [FromBody] UpdateProjectRequest request) {
    var command = new UpdateProjectCommand {
      ProjectId = id,
      Title = request.Title,
      Description = request.Description,
      ShortDescription = request.ShortDescription,
      ImageUrl = request.ImageUrl,
      RepositoryUrl = request.RepositoryUrl,
      WebsiteUrl = request.WebsiteUrl,
      DownloadUrl = request.DownloadUrl,
      Type = (GameGuild.ProjectType?)request.Type,
      CategoryId = request.CategoryId,
      Visibility = request.Visibility,
      Status = request.Status,
      Tags = request.Tags,
      UpdatedBy = _actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty,
    };

    var result = await _mediator.Send(command).ConfigureAwait(false);

    return result.IsSuccess
      ? Ok(ProjectApiResponse.FromProject(result.Value))
      : ToActionResult(Result.Failure<ProjectApiResponse>(result.Error));
  }

  /// <summary> Delete a project </summary>
  [HttpDelete("{id:guid}")]
  public async Task<ActionResult<bool>> DeleteProject(Guid id, [FromQuery] bool softDelete = true, [FromQuery] string? reason = null) {
    if (!softDelete && !await HasSensitiveActionAssuranceAsync().ConfigureAwait(false)) return Forbid();

    var command = new DeleteProjectCommand { ProjectId = id, DeletedBy = _actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty, SoftDelete = softDelete, Reason = reason };

    var result = await _mediator.Send(command).ConfigureAwait(false);

    return ToActionResult(result);
  }

  private async Task<bool> HasSensitiveActionAssuranceAsync() {
    var actor = _actorContextAccessor.ActorContext;
    if (actor.SubjectIdAsGuid is not { } actorId ||
        actor.TypedAttributes.AuthenticatedAt is not { } authenticatedAt ||
        authenticatedAt < DateTimeOffset.UtcNow.Subtract(RecentAuthenticationWindow))
      return false;

    var hasMfa = await _context.Set<UserMfaConfiguration>().AsNoTracking()
      .AnyAsync(configuration => configuration.UserId == actorId && configuration.IsEnabled)
      .ConfigureAwait(false);
    return !hasMfa || actor.IsMfaVerified;
  }

  /// <summary> Publish a project </summary>
  [HttpPost("{id:guid}:publish")]
  public async Task<ActionResult<ProjectApiResponse>> PublishProject(Guid id) {
    var command = new PublishProjectCommand { ProjectId = id, PublishedBy = _actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty };

    var result = await _mediator.Send(command).ConfigureAwait(false);

    return result.IsSuccess
      ? Ok(ProjectApiResponse.FromProject(result.Value))
      : ToActionResult(Result.Failure<ProjectApiResponse>(result.Error));
  }

  /// <summary> Unpublish a project </summary>
  [HttpPost("{id:guid}:unpublish")]
  public async Task<ActionResult<ProjectApiResponse>> UnpublishProject(Guid id) {
    var command = new UnpublishProjectCommand { ProjectId = id, UnpublishedBy = _actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty };

    var result = await _mediator.Send(command).ConfigureAwait(false);

    return result.IsSuccess
      ? Ok(ProjectApiResponse.FromProject(result.Value))
      : ToActionResult(Result.Failure<ProjectApiResponse>(result.Error));
  }

  /// <summary> Archive a project </summary>
  [HttpPost("{id:guid}:archive")]
  public async Task<ActionResult<ProjectApiResponse>> ArchiveProject(Guid id) {
    var command = new ArchiveProjectCommand { ProjectId = id, ArchivedBy = _actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty };

    var result = await _mediator.Send(command).ConfigureAwait(false);

    return result.IsSuccess
      ? Ok(ProjectApiResponse.FromProject(result.Value))
      : ToActionResult(Result.Failure<ProjectApiResponse>(result.Error));
  }

  /// <summary> Restore an archived or soft-deleted project </summary>
  [HttpPost("{id:guid}:restore")]
  public async Task<ActionResult<ProjectApiResponse>> RestoreProject(Guid id, CancellationToken cancellationToken) {
    if (!await _authorizationService
          .HasPermissionIncludingDeletedAsync(id, PermissionType.Restore, cancellationToken)
          .ConfigureAwait(false))
      return NotFound();

    var project = await _context.Set<Project>()
      .IgnoreQueryFilters()
      .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
      .ConfigureAwait(false);
    if (project == null) return NotFound();

    var wasSoftDeleted = project.DeletedAt != null;
    var wasArchived = project.Status is ContentStatus.Archived or ContentStatus.Deleted;
    if (!wasSoftDeleted && !wasArchived)
      return Conflict(new { code = "Project.NotRestorable", message = "Only archived or deleted projects can be restored." });

    if (wasSoftDeleted) project.Restore();
    if (wasArchived) project.Status = ContentStatus.Draft;
    project.Touch();
    project = await _mediator.Send(new RestoreProjectEndpointCommand(project), cancellationToken).ConfigureAwait(false);
    return Ok(ProjectApiResponse.FromProject(project));
  }

  /// <summary> Search projects </summary>
  [HttpGet("search")]
  [AllowAnonymous]
  public async Task<ActionResult<IEnumerable<ProjectApiResponse>>> SearchProjects(
    [FromQuery] string searchTerm,
    [FromQuery] ProjectType? type = null,
    [FromQuery] Guid? categoryId = null,
    [FromQuery] ContentStatus? status = null,
    [FromQuery] ContentVisibility? visibility = null,
    [FromQuery] int skip = 0,
    [FromQuery] int take = 50,
    [FromQuery] string? sortBy = "Relevance",
    [FromQuery] string? sortDirection = "DESC"
  ) {
    var query = new SearchProjectsQuery { SearchTerm = searchTerm, Type = type, CategoryId = categoryId, Status = status, Visibility = visibility, Skip = skip, Take = Math.Min(take, 100), SortBy = sortBy, SortDirection = sortDirection };

    var projects = await _mediator.Send(query).ConfigureAwait(false);

    return projects.IsSuccess
      ? Ok(projects.Value.Select(ProjectApiResponse.FromProject))
      : ToActionResult(Result.Failure<IEnumerable<ProjectApiResponse>>(projects.Error));
  }

  /// <summary> Get popular projects </summary>
  [HttpGet("popular")]
  [AllowAnonymous]
  public async Task<ActionResult<IEnumerable<ProjectApiResponse>>> GetPopularProjects([FromQuery] ProjectType? type = null, [FromQuery] int take = 10) {
    var query = new GetPopularProjectsQuery { Type = type, Take = Math.Min(take, 50) };

    var projects = await _mediator.Send(query).ConfigureAwait(false);

    return projects.IsSuccess
      ? Ok(projects.Value.Select(ProjectApiResponse.FromProject))
      : ToActionResult(Result.Failure<IEnumerable<ProjectApiResponse>>(projects.Error));
  }

  /// <summary> Get recent projects </summary>
  [HttpGet("recent")]
  [AllowAnonymous]
  public async Task<ActionResult<IEnumerable<ProjectApiResponse>>> GetRecentProjects([FromQuery] ProjectType? type = null, [FromQuery] int take = 10) {
    var query = new GetRecentProjectsQuery { Type = type, Take = Math.Min(take, 50) };

    var projects = await _mediator.Send(query).ConfigureAwait(false);

    return projects.IsSuccess
      ? Ok(projects.Value.Select(ProjectApiResponse.FromProject))
      : ToActionResult(Result.Failure<IEnumerable<ProjectApiResponse>>(projects.Error));
  }

  /// <summary> Get featured projects </summary>
  [HttpGet("featured")]
  [AllowAnonymous]
  public async Task<ActionResult<IEnumerable<ProjectApiResponse>>> GetFeaturedProjects([FromQuery] ProjectType? type = null, [FromQuery] int take = 10) {
    var query = new GetFeaturedProjectsQuery { Type = type, Take = Math.Min(take, 50) };

    var projects = await _mediator.Send(query).ConfigureAwait(false);

    return projects.IsSuccess
      ? Ok(projects.Value.Select(ProjectApiResponse.FromProject))
      : ToActionResult(Result.Failure<IEnumerable<ProjectApiResponse>>(projects.Error));
  }

  /// <summary> Get project statistics </summary>
  [HttpGet("{id:guid}/statistics")]
  [AllowAnonymous]
  public async Task<ActionResult<ProjectStatistics>> GetProjectStatistics(Guid id, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null) {
    var query = new GetProjectStatisticsQuery { ProjectId = id, FromDate = fromDate, ToDate = toDate };

    var statistics = await _mediator.Send(query).ConfigureAwait(false);

    return statistics.IsSuccess ? Ok(statistics.Value) : ToActionResult(statistics);
  }

  /// <summary> Get projects by category </summary>
  [HttpGet("category/{categoryId:guid}")]
  [AllowAnonymous]
  public async Task<ActionResult<IEnumerable<ProjectApiResponse>>> GetProjectsByCategory(Guid categoryId, [FromQuery] ContentStatus? status = null, [FromQuery] int skip = 0, [FromQuery] int take = 50) {
    var query = new GetProjectsByCategoryQuery { CategoryId = categoryId, Status = status, Skip = skip, Take = Math.Min(take, 100) };

    var projects = await _mediator.Send(query).ConfigureAwait(false);

    return projects.IsSuccess
      ? Ok(projects.Value.Select(ProjectApiResponse.FromProject))
      : ToActionResult(Result.Failure<IEnumerable<ProjectApiResponse>>(projects.Error));
  }

  /// <summary> Get projects by creator </summary>
  [HttpGet("creator/{creatorId:guid}")]
  [AllowAnonymous]
  public async Task<ActionResult<IEnumerable<ProjectApiResponse>>> GetProjectsByCreator(Guid creatorId, [FromQuery] ContentStatus? status = null, [FromQuery] int skip = 0, [FromQuery] int take = 50) {
    var query = new GetProjectsByCreatorQuery { CreatorId = creatorId, Status = status, Skip = skip, Take = Math.Min(take, 100) };

    var projects = await _mediator.Send(query).ConfigureAwait(false);

    return projects.IsSuccess
      ? Ok(projects.Value.Select(ProjectApiResponse.FromProject))
      : ToActionResult(Result.Failure<IEnumerable<ProjectApiResponse>>(projects.Error));
  }

  /// <summary> Get available role templates for projects </summary>
  [HttpGet("role-templates")]
  [AllowAnonymous]
  public ActionResult<IEnumerable<object>> GetProjectRoleTemplates() {
    // Return predefined project roles
    var roleTemplates = new[] {
      new { Name = "Owner", Description = "Full access to project", Permissions = new[] { PermissionType.Read, PermissionType.Edit, PermissionType.Delete, PermissionType.Create, PermissionType.Share, PermissionType.Comment, PermissionType.Reply, PermissionType.Review, PermissionType.Approve, PermissionType.Publish, PermissionType.Archive, PermissionType.Restore } },
      new { Name = "Admin", Description = "Administrative access", Permissions = new[] { PermissionType.Read, PermissionType.Edit, PermissionType.Delete, PermissionType.Create, PermissionType.Share, PermissionType.Comment, PermissionType.Reply, PermissionType.Review, PermissionType.Approve, PermissionType.Publish, PermissionType.Archive, PermissionType.Restore } },
      new { Name = "Editor", Description = "Can edit project content", Permissions = new[] { PermissionType.Read, PermissionType.Edit, PermissionType.Create, PermissionType.Comment, PermissionType.Reply, PermissionType.Share, PermissionType.Review, PermissionType.Approve, PermissionType.Publish } },
      new { Name = "Viewer", Description = "Read-only access", Permissions = new[] { PermissionType.Read, PermissionType.Comment } },
      new { Name = "Collaborator", Description = "Can collaborate on project", Permissions = new[] { PermissionType.Read, PermissionType.Edit, PermissionType.Comment, PermissionType.Reply, PermissionType.Share, PermissionType.Create } }
    };

    return Ok(roleTemplates);
  }

  /// <summary> Get current user's project invitations </summary>
  [HttpGet("my-invitations")]
  public async Task<ActionResult<IEnumerable<ProjectInvitationDto>>> GetMyProjectInvitations() {
    var actor = _actorContextAccessor.ActorContext;
    var userId = actor.SubjectIdAsGuid;
    if (!userId.HasValue || !actor.TenantId.HasValue) return Unauthorized();
    var email = await _context.Set<User>().AsNoTracking()
      .Where(user => user.Id == userId.Value && user.IsActive && user.DeletedAt == null)
      .Select(user => user.Email)
      .SingleOrDefaultAsync().ConfigureAwait(false);
    var normalizedEmail = email?.Trim().ToLower();

    var invitations = await _context.Set<ProjectInvitation>()
      .AsNoTracking()
      .Include(invitation => invitation.Project)
      .Where(invitation =>
        invitation.TenantId == actor.TenantId &&
        invitation.Status == ProjectInvitationStatus.Pending &&
        (invitation.InvitedUserId == userId.Value ||
         (invitation.InvitedUserId == null && normalizedEmail != null && invitation.InvitedEmail != null &&
          invitation.InvitedEmail.ToLower() == normalizedEmail)))
      .OrderByDescending(invitation => invitation.InvitedAt)
      .ToListAsync()
      .ConfigureAwait(false);

    return Ok(invitations.Select(ProjectInvitationDto.FromInvitation).ToList());
  }

  /// <summary> Get permissions for a specific role </summary>
  [HttpGet("roles/{roleName}/permissions")]
  [AllowAnonymous]
  public ActionResult<IEnumerable<PermissionType>> GetRolePermissions(string roleName) {
    PermissionType[] permissions = roleName.ToLower() switch {
      "owner" => [PermissionType.Read, PermissionType.Edit, PermissionType.Delete, PermissionType.Create, PermissionType.Share, PermissionType.Comment, PermissionType.Reply, PermissionType.Review, PermissionType.Approve, PermissionType.Publish, PermissionType.Archive, PermissionType.Restore],
      "admin" => [PermissionType.Read, PermissionType.Edit, PermissionType.Delete, PermissionType.Create, PermissionType.Share, PermissionType.Comment, PermissionType.Reply, PermissionType.Review, PermissionType.Approve, PermissionType.Publish, PermissionType.Archive, PermissionType.Restore],
      "editor" => [PermissionType.Read, PermissionType.Edit, PermissionType.Create, PermissionType.Comment, PermissionType.Reply, PermissionType.Share, PermissionType.Review, PermissionType.Approve, PermissionType.Publish],
      "viewer" => [PermissionType.Read, PermissionType.Comment],
      "collaborator" => [PermissionType.Read, PermissionType.Edit, PermissionType.Comment, PermissionType.Reply, PermissionType.Share, PermissionType.Create],
      _ => []
    };

    if (permissions.Length == 0) {
      return BadRequest($"Role '{roleName}' not found");
    }

    return Ok(permissions);
  }

  /// <summary> Accept a project invitation </summary>
  [HttpPost("invitations/{invitationToken}:accept")]
  public async Task<ActionResult<ProjectInvitationDto>> AcceptProjectInvitation(string invitationToken) {
    var invitation = await GetRespondableInvitation(invitationToken).ConfigureAwait(false);
    if (invitation == null) return NotFound(new { Message = "Invitation not found" });
    if (!invitation.CanRespond) return Conflict(new { Message = "Invitation is no longer pending or has expired" });

    var actor = _actorContextAccessor.ActorContext;
    var userId = actor.SubjectIdAsGuid;
    if (!userId.HasValue || !await CanRespondToInvitationAsync(invitation, userId.Value).ConfigureAwait(false)) return Forbid();
    if (!TryNormalizeProjectPermissions(invitation.Permissions, [], out var invitationPermissions))
      return UnprocessableEntity(new { Code = "Projects.InvalidProjectPermissions" });

    invitation.Accept();

    var collaborator = await _context.Set<ProjectCollaborator>()
      .FirstOrDefaultAsync(c => c.ProjectId == invitation.ProjectId && c.UserId == userId.Value)
      .ConfigureAwait(false);

    if (collaborator == null) {
      collaborator = new ProjectCollaborator {
        TenantId = invitation.Project.TenantId,
        ProjectId = invitation.ProjectId,
        UserId = userId.Value,
        Role = invitation.Role,
        Permissions = invitationPermissions,
        IsActive = true,
        JoinedAt = SystemClock.UtcNow
      };
      _context.Set<ProjectCollaborator>().Add(collaborator);
    }
    else {
      collaborator.Role = invitation.Role;
      collaborator.Permissions = invitationPermissions;
      collaborator.IsActive = true;
      collaborator.LeftAt = null;
    }

    invitation = await _mediator.Send(new AcceptProjectInvitationEndpointCommand(invitation)).ConfigureAwait(false);

    return Ok(ProjectInvitationDto.FromInvitation(invitation));
  }

  /// <summary> Decline a project invitation </summary>
  [HttpPost("invitations/{invitationToken}:decline")]
  public async Task<ActionResult<ProjectInvitationDto>> DeclineProjectInvitation(string invitationToken) {
    var invitation = await GetRespondableInvitation(invitationToken).ConfigureAwait(false);
    if (invitation == null) return NotFound(new { Message = "Invitation not found" });
    if (!invitation.CanRespond) return Conflict(new { Message = "Invitation is no longer pending or has expired" });

    var actor = _actorContextAccessor.ActorContext;
    var userId = actor.SubjectIdAsGuid;
    if (!userId.HasValue || !await CanRespondToInvitationAsync(invitation, userId.Value).ConfigureAwait(false)) return Forbid();

    invitation.Decline();
    invitation = await _mediator.Send(new DeclineProjectInvitationEndpointCommand(invitation)).ConfigureAwait(false);

    return Ok(ProjectInvitationDto.FromInvitation(invitation));
  }

  /// <summary> Get project collaborators </summary>
  [HttpGet("{id:guid}/collaborators")]
  public async Task<ActionResult<IEnumerable<CollaboratorDto>>> GetProjectCollaborators(Guid id) {
    if (!await _authorizationService.HasPermissionAsync(id, PermissionType.Read).ConfigureAwait(false)) return NotFound();
    var collaborators = await _context.Set<ProjectCollaborator>()
      .Where(c => c.ProjectId == id && c.IsActive)
      .Include(c => c.User)
      .OrderBy(c => c.JoinedAt)
      .Select(c => new CollaboratorDto {
        Id = c.Id,
        UserId = c.UserId,
        UserName = c.User != null ? c.User.Name : "Unknown",
        Role = c.Role,
        Permissions = c.Permissions,
        JoinedAt = c.JoinedAt,
        IsActive = c.IsActive
      })
      .ToListAsync().ConfigureAwait(false);

    return Ok(collaborators);
  }

  /// <summary> Add project collaborator </summary>
  [HttpPost("{id:guid}/collaborators")]
  public async Task<ActionResult<CollaboratorDto>> AddProjectCollaborator(Guid id, [FromBody] AddProjectCollaboratorRequest request) {
    if (!await _authorizationService.HasPermissionAsync(id, PermissionType.Edit).ConfigureAwait(false)) return NotFound();
    var project = await _context.Set<Project>().FindAsync(id).ConfigureAwait(false);
    if (project == null) return NotFound();

    var actor = _actorContextAccessor.ActorContext;
    var userId = actor.SubjectIdAsGuid ?? Guid.Empty;
    if (!TryNormalizeProjectPermissions(
          request.Permissions,
          [PermissionType.Read, PermissionType.Comment],
          out var permissions))
      return UnprocessableEntity(new { Code = "Projects.InvalidProjectPermissions" });
    if (!await IsActiveProjectTenantMemberAsync(project, request.UserId).ConfigureAwait(false))
      return UnprocessableEntity(new { Code = "Projects.ActiveTenantMembershipRequired" });

    // Check if user is already a collaborator
    var exists = await _context.Set<ProjectCollaborator>()
      .AnyAsync(c => c.ProjectId == id && c.UserId == request.UserId && c.IsActive).ConfigureAwait(false);
    if (exists) return Conflict(new { Message = "User is already a collaborator" });

    var collaborator = new ProjectCollaborator {
      TenantId = project.TenantId,
      ProjectId = id,
      UserId = request.UserId,
      Role = request.Role ?? "Collaborator",
      Permissions = permissions,
      IsActive = true,
      JoinedAt = SystemClock.UtcNow
    };

    _context.Set<ProjectCollaborator>().Add(collaborator);
    collaborator = await _mediator.Send(new AddProjectCollaboratorEndpointCommand(collaborator)).ConfigureAwait(false);

    _logger.LogInformation("User {AdminId} added collaborator {UserId} to project {ProjectId}", userId, request.UserId, id);

    return CreatedAtAction(nameof(GetProjectCollaborators), new { id }, new CollaboratorDto {
      Id = collaborator.Id,
      UserId = collaborator.UserId,
      Role = collaborator.Role,
      Permissions = collaborator.Permissions,
      JoinedAt = collaborator.JoinedAt,
      IsActive = collaborator.IsActive
    });
  }

  /// <summary> Update project collaborator </summary>
  [HttpPut("{id:guid}/collaborators/{collaboratorId:guid}")]
  public async Task<ActionResult<CollaboratorDto>> UpdateProjectCollaborator(Guid id, Guid collaboratorId, [FromBody] UpdateProjectCollaboratorRequest request) {
    if (!await _authorizationService.HasPermissionAsync(id, PermissionType.Edit).ConfigureAwait(false)) return NotFound();
    var collaborator = await _context.Set<ProjectCollaborator>()
      .FirstOrDefaultAsync(c => c.Id == collaboratorId && c.ProjectId == id).ConfigureAwait(false);
    if (collaborator == null) return NotFound();

    var userId = _actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty;

    if (request.Role != null) collaborator.Role = request.Role;
    if (request.Permissions != null) {
      if (!TryNormalizeProjectPermissions(request.Permissions, [], out var permissions))
        return UnprocessableEntity(new { Code = "Projects.InvalidProjectPermissions" });
      collaborator.Permissions = permissions;
    }

    collaborator = await _mediator.Send(new UpdateProjectCollaboratorEndpointCommand(collaborator)).ConfigureAwait(false);

    _logger.LogInformation("User {AdminId} updated collaborator {CollaboratorId} on project {ProjectId}", userId, collaboratorId, id);

    return Ok(new CollaboratorDto {
      Id = collaborator.Id,
      UserId = collaborator.UserId,
      Role = collaborator.Role,
      Permissions = collaborator.Permissions,
      JoinedAt = collaborator.JoinedAt,
      IsActive = collaborator.IsActive
    });
  }

  /// <summary> Remove project collaborator </summary>
  [HttpDelete("{id:guid}/collaborators/{collaboratorId:guid}")]
  public async Task<ActionResult> RemoveProjectCollaborator(Guid id, Guid collaboratorId) {
    if (!await _authorizationService.HasPermissionAsync(id, PermissionType.Edit).ConfigureAwait(false)) return NotFound();
    var collaborator = await _context.Set<ProjectCollaborator>()
      .FirstOrDefaultAsync(c => c.Id == collaboratorId && c.ProjectId == id).ConfigureAwait(false);
    if (collaborator == null) return NotFound();

    var userId = _actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty;

    // Soft-delete: mark as inactive
    collaborator.IsActive = false;
    collaborator.LeftAt = SystemClock.UtcNow;
    await _mediator.Send(new RemoveProjectCollaboratorEndpointCommand(collaborator)).ConfigureAwait(false);

    _logger.LogInformation("User {AdminId} removed collaborator {CollaboratorId} from project {ProjectId}", userId, collaboratorId, id);

    return NoContent();
  }

  /// <summary> Share project with a user by assigning a role </summary>
  [HttpPost("{id:guid}:share")]
  public async Task<ActionResult<CollaboratorDto>> ShareProject(Guid id, [FromBody] ShareProjectRequest request) {
    if (!await _authorizationService.HasPermissionAsync(id, PermissionType.Share).ConfigureAwait(false)) return NotFound();
    var project = await _context.Set<Project>().FindAsync(id).ConfigureAwait(false);
    if (project == null) return NotFound();

    var actor = _actorContextAccessor.ActorContext;
    var userId = actor.SubjectIdAsGuid ?? Guid.Empty;
    if (!TryNormalizeProjectPermissions(request.Permissions, [PermissionType.Read], out var permissions))
      return UnprocessableEntity(new { Code = "Projects.InvalidProjectPermissions" });
    if (!await IsActiveProjectTenantMemberAsync(project, request.UserId).ConfigureAwait(false))
      return UnprocessableEntity(new { Code = "Projects.ActiveTenantMembershipRequired" });
    // Check if already shared
    var existing = await _context.Set<ProjectCollaborator>()
      .FirstOrDefaultAsync(c => c.ProjectId == id && c.UserId == request.UserId).ConfigureAwait(false);

    if (existing != null) {
      // Re-activate if previously removed, or update role
      existing.IsActive = true;
      existing.Role = request.Role ?? "Viewer";
      existing.Permissions = permissions;
      existing.TenantId = project.TenantId;
      existing.LeftAt = null;
    } else {
      var collaborator = new ProjectCollaborator {
        TenantId = project.TenantId,
        ProjectId = id,
        UserId = request.UserId,
        Role = request.Role ?? "Viewer",
        Permissions = permissions,
        IsActive = true,
        JoinedAt = SystemClock.UtcNow
      };
      _context.Set<ProjectCollaborator>().Add(collaborator);
    }

    await _mediator.Send(new ShareProjectEndpointCommand(id, request.UserId)).ConfigureAwait(false);

    _logger.LogInformation("User {AdminId} shared project {ProjectId} with user {TargetUserId}", userId, id, request.UserId);

    return Ok(new { Message = "Project shared", ProjectId = id, UserId = request.UserId, Role = request.Role ?? "Viewer" });
  }

  /// <summary> Invite a user to collaborate on a project without granting access until acceptance </summary>
  [HttpPost("{id:guid}/invitations")]
  public async Task<ActionResult<ProjectInvitationDto>> InviteProjectCollaborator(Guid id, [FromBody] InviteProjectCollaboratorRequest request) {
    if (!await _authorizationService.HasPermissionAsync(id, PermissionType.Share).ConfigureAwait(false)) return NotFound();
    var project = await _context.Set<Project>().FindAsync(id).ConfigureAwait(false);
    if (project == null) return NotFound();

    var actor = _actorContextAccessor.ActorContext;
    var userId = actor.SubjectIdAsGuid ?? Guid.Empty;
    if (!request.UserId.HasValue && string.IsNullOrWhiteSpace(request.Email)) {
      return BadRequest(new { Message = "Provide either a user id or an email address." });
    }
    if (!TryNormalizeProjectPermissions(request.Permissions, [PermissionType.Read], out var permissions))
      return UnprocessableEntity(new { Code = "Projects.InvalidProjectPermissions" });
    if (request.UserId.HasValue &&
        !await IsActiveProjectTenantMemberAsync(project, request.UserId.Value).ConfigureAwait(false))
      return UnprocessableEntity(new { Code = "Projects.ActiveTenantMembershipRequired" });

    var invitation = new ProjectInvitation {
      TenantId = project.TenantId,
      ProjectId = id,
      InvitedUserId = request.UserId,
      InvitedEmail = request.Email?.Trim(),
      InvitedByUserId = userId,
      Role = request.Role ?? "Viewer",
      Permissions = permissions,
      ExpiresAt = request.ExpiresAt,
      Token = Guid.NewGuid().ToString("N"),
    };

    _context.Set<ProjectInvitation>().Add(invitation);
    invitation = await _mediator.Send(new InviteProjectCollaboratorEndpointCommand(invitation)).ConfigureAwait(false);

    return CreatedAtAction(nameof(GetMyProjectInvitations), new { }, ProjectInvitationDto.FromInvitation(invitation));
  }

  private async Task<ProjectInvitation?> GetRespondableInvitation(string invitationToken) {
    return await _context.Set<ProjectInvitation>()
      .Include(invitation => invitation.Project)
      .FirstOrDefaultAsync(invitation => invitation.Token == invitationToken)
      .ConfigureAwait(false);
  }

  private async Task<bool> CanRespondToInvitationAsync(ProjectInvitation invitation, Guid userId) {
    var actor = _actorContextAccessor.ActorContext;
    var projectTenantId = invitation.Project.TenantId;
    if (!projectTenantId.HasValue || actor.TenantId != projectTenantId ||
        !await IsActiveProjectTenantMemberAsync(invitation.Project, userId).ConfigureAwait(false))
      return false;

    if (invitation.InvitedUserId.HasValue) return invitation.InvitedUserId == userId;
    if (string.IsNullOrWhiteSpace(invitation.InvitedEmail)) return false;

    var email = await _context.Set<User>().AsNoTracking()
      .Where(user => user.Id == userId && user.IsActive && user.DeletedAt == null)
      .Select(user => user.Email)
      .SingleOrDefaultAsync()
      .ConfigureAwait(false);
    return email != null && string.Equals(email, invitation.InvitedEmail, StringComparison.OrdinalIgnoreCase);
  }

  private Task<bool> IsActiveProjectTenantMemberAsync(Project project, Guid userId) {
    if (!project.TenantId.HasValue) return Task.FromResult(false);
    return _context.Set<TenantMember>().AsNoTracking().AnyAsync(member =>
      member.UserId == userId &&
      member.TenantId == project.TenantId.Value &&
      member.IsActive &&
      member.DeletedAt == null);
  }

  private static bool TryNormalizeProjectPermissions(
    string? rawPermissions,
    IReadOnlyCollection<PermissionType> defaults,
    out string normalized) {
    var tokens = string.IsNullOrWhiteSpace(rawPermissions)
      ? []
      : rawPermissions
        .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var parsed = ProjectPermissionSet.Parse(tokens);
    if (tokens.Length > 0 && parsed.Length != tokens.Length) {
      normalized = string.Empty;
      return false;
    }

    var effective = parsed.Length > 0 ? parsed : defaults.Distinct().ToArray();
    if (effective.Length == 0 || effective.Any(permission => !ProjectPermissionSet.All.Contains(permission))) {
      normalized = string.Empty;
      return false;
    }
    normalized = string.Join(',', ProjectPermissionSet.Names(effective));
    return true;
  }
}

/// <summary> Request DTOs for REST API </summary>
public sealed record CreateProjectRequest {
  [Required(ErrorMessage = "Title is required")]
  [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters")]
  public string Title { get; init; } = string.Empty;

  [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")] public string? Description { get; init; }

  [StringLength(500, ErrorMessage = "Short description cannot exceed 500 characters")] public string? ShortDescription { get; init; }

  [Url(ErrorMessage = "Image URL must be a valid URL")] public string? ImageUrl { get; init; }

  [Url(ErrorMessage = "Repository URL must be a valid URL")] public string? RepositoryUrl { get; init; }

  [Url(ErrorMessage = "Website URL must be a valid URL")] public string? WebsiteUrl { get; init; }

  [Url(ErrorMessage = "Download URL must be a valid URL")] public string? DownloadUrl { get; init; }

  public ProjectType Type { get; init; } = ProjectType.Game;

  public Guid? CategoryId { get; init; }

  public ContentVisibility Visibility { get; init; } = ContentVisibility.Public;

  public ContentStatus Status { get; init; } = ContentStatus.Draft;

  public List<string>? Tags { get; init; }

  public Guid? OwnerTeamId { get; init; }
}

public sealed class CreateProjectVersionRequest {
  [Required, MaxLength(50)] public string VersionNumber { get; set; } = string.Empty;
  public ProjectVersionStatus? Status { get; set; }
  [MaxLength(10000)] public string? ReleaseNotes { get; set; }
}

public sealed record UpdateProjectVersionRequest(
  [property: Required, MaxLength(50)] string VersionNumber,
  [property: MaxLength(10000)] string? ReleaseNotes);

public sealed record ProjectVersionOptionProjection(
  Guid Id,
  Guid ProjectId,
  string ProjectTitle,
  string VersionNumber,
  ProjectVersionStatus Status,
  DateTime UpdatedAt);

public sealed record UpdateProjectRequest {
  public string? Title { get; init; }

  public string? Description { get; init; }

  public string? ShortDescription { get; init; }

  public string? ImageUrl { get; init; }

  public string? RepositoryUrl { get; init; }

  public string? WebsiteUrl { get; init; }

  public string? DownloadUrl { get; init; }

  public ProjectType? Type { get; init; }

  public Guid? CategoryId { get; init; }

  public ContentVisibility? Visibility { get; init; }

  public ContentStatus? Status { get; init; }

  public List<string>? Tags { get; init; }
}

/// <summary> DTO for collaborator responses </summary>
public sealed record CollaboratorDto {
  public Guid Id { get; init; }
  public Guid UserId { get; init; }
  public string UserName { get; init; } = string.Empty;
  public string Role { get; init; } = string.Empty;
  public string Permissions { get; init; } = string.Empty;
  public DateTime JoinedAt { get; init; }
  public bool IsActive { get; init; }
}

/// <summary> Request to add a project collaborator by ID </summary>
public sealed record AddProjectCollaboratorRequest {
  [Required] public Guid UserId { get; init; }
  public string? Role { get; init; }
  public string? Permissions { get; init; }
}

/// <summary> Request to update a project collaborator </summary>
public sealed record UpdateProjectCollaboratorRequest {
  public string? Role { get; init; }
  public string? Permissions { get; init; }
}

/// <summary> Request to share a project </summary>
public sealed record ShareProjectRequest {
  [Required] public Guid UserId { get; init; }
  public string? Role { get; init; }
  public string? Permissions { get; init; }
}

public sealed record InviteProjectCollaboratorRequest {
  public Guid? UserId { get; init; }
  public string? Email { get; init; }
  public string? Role { get; init; }
  public string? Permissions { get; init; }
  public DateTime? ExpiresAt { get; init; }
}

public sealed record ProjectInvitationDto(
  Guid Id,
  Guid ProjectId,
  string ProjectTitle,
  Guid? InvitedUserId,
  string? InvitedEmail,
  Guid InvitedByUserId,
  string Role,
  string Permissions,
  string Token,
  ProjectInvitationStatus Status,
  DateTime InvitedAt,
  DateTime? ExpiresAt,
  DateTime? RespondedAt
) {
  public static ProjectInvitationDto FromInvitation(ProjectInvitation invitation)
    => new(
      invitation.Id,
      invitation.ProjectId,
      invitation.Project?.Title ?? string.Empty,
      invitation.InvitedUserId,
      invitation.InvitedEmail,
      invitation.InvitedByUserId,
      invitation.Role,
      invitation.Permissions,
      invitation.Token,
      invitation.Status,
      invitation.InvitedAt,
      invitation.ExpiresAt,
      invitation.RespondedAt);
}
