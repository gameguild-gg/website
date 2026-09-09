using GameGuild.Identity.Tenants;
using GameGuild.Notifications.Services.Email.Handlers;
using Microsoft.Extensions.Configuration;

namespace GameGuild.Notifications.UnitTests.Services.Email;

public sealed class DurableTenantInviteTests
{
    [Fact]
    public async Task DurableDelivery_LoadsPrivateMetadataAndPropagatesQueueFailure()
    {
        var tenant = new Tenant { Name = "Test tenant" };
        var member = new TenantMember
        {
            TenantId = tenant.Id, Role = "Member",
            Metadata = TenantMemberInviteMetadata.CreatePending("admin@example.test", DateTime.UtcNow, "invitee@example.test", "Invitee").ToJson()
        };
        var members = new Mock<ITenantMemberRepository>();
        members.Setup(x => x.GetByIdAsync(member.Id, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        var tenants = new Mock<ITenantRepository>();
        tenants.Setup(x => x.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        var queue = new Mock<INotificationService>();
        queue.SetReturnsDefault(Task.FromResult(Result.Failure<Notification>(Error.Failure("Queue.Unavailable", "Unavailable"))));
        var handler = new TenantInviteRequestedHandler(queue.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantInviteRequestedHandler>.Instance,
            members.Object, tenants.Object, new ConfigurationBuilder().Build());
        var message = new TenantMemberInviteRequestedV1(member.Id) { TenantId = tenant.Id, AggregateType = "TenantMember", AggregateId = member.Id.ToString() };

        var act = () => handler.HandleAsync(message);

        await act.Should().ThrowAsync<InvalidOperationException>();
        System.Text.Json.JsonSerializer.Serialize(message).Should().NotContain("@example.test");
        queue.Invocations.Should().ContainSingle(x => x.Method.Name == "SendAsync");
        queue.Invocations[0].Arguments[11].Should().Be("invitee@example.test");
    }

    [Fact]
    public async Task DurableDelivery_CancelledInviteDoesNotQueueEmail()
    {
        var member = new TenantMember
        {
            TenantId = Guid.NewGuid(),
            Metadata = TenantMemberInviteMetadata.CreatePending(null, DateTime.UtcNow, "invitee@example.test")
                .MarkCancelled(DateTime.UtcNow).ToJson()
        };
        var members = new Mock<ITenantMemberRepository>();
        members.Setup(x => x.GetByIdAsync(member.Id, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        var queue = new Mock<INotificationService>(MockBehavior.Strict);
        var handler = new TenantInviteRequestedHandler(queue.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TenantInviteRequestedHandler>.Instance,
            members.Object, Mock.Of<ITenantRepository>(), new ConfigurationBuilder().Build());

        await handler.HandleAsync(new TenantMemberInviteRequestedV1(member.Id) { TenantId = member.TenantId, AggregateType = "TenantMember", AggregateId = member.Id.ToString() });

        queue.VerifyNoOtherCalls();
    }
}
