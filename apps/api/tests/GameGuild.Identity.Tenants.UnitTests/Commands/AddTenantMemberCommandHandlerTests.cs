using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GameGuild.Identity.Tenants.UnitTests.Commands;

public class AddTenantMemberCommandHandlerTests
{
    private readonly Mock<ITenantRepository> _tenantRepositoryMock;
    private readonly Mock<ITenantMemberRepository> _memberRepositoryMock;
    private readonly AddTenantMemberCommandHandler _handler;

    public AddTenantMemberCommandHandlerTests()
    {
        _tenantRepositoryMock = new Mock<ITenantRepository>();
        _memberRepositoryMock = new Mock<ITenantMemberRepository>();
        _handler = new AddTenantMemberCommandHandler(_tenantRepositoryMock.Object, _memberRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenTenantNotFound_ShouldReturnFailure()
    {
        var tenantId = Guid.NewGuid();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var result = await _handler.Handle(new TestAddTenantMemberCommand(tenantId, Guid.NewGuid(), "Member"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenMemberExists_ShouldReturnFailure()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "Tenant", Slug = "tenant" };

        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMember { TenantId = tenantId, UserId = userId, Role = "Member" });

        var result = await _handler.Handle(new TestAddTenantMemberCommand(tenantId, userId, "Member"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already a member");
    }

    [Fact]
    public async Task Handle_WhenMemberExistsInactiveWithoutAcceptance_ShouldReactivateMembership()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var tenant = new Tenant { Id = tenantId, Name = "Tenant", Slug = "tenant" };
        var member = new TenantMember
        {
            TenantId = tenantId,
            UserId = userId,
            Role = "Member",
            IsActive = false,
            LeftAt = now,
            LeaveReason = "Invite cancelled",
            Metadata = TenantMemberInviteMetadata.CreatePending("admin@example.com", now, "member@example.com")
                .MarkCancelled(now)
                .ToJson()
        };

        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _memberRepositoryMock.Setup(r => r.UpdateAsync(member, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var result = await _handler.Handle(
            new TestAddTenantMemberCommand(tenantId, userId, "SystemAdmin"),
            CancellationToken.None);

        var metadata = TenantMemberInviteMetadata.FromJson(member.Metadata);
        result.Success.Should().BeTrue();
        member.IsActive.Should().BeTrue();
        member.Role.Should().Be("SystemAdmin");
        member.LeftAt.Should().BeNull();
        member.LeaveReason.Should().BeNull();
        metadata.InviteStatus.Should().Be(TenantMemberInviteStatuses.Accepted);
    }

    [Fact]
    public async Task Handle_WhenCancelledMemberIsInvitedAgain_ShouldCreateNewPendingInvite()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var tenant = new Tenant { Id = tenantId, Name = "Tenant", Slug = "tenant" };
        var member = new TenantMember
        {
            TenantId = tenantId,
            UserId = userId,
            Role = "Member",
            IsActive = false,
            LeftAt = now,
            LeaveReason = "Invite cancelled",
            Metadata = TenantMemberInviteMetadata.CreatePending("old-admin@example.com", now, "member@example.com")
                .MarkCancelled(now)
                .ToJson()
        };

        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _memberRepositoryMock.Setup(r => r.UpdateAsync(member, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var result = await _handler.Handle(
            new TestAddTenantMemberCommand(
                tenantId,
                userId,
                "Moderator",
                "new-admin@example.com",
                RequiresAcceptance: true,
                InviteeEmail: "member@example.com",
                InviteeName: "Member One"),
            CancellationToken.None);

        var metadata = TenantMemberInviteMetadata.FromJson(member.Metadata);
        result.Success.Should().BeTrue();
        member.IsActive.Should().BeFalse();
        member.Role.Should().Be("Moderator");
        member.LeftAt.Should().BeNull();
        member.LeaveReason.Should().BeNull();
        metadata.InviteStatus.Should().Be(TenantMemberInviteStatuses.Pending);
        metadata.InvitedByEmail.Should().Be("new-admin@example.com");
        metadata.InviteeEmail.Should().Be("member@example.com");
    }

    [Fact]
    public async Task Handle_Should_Create_Member_And_Add_Domain_Event()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "Tenant", Slug = "tenant" };
        TenantMember? capturedMember = null;

        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantMember?)null);
        _memberRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<TenantMember>(), It.IsAny<CancellationToken>()))
            .Callback<TenantMember, CancellationToken>((member, _) => capturedMember = member)
            .ReturnsAsync((TenantMember m, CancellationToken _) => m);

        var result = await _handler.Handle(new TestAddTenantMemberCommand(tenantId, userId, "Member", "inviter@example.com"), CancellationToken.None);

        result.Success.Should().BeTrue();
        capturedMember.Should().NotBeNull();
        capturedMember!.IsActive.Should().BeTrue();
        capturedMember.Metadata.Should().BeNull();
        tenant.DomainEvents.Should().Contain(e => e is TenantMemberAddedEvent);
    }

