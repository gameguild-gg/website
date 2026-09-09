using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.Teams;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Teams;
using GameGuild.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GameGuild.API.UnitTests.Teams;

public sealed class TeamsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<IResourceQuotaEnforcer> _quotaEnforcer = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public TeamsControllerTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(Actor());
        _quotaEnforcer.Setup(service => service.TryAtomicConsumeAsync(
                It.IsAny<Guid>(), ResourceUsageType.Teams, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 1L, (long?)null));
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Create_Should_Use_Authenticated_Tenant_And_Actor_As_Owner()
    {
        AddIdentity();
        await _context.SaveChangesAsync();

        var result = await Controller().Create(
            new CreateTeamRequest("Studio", "studio", TeamVisibility.Private, null),
            CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Which.Value
            .Should().BeOfType<TeamDto>().Subject;
        created.TenantId.Should().Be(_tenantId);
        var team = await _context.Set<Team>().Include(candidate => candidate.Members).SingleAsync();
        team.Members.Should().ContainSingle(member =>
            member.UserId == _actorId && member.Authority == TeamMemberAuthority.Owner);
    }

    [Fact]
    public async Task Create_Should_Reject_When_Team_Quota_Is_Exhausted()
    {
        AddIdentity();
        await _context.SaveChangesAsync();
        _quotaEnforcer.Setup(service => service.TryAtomicConsumeAsync(
                _tenantId, ResourceUsageType.Teams, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, 3L, (long?)3L));

        var result = await Controller().Create(
            new CreateTeamRequest("Studio", "studio", TeamVisibility.Private, null),
            CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        (await _context.Set<Team>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Get_Should_Return_NotFound_For_Private_Team_Without_Membership()
    {
        AddIdentity();
        var team = Team.Create(_tenantId, "Private", "private", Guid.NewGuid());
        _context.Set<Team>().Add(team);
        await _context.SaveChangesAsync();

        var result = await Controller().Get(team.Id, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_Should_Return_Forbidden_For_A_Viewer()
    {
        AddIdentity();
        var team = Team.Create(_tenantId, "Studio", "studio", Guid.NewGuid());
        team.AddMember(_actorId, TeamMemberAuthority.Viewer);
        _context.Set<Team>().Add(team);
        await _context.SaveChangesAsync();

        var result = await Controller().Update(
            team.Id,
            new UpdateTeamRequest("Changed", "changed", TeamVisibility.Private, null),
            CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Invitations_Should_List_Without_Exposing_Token_And_Allow_Manager_To_Revoke()
    {
        AddIdentity();
        var team = Team.Create(_tenantId, "Studio", "studio", _actorId);
        var invitation = TeamInvitation.Create(
            _tenantId,
            team.Id,
            _actorId,
            "invitee@example.com",
            TeamMemberAuthority.Member,
            "secret-token",
            DateTime.UtcNow.AddDays(2));
        _context.Set<Team>().Add(team);
        _context.Set<TeamInvitation>().Add(invitation);
        await _context.SaveChangesAsync();

        var listed = await Controller().ListInvitations(team.Id, CancellationToken.None);

        var dto = listed.Result.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeAssignableTo<IReadOnlyList<TeamInvitationDto>>().Subject.Single();
        dto.Id.Should().Be(invitation.Id);
        dto.InvitedEmail.Should().Be("invitee@example.com");

        (await Controller().RevokeInvitation(team.Id, invitation.Id, CancellationToken.None))
            .Should().BeOfType<NoContentResult>();
        (await _context.Set<TeamInvitation>().SingleAsync()).RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeInvitation_Should_Not_Expose_Another_Tenants_Invitation()
    {
        AddIdentity();
        var otherTenantId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        _context.Set<Tenant>().Add(new Tenant
        {
            Id = otherTenantId,
            Name = "Other",
            Slug = "other",
            AdminEmail = "admin@other.example"
        });
        _context.Set<User>().Add(new User { Id = otherOwnerId, IsActive = true });
        var team = Team.Create(otherTenantId, "Other", "other", otherOwnerId);
        var invitation = TeamInvitation.Create(
            otherTenantId, team.Id, otherOwnerId, "other@example.com", TeamMemberAuthority.Member,
            "other-secret", DateTime.UtcNow.AddDays(1));
        _context.Set<Team>().Add(team);
        _context.Set<TeamInvitation>().Add(invitation);
        await _context.SaveChangesAsync();

        (await Controller().RevokeInvitation(team.Id, invitation.Id, CancellationToken.None))
            .Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AcceptInvitation_Should_Reject_A_Different_Authenticated_Email()
    {
        AddIdentity();
        var ownerId = Guid.NewGuid();
        _context.Set<User>().Add(new User
        {
            Id = ownerId,
            Email = "owner@example.com",
            Name = "Owner",
            IsActive = true
        });
        var team = Team.Create(_tenantId, "Studio", "studio", ownerId);
        const string token = "private-invitation-token";
        var invitation = TeamInvitation.Create(
            _tenantId, team.Id, ownerId, "different@example.com", TeamMemberAuthority.Member,
            token, DateTime.UtcNow.AddDays(1));
        _context.Set<Team>().Add(team);
        _context.Set<TeamInvitation>().Add(invitation);
        await _context.SaveChangesAsync();

        var result = await Controller().AcceptInvitation(new AcceptTeamInvitationRequest(token), CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        (await _context.Set<TeamInvitation>().SingleAsync()).UsedAt.Should().BeNull();
    }

    [Fact]
    public async Task Authenticated_User_Should_List_And_Accept_Their_Team_Invitation_Without_Exposing_Token()
    {
        AddIdentity();
        var ownerId = Guid.NewGuid();
        _context.Set<User>().Add(new User { Id = ownerId, Email = "owner@example.com", IsActive = true });
        var team = Team.Create(_tenantId, "Inviting Team", "inviting-team", ownerId);
        var invitation = TeamInvitation.Create(
            _tenantId, team.Id, ownerId, null, TeamMemberAuthority.Member,
            "never-return-this-token", DateTime.UtcNow.AddDays(1), _actorId);
        _context.Set<Team>().Add(team);
        _context.Set<TeamInvitation>().Add(invitation);
        await _context.SaveChangesAsync();

        var listed = await Controller().ListMyInvitations(CancellationToken.None);
        var dto = listed.Result.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeAssignableTo<IReadOnlyList<MyTeamInvitationDto>>().Subject.Single();
        dto.Id.Should().Be(invitation.Id);
        dto.TeamName.Should().Be("Inviting Team");

        var accepted = await Controller().AcceptAuthenticatedInvitation(invitation.Id, CancellationToken.None);

        accepted.Result.Should().BeOfType<OkObjectResult>();
        invitation.UsedAt.Should().NotBeNull();
        team.Members.Should().Contain(member => member.UserId == _actorId && member.IsActive);
    }

    [Fact]
    public async Task ChangeMember_Should_Require_RecentAuthentication_WhenPromotingOwner()
    {
        AddIdentity();
        var memberId = Guid.NewGuid();
        _context.Set<User>().Add(new User { Id = memberId, IsActive = true });
        _context.Set<TenantMember>().Add(new TenantMember { UserId = memberId, TenantId = _tenantId, IsActive = true });
        var team = Team.Create(_tenantId, "Studio", "studio", _actorId);
        team.AddMember(memberId, TeamMemberAuthority.Member);
        _context.Set<Team>().Add(team);
        await _context.SaveChangesAsync();

        var result = await Controller().ChangeMember(
            team.Id, memberId, new ChangeTeamMemberRequest(TeamMemberAuthority.Owner, null), CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        team.Members.Single(member => member.UserId == memberId).Authority.Should().Be(TeamMemberAuthority.Member);
    }

    [Fact]
    public async Task AddMember_Should_Reject_User_WithoutActiveMembershipInTeamTenant()
    {
        AddIdentity();
        var outsiderId = Guid.NewGuid();
        _context.Set<User>().Add(new User { Id = outsiderId, IsActive = true });
        var team = Team.Create(_tenantId, "Studio", "studio", _actorId);
        _context.Set<Team>().Add(team);
        await _context.SaveChangesAsync();

        var result = await Controller().AddMember(
            team.Id, new AddTeamMemberRequest(outsiderId, TeamMemberAuthority.Member, null), CancellationToken.None);

        result.Result.Should().BeOfType<UnprocessableEntityObjectResult>();
        team.Members.Should().NotContain(member => member.UserId == outsiderId);
    }

    private TeamsController Controller()
    {
        var authorization = new TeamAuthorizationService(_context, _actorAccessor.Object);
        var sender = new DirectHandlerSender(new TeamEndpointCommandHandler(_context));
        return new TeamsController(_context, _actorAccessor.Object, authorization, _quotaEnforcer.Object, sender);
    }

    private void AddIdentity()
    {
        _context.Set<User>().Add(new User
        {
            Id = _actorId,
            Email = "actor@example.com",
            Name = "Actor",
            IsActive = true
        });
        _context.Set<TenantMember>().Add(new TenantMember
        {
            UserId = _actorId,
            TenantId = _tenantId,
            IsActive = true
        });
    }

    private ActorContext Actor() => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = _actorId.ToString(),
        TenantId = _tenantId,
        Roles = new HashSet<string> { "Member" },
        Permissions = new HashSet<string>(),
        TypedAttributes = ActorAttributes.Empty,
        AuthScheme = "Bearer",
        IsAuthenticated = true
    };
}
