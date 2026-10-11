using FluentAssertions;
using GameGuild.API.Core.Security;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Options;
using Moq;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;

namespace GameGuild.API.UnitTests.Security;

/// <summary>
///     Tests for the composition-root handler that delivers in-app and email alerts for
///     permission-grant expiration lifecycle events (issue #331). Covers the config gate,
///     recipient resolution, channel selection, priority mapping, and failure tolerance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Security", "PermissionExpiration")]
public sealed class PermissionExpirationAlertHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly PermissionExpirationOptions _options = new() { Enabled = true };

    private PermissionExpirationAlertHandler CreateHandler() =>
        new(_users.Object, _notifications.Object, Options.Create(_options),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PermissionExpirationAlertHandler>.Instance);

    private static PermissionExpirationNotification CreateNotification(
        Guid? userId,
        PermissionExpirationKind kind = PermissionExpirationKind.Upcoming,
        Guid? tenantId = null,
        string[]? permissions = null) =>
        new(Guid.NewGuid(), userId, tenantId, permissions ?? ["courses:create", "courses:*"], DateTime.UtcNow.AddDays(3), kind);

    private User CreateUser(Guid userId, string email = "user@example.com") =>
        new() { Id = userId, Email = email, Name = "Test User" };

    [Fact]
    public async Task Handle_WhenDisabledByConfig_ShouldNotQueueAnything()
    {
        var userId = Guid.NewGuid();
        _options.Enabled = false;
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(userId));

        await CreateHandler().Handle(CreateNotification(userId), CancellationToken.None);

        _notifications.Verify(s => s.SendAsync(It.IsAny<Guid?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _users.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutUserId_ShouldNotTouchUsersOrNotifications()
    {
        await CreateHandler().Handle(CreateNotification(userId: null), CancellationToken.None);

        _users.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(s => s.SendAsync(It.IsAny<Guid?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldNotQueueAnything()
    {
        var userId = Guid.NewGuid();
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await CreateHandler().Handle(CreateNotification(userId), CancellationToken.None);

        _notifications.Verify(s => s.SendAsync(It.IsAny<Guid?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserIsDeleted_ShouldNotQueueAnything()
    {
        var userId = Guid.NewGuid();
        var deleted = CreateUser(userId);
        deleted.DeletedAt = DateTime.UtcNow; // EntityBase.IsDeleted is derived from DeletedAt; SoftDelete() refuses unpersisted entities.
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(deleted);

        await CreateHandler().Handle(CreateNotification(userId), CancellationToken.None);

        _notifications.Verify(s => s.SendAsync(It.IsAny<Guid?>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ForUpcomingExpiration_ShouldQueueInAppAndEmailWithNormalPriority()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var notification = CreateNotification(userId, PermissionExpirationKind.Upcoming, tenantId);
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(userId));
        SetupSendSuccess();

        await CreateHandler().Handle(notification, CancellationToken.None);

        _notifications.Verify(s => s.SendAsync(userId, NotificationType.Security, "Permissions expiring soon",
            It.Is<string>(m => m.Contains("expires on") && m.Contains("courses:create")),
            NotificationChannel.InApp, tenantId, null, NotificationPriority.Normal,
            notification.PermissionId, nameof(PermissionExpirationNotification), null, null,
            It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(s => s.SendAsync(userId, NotificationType.Security, "Permissions expiring soon",
            It.IsAny<string>(), NotificationChannel.Email, tenantId, null, NotificationPriority.Normal,
            notification.PermissionId, nameof(PermissionExpirationNotification), null, "user@example.com",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForExpiredGrant_ShouldQueueInAppAndEmailWithUrgentPriority()
    {
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId, PermissionExpirationKind.Expired);
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(userId));
        SetupSendSuccess();

        await CreateHandler().Handle(notification, CancellationToken.None);

        _notifications.Verify(s => s.SendAsync(userId, NotificationType.Security, "Permissions expired",
            It.Is<string>(m => m.Contains("expired on") && m.Contains("no longer grants access")),
            NotificationChannel.InApp, notification.TenantId, null, NotificationPriority.Urgent,
            notification.PermissionId, nameof(PermissionExpirationNotification), null, null,
            It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(s => s.SendAsync(userId, NotificationType.Security, "Permissions expired",
            It.IsAny<string>(), NotificationChannel.Email, notification.TenantId, null, NotificationPriority.Urgent,
            notification.PermissionId, nameof(PermissionExpirationNotification), null, "user@example.com",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserHasNoEmail_ShouldQueueOnlyInAppChannel()
    {
        var userId = Guid.NewGuid();
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(userId, email: "   "));
        SetupSendSuccess();

        await CreateHandler().Handle(CreateNotification(userId), CancellationToken.None);

        _notifications.Verify(s => s.SendAsync(userId, NotificationType.Security, It.IsAny<string>(), It.IsAny<string>(),
            NotificationChannel.InApp, It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), null, It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(s => s.SendAsync(userId, NotificationType.Security, It.IsAny<string>(), It.IsAny<string>(),
            NotificationChannel.Email, It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEmailChannelFails_ShouldStillSucceedWithoutThrowing()
    {
        var userId = Guid.NewGuid();
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(userId));
        SetupSendSuccess();
        _notifications
            .Setup(s => s.SendAsync(It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                NotificationChannel.Email, It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Notification>(Error.Failure("Notification.EmailFailed", "smtp down")));

        var act = () => CreateHandler().Handle(CreateNotification(userId), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WithManyPermissions_ShouldTruncateTheDisplayedList()
    {
        var userId = Guid.NewGuid();
        var permissions = Enumerable.Range(1, 8).Select(i => $"perm:{i}").ToArray();
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUser(userId));
        _notifications
            .Setup(s => s.SendAsync(It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                NotificationChannel.InApp, It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid?, NotificationType, string, string, NotificationChannel, Guid?, string?, NotificationPriority, Guid?, string?, string?, string?, CancellationToken>(
                (_, _, _, message, _, _, _, _, _, _, _, _, _) => message.Should().Contain("and 3 more").And.Contain("perm:5"))
            .ReturnsAsync(SuccessNotification());

        await CreateHandler().Handle(CreateNotification(userId, permissions: permissions), CancellationToken.None);
    }

    private void SetupSendSuccess()
    {
        _notifications
            .Setup(s => s.SendAsync(It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessNotification());
    }

    private static Notification SuccessNotification() =>
        Notification.Create(null, NotificationType.Security, NotificationChannel.InApp, "title", "message");

    [Theory]
    [InlineData(PermissionExpirationKind.Upcoming, NotificationChannel.InApp, false)]
    [InlineData(PermissionExpirationKind.Upcoming, NotificationChannel.Email, false)]
    [InlineData(PermissionExpirationKind.Expired, NotificationChannel.InApp, false)]
    [InlineData(PermissionExpirationKind.Expired, NotificationChannel.Email, false)]
    [InlineData(PermissionExpirationKind.Upcoming, NotificationChannel.InApp, true)]
    [InlineData(PermissionExpirationKind.Upcoming, NotificationChannel.Email, true)]
    [InlineData(PermissionExpirationKind.Expired, NotificationChannel.InApp, true)]
    [InlineData(PermissionExpirationKind.Expired, NotificationChannel.Email, true)]
    public async Task Handle_LogPrivacy_QueueFailurePreservesBothChannelsWithoutRecipientOrError(
        PermissionExpirationKind kind, NotificationChannel failedChannel, bool nullResult)
    {
        const string email = "private-expiration-owner@example.test";
        const string privateValue = "private-notification-value";
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId, kind, Guid.NewGuid());
        _users.Setup(repository => repository.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(userId, email));
        SetupSendSuccess();
        _notifications.Setup(service => service.SendAsync(
                It.IsAny<Guid?>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                failedChannel, It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nullResult ? (Result<Notification>)null!
                : Result.Failure<Notification>(Error.Failure("Notification." + privateValue, email + " " + privateValue)));
        var logger = new CapturingLogger();
        var handler = new PermissionExpirationAlertHandler(_users.Object, _notifications.Object, Options.Create(_options), logger);

        await handler.Handle(notification, CancellationToken.None);

        var priority = kind == PermissionExpirationKind.Expired ? NotificationPriority.Urgent : NotificationPriority.Normal;
        _notifications.Verify(service => service.SendAsync(userId, NotificationType.Security, It.IsAny<string>(), It.IsAny<string>(),
            NotificationChannel.InApp, notification.TenantId, null, priority, notification.PermissionId,
            nameof(PermissionExpirationNotification), null, null, CancellationToken.None), Times.Once);
        _notifications.Verify(service => service.SendAsync(userId, NotificationType.Security, It.IsAny<string>(), It.IsAny<string>(),
            NotificationChannel.Email, notification.TenantId, null, priority, notification.PermissionId,
            nameof(PermissionExpirationNotification), null, email, CancellationToken.None), Times.Once);
        logger.Entries.Should().ContainSingle();
        var log = logger.Entries[0];
        log.Level.Should().Be(Microsoft.Extensions.Logging.LogLevel.Warning);
        log.Exception.Should().BeNull();
        var properties = log.Properties;
        properties.Single(pair => pair.Key == "Kind").Value.Should().Be(kind);
        properties.Single(pair => pair.Key == "PermissionId").Value.Should().Be(notification.PermissionId);
        properties.Single(pair => pair.Key == "Channel").Value.Should().Be(
            failedChannel == NotificationChannel.InApp ? "in-app" : "email");
        properties.Single(pair => pair.Key == "TenantId").Value.Should().Be(notification.TenantId);
        properties.Should().NotContain(pair => pair.Key == "Error" || pair.Key == "UserId" || pair.Key == "Email" || pair.Key == "Token");
        foreach (var privateText in new[] { email, privateValue, userId.ToString() })
        {
            log.Message.Should().NotContain(privateText);
            properties.Should().OnlyContain(pair => pair.Value == null || !pair.Value.ToString()!.Contains(privateText, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Handle_LogPrivacy_SuccessfulDeliveryKeepsRecipientsWithoutFailureLogs()
    {
        const string email = "private-expiration-owner@example.test";
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId);
        _users.Setup(repository => repository.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(userId, email));
        SetupSendSuccess();
        var logger = new CapturingLogger();
        var handler = new PermissionExpirationAlertHandler(_users.Object, _notifications.Object, Options.Create(_options), logger);

        await handler.Handle(notification, CancellationToken.None);

        _notifications.Verify(service => service.SendAsync(userId, NotificationType.Security, It.IsAny<string>(), It.IsAny<string>(),
            NotificationChannel.InApp, notification.TenantId, null, NotificationPriority.Normal, notification.PermissionId,
            nameof(PermissionExpirationNotification), null, null, CancellationToken.None), Times.Once);
        _notifications.Verify(service => service.SendAsync(userId, NotificationType.Security, It.IsAny<string>(), It.IsAny<string>(),
            NotificationChannel.Email, notification.TenantId, null, NotificationPriority.Normal, notification.PermissionId,
            nameof(PermissionExpirationNotification), null, email, CancellationToken.None), Times.Once);
        logger.Entries.Should().BeEmpty();
    }
    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger<PermissionExpirationAlertHandler>
    {
        public List<CapturedLog> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            IReadOnlyList<KeyValuePair<string, object?>> properties =
                state is IEnumerable<KeyValuePair<string, object?>> values
                    ? values.ToArray()
                    : Array.Empty<KeyValuePair<string, object?>>();
            Entries.Add(new CapturedLog(logLevel, eventId, properties, exception, formatter(state, exception)));
        }
    }

    private sealed record CapturedLog(
        Microsoft.Extensions.Logging.LogLevel Level,
        Microsoft.Extensions.Logging.EventId EventId,
        IReadOnlyList<KeyValuePair<string, object?>> Properties,
        Exception? Exception,
        string Message);

}
