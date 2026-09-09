using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Projects;
using GameGuild.Teams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Projects;

public sealed record ProjectTeamOwnershipDto(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string TeamSlug,
    ProjectTeamRole Role,
    ProjectTeamParticipationMode ParticipationMode,
    IReadOnlyList<string> Permissions,
    bool IsActive,
    DateTime AssignedAt,
    DateTime? EndedAt);

public sealed record ProjectAllocationDto(
    Guid Id,
    Guid ProjectTeamId,
    Guid UserId,
    string Function,
    decimal CapacityPercentage,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive);

public sealed record ProjectTeamAgreementDto(
    Guid Id,
    Guid ProposingTeamId,
    Guid ReceivingTeamId,
    Guid ProposedByUserId,
    Guid? AcceptedByUserId,
    ProjectTeamAgreementStatus Status,
    string Scope,
    string Deliverables,
    DateTime StartsAt,
    DateTime EndsAt,
    int Revision);

public sealed record ProjectOwnershipDto(
    Guid ProjectId,
    IReadOnlyList<ProjectTeamOwnershipDto> Teams,
    IReadOnlyList<ProjectAllocationDto> Allocations,
    IReadOnlyList<ProjectTeamAgreementDto> Agreements);

public sealed record AddProjectTeamRequest(
    Guid TeamId,
    ProjectTeamRole Role,
    ProjectTeamParticipationMode ParticipationMode,
    IReadOnlyList<PermissionType>? Permissions,
    string? Notes,
    decimal ContributionPercentage = 0);

public sealed record UpdateProjectTeamRequest(
    ProjectTeamRole Role,
    ProjectTeamParticipationMode ParticipationMode,
    IReadOnlyList<PermissionType>? Permissions,
    string? Notes,
    decimal ContributionPercentage = 0);

public sealed record TransferProjectOwnerTeamRequest(Guid TeamId);

public sealed record CreateProjectAllocationRequest(
    Guid ProjectTeamId,
    Guid UserId,
    string Function,
    decimal CapacityPercentage,
    DateTime StartsAt,
    DateTime? EndsAt);

public sealed record UpdateProjectAllocationRequest(
    string Function,
    decimal CapacityPercentage,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive);

public sealed record CreateProjectTeamAgreementRequest(
    Guid ProposingTeamId,
    Guid ReceivingTeamId,
    string Scope,
    string Deliverables,
    DateTime StartsAt,
    DateTime EndsAt);

