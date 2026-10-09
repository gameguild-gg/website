using FluentAssertions;
using FluentValidation.TestHelper;
using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;
using Moq;
using static GameGuild.Identity.Users.UnitTests.JsonTestData;
using Xunit;

namespace GameGuild.Identity.Users.UnitTests;

#region SuspendUserCommandHandler Tests

public class SuspendUserCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new SuspendUserCommandHandler(repo.Object, Mock.Of<IPublisher>());
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new SuspendUserCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidUser_SuspendsAndPublishes()
    {
        var user = User.Create("test@test.com", "Test User");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repo.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var publisher = new Mock<IPublisher>();

        var handler = new SuspendUserCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(new SuspendUserCommand(user.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.Email.Should().Be("test@test.com");
        publisher.Verify(p => p.Publish(It.IsAny<UserSuspendedNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

#endregion

#region UnsuspendUserCommandHandler Tests

public class UnsuspendUserCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new UnsuspendUserCommandHandler(repo.Object, Mock.Of<IPublisher>());
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new UnsuspendUserCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidUser_UnsuspendsAndPublishes()
    {
        var user = User.Create("u@test.com", "User");
        user.Suspend();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repo.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var publisher = new Mock<IPublisher>();

        var handler = new UnsuspendUserCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(new UnsuspendUserCommand(user.Id), CancellationToken.None);

        result.Should().NotBeNull();
        publisher.Verify(p => p.Publish(It.IsAny<UserUnsuspendedNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

#endregion

#region RestoreUserCommandHandler Tests

public class RestoreUserCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new RestoreUserCommandHandler(repo.Object, Mock.Of<IPublisher>());
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new RestoreUserCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidUser_RestoresAndPublishes()
    {
        var user = User.Create("r@test.com", "Restore");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repo.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var publisher = new Mock<IPublisher>();

        var handler = new RestoreUserCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(new RestoreUserCommand(user.Id), CancellationToken.None);

        result.Should().NotBeNull();
        publisher.Verify(p => p.Publish(It.IsAny<UserRestoredNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

#endregion

#region BulkPurgeUsersCommandHandler Tests

public class BulkPurgeUsersCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_ValidUsers_PurgesAll()
    {
        var user1 = User.Create("a@test.com", "AA");
        var user2 = User.Create("b@test.com", "BB");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user1, user2 });
        repo.Setup(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var publisher = new Mock<IPublisher>();

        var handler = new BulkPurgeUsersCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(
            new BulkPurgeUsersCommand(new[] { user1.Id, user2.Id }, PurgeStrategy.Immediate), CancellationToken.None);

        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Handle_OneFailure_ContinuesWithOthers()
    {
        var user1 = User.Create("c@test.com", "CC");
        var user2 = User.Create("d@test.com", "DD");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user1, user2 });
        var callCount = 0;
        repo.Setup(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new Exception("DB error");
                }

                return Task.CompletedTask;
            });
        var publisher = new Mock<IPublisher>();

        var handler = new BulkPurgeUsersCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(
            new BulkPurgeUsersCommand(new[] { user1.Id, user2.Id }, PurgeStrategy.GracePeriod), CancellationToken.None);

        result.Should().Be(Unit.Value);
    }

    [Theory]
    [InlineData(PurgeStrategy.Immediate)]
    [InlineData(PurgeStrategy.Scheduled)]
    [InlineData(PurgeStrategy.GracePeriod)]
    public async Task Handle_AllStrategies_CallDelete(PurgeStrategy strategy)
    {
        var user = User.Create("s@test.com", "SS");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        repo.Setup(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var publisher = new Mock<IPublisher>();

        var handler = new BulkPurgeUsersCommandHandler(repo.Object, publisher.Object);
        await handler.Handle(new BulkPurgeUsersCommand(new[] { user.Id }, strategy), CancellationToken.None);

        repo.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }
}

#endregion

#region BulkRestoreUsersCommandHandler Tests

public class BulkRestoreUsersCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_MissingUser_AddsToFailed()
    {
        var existingUser = User.Create("e@test.com", "EE");
        var missingId = Guid.NewGuid();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existingUser });
        repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var publisher = new Mock<IPublisher>();

        var handler = new BulkRestoreUsersCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(
            new BulkRestoreUsersCommand(new[] { existingUser.Id, missingId }), CancellationToken.None);

        result.FailedUserIds.Should().Contain(missingId);
        result.RestoredUsers.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_RestoreThrows_AddsToFailed()
    {
        var user = User.Create("f@test.com", "FF");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("fail"));
        var publisher = new Mock<IPublisher>();

        var handler = new BulkRestoreUsersCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(
            new BulkRestoreUsersCommand(new[] { user.Id }), CancellationToken.None);

        result.FailedUserIds.Should().Contain(user.Id);
    }
}

#endregion

#region BulkUpdateUsersCommandHandler Tests

public class BulkUpdateUsersCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_ValidUpdates_UpdatesUsers()
    {
        var user = User.Create("g@test.com", "Original");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user });
        repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new BulkUpdateUsersCommandHandler(repo.Object);
        var result = await handler.Handle(
            new BulkUpdateUsersCommand(new[]
            {
                new UpdateUserRequestItem(user.Id, "Updated", "+1234567890")
            }), CancellationToken.None);

        result.Should().Be(Unit.Value);
        repo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotInDict_Skips()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var handler = new BulkUpdateUsersCommandHandler(repo.Object);
        var result = await handler.Handle(
            new BulkUpdateUsersCommand(new[]
            {
                new UpdateUserRequestItem(Guid.NewGuid(), "Name")
            }), CancellationToken.None);

        result.Should().Be(Unit.Value);
        repo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UpdateThrows_ContinuesWithOthers()
    {
        var user1 = User.Create("h@test.com", "HH");
        var user2 = User.Create("i@test.com", "II");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { user1, user2 });
        var count = 0;
        repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                count++;
                if (count == 1)
                {
                    throw new Exception("fail");
                }

                return Task.CompletedTask;
            });

        var handler = new BulkUpdateUsersCommandHandler(repo.Object);
        var result = await handler.Handle(
            new BulkUpdateUsersCommand(new[]
            {
                new UpdateUserRequestItem(user1.Id, "XX"),
                new UpdateUserRequestItem(user2.Id, "YY")
            }), CancellationToken.None);

        result.Should().Be(Unit.Value);
    }
}

