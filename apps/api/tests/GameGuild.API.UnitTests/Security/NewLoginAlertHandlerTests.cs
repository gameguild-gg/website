using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Core.Security;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;
using Xunit;

namespace GameGuild.API.UnitTests.Security;

/// <summary>
///     Ordinary-new-login owner emails (issue #278): strictly opt-in via the
///     <c>security.new_login_email</c> user notification preference (default off) and fail
///     closed when the preference cannot be read.
/// </summary>
public class NewLoginAlertHandlerTests
{
    private const string Email = "new-login-owner@example.test";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserPreferencesRepository> _preferences = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<INotificationPreferenceService> _delivery = new();
    private readonly User _user = User.Create(Email, "New login owner");

    public NewLoginAlertHandlerTests()
    {
        _user.Id = Guid.NewGuid();
        _user.Username = "new-login-owner";
        _users.Setup(repository => repository.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_user);
        _delivery.Setup(service => service.DecideDeliveryAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<NotificationChannel>(),
                It.IsAny<NotificationPriority>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.Send());
        _notifications.Setup(service => service.SendAsync(
                It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Notification.Create(
                _user.Id, NotificationType.Security, NotificationChannel.Email, "New sign-in to your account", "message", null)));
    }

    [Fact]
    public async Task HandleAsync_SendsNothing_ByDefault()
    {
        // Default off: no preference row at all.
        _preferences.Setup(repository => repository.GetByUserIdAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferences?)null);

        await CreateSut().HandleAsync(NewEvent());

        VerifyNoEmailSent();
    }

    [Fact]
    public async Task HandleAsync_SendsNothing_WhenPreferenceKeyIsMissing()
    {
        StorePreferences(new Dictionary<string, object?>());

        await CreateSut().HandleAsync(NewEvent());

        VerifyNoEmailSent();
    }

    [Theory]
    [InlineData(false)]
    [InlineData("maybe")]
    [InlineData(1)]
    public async Task HandleAsync_SendsNothing_WhenPreferenceIsNotBooleanTrue(object value)
    {
        StorePreferences(new Dictionary<string, object?> { [NewLoginAlertHandler.PreferenceKey] = value });

        await CreateSut().HandleAsync(NewEvent());

        VerifyNoEmailSent();
    }

    [Fact]
    public async Task HandleAsync_SendsEmail_WhenUserOptedIn()
    {
        StorePreferences(new Dictionary<string, object?>
        {
            [NewLoginAlertHandler.PreferenceKey] = JsonSerializer.SerializeToElement(true)
        });

        await CreateSut().HandleAsync(NewEvent());

        _notifications.Verify(service => service.SendAsync(
            _user.Id,
            NotificationType.Security,
            It.Is<string>(title => title.Contains("New sign-in", StringComparison.Ordinal)),
            It.Is<string>(message =>
                message.Contains("new sign-in", StringComparison.Ordinal)
                && !message.Contains(Email, StringComparison.Ordinal)
                && !message.Contains("203.0.113.10", StringComparison.Ordinal)),
            NotificationChannel.Email,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            NotificationPriority.Normal,
            It.IsAny<Guid?>(),
            nameof(NewLoginDetectedV1),
            It.IsAny<string?>(),
            Email,
            It.IsAny<CancellationToken>()), Times.Once);
        _delivery.Verify(service => service.DecideDeliveryAsync(
            _user.Id, NotificationType.Security, NotificationChannel.Email, NotificationPriority.Normal,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FailsClosed_WhenPreferenceReadThrows()
    {
        _preferences.Setup(repository => repository.GetByUserIdAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("preferences store unavailable"));

        var act = () => CreateSut().HandleAsync(NewEvent());

        // Fail closed AND fail safe: an unreadable preference must neither send the email nor
        // crash the durable consumer.
        await act.Should().NotThrowAsync();
        VerifyNoEmailSent();
    }

    [Fact]
    public async Task HandleAsync_SendsNothing_WhenFeatureIsDisabled()
    {
        StorePreferences(new Dictionary<string, object?>
        {
            [NewLoginAlertHandler.PreferenceKey] = JsonSerializer.SerializeToElement(true)
        });

        await CreateSut(enabled: false).HandleAsync(NewEvent());

        VerifyNoEmailSent();
    }

    [Fact]
    public async Task HandleAsync_SendsNothing_WhenDeliveryDecisionDropsEmail()
    {
        StorePreferences(new Dictionary<string, object?>
        {
            [NewLoginAlertHandler.PreferenceKey] = JsonSerializer.SerializeToElement(true)
        });
        _delivery.Setup(service => service.DecideDeliveryAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<NotificationChannel>(),
                It.IsAny<NotificationPriority>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.Drop("quiet hours"));

        await CreateSut().HandleAsync(NewEvent());

        VerifyNoEmailSent();
    }

    [Fact]
    public async Task HandleAsync_SendsNothing_WhenOwnerIsUnknown()
    {
        _users.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await CreateSut().HandleAsync(NewEvent());

        VerifyNoEmailSent();
    }

    [Fact]
    public async Task HandleAsync_SendsNothing_WhenOwnerHasNoEmail()
    {
        _user.Email = string.Empty;

        await CreateSut().HandleAsync(NewEvent());

        VerifyNoEmailSent();
    }

    private NewLoginAlertHandler CreateSut(bool enabled = true) =>
        new(_users.Object, _preferences.Object, _notifications.Object, _delivery.Object,
            BuildConfiguration(enabled), NullLogger<NewLoginAlertHandler>.Instance);

    private static IConfiguration BuildConfiguration(bool enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:NewLoginNotifications:Enabled"] = enabled.ToString()
            })
            .Build();

    private NewLoginDetectedV1 NewEvent() =>
        new(_user.Id, Guid.NewGuid(), "Chrome 120 on Windows")
        {
            TenantId = DurableIntegrationEventTenants.Platform,
            ActorId = DurableIntegrationEventActors.System,
            AggregateType = "User",
            AggregateId = _user.Id.ToString()
        };

    private void StorePreferences(Dictionary<string, object?> notificationPreferences)
    {
        var preferences = new UserPreferences { UserId = _user.Id };
        preferences.SetNotificationPreferences(notificationPreferences);
        _preferences.Setup(repository => repository.GetByUserIdAsync(_user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);
    }

    private void VerifyNoEmailSent() =>
        _notifications.Verify(service => service.SendAsync(
            It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
}