    [Fact]
    public async Task Handle_WhenRequiresAcceptance_Should_Create_Pending_Invite_Metadata()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "Tenant", Slug = "tenant" };
        TenantMember? capturedMember = null;

        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantMember?)null);
        _memberRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<TenantMember>(), It.IsAny<CancellationToken>()))
            .Callback<TenantMember, CancellationToken>((member, _) => capturedMember = member)
            .ReturnsAsync((TenantMember m, CancellationToken _) => m);

        var result = await _handler.Handle(
            new TestAddTenantMemberCommand(tenantId, userId, "Moderator", "admin@game-guild.com", RequiresAcceptance: true),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        capturedMember.Should().NotBeNull();
        capturedMember!.IsActive.Should().BeFalse();
        capturedMember.LeftAt.Should().BeNull();
        capturedMember.LeaveReason.Should().BeNull();
        capturedMember.Metadata.Should().Contain("\"inviteStatus\":\"Pending\"");
        capturedMember.Metadata.Should().Contain("\"invitedByEmail\":\"admin@game-guild.com\"");
        capturedMember.Metadata.Should().Contain("\"lastSentAt\"");
    }

    [Fact]
    public async Task Handle_WhenDefaultTenantRequiresAcceptance_Should_Create_Active_Base_Membership()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "GameGuild", Slug = "gameguild", IsDefault = true };
        TenantMember? capturedMember = null;

        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantMember?)null);
        _memberRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<TenantMember>(), It.IsAny<CancellationToken>()))
            .Callback<TenantMember, CancellationToken>((member, _) => capturedMember = member)
            .ReturnsAsync((TenantMember member, CancellationToken _) => member);

        var result = await _handler.Handle(
            new TestAddTenantMemberCommand(
                tenantId,
                userId,
                "Member",
                "admin@game-guild.com",
                RequiresAcceptance: true,
                InviteeEmail: "member@example.com"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        capturedMember.Should().NotBeNull();
        capturedMember!.IsActive.Should().BeTrue("the base tenant membership is mandatory");
        capturedMember.LeftAt.Should().BeNull();
        capturedMember.LeaveReason.Should().BeNull();
        TenantMemberInviteMetadata.FromJson(capturedMember.Metadata).InviteStatus
            .Should().NotBe(TenantMemberInviteStatuses.Pending);
    }

    [Fact]
    public async Task Handle_WhenDefaultMembershipIsInactive_Should_Reactivate_Even_WhenAcceptanceWasRequested()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var tenant = new Tenant { Id = tenantId, Name = "GameGuild", Slug = "gameguild", IsDefault = true };
        var member = new TenantMember
        {
            TenantId = tenantId,
            UserId = userId,
            Role = "Member",
            IsActive = false,
            LeftAt = now,
            LeaveReason = "Invite cancelled",
            Metadata = TenantMemberInviteMetadata.CreatePending("admin@example.com", now, "member@example.com")
                .MarkCancelled(now)
                .ToJson()
        };

        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _memberRepositoryMock.Setup(r => r.UpdateAsync(member, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var result = await _handler.Handle(
            new TestAddTenantMemberCommand(
                tenantId,
                userId,
                "Member",
                "admin@game-guild.com",
                RequiresAcceptance: true,
                InviteeEmail: "member@example.com"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        member.IsActive.Should().BeTrue("the base tenant membership cannot be deactivated");
        member.LeftAt.Should().BeNull();
        member.LeaveReason.Should().BeNull();
        TenantMemberInviteMetadata.FromJson(member.Metadata).InviteStatus
            .Should().Be(TenantMemberInviteStatuses.Accepted);
    }

    [Fact]
    public async Task Handle_WhenRecoveringDefaultSystemAdmin_Should_PreserveExistingRole()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var tenant = new Tenant { Id = tenantId, Name = "GameGuild", Slug = "gameguild", IsDefault = true };
        var member = new TenantMember
        {
            TenantId = tenantId,
            UserId = userId,
            Role = "SystemAdmin",
            IsActive = false,
            LeftAt = now,
            LeaveReason = "Legacy cancelled membership",
            Metadata = TenantMemberInviteMetadata.CreatePending("admin@example.com", now, "member@example.com")
                .MarkCancelled(now)
                .ToJson()
        };

        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _memberRepositoryMock.Setup(r => r.UpdateAsync(member, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var result = await _handler.Handle(
            new TestAddTenantMemberCommand(
                tenantId,
                userId,
                "Member"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        member.IsActive.Should().BeTrue();
        member.Role.Should().Be("SystemAdmin", "base-tenant recovery must never downgrade an existing platform role");
        member.LeftAt.Should().BeNull();
        member.LeaveReason.Should().BeNull();
        TenantMemberInviteMetadata.FromJson(member.Metadata).InviteStatus
            .Should().Be(TenantMemberInviteStatuses.Accepted);
    }

    [Fact]
    public async Task Handle_WhenRequiresAcceptanceAndInviteeEmail_Should_Queue_Invite_Notification()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "GameGuild Studio", Slug = "gameguild-studio" };
        TenantMember? capturedMember = null;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Invitations:ReviewPath"] = "/account/invitations"
            })
            .Build();
        var handler = new AddTenantMemberCommandHandler(
            _tenantRepositoryMock.Object,
            _memberRepositoryMock.Object,
            configuration);

        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantMember?)null);
        _memberRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<TenantMember>(), It.IsAny<CancellationToken>()))
            .Callback<TenantMember, CancellationToken>((member, _) => capturedMember = member)
            .ReturnsAsync((TenantMember m, CancellationToken _) => m);

        var result = await handler.Handle(
            new TestAddTenantMemberCommand(
                tenantId,
                userId,
                "Moderator",
                "admin@game-guild.com",
                RequiresAcceptance: true,
                InviteeEmail: "learner@example.com",
                InviteeName: "Learner One"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        var inviteEvent = capturedMember!.IntegrationEvents.OfType<TenantMemberInviteRequestedV1>().Should().ContainSingle().Subject;
        inviteEvent.MemberId.Should().Be(capturedMember.Id);
        inviteEvent.TenantId.Should().Be(tenantId);
        inviteEvent.Resend.Should().BeFalse();
        System.Text.Json.JsonSerializer.Serialize(inviteEvent).Should().NotContain("learner@example.com");
        capturedMember!.Metadata.Should().Contain("\"inviteeEmail\":\"learner@example.com\"");
        capturedMember.Metadata.Should().Contain("\"inviteeName\":\"Learner One\"");
    }

    private sealed record TestAddTenantMemberCommand(
        Guid TenantId,
        Guid UserId,
        string Role,
        string? InvitedByEmail = null,
        bool RequiresAcceptance = false,
        string? InviteeEmail = null,
        string? InviteeName = null)
        : AddTenantMemberCommand(TenantId, UserId, Role, InvitedByEmail, RequiresAcceptance, InviteeEmail, InviteeName);
}
