using GameGuild.Notifications.Services.Email;
using Microsoft.AspNetCore.DataProtection;
using GameGuild.Identity.Tenants;
using GameGuild.Notifications.Services.Email.Handlers;
using GameGuild.Notifications.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace GameGuild.Notifications.UnitTests.Services.Email;

public sealed class TenantInviteRequestedHandlerTests
{
    [Fact]
    public async Task Handle_Creates_Email_Row_With_Null_Recipient_And_Email_Address()
    {
        using var context = CreateContext();
        var deliveryService = new NotificationDeliveryService(
            new ApplicationDbContextAdapter(context),
            Mock.Of<INotificationPreferenceService>(),
            Mock.Of<INotificationTemplateService>(),
            NullLogger<NotificationDeliveryService>.Instance,
            new NotificationMetadataProtector(new EphemeralDataProtectionProvider()));
        var notificationService = new NotificationService(
            deliveryService,
            Mock.Of<INotificationPreferenceService>(),
            Mock.Of<INotificationTemplateService>());
        var handler = new TenantInviteRequestedHandler(
            notificationService,
            NullLogger<TenantInviteRequestedHandler>.Instance,
            Mock.Of<ITenantMemberRepository>(), Mock.Of<ITenantRepository>(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var tenantId = Guid.NewGuid();

        await handler.Handle(new TenantInviteRequestedNotification(
            tenantId,
            "learner@example.com",
            "Learner One",
            "admin@game-guild.com",
            "GameGuild Studio",
            "Moderator",
            "https://app.example.com/sign-in?callbackUrl=%2Finvitations",
            "https://app.example.com/forgot-password?email=learner%40example.com",
            resend: false), CancellationToken.None);

        var row = context.Notifications.Should().ContainSingle().Subject;
        row.RecipientId.Should().BeNull();
        row.RecipientEmail.Should().Be("learner@example.com");
        row.TenantId.Should().Be(tenantId);
        row.Type.Should().Be(NotificationType.TenantInvite);
        row.Channel.Should().Be(NotificationChannel.Email);
        row.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        row.Metadata.Should().Contain("\"resend\":false");
    }

    [Fact]
    public async Task Handle_Does_Not_Throw_When_Row_Creation_Fails()
    {
        var notificationService = new Mock<INotificationService>();
        notificationService
            .Setup(service => service.SendAsync(
                It.IsAny<Guid?>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationChannel>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));
        var handler = new TenantInviteRequestedHandler(
            notificationService.Object,
            NullLogger<TenantInviteRequestedHandler>.Instance,
            Mock.Of<ITenantMemberRepository>(), Mock.Of<ITenantRepository>(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        var act = () => handler.Handle(new TenantInviteRequestedNotification(
            Guid.NewGuid(),
            "learner@example.com",
            null,
            null,
            "GameGuild Studio",
            "Member",
            "https://app.example.com/sign-in",
            "https://app.example.com/forgot-password",
            resend: true), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("message")]
    [InlineData("inner")]
    [InlineData("data")]
    public async Task Handle_LogPrivacy_QueueFailurePreservesRetryWithoutExposingException(string channel)
    {
        var email = $"private-{Guid.NewGuid():N}@example.test";
        var privateDetail = Guid.NewGuid().ToString("N");
        var payload = $"{email}; {privateDetail}";
        var failure = new InvalidOperationException(
            channel == "message" ? payload : "Queue unavailable",
            channel == "inner" ? new Exception(payload) : null);
        if (channel == "data")
        {
            failure.Data["detail"] = payload;
        }
        var notificationService = new Mock<INotificationService>();
        notificationService.Setup(service => service.SendAsync(
                It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var logger = new Mock<ILogger<TenantInviteRequestedHandler>>();
        var handler = new TenantInviteRequestedHandler(notificationService.Object, logger.Object,
            Mock.Of<ITenantMemberRepository>(), Mock.Of<ITenantRepository>(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        var result = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new TenantInviteRequestedNotification(
                Guid.NewGuid(), email, null, null, "Private tenant", "Member",
                "https://app.example.test/sign-in", "https://app.example.test/forgot-password",
                resend: true), CancellationToken.None));

        result.Should().BeSameAs(failure, "the durable outbox must still retry queue failures");
        var entry = logger.Invocations.Should().ContainSingle(invocation =>
            invocation.Method.Name == nameof(ILogger.Log)).Subject;
        entry.Arguments[0].Should().Be(LogLevel.Warning);
        entry.Arguments[3].Should().BeNull("exception messages, inner exceptions and data may contain private values");
        var rendered = entry.Arguments[2].ToString()!;
        rendered.Should().NotContain(email).And.NotContain(privateDetail);
        var fields = entry.Arguments[2].Should().BeAssignableTo<IEnumerable<KeyValuePair<string, object?>>>().Subject;
        fields.Should().Contain(pair => pair.Key == "Email" && Equals(pair.Value, LogRedaction.MaskEmail(email)));
        fields.Should().Contain(pair => pair.Key == "ErrorType" && Equals(pair.Value, failure.GetType().FullName));
        foreach (var field in fields)
        {
            (field.Value?.ToString() ?? string.Empty).Should().NotContain(email).And.NotContain(privateDetail);
        }
    }

    private static NotificationsTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new NotificationsTestDbContext(options);
    }

    private sealed class ApplicationDbContextAdapter(NotificationsTestDbContext inner) : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => inner.Set<T>();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => inner.SaveChangesAsync(cancellationToken);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Mock.Of<IDbContextTransaction>());
    }
}
