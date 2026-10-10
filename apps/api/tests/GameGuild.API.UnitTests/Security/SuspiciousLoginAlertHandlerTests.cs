using FluentAssertions;
using GameGuild.API.Core.Security;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;
using Xunit;

namespace GameGuild.API.UnitTests.Security;

public class SuspiciousLoginAlertHandlerTests
{
    private const string Email = "suspicious-login-owner@example.test";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<INotificationPreferenceService> _preferences = new();
    private readonly User _user = User.Create(Email, "Suspicious login owner");

    public SuspiciousLoginAlertHandlerTests()
    {
        _user.Id = Guid.NewGuid();
        _user.Username = "suspicious-login-owner";
        _users.Setup(repository => repository.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user);
        _notifications.Setup(service => service.SendAsync(
                It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Notification.Create(
                _user.Id, NotificationType.Security, NotificationChannel.InApp, "Account security alert", "message", null)));
        _preferences.Setup(service => service.DecideDeliveryAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<NotificationChannel>(),
                It.IsAny<NotificationPriority>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.Send());
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenNotificationsAreDisabled()
    {
        var @event = NewEvent();

        await CreateSut(enabled: false).HandleAsync(@event);

        _notifications.Verify(service => service.SendAsync(
            It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenOwnerIsUnknown()
    {
        _users.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await CreateSut().HandleAsync(NewEvent());

        _notifications.Verify(service => service.SendAsync(
            It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DoesNothing_WhenOwnerIsDeleted()
    {
        _user.DeletedAt = DateTime.UtcNow;

        await CreateSut().HandleAsync(NewEvent());

        _notifications.Verify(service => service.SendAsync(
            It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_QueuesInAppAlwaysAndEmailPerPreferences()
    {
        var @event = NewEvent();

        await CreateSut().HandleAsync(@event);

        VerifySent(@event, NotificationChannel.InApp, recipientEmail: null, tenantId: null);
        VerifySent(@event, NotificationChannel.Email, recipientEmail: Email, tenantId: null);
        _preferences.Verify(service => service.DecideDeliveryAsync(
            _user.Id, NotificationType.Security, NotificationChannel.Email, NotificationPriority.Urgent,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SkipsEmail_WhenPreferencesDropIt()
    {
        _preferences.Setup(service => service.DecideDeliveryAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<NotificationChannel>(),
                It.IsAny<NotificationPriority>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.Drop("muted"));
        var @event = NewEvent();

        await CreateSut().HandleAsync(@event);

        VerifySent(@event, NotificationChannel.InApp, recipientEmail: null, tenantId: null);
        _notifications.Verify(service => service.SendAsync(
            It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
            NotificationChannel.Email, It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SkipsEmail_WhenOwnerHasNoEmailAddress()
    {
        _user.Email = string.Empty;
        var @event = NewEvent();

        await CreateSut().HandleAsync(@event);

        VerifySent(@event, NotificationChannel.InApp, recipientEmail: null, tenantId: null);
        _preferences.Verify(service => service.DecideDeliveryAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<NotificationChannel>(),
            It.IsAny<NotificationPriority>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PreservesTenantIdAndMapsPlatformSentinelToNull()
    {
        var tenantId = Guid.NewGuid();
        var @event = NewEvent(tenantId);

        await CreateSut().HandleAsync(@event);

        VerifySent(@event, NotificationChannel.InApp, recipientEmail: null, tenantId: tenantId);
    }

    [Theory]
    [InlineData(SecurityAlertKinds.LoginStepUpRequired, "high-risk sign-in attempt")]
    [InlineData(SecurityAlertKinds.BruteForceDetected, "repeated failed sign-in attempts")]
    [InlineData(SecurityAlertKinds.ImpossibleTravel, "successful sign-in")]
    [InlineData("UnknownSignal", "suspicious sign-in activity")]
    public async Task HandleAsync_UsesRedactedKindSpecificCopy(string alertKind, string expectedMessageFragment)
    {
        var @event = NewEvent(alertKind: alertKind);

        await CreateSut().HandleAsync(@event);

        _notifications.Verify(service => service.SendAsync(
            _user.Id,
            NotificationType.Security,
            "Account security alert",
            It.Is<string>(message =>
                message.Contains(expectedMessageFragment, StringComparison.Ordinal)
                && !message.Contains(Email, StringComparison.Ordinal)
                && !message.Contains("127.0.0.1", StringComparison.Ordinal)),
            NotificationChannel.InApp,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            NotificationPriority.Urgent,
            @event.EventId,
            nameof(SuspiciousLoginDetectedV1),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Throws_WhenQueueingFails()
    {
        _notifications.Setup(service => service.SendAsync(
                It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                NotificationChannel.InApp, It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Notification>(Error.Failure("Notification.QueueUnavailable", "queue unavailable")));

        var act = () => CreateSut().HandleAsync(NewEvent());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private SuspiciousLoginAlertHandler CreateSut(bool enabled = true) =>
        new(_users.Object, _notifications.Object, _preferences.Object, BuildConfiguration(enabled));

    private static IConfiguration BuildConfiguration(bool enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:SecurityNotifications:Enabled"] = enabled.ToString()
            })
            .Build();

    private SuspiciousLoginDetectedV1 NewEvent(
            Guid? tenantId = null,
            string alertKind = SecurityAlertKinds.LoginStepUpRequired) =>
        new(_user.Id, alertKind, nameof(RiskLevel.High), 65)
        {
            TenantId = tenantId ?? DurableIntegrationEventTenants.Platform,
            ActorId = DurableIntegrationEventActors.System,
            AggregateType = "User",
            AggregateId = _user.Id.ToString()
        };

    private void VerifySent(
        SuspiciousLoginDetectedV1 @event,
        NotificationChannel channel,
        string? recipientEmail,
        Guid? tenantId) =>
        _notifications.Verify(service => service.SendAsync(
            _user.Id,
            NotificationType.Security,
            It.IsAny<string>(),
            It.IsAny<string>(),
            channel,
            tenantId,
            It.IsAny<string?>(),
            NotificationPriority.Urgent,
            @event.EventId,
            nameof(SuspiciousLoginDetectedV1),
            It.IsAny<string?>(),
            recipientEmail,
            It.IsAny<CancellationToken>()), Times.Once);
}