#endregion

#region BulkNotification CommandHandler Tests

public class BulkArchiveNotificationsCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_EmptyIds_ReturnsUnit()
    {
        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserNotification>());
        var handler = new BulkArchiveNotificationsCommandHandler(repo.Object);
        var result = await handler.Handle(
            new BulkArchiveNotificationsCommand(Guid.NewGuid(), new List<Guid>()), CancellationToken.None);
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Handle_WithNotifications_ArchivesAll()
    {
        var userId = Guid.NewGuid();
        var n1 = UserNotification.Create(userId, "type", "Title1", "Content1");
        var n2 = UserNotification.Create(userId, "type", "Title2", "Content2");
        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetByIdsAsync(userId, It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserNotification> { n1, n2 });
        repo.Setup(r => r.UpdateAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new BulkArchiveNotificationsCommandHandler(repo.Object);
        var result = await handler.Handle(
            new BulkArchiveNotificationsCommand(userId, new List<Guid> { n1.Id, n2.Id }), CancellationToken.None);

        result.Should().Be(Unit.Value);
    }
}

public class BulkUnarchiveNotificationsCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_EmptyIds_ReturnsUnit()
    {
        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserNotification>());
        var handler = new BulkUnarchiveNotificationsCommandHandler(repo.Object);
        var result = await handler.Handle(
            new BulkUnarchiveNotificationsCommand(Guid.NewGuid(), new List<Guid>()), CancellationToken.None);
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Handle_WithNotifications_UnarchivesAll()
    {
        var userId = Guid.NewGuid();
        var n1 = UserNotification.Create(userId, "type", "T1", "C1");
        n1.Archive();
        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetByIdsAsync(userId, It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserNotification> { n1 });
        repo.Setup(r => r.UpdateAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new BulkUnarchiveNotificationsCommandHandler(repo.Object);
        await handler.Handle(
            new BulkUnarchiveNotificationsCommand(userId, new List<Guid> { n1.Id }), CancellationToken.None);

        repo.Verify(r => r.UpdateAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class BulkMarkNotificationsAsReadCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_EmptyIds_ReturnsUnit()
    {
        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserNotification>());
        var handler = new BulkMarkNotificationsAsReadCommandHandler(repo.Object);
        var result = await handler.Handle(
            new BulkMarkNotificationsAsReadCommand(Guid.NewGuid(), new List<Guid>()), CancellationToken.None);
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Handle_WithNotifications_MarksAllRead()
    {
        var userId = Guid.NewGuid();
        var n1 = UserNotification.Create(userId, "type", "T1", "C1");
        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetByIdsAsync(userId, It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserNotification> { n1 });
        repo.Setup(r => r.UpdateAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new BulkMarkNotificationsAsReadCommandHandler(repo.Object);
        await handler.Handle(
            new BulkMarkNotificationsAsReadCommand(userId, new List<Guid> { n1.Id }), CancellationToken.None);

        repo.Verify(r => r.UpdateAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class BulkMarkNotificationsAsUnreadCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_EmptyIds_ReturnsUnit()
    {
        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserNotification>());
        var handler = new BulkMarkNotificationsAsUnreadCommandHandler(repo.Object);
        var result = await handler.Handle(
            new BulkMarkNotificationsAsUnreadCommand(Guid.NewGuid(), new List<Guid>()), CancellationToken.None);
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Handle_WithNotifications_MarksAllUnread()
    {
        var userId = Guid.NewGuid();
        var n1 = UserNotification.Create(userId, "type", "T1", "C1");
        n1.MarkAsRead();
        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetByIdsAsync(userId, It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserNotification> { n1 });
        repo.Setup(r => r.UpdateAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new BulkMarkNotificationsAsUnreadCommandHandler(repo.Object);
        await handler.Handle(
            new BulkMarkNotificationsAsUnreadCommand(userId, new List<Guid> { n1.Id }), CancellationToken.None);

        repo.Verify(r => r.UpdateAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

#endregion

#region UnarchiveNotificationCommandHandler Tests

public class UnarchiveNotificationCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new UnarchiveNotificationCommandHandler(userRepo.Object, Mock.Of<IUserNotificationRepository>());
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new UnarchiveNotificationCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NotificationNotFound_Throws()
    {
        var user = User.Create("ua@test.com", "UAUA");
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var notifRepo = new Mock<IUserNotificationRepository>();
        notifRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNotification?)null);

        var handler = new UnarchiveNotificationCommandHandler(userRepo.Object, notifRepo.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UnarchiveNotificationCommand(user.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NotificationBelongsToOtherUser_Throws()
    {
        var user = User.Create("ub@test.com", "UBUB");
        var otherId = Guid.NewGuid();
        var notif = UserNotification.Create(otherId, "type", "T", "C");
        notif.Archive();
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var notifRepo = new Mock<IUserNotificationRepository>();
        notifRepo.Setup(r => r.GetByIdAsync(notif.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notif);

        var handler = new UnarchiveNotificationCommandHandler(userRepo.Object, notifRepo.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UnarchiveNotificationCommand(user.Id, notif.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidNotification_Unarchives()
    {
        var user = User.Create("uc@test.com", "UCUC");
        var notif = UserNotification.Create(user.Id, "type", "T", "C");
        notif.Archive();
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var notifRepo = new Mock<IUserNotificationRepository>();
        notifRepo.Setup(r => r.GetByIdAsync(notif.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notif);
        notifRepo.Setup(r => r.UpdateAsync(notif, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        notifRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new UnarchiveNotificationCommandHandler(userRepo.Object, notifRepo.Object);
        var result = await handler.Handle(
            new UnarchiveNotificationCommand(user.Id, notif.Id), CancellationToken.None);
        result.Should().Be(Unit.Value);
    }
}

#endregion

#region Localization Preferences CommandHandler Tests

public class ReplaceUserLocalizationPreferencesCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new ReplaceUserLocalizationPreferencesCommandHandler(userRepo.Object, Mock.Of<IUserPreferencesRepository>());
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new ReplaceUserLocalizationPreferencesCommand(
                Guid.NewGuid(), new ReplaceUserLocalizationPreferencesRequest(JsonMap(new Dictionary<string, object?> { ["lang"] = "en" }))), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PreferencesExist_ReplacesLocalization()
    {
        var user = User.Create("lp@test.com", "LPLP");
        var prefs = UserPreferences.Create(user.Id);
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var prefsRepo = new Mock<IUserPreferencesRepository>();
        prefsRepo.Setup(r => r.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(prefs);
        prefsRepo.Setup(r => r.UpdateAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        prefsRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new ReplaceUserLocalizationPreferencesCommandHandler(userRepo.Object, prefsRepo.Object);
        var result = await handler.Handle(
            new ReplaceUserLocalizationPreferencesCommand(user.Id,
                new ReplaceUserLocalizationPreferencesRequest(JsonMap(new Dictionary<string, object?> { ["lang"] = "fr" }))),
            CancellationToken.None);
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Handle_PreferencesNotExist_CreatesAndReplaces()
    {
        var user = User.Create("lp2@test.com", "LP2LP2");
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var prefsRepo = new Mock<IUserPreferencesRepository>();
        prefsRepo.Setup(r => r.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((UserPreferences?)null);
        prefsRepo.Setup(r => r.AddAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        prefsRepo.Setup(r => r.UpdateAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        prefsRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new ReplaceUserLocalizationPreferencesCommandHandler(userRepo.Object, prefsRepo.Object);
        var result = await handler.Handle(
            new ReplaceUserLocalizationPreferencesCommand(user.Id,
                new ReplaceUserLocalizationPreferencesRequest(JsonMap(new Dictionary<string, object?> { ["lang"] = "es" }))),
            CancellationToken.None);
        result.Should().Be(Unit.Value);
        prefsRepo.Verify(r => r.AddAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class UpdateUserLocalizationPreferencesCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new UpdateUserLocalizationPreferencesCommandHandler(userRepo.Object, Mock.Of<IUserPreferencesRepository>());
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new UpdateUserLocalizationPreferencesCommand(
                Guid.NewGuid(), new UpdateUserLocalizationPreferencesRequest(JsonMap(new Dictionary<string, object?> { ["tz"] = "UTC" }))), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ExistingPrefs_MergesLocalization()
    {
        var user = User.Create("up@test.com", "UPUP");
        var prefs = UserPreferences.Create(user.Id);
        prefs.SetLocalizationPreferences(new Dictionary<string, object?> { ["lang"] = "en" });
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var prefsRepo = new Mock<IUserPreferencesRepository>();
        prefsRepo.Setup(r => r.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(prefs);
        prefsRepo.Setup(r => r.UpdateAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        prefsRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new UpdateUserLocalizationPreferencesCommandHandler(userRepo.Object, prefsRepo.Object);
        var result = await handler.Handle(
            new UpdateUserLocalizationPreferencesCommand(user.Id,
                new UpdateUserLocalizationPreferencesRequest(JsonMap(new Dictionary<string, object?> { ["tz"] = "UTC" }))),
            CancellationToken.None);
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Handle_NoExistingPrefs_CreatesAndUpdates()
    {
        var user = User.Create("up2@test.com", "UP2UP2");
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var prefsRepo = new Mock<IUserPreferencesRepository>();
        prefsRepo.Setup(r => r.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((UserPreferences?)null);
        prefsRepo.Setup(r => r.AddAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        prefsRepo.Setup(r => r.UpdateAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        prefsRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new UpdateUserLocalizationPreferencesCommandHandler(userRepo.Object, prefsRepo.Object);
        await handler.Handle(
            new UpdateUserLocalizationPreferencesCommand(user.Id,
                new UpdateUserLocalizationPreferencesRequest(JsonMap(new Dictionary<string, object?> { ["lang"] = "de" }))),
            CancellationToken.None);
        prefsRepo.Verify(r => r.AddAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class ResetUserLocalizationPreferencesCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new ResetUserLocalizationPreferencesCommandHandler(userRepo.Object, Mock.Of<IUserPreferencesRepository>());
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new ResetUserLocalizationPreferencesCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NoPreferences_ReturnsUnit()
    {
        var user = User.Create("res@test.com", "ResRes");
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var prefsRepo = new Mock<IUserPreferencesRepository>();
        prefsRepo.Setup(r => r.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((UserPreferences?)null);

        var handler = new ResetUserLocalizationPreferencesCommandHandler(userRepo.Object, prefsRepo.Object);
        var result = await handler.Handle(
            new ResetUserLocalizationPreferencesCommand(user.Id), CancellationToken.None);
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task Handle_PreferencesExist_ResetsToEmpty()
    {
        var user = User.Create("res2@test.com", "Res2Res2");
        var prefs = UserPreferences.Create(user.Id);
        prefs.SetLocalizationPreferences(new Dictionary<string, object?> { ["lang"] = "en" });
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var prefsRepo = new Mock<IUserPreferencesRepository>();
        prefsRepo.Setup(r => r.GetByUserIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(prefs);
        prefsRepo.Setup(r => r.UpdateAsync(It.IsAny<UserPreferences>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        prefsRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new ResetUserLocalizationPreferencesCommandHandler(userRepo.Object, prefsRepo.Object);
        var result = await handler.Handle(
            new ResetUserLocalizationPreferencesCommand(user.Id), CancellationToken.None);
        result.Should().Be(Unit.Value);
    }
}

#endregion

#region Validator Tests

public class CreateUserRequestValidatorAdditionalTests
{
    private readonly CreateUserRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Should_Pass()
    {
        var result = _validator.TestValidate(new CreateUserRequest("test@test.com", "Test User"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_Email_Should_Fail()
    {
        var result = _validator.TestValidate(new CreateUserRequest("", "Test"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Invalid_Email_Should_Fail()
    {
        var result = _validator.TestValidate(new CreateUserRequest("notanemail", "Test"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Empty_Name_Should_Fail()
    {
        var result = _validator.TestValidate(new CreateUserRequest("t@t.com", ""));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Long_Phone_Should_Fail()
    {
        var result = _validator.TestValidate(new CreateUserRequest("t@t.com", "Test", new string('1', 21)));
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }
}

public class UpdateUserRequestValidatorAdditionalTests
{
    private readonly UpdateUserRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Should_Pass()
    {
        var result = _validator.TestValidate(new UpdateUserRequest("Updated Name"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_Name_Should_Fail()
    {
        var result = _validator.TestValidate(new UpdateUserRequest(""));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Long_Phone_Should_Fail()
    {
        var result = _validator.TestValidate(new UpdateUserRequest("Test", new string('1', 21)));
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }
}

public class BulkUpdateUsersCommandValidatorAdditionalTests
{
    private readonly BulkUpdateUsersCommandValidator _validator = new();

    [Fact]
    public void Valid_Command_Should_Pass()
    {
        var result = _validator.TestValidate(new BulkUpdateUsersCommand(
            new[] { new UpdateUserRequestItem(Guid.NewGuid(), "ValidName") }));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_Updates_Should_Fail()
    {
        var result = _validator.TestValidate(new BulkUpdateUsersCommand(Array.Empty<UpdateUserRequestItem>()));
        result.ShouldHaveValidationErrorFor(x => x.Updates);
    }

    [Fact]
    public void Invalid_Item_Name_Should_Fail()
    {
        var result = _validator.TestValidate(new BulkUpdateUsersCommand(
            new[] { new UpdateUserRequestItem(Guid.NewGuid(), "") }));
        result.ShouldHaveValidationErrorFor("Updates[0].Name");
    }
}

public class UpdateUserRequestItemValidatorAdditionalTests
{
    private readonly UpdateUserRequestItemValidator _validator = new();

    [Fact]
    public void Valid_Item_Should_Pass()
    {
        var result = _validator.TestValidate(new UpdateUserRequestItem(Guid.NewGuid(), "ValidName"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_UserId_Should_Fail()
    {
        var result = _validator.TestValidate(new UpdateUserRequestItem(Guid.Empty, "ValidName"));
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Short_Name_Should_Fail()
    {
        var result = _validator.TestValidate(new UpdateUserRequestItem(Guid.NewGuid(), "A"));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Long_Name_Should_Fail()
    {
        var result = _validator.TestValidate(new UpdateUserRequestItem(Guid.NewGuid(), new string('A', 101)));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Long_Phone_Should_Fail()
    {
        var result = _validator.TestValidate(new UpdateUserRequestItem(Guid.NewGuid(), "ValidName", new string('1', 21)));
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }
}

public class CreateUserRequestItemValidatorAdditionalTests
{
    private readonly CreateUserRequestItemValidator _validator = new();

    [Fact]
    public void Valid_Item_Should_Pass()
    {
        var result = _validator.TestValidate(new CreateUserRequestItem("e@e.com", "ValidName"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_Email_Should_Fail()
    {
        var result = _validator.TestValidate(new CreateUserRequestItem("", "ValidName"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Invalid_Email_Should_Fail()
    {
        var result = _validator.TestValidate(new CreateUserRequestItem("bad", "ValidName"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Empty_Name_Should_Fail()
    {
        var result = _validator.TestValidate(new CreateUserRequestItem("e@e.com", ""));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}

#endregion

#region Exception Tests

public class UserAlreadyExistsExceptionAdditionalTests
{
    [Fact]
    public void Default_Constructor()
    {
        var ex = new UserAlreadyExistsException();
        ex.Should().NotBeNull();
        ex.Email.Should().Be(string.Empty);
    }

    [Fact]
    public void Email_Constructor()
    {
        var ex = new UserAlreadyExistsException("dup@test.com");
        ex.Message.Should().Contain("dup@test.com");
        ex.Email.Should().Be("dup@test.com");
    }

    [Fact]
    public void InnerException_Constructor()
    {
        var inner = new Exception("inner");
        var ex = new UserAlreadyExistsException("msg", inner);
        ex.Message.Should().Be("msg");
        ex.InnerException.Should().Be(inner);
    }
}

public class UserNotFoundExceptionAdditionalTests
{
    [Fact]
    public void Guid_Constructor()
    {
        var id = Guid.NewGuid();
        var ex = new UserNotFoundException(id);
        ex.Message.Should().Contain(id.ToString());
        ex.UserId.Should().Be(id);
    }

    [Fact]
    public void InnerException_Constructor()
    {
        var inner = new Exception("inner");
        var ex = new UserNotFoundException("msg", inner);
        ex.Message.Should().Be("msg");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void Default_Constructor()
    {
        var ex = new UserNotFoundException();
        ex.Should().NotBeNull();
    }
}

#endregion

#region GetUserNotificationsPagedQueryHandler Tests

public class GetUserNotificationsPagedQueryHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_ReturnsPagedNotifications()
    {
        var userId = Guid.NewGuid();
        var n1 = UserNotification.Create(userId, "info", "Title1", "Content1");
        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetPagedByUserIdAsync(
                userId, 1, 20, null, null, "desc", null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserNotification> { n1 }, 1));

        var handler = new GetUserNotificationsPagedQueryHandler(repo.Object);
        var result = await handler.Handle(
            new GetUserNotificationsPagedQuery(userId), CancellationToken.None);

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithMetadata_DeserializesJson()
    {
        var userId = Guid.NewGuid();
        var n1 = UserNotification.Create(userId, "alert", "Alert", "Alert content");
        // Set metadata via reflection since it's likely a private setter
        var metaProp = typeof(UserNotification).GetProperty("Metadata");
        if (metaProp != null && metaProp.CanWrite)
        {
            metaProp.SetValue(n1, "{\"key\":\"value\"}");
        }

        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetPagedByUserIdAsync(
                userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserNotification> { n1 }, 1));

        var handler = new GetUserNotificationsPagedQueryHandler(repo.Object);
        var result = await handler.Handle(
            new GetUserNotificationsPagedQuery(userId, PageNumber: 1, PageSize: 10), CancellationToken.None);

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsEmptyResult()
    {
        var userId = Guid.NewGuid();
        var repo = new Mock<IUserNotificationRepository>();
        repo.Setup(r => r.GetPagedByUserIdAsync(
                userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserNotification>(), 0));

        var handler = new GetUserNotificationsPagedQueryHandler(repo.Object);
        var result = await handler.Handle(
            new GetUserNotificationsPagedQuery(userId), CancellationToken.None);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }
}

#endregion

#region GetUserProfilesPagedQueryHandler Tests

public class GetUserProfilesPagedQueryHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_ReturnsPagedProfiles()
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "TestUser");
        var repo = new Mock<IUserProfileRepository>();
        repo.Setup(r => r.GetProfilesPagedAsync(
                null, null, "asc", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserProfile> { profile }, 1));

        var handler = new GetUserProfilesPagedQueryHandler(repo.Object);
        var result = await handler.Handle(
            new GetUserProfilesPagedQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithSearch_PassesParameters()
    {
        var repo = new Mock<IUserProfileRepository>();
        repo.Setup(r => r.GetProfilesPagedAsync(
                "search", "displayname", "desc", 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserProfile>(), 0));

        var handler = new GetUserProfilesPagedQueryHandler(repo.Object);
        var result = await handler.Handle(
            new GetUserProfilesPagedQuery("search", "displayname", "desc", 2, 10), CancellationToken.None);

        result.TotalCount.Should().Be(0);
    }
}

#endregion

#region UserPreferences Additional Method Tests

public class UserPreferencesAdditionalMethodTests
{
    [Fact]
    public void SetAndGetNotificationPreferences_RoundTrips()
    {
        var prefs = UserPreferences.Create(Guid.NewGuid());
        var dict = new Dictionary<string, object?> { ["enabled"] = true, ["level"] = "all" };
        prefs.SetNotificationPreferences(dict);
        var result = prefs.GetNotificationPreferences();
        result.Should().ContainKey("enabled");
    }

    [Fact]
    public void SetAndGetAccessibilityPreferences_RoundTrips()
    {
        var prefs = UserPreferences.Create(Guid.NewGuid());
        var dict = new Dictionary<string, object?> { ["highContrast"] = true };
        prefs.SetAccessibilityPreferences(dict);
        var result = prefs.GetAccessibilityPreferences();
        result.Should().ContainKey("highContrast");
    }

    [Fact]
    public void SetAndGetPrivacyPreferences_RoundTrips()
    {
        var prefs = UserPreferences.Create(Guid.NewGuid());
        var dict = new Dictionary<string, object?> { ["shareData"] = false };
        prefs.SetPrivacyPreferences(dict);
        var result = prefs.GetPrivacyPreferences();
        result.Should().ContainKey("shareData");
    }

    [Fact]
    public void PartialConstructor_Works()
    {
        var prefs = new UserPreferences(new object());
        prefs.Should().NotBeNull();
    }
}

#endregion

#region Entity Partial Constructor Tests

public class EntityPartialConstructorTests
{
    [Fact]
    public void User_PartialConstructor_Works()
    {
        var user = new User(new object());
        user.Should().NotBeNull();
    }

    [Fact]
    public void UserNotification_PartialConstructor_Works()
    {
        var notif = new UserNotification(new object());
        notif.Should().NotBeNull();
    }

    [Fact]
    public void UserMetadata_PartialConstructor_Works()
    {
        var meta = new UserMetadata(new object());
        meta.Should().NotBeNull();
    }
}

#endregion

#region UserPreferences Catch Block Tests

public class UserPreferencesCatchBlockTests
{
    [Fact]
    public void GetNotificationPreferences_InvalidJson_ReturnsCatchBlock()
    {
        var prefs = UserPreferences.Create(Guid.NewGuid());
        // Set invalid JSON via reflection to trigger catch block
        typeof(UserPreferences).GetProperty("NotificationPreferences")!.SetValue(prefs, "not valid json");
        var result = prefs.GetNotificationPreferences();
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAccessibilityPreferences_InvalidJson_ReturnsCatchBlock()
    {
        var prefs = UserPreferences.Create(Guid.NewGuid());
        typeof(UserPreferences).GetProperty("AccessibilityPreferences")!.SetValue(prefs, "{bad");
        var result = prefs.GetAccessibilityPreferences();
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetPrivacyPreferences_InvalidJson_ReturnsCatchBlock()
    {
        var prefs = UserPreferences.Create(Guid.NewGuid());
        typeof(UserPreferences).GetProperty("PrivacyPreferences")!.SetValue(prefs, "<xml/>");
        var result = prefs.GetPrivacyPreferences();
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetLocalizationPreferences_InvalidJson_ReturnsCatchBlock()
    {
        var prefs = UserPreferences.Create(Guid.NewGuid());
        typeof(UserPreferences).GetProperty("LocalizationPreferences")!.SetValue(prefs, "broken json");
        var result = prefs.GetLocalizationPreferences();
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}

#endregion

#region EF Configuration Tests

public class TestUsersDbContext : DbContext
{
    public TestUsersDbContext(DbContextOptions<TestUsersDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<UserPreferences> UserPreferencesSet => Set<UserPreferences>();
    public DbSet<UserMetadata> UserMetadataSet => Set<UserMetadata>();
    public DbSet<UserProfile> UserProfilesSet => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserNotificationConfiguration());
        modelBuilder.ApplyConfiguration(new UserPreferencesConfiguration());
        modelBuilder.ApplyConfiguration(new UserMetadataConfiguration());
        modelBuilder.ApplyConfiguration(new UserProfileConfiguration());
    }
}

public class EfConfigurationTests : IDisposable
{
    private readonly TestUsersDbContext _context;

    public EfConfigurationTests()
    {
        var options = new DbContextOptionsBuilder<TestUsersDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestUsers_{Guid.NewGuid()}")
            .Options;
        _context = new TestUsersDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public void UserConfiguration_ModelBuildsSuccessfully()
    {
        var model = _context.Model;
        var userEntity = model.FindEntityType(typeof(User));
        userEntity.Should().NotBeNull();
    }

    [Fact]
    public void UserNotificationConfiguration_ModelBuildsSuccessfully()
    {
        var model = _context.Model;
        var entity = model.FindEntityType(typeof(UserNotification));
        entity.Should().NotBeNull();
    }

    [Fact]
    public void UserPreferencesConfiguration_ModelBuildsSuccessfully()
    {
        var model = _context.Model;
        var entity = model.FindEntityType(typeof(UserPreferences));
        entity.Should().NotBeNull();
    }

    [Fact]
    public void UserMetadataConfiguration_ModelBuildsSuccessfully()
    {
        var model = _context.Model;
        var entity = model.FindEntityType(typeof(UserMetadata));
        entity.Should().NotBeNull();
    }

    [Fact]
    public void UserProfileConfiguration_ModelBuildsSuccessfully()
    {
        var model = _context.Model;
        var entity = model.FindEntityType(typeof(UserProfile));
        entity.Should().NotBeNull();
    }

    [Fact]
    public async Task UserCrud_InMemory_Works()
    {
        var user = User.Create("eftest@test.com", "EF Test User");
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var found = await _context.Users.FindAsync(user.Id);
        found.Should().NotBeNull();
        found!.Email.Should().Be("eftest@test.com");
    }

    [Fact]
    public async Task UserNotificationCrud_InMemory_Works()
    {
        var userId = Guid.NewGuid();
        var notif = UserNotification.Create(userId, "info", "Test Notif", "Content");
        notif.Metadata = "{}";
        _context.UserNotifications.Add(notif);
        await _context.SaveChangesAsync();

        var found = await _context.UserNotifications.FindAsync(notif.Id);
        found.Should().NotBeNull();
    }

    [Fact]
    public async Task UserPreferencesCrud_InMemory_Works()
    {
        var prefs = UserPreferences.Create(Guid.NewGuid());
        _context.UserPreferencesSet.Add(prefs);
        await _context.SaveChangesAsync();

        var found = await _context.UserPreferencesSet.FindAsync(prefs.Id);
        found.Should().NotBeNull();
    }

    [Fact]
    public async Task UserMetadataCrud_InMemory_Works()
    {
        var meta = UserMetadata.Create(Guid.NewGuid());
        _context.UserMetadataSet.Add(meta);
        await _context.SaveChangesAsync();

        var found = await _context.UserMetadataSet.FindAsync(meta.Id);
        found.Should().NotBeNull();
    }

    [Fact]
    public async Task UserProfileCrud_InMemory_Works()
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Profile");
        _context.UserProfilesSet.Add(profile);
        await _context.SaveChangesAsync();

        var found = await _context.UserProfilesSet.FindAsync(profile.Id);
        found.Should().NotBeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

#endregion
