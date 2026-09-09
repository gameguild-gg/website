using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.Projects;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Projects;
using GameGuild.Teams;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GameGuild.API.UnitTests.Projects;

public sealed class ProjectOwnershipControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public ProjectOwnershipControllerTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(Actor());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task AddTeam_Should_Persist_A_New_ProjectTeam_InsteadOfUpdatingANonexistentRow()
    {
        var (project, _, replacement) = AddProjectGraph();
        await _context.SaveChangesAsync();

        var result = await Controller().AddTeam(
            project.Id,
            new AddProjectTeamRequest(
                replacement.Id,
                ProjectTeamRole.Contributor,
                ProjectTeamParticipationMode.SelectedMembers,
                [PermissionType.Read],
                null,
                25),
            CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        (await _context.Set<ProjectTeam>().CountAsync(team => team.ProjectId == project.Id)).Should().Be(2);
    }

    [Fact]
    public async Task TransferOwnerTeam_Should_Require_Recent_Authentication()
    {
        var (project, owner, replacement) = AddProjectGraph();
        await _context.SaveChangesAsync();

        var result = await Controller().TransferOwnerTeam(
            project.Id,
            new TransferProjectOwnerTeamRequest(replacement.Id),
            CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        project.Teams.Should().ContainSingle(team => team.Role == ProjectTeamRole.Owner && team.TeamId == owner.Id);
    }

    [Fact]
    public async Task CreateAllocation_Should_Reject_User_Outside_The_Selected_Team()
    {
        var (project, _, replacement) = AddProjectGraph();
        var projectTeam = project.AddParticipatingTeam(replacement.Id, ProjectTeamRole.Contributor);
        await _context.SaveChangesAsync();

        var result = await Controller().CreateAllocation(
            project.Id,
            new CreateProjectAllocationRequest(
                projectTeam.Id,
                Guid.NewGuid(),
                "Developer",
                50,
                SystemClock.UtcNow,
                null),
            CancellationToken.None);

        result.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
        project.Allocations.Should().BeEmpty();
    }

    [Fact]
    public async Task AcceptAgreement_Should_Reject_The_Proposing_Actor()
    {
        var (project, owner, replacement) = AddProjectGraph();
        project.AddParticipatingTeam(replacement.Id, ProjectTeamRole.Contributor);
        var agreement = ProjectTeamAgreement.Create(
            project.Id,
            owner.Id,
            replacement.Id,
            _actorId,
            "Prototype",
            "Playable build",
            SystemClock.UtcNow,
            SystemClock.UtcNow.AddDays(30));
        _context.Set<ProjectTeamAgreement>().Add(agreement);
        await _context.SaveChangesAsync();

        var result = await Controller().AcceptAgreement(project.Id, agreement.Id, CancellationToken.None);

        result.Result.Should().BeOfType<ConflictObjectResult>();
        agreement.Status.Should().Be(ProjectTeamAgreementStatus.Proposed);
    }

    [Fact]
    public async Task AcceptAgreement_Should_Require_RecentAuthentication()
    {
        var (project, owner, replacement) = AddProjectGraph();
        var receivingOwnerId = Guid.NewGuid();
        _context.Set<User>().Add(new User { Id = receivingOwnerId, IsActive = true });
        _context.Set<TenantMember>().Add(new TenantMember { UserId = receivingOwnerId, TenantId = _tenantId, IsActive = true });
        replacement.AddMember(receivingOwnerId, TeamMemberAuthority.Owner);
        project.AddParticipatingTeam(replacement.Id, ProjectTeamRole.Contributor);
        var agreement = ProjectTeamAgreement.Create(
            project.Id, owner.Id, replacement.Id, _actorId, "Prototype", "Build",
            SystemClock.UtcNow, SystemClock.UtcNow.AddDays(30));
        agreement.TenantId = _tenantId;
        _context.Set<ProjectTeamAgreement>().Add(agreement);
        await _context.SaveChangesAsync();
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(Actor(receivingOwnerId));

        var result = await Controller().AcceptAgreement(project.Id, agreement.Id, CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        agreement.Status.Should().Be(ProjectTeamAgreementStatus.Proposed);
    }

    [Fact]
    public async Task TransferOwnerTeam_Should_Require_AcceptedAgreement_WhenAnotherOwnerIsAvailable()
    {
        var (project, owner, replacement) = AddProjectGraph();
        var otherOwnerId = Guid.NewGuid();
        _context.Set<User>().Add(new User { Id = otherOwnerId, IsActive = true });
        _context.Set<TenantMember>().Add(new TenantMember { UserId = otherOwnerId, TenantId = _tenantId, IsActive = true });
        replacement.AddMember(otherOwnerId, TeamMemberAuthority.Owner);
        project.AddParticipatingTeam(replacement.Id, ProjectTeamRole.Contributor);
        await _context.SaveChangesAsync();
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(Actor(_actorId, recent: true));

        var result = await Controller().TransferOwnerTeam(
            project.Id, new TransferProjectOwnerTeamRequest(replacement.Id), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<ConflictObjectResult>().Which;
        conflict.Value.Should().BeOfType<ProblemDetails>()
            .Which.Title!.ToLowerInvariant().Should().Contain("accepted agreement");
        project.Teams.Should().ContainSingle(team => team.Role == ProjectTeamRole.Owner && team.TeamId == owner.Id);
    }

    private ProjectOwnershipController Controller() => new(
        _context,
        new DirectHandlerSender(new ProjectOwnershipEndpointCommandHandler(_context)),
        _actorAccessor.Object,
        new ProjectAuthorizationService(_context, _actorAccessor.Object),
        new TeamAuthorizationService(_context, _actorAccessor.Object));

    private (Project Project, Team Owner, Team Replacement) AddProjectGraph()
    {
        _context.Set<User>().Add(new User { Id = _actorId, IsActive = true });
        _context.Set<TenantMember>().Add(new TenantMember
        {
            UserId = _actorId,
            TenantId = _tenantId,
            IsActive = true
        });
        var owner = Team.Create(_tenantId, "Owner", "owner", _actorId);
        var replacement = Team.Create(_tenantId, "Replacement", "replacement", _actorId);
        var project = new Project
        {
            TenantId = _tenantId,
            Title = "Project",
            Slug = "project",
            CreatedById = _actorId
        };
        project.SetOwnerTeam(owner.Id);
        _context.Set<Team>().AddRange(owner, replacement);
        _context.Set<Project>().Add(project);
        return (project, owner, replacement);
    }

    private ActorContext Actor(Guid? actorId = null, bool recent = false) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = (actorId ?? _actorId).ToString(),
        TenantId = _tenantId,
        Roles = new HashSet<string> { "Member" },
        Permissions = new HashSet<string>(),
        TypedAttributes = recent
            ? new ActorAttributes { AuthenticatedAt = DateTimeOffset.UtcNow }
            : ActorAttributes.Empty,
        AuthScheme = "Bearer",
        IsAuthenticated = true
    };
}