public sealed record CounterProjectTeamAgreementRequest(
    string Scope,
    string Deliverables,
    DateTime StartsAt,
    DateTime EndsAt);

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("v{version:apiVersion}/projects/{projectId:guid}/ownership")]
public sealed class ProjectOwnershipController(
    IApplicationDbContext context,
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    IProjectAuthorizationService projectAuthorization,
    ITeamAuthorizationService teamAuthorization) : ControllerBase
{
    private static readonly TimeSpan RecentAuthenticationWindow = TimeSpan.FromMinutes(15);

    [HttpGet]
    public async Task<ActionResult<ProjectOwnershipDto>> Get(Guid projectId, CancellationToken cancellationToken)
    {
        if (!await projectAuthorization.HasPermissionAsync(projectId, PermissionType.Read, cancellationToken).ConfigureAwait(false))
            return NotFound();
        var project = await LoadProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        return project == null ? NotFound() : Ok(Map(project));
    }

    [HttpPost("teams")]
    public async Task<ActionResult<ProjectTeamOwnershipDto>> AddTeam(
        Guid projectId,
        AddProjectTeamRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagementAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (access.Result != null) return access.Result;
        var project = access.Project!;
        if (request.Role == ProjectTeamRole.Owner)
            return UnprocessableEntity(new { code = "Projects.UseOwnerTransfer" });
        if (request.ContributionPercentage is < 0 or > 100)
            return UnprocessableEntity(new { code = "Projects.InvalidContributionPercentage" });
        var targetTeam = await GetActiveTeamInProjectTenantAsync(request.TeamId, project.TenantId!.Value, cancellationToken).ConfigureAwait(false);
        if (targetTeam == null)
            return NotFound();

        try
        {
            var team = project.AddParticipatingTeam(request.TeamId, request.Role);
            Apply(team, request.ParticipationMode, request.Permissions, request.Notes, request.ContributionPercentage);
            // ProjectTeam has a client-generated Guid. Explicitly mark a relationship
            // added to an already tracked Project as Added; otherwise EF can infer an
            // update and produce an optimistic-concurrency failure for a row that does
            // not exist yet.
            await sender.Send(new AddProjectTeamOwnershipEndpointCommand(team), cancellationToken).ConfigureAwait(false);
            return Ok(Map(team, targetTeam));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(Problem(exception.Message, StatusCodes.Status409Conflict));
        }
    }

    [HttpPut("teams/{projectTeamId:guid}")]
    public async Task<ActionResult<ProjectTeamOwnershipDto>> UpdateTeam(
        Guid projectId,
        Guid projectTeamId,
        UpdateProjectTeamRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagementAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (access.Result != null) return access.Result;
        if (request.Role == ProjectTeamRole.Owner)
            return UnprocessableEntity(new { code = "Projects.UseOwnerTransfer" });
        if (request.ContributionPercentage is < 0 or > 100)
            return UnprocessableEntity(new { code = "Projects.InvalidContributionPercentage" });
        var team = access.Project!.Teams.SingleOrDefault(candidate => candidate.Id == projectTeamId && candidate.IsActive);
        if (team == null) return NotFound();
        if (team.Role == ProjectTeamRole.Owner)
            return Conflict(Problem("The owner team must be transferred before changing its role.", StatusCodes.Status409Conflict));

        team.Role = request.Role;
        Apply(team, request.ParticipationMode, request.Permissions, request.Notes, request.ContributionPercentage);
        team.Touch();
        await sender.Send(new UpdateProjectTeamOwnershipEndpointCommand(team), cancellationToken).ConfigureAwait(false);
        return Ok(Map(team));
    }

    [HttpDelete("teams/{projectTeamId:guid}")]
    public async Task<IActionResult> RemoveTeam(Guid projectId, Guid projectTeamId, CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagementAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (access.Result != null) return access.Result;
        var team = access.Project!.Teams.SingleOrDefault(candidate => candidate.Id == projectTeamId && candidate.IsActive);
        if (team == null) return NotFound();
        if (team.Role == ProjectTeamRole.Owner)
            return Conflict(Problem("The owner team cannot be removed.", StatusCodes.Status409Conflict));
        team.IsActive = false;
        team.EndedAt = SystemClock.UtcNow;
        team.Touch();
        foreach (var allocation in team.Allocations.Where(candidate => candidate.IsActive))
        {
            allocation.IsActive = false;
            allocation.EndsAt ??= SystemClock.UtcNow;
            allocation.Touch();
        }
        await sender.Send(new RemoveProjectTeamOwnershipEndpointCommand(team), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("owner-team")]
    public async Task<ActionResult<ProjectOwnershipDto>> TransferOwnerTeam(
        Guid projectId,
        TransferProjectOwnerTeamRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagementAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (access.Result != null) return access.Result;
        var project = access.Project!;
        var currentOwner = project.Teams.SingleOrDefault(team => team.IsActive && team.Role == ProjectTeamRole.Owner);
        if (currentOwner == null) return Conflict(Problem("The project has no active owner team.", StatusCodes.Status409Conflict));
        if (!await teamAuthorization.HasAuthorityAsync(currentOwner.TeamId, TeamMemberAuthority.Owner, cancellationToken).ConfigureAwait(false) ||
            !await teamAuthorization.HasAuthorityAsync(request.TeamId, TeamMemberAuthority.Owner, cancellationToken).ConfigureAwait(false))
            return Forbid();
        if (!await HasSensitiveActionAssuranceAsync(cancellationToken).ConfigureAwait(false))
            return Forbid();
        if (await GetActiveTeamInProjectTenantAsync(request.TeamId, project.TenantId!.Value, cancellationToken).ConfigureAwait(false) == null)
            return NotFound();
        if (!await HasOwnerTransferApprovalAsync(project, currentOwner.TeamId, request.TeamId, cancellationToken).ConfigureAwait(false))
            return Conflict(Problem("An accepted agreement approved by a distinct team owner is required for this owner transfer.", StatusCodes.Status409Conflict));

        project.SetOwnerTeam(request.TeamId);
        await sender.Send(new TransferProjectOwnerTeamEndpointCommand(project), cancellationToken).ConfigureAwait(false);
        return Ok(Map(project));
    }

    [HttpPost("allocations")]
    public async Task<ActionResult<ProjectAllocationDto>> CreateAllocation(
        Guid projectId,
        CreateProjectAllocationRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagementAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (access.Result != null) return access.Result;
        var project = access.Project!;
        var projectTeam = project.Teams.SingleOrDefault(team => team.Id == request.ProjectTeamId && team.IsActive && team.EndedAt == null);
        if (projectTeam == null) return NotFound();
        if (!await IsActiveTeamMemberAsync(projectTeam.TeamId, request.UserId, cancellationToken).ConfigureAwait(false))
            return UnprocessableEntity(new { code = "Projects.AllocationRequiresActiveTeamMember" });
        if (string.IsNullOrWhiteSpace(request.Function))
            return UnprocessableEntity(new { code = "Projects.AllocationFunctionRequired" });

        try
        {
            var allocation = project.AddAllocation(
                request.ProjectTeamId,
                request.UserId,
                request.Function,
                request.CapacityPercentage,
                request.StartsAt,
                request.EndsAt);
            await sender.Send(new CreateProjectAllocationEndpointCommand(allocation), cancellationToken).ConfigureAwait(false);
            return Ok(Map(allocation));
        }
        catch (ArgumentException exception)
        {
            return UnprocessableEntity(new { code = "Projects.InvalidAllocation", detail = exception.Message });
        }
    }

    [HttpPut("allocations/{allocationId:guid}")]
    public async Task<ActionResult<ProjectAllocationDto>> UpdateAllocation(
        Guid projectId,
        Guid allocationId,
        UpdateProjectAllocationRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagementAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (access.Result != null) return access.Result;
        var allocation = access.Project!.Allocations.SingleOrDefault(candidate => candidate.Id == allocationId);
        if (allocation == null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Function) || request.CapacityPercentage is <= 0 or > 100 || request.EndsAt <= request.StartsAt)
            return UnprocessableEntity(new { code = "Projects.InvalidAllocation" });
        allocation.Function = request.Function.Trim();
        allocation.CapacityPercentage = request.CapacityPercentage;
        allocation.StartsAt = request.StartsAt;
        allocation.EndsAt = request.EndsAt;
        allocation.IsActive = request.IsActive;
        allocation.Touch();
        await sender.Send(new UpdateProjectAllocationEndpointCommand(allocation), cancellationToken).ConfigureAwait(false);
        return Ok(Map(allocation));
    }

    [HttpDelete("allocations/{allocationId:guid}")]
    public async Task<IActionResult> RemoveAllocation(Guid projectId, Guid allocationId, CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagementAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (access.Result != null) return access.Result;
        var allocation = access.Project!.Allocations.SingleOrDefault(candidate => candidate.Id == allocationId && candidate.IsActive);
        if (allocation == null) return NotFound();
        allocation.IsActive = false;
        allocation.EndsAt ??= SystemClock.UtcNow;
        allocation.Touch();
        await sender.Send(new RemoveProjectAllocationEndpointCommand(allocation), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("agreements")]
    public async Task<ActionResult<ProjectTeamAgreementDto>> CreateAgreement(
        Guid projectId,
        CreateProjectTeamAgreementRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireProjectManagementAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (access.Result != null) return access.Result;
        var project = access.Project!;
        if (!IsParticipatingTeam(project, request.ProposingTeamId) || !IsParticipatingTeam(project, request.ReceivingTeamId))
            return UnprocessableEntity(new { code = "Projects.AgreementRequiresParticipatingTeams" });
        if (!await teamAuthorization.HasAuthorityAsync(request.ProposingTeamId, TeamMemberAuthority.Manager, cancellationToken).ConfigureAwait(false))
            return Forbid();
        if (actorContextAccessor.ActorContext.SubjectIdAsGuid is not { } actorId) return Unauthorized();

        try
        {
            var agreement = ProjectTeamAgreement.Create(
                projectId,
                request.ProposingTeamId,
                request.ReceivingTeamId,
                actorId,
                request.Scope,
                request.Deliverables,
                request.StartsAt,
                request.EndsAt);
            agreement.TenantId = project.TenantId;
            await sender.Send(new CreateProjectTeamAgreementEndpointCommand(agreement), cancellationToken).ConfigureAwait(false);
            return Ok(Map(agreement));
        }
        catch (ArgumentException exception)
        {
            return UnprocessableEntity(new { code = "Projects.InvalidAgreement", detail = exception.Message });
        }
    }

    [HttpPost("agreements/{agreementId:guid}/counter")]
    public async Task<ActionResult<ProjectTeamAgreementDto>> CounterAgreement(
        Guid projectId,
        Guid agreementId,
        CounterProjectTeamAgreementRequest request,
        CancellationToken cancellationToken)
    {
        var change = await PrepareAgreementChangeAsync(projectId, agreementId, "counter", request, cancellationToken).ConfigureAwait(false);
        if (change.Result != null) return change.Result;
        await sender.Send(new CounterProjectTeamAgreementEndpointCommand(change.Agreement!), cancellationToken).ConfigureAwait(false);
        return Ok(Map(change.Agreement!));
    }

    [HttpPost("agreements/{agreementId:guid}/accept")]
    public async Task<ActionResult<ProjectTeamAgreementDto>> AcceptAgreement(
        Guid projectId,
        Guid agreementId,
        CancellationToken cancellationToken)
    {
        var change = await PrepareAgreementChangeAsync(projectId, agreementId, "accept", null, cancellationToken).ConfigureAwait(false);
        if (change.Result != null) return change.Result;
        await sender.Send(new AcceptProjectTeamAgreementEndpointCommand(change.Agreement!), cancellationToken).ConfigureAwait(false);
        return Ok(Map(change.Agreement!));
    }

    [HttpPost("agreements/{agreementId:guid}/cancel")]
    public async Task<ActionResult<ProjectTeamAgreementDto>> CancelAgreement(
        Guid projectId,
        Guid agreementId,
        CancellationToken cancellationToken)
    {
        var change = await PrepareAgreementChangeAsync(projectId, agreementId, "cancel", null, cancellationToken).ConfigureAwait(false);
        if (change.Result != null) return change.Result;
        await sender.Send(new CancelProjectTeamAgreementEndpointCommand(change.Agreement!), cancellationToken).ConfigureAwait(false);
        return Ok(Map(change.Agreement!));
    }

    [HttpPost("agreements/{agreementId:guid}/complete")]
    public async Task<ActionResult<ProjectTeamAgreementDto>> CompleteAgreement(
        Guid projectId,
        Guid agreementId,
        CancellationToken cancellationToken)
    {
        var change = await PrepareAgreementChangeAsync(projectId, agreementId, "complete", null, cancellationToken).ConfigureAwait(false);
        if (change.Result != null) return change.Result;
        await sender.Send(new CompleteProjectTeamAgreementEndpointCommand(change.Agreement!), cancellationToken).ConfigureAwait(false);
        return Ok(Map(change.Agreement!));
    }

    private async Task<(ProjectTeamAgreement? Agreement, ActionResult<ProjectTeamAgreementDto>? Result)> PrepareAgreementChangeAsync(
        Guid projectId,
        Guid agreementId,
        string action,
        CounterProjectTeamAgreementRequest? counter,
        CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (actor.TenantId is not { } tenantId) return (null, Unauthorized());
        var project = await LoadProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project == null || project.TenantId != tenantId) return (null, NotFound());
        var agreement = project.TeamAgreements.SingleOrDefault(candidate => candidate.Id == agreementId && candidate.DeletedAt == null);
        if (agreement == null) return (null, NotFound());
        var authorityTeamId = action == "accept" ? agreement.ReceivingTeamId :
            await teamAuthorization.HasAuthorityAsync(agreement.ProposingTeamId, TeamMemberAuthority.Manager, cancellationToken).ConfigureAwait(false)
                ? agreement.ProposingTeamId
                : agreement.ReceivingTeamId;
        if (!await teamAuthorization.HasAuthorityAsync(authorityTeamId, TeamMemberAuthority.Manager, cancellationToken).ConfigureAwait(false))
            return (null, Forbid());
        if (actorContextAccessor.ActorContext.SubjectIdAsGuid is not { } actorId) return (null, Unauthorized());
        if (action == "accept" && agreement.ProposedByUserId == actorId)
            return (null, Conflict(Problem("An agreement must be accepted by a different actor.", StatusCodes.Status409Conflict)));
        if (action == "accept" && !await HasSensitiveActionAssuranceAsync(cancellationToken).ConfigureAwait(false))
            return (null, Forbid());

        try
        {
            switch (action)
            {
                case "counter":
                    agreement.CounterPropose(actorId, counter!.Scope, counter.Deliverables, counter.StartsAt, counter.EndsAt);
                    break;
                case "accept": agreement.Accept(actorId); break;
                case "cancel": agreement.Cancel(); break;
                case "complete": agreement.Complete(); break;
                default: throw new InvalidOperationException("Unsupported agreement action.");
            }
            return (agreement, null);
        }
        catch (InvalidOperationException exception)
        {
            return (null, Conflict(Problem(exception.Message, StatusCodes.Status409Conflict)));
        }
        catch (ArgumentException exception)
        {
            return (null, UnprocessableEntity(new { code = "Projects.InvalidAgreement", detail = exception.Message }));
        }
    }

    private async Task<(Project? Project, ActionResult? Result)> RequireProjectManagementAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!await projectAuthorization.HasPermissionAsync(projectId, PermissionType.Read, cancellationToken).ConfigureAwait(false))
            return (null, NotFound());
        if (!await projectAuthorization.HasPermissionAsync(projectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
            return (null, Forbid());
        var project = await LoadProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project == null) return (null, NotFound());
        var owner = project.Teams.SingleOrDefault(team => team.IsActive && team.Role == ProjectTeamRole.Owner);
        if (owner == null) return (null, Conflict(Problem("The project has no active owner team.", StatusCodes.Status409Conflict)));
        if (!await teamAuthorization.HasAuthorityAsync(owner.TeamId, TeamMemberAuthority.Manager, cancellationToken).ConfigureAwait(false))
            return (null, Forbid());
        return (project, null);
    }

    private Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.Set<Project>()
            .Include(project => project.Teams.Where(team => team.DeletedAt == null))
                .ThenInclude(team => team.Team)
            .Include(project => project.Teams.Where(team => team.DeletedAt == null))
                .ThenInclude(team => team.Allocations)
            .Include(project => project.TeamAgreements.Where(agreement => agreement.DeletedAt == null))
            .SingleOrDefaultAsync(project => project.Id == projectId && project.DeletedAt == null, cancellationToken);

    private async Task<bool> HasSensitiveActionAssuranceAsync(CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (actor.TypedAttributes.AuthenticatedAt is not { } authenticatedAt ||
            authenticatedAt < DateTimeOffset.UtcNow.Subtract(RecentAuthenticationWindow))
            return false;
        if (actor.SubjectIdAsGuid is not { } actorId) return false;
        var hasMfa = await context.Set<UserMfaConfiguration>().AsNoTracking()
            .AnyAsync(configuration =>
                configuration.UserId == actorId && configuration.IsEnabled && configuration.IsSetupComplete,
                cancellationToken).ConfigureAwait(false);
        return !hasMfa || actor.IsMfaVerified;
    }

    private async Task<bool> HasOwnerTransferApprovalAsync(
        Project project,
        Guid currentOwnerTeamId,
        Guid targetTeamId,
        CancellationToken cancellationToken)
    {
        if (actorContextAccessor.ActorContext.SubjectIdAsGuid is not { } actorId) return false;
        var anotherOwnerAvailable = await context.Set<TeamMember>().AsNoTracking().AnyAsync(member =>
            (member.TeamId == currentOwnerTeamId || member.TeamId == targetTeamId) &&
            member.UserId != actorId &&
            member.Authority == TeamMemberAuthority.Owner &&
            member.IsActive && member.LeftAt == null && member.DeletedAt == null &&
            member.TenantId == project.TenantId,
            cancellationToken).ConfigureAwait(false);
        if (!anotherOwnerAvailable) return true;

        return project.TeamAgreements.Any(agreement =>
            agreement.DeletedAt == null &&
            agreement.Status == ProjectTeamAgreementStatus.Accepted &&
            agreement.AcceptedByUserId.HasValue &&
            agreement.AcceptedByUserId != agreement.ProposedByUserId &&
            agreement.EndsAt >= SystemClock.UtcNow &&
            ((agreement.ProposingTeamId == currentOwnerTeamId && agreement.ReceivingTeamId == targetTeamId) ||
             (agreement.ProposingTeamId == targetTeamId && agreement.ReceivingTeamId == currentOwnerTeamId)));
    }

    private Task<Team?> GetActiveTeamInProjectTenantAsync(Guid teamId, Guid tenantId, CancellationToken cancellationToken) =>
        context.Set<Team>().AsNoTracking().SingleOrDefaultAsync(team =>
            team.Id == teamId &&
            team.TenantId == tenantId &&
            team.IsActive &&
            team.Status == TeamStatus.Active &&
            team.DeletedAt == null,
            cancellationToken);

    private Task<bool> IsActiveTeamMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken) =>
        context.Set<TeamMember>().AsNoTracking().AnyAsync(member =>
            member.TeamId == teamId &&
            member.UserId == userId &&
            member.IsActive &&
            member.LeftAt == null &&
            member.DeletedAt == null,
            cancellationToken);

    private static bool IsParticipatingTeam(Project project, Guid teamId) =>
        project.Teams.Any(team => team.TeamId == teamId && team.IsActive && team.EndedAt == null && team.DeletedAt == null);

    private static void Apply(
        ProjectTeam team,
        ProjectTeamParticipationMode participationMode,
        IReadOnlyList<PermissionType>? permissions,
        string? notes,
        decimal contributionPercentage)
    {
        team.ParticipationMode = participationMode;
        team.Permissions = permissions == null
            ? null
            : string.Join(',', permissions.Distinct().Select(permission => permission.ToString()));
        team.Notes = notes?.Trim();
        team.ContributionPercentage = contributionPercentage;
    }

    private static ProjectOwnershipDto Map(Project project) => new(
        project.Id,
        project.Teams.Where(team => team.DeletedAt == null).Select(Map).ToArray(),
        project.Allocations.Where(allocation => allocation.DeletedAt == null).Select(Map).ToArray(),
        project.TeamAgreements.Where(agreement => agreement.DeletedAt == null).Select(Map).ToArray());

    private static ProjectTeamOwnershipDto Map(ProjectTeam team) => new(
        team.Id,
        team.TeamId,
        team.Team?.Name ?? string.Empty,
        team.Team?.Slug ?? string.Empty,
        team.Role,
        team.ParticipationMode,
        ParsePermissions(team.Permissions),
        team.IsActive,
        team.AssignedAt,
        team.EndedAt);

    private static ProjectTeamOwnershipDto Map(ProjectTeam team, Team relatedTeam) => new(
        team.Id,
        team.TeamId,
        relatedTeam.Name,
        relatedTeam.Slug,
        team.Role,
        team.ParticipationMode,
        ParsePermissions(team.Permissions),
        team.IsActive,
        team.AssignedAt,
        team.EndedAt);

    private static ProjectAllocationDto Map(ProjectMemberAllocation allocation) => new(
        allocation.Id,
        allocation.ProjectTeamId,
        allocation.UserId,
        allocation.Function,
        allocation.CapacityPercentage,
        allocation.StartsAt,
        allocation.EndsAt,
        allocation.IsActive);

    private static ProjectTeamAgreementDto Map(ProjectTeamAgreement agreement) => new(
        agreement.Id,
        agreement.ProposingTeamId,
        agreement.ReceivingTeamId,
        agreement.ProposedByUserId,
        agreement.AcceptedByUserId,
        agreement.Status,
        agreement.Scope,
        agreement.Deliverables,
        agreement.StartsAt,
        agreement.EndsAt,
        agreement.Revision);

    private static IReadOnlyList<string> ParsePermissions(string? permissions) =>
        string.IsNullOrWhiteSpace(permissions)
            ? Array.Empty<string>()
            : permissions.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static ProblemDetails Problem(string title, int status) => new() { Title = title, Status = status };
}
