using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GameGuild.Identity.Tenants.UnitTests.Commands;

public class UpdateTenantMemberInviteCommandHandlerTests
{
    private readonly Mock<ITenantMemberRepository> _memberRepositoryMock = new();
    private readonly UpdateTenantMemberInviteCommandHandler _handler;

    public UpdateTenantMemberInviteCommandHandlerTests()
    {
        _handler = new UpdateTenantMemberInviteCommandHandler(_memberRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenMemberMissing_Should_Return_NotFound()
    {
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantMember?)null);

        var result = await _handler.Handle(
            new UpdateTenantMemberInviteCommand(Guid.NewGuid(), Guid.NewGuid(), TenantMemberInviteAction.Accept),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenResendingPendingInvite_Should_Update_LastSent_And_Count()
    {
        var member = CreatePendingInvite();
        TenantMember? updated = null;
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(member.UserId, member.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _memberRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<TenantMember>(), It.IsAny<CancellationToken>()))
            .Callback<TenantMember, CancellationToken>((entity, _) => updated = entity)
            .ReturnsAsync((TenantMember entity, CancellationToken _) => entity);

        var result = await _handler.Handle(
            new UpdateTenantMemberInviteCommand(member.TenantId, member.UserId, TenantMemberInviteAction.Resend, "admin@game-guild.com"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        updated.Should().NotBeNull();
        updated!.IsActive.Should().BeFalse();
        updated.Metadata.Should().Contain("\"inviteStatus\":\"Pending\"");
        updated.Metadata.Should().Contain("\"resendCount\":2");
        updated.Metadata.Should().Contain("\"lastSentAt\"");
    }

    [Fact]
    public async Task Handle_WhenResendingPendingInviteWithInviteeEmail_Should_Queue_Resend_Notification()
    {
        var member = CreatePendingInvite();
        TenantMember? updated = null;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Invitations:ReviewPath"] = "/account/invitations"
            })
            .Build();
        var handler = new UpdateTenantMemberInviteCommandHandler(
            _memberRepositoryMock.Object,
            configuration);

        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(member.UserId, member.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _memberRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<TenantMember>(), It.IsAny<CancellationToken>()))
            .Callback<TenantMember, CancellationToken>((entity, _) => updated = entity)
            .ReturnsAsync((TenantMember entity, CancellationToken _) => entity);

        var result = await handler.Handle(
            new UpdateTenantMemberInviteCommand(member.TenantId, member.UserId, TenantMemberInviteAction.Resend, "admin@game-guild.com"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        var inviteEvent = member.IntegrationEvents.OfType<TenantMemberInviteRequestedV1>().Should().ContainSingle().Subject;
        inviteEvent.MemberId.Should().Be(member.Id);
        inviteEvent.TenantId.Should().Be(member.TenantId);
        inviteEvent.Resend.Should().BeTrue();
        System.Text.Json.JsonSerializer.Serialize(inviteEvent).Should().NotContain("learner@example.com");
        updated.Should().NotBeNull();
        updated!.Metadata.Should().Contain("\"resendCount\":2");
    }

    [Fact]
    public async Task Handle_WhenCancellingPendingInvite_Should_Mark_Cancelled_Without_Deleting()
    {
        var member = CreatePendingInvite();
        TenantMember? updated = null;
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(member.UserId, member.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _memberRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<TenantMember>(), It.IsAny<CancellationToken>()))
            .Callback<TenantMember, CancellationToken>((entity, _) => updated = entity)
            .ReturnsAsync((TenantMember entity, CancellationToken _) => entity);

        var result = await _handler.Handle(
            new UpdateTenantMemberInviteCommand(member.TenantId, member.UserId, TenantMemberInviteAction.Cancel, "admin@game-guild.com"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        updated.Should().NotBeNull();
        updated!.IsActive.Should().BeFalse();
        updated.LeftAt.Should().NotBeNull();
        updated.LeaveReason.Should().Be("Invite cancelled");
        updated.Metadata.Should().Contain("\"inviteStatus\":\"Cancelled\"");
        updated.Metadata.Should().Contain("\"cancelledAt\"");
    }

    [Fact]
    public async Task Handle_WhenCancellingDefaultTenantInvite_Should_RejectAndKeepMembershipActive()
    {
        var member = CreatePendingInvite();
        member.Tenant = new Tenant
        {
            Id = member.TenantId,
            Name = "GameGuild",
            Slug = "gameguild",
            IsDefault = true
        };
        member.Activate();

        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(member.UserId, member.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var result = await _handler.Handle(
            new UpdateTenantMemberInviteCommand(
                member.TenantId,
                member.UserId,
                TenantMemberInviteAction.Cancel),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("default tenant");
        member.IsActive.Should().BeTrue();
        _memberRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<TenantMember>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAcceptingPendingInvite_Should_Activate_Membership()
    {
        var member = CreatePendingInvite();
        TenantMember? updated = null;
        _memberRepositoryMock.Setup(r => r.GetByUserAndTenantAsync(member.UserId, member.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _memberRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<TenantMember>(), It.IsAny<CancellationToken>()))
            .Callback<TenantMember, CancellationToken>((entity, _) => updated = entity)
            .ReturnsAsync((TenantMember entity, CancellationToken _) => entity);

        var result = await _handler.Handle(
            new UpdateTenantMemberInviteCommand(member.TenantId, member.UserId, TenantMemberInviteAction.Accept),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        updated.Should().NotBeNull();
        updated!.IsActive.Should().BeTrue();
        updated.LeftAt.Should().BeNull();
        updated.LeaveReason.Should().BeNull();
        updated.Metadata.Should().Contain("\"inviteStatus\":\"Accepted\"");
        updated.Metadata.Should().Contain("\"acceptedAt\"");
    }

    private static TenantMember CreatePendingInvite()
    {
        var now = DateTime.Parse("2026-07-01T12:00:00Z").ToUniversalTime();

        return new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Role = "Member",
            IsActive = false,
            JoinedAt = now,
            Tenant = new Tenant { Id = Guid.NewGuid(), Name = "GameGuild Studio", Slug = "gameguild-studio" },
            Metadata = $$"""
            {"inviteStatus":"Pending","invitedByEmail":"admin@game-guild.com","inviteeEmail":"learner@example.com","inviteeName":"Learner One","invitedAt":"{{now:O}}","lastSentAt":"{{now:O}}","resendCount":1}
            """
        };
    }
}
