using FluentAssertions;
using GameGuild.CQRS;
using Moq;
using Xunit;

namespace GameGuild.Identity.Users.UnitTests.Commands;

public class SuspendUserCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?) null);
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
        user.IsSuspended.Should().BeTrue();
        publisher.Verify(p => p.Publish(It.IsAny<UserSuspendedNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class UnsuspendUserCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?) null);
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
        user.IsSuspended.Should().BeFalse();
        publisher.Verify(p => p.Publish(It.IsAny<UserUnsuspendedNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class RestoreUserCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_UserNotFound_Throws()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?) null);
        var handler = new RestoreUserCommandHandler(repo.Object, Mock.Of<IPublisher>());

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new RestoreUserCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidUser_RestoresAndPublishes()
    {
        var user = User.Create("r@test.com", "Restore");
        user.Version = 1;
        user.MarkDeleted();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repo.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var publisher = new Mock<IPublisher>();

        var handler = new RestoreUserCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(new RestoreUserCommand(user.Id), CancellationToken.None);

        result.Should().NotBeNull();
        user.IsDeleted.Should().BeFalse();
        user.IsActive.Should().BeTrue();
        publisher.Verify(p => p.Publish(It.IsAny<UserRestoredNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class BulkPurgeUsersCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_ValidUsers_PurgesAll()
    {
        var user1 = User.Create("a@test.com", "AA");
        var user2 = User.Create("b@test.com", "BB");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([user1, user2]);
        repo.Setup(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var publisher = new Mock<IPublisher>();

        var handler = new BulkPurgeUsersCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(
            new BulkPurgeUsersCommand([user1.Id, user2.Id], PurgeStrategy.Immediate),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        repo.Verify(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        publisher.Verify(p => p.Publish(It.IsAny<UserPurgedNotification>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_OneFailure_ContinuesWithOthers()
    {
        var user1 = User.Create("c@test.com", "CC");
        var user2 = User.Create("d@test.com", "DD");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([user1, user2]);
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
            new BulkPurgeUsersCommand([user1.Id, user2.Id], PurgeStrategy.GracePeriod),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        repo.Verify(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        publisher.Verify(p => p.Publish(It.IsAny<UserPurgedNotification>(), It.IsAny<CancellationToken>()), Times.Once);
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
            .ReturnsAsync([user]);
        repo.Setup(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var publisher = new Mock<IPublisher>();

        var handler = new BulkPurgeUsersCommandHandler(repo.Object, publisher.Object);
        await handler.Handle(new BulkPurgeUsersCommand([user.Id], strategy), CancellationToken.None);

        repo.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class BulkRestoreUsersCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_MissingUser_AddsToFailed()
    {
        var existingUser = User.Create("e@test.com", "EE");
        existingUser.Version = 1;
        existingUser.MarkDeleted();
        var missingId = Guid.NewGuid();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existingUser]);
        repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var publisher = new Mock<IPublisher>();

        var handler = new BulkRestoreUsersCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(
            new BulkRestoreUsersCommand([existingUser.Id, missingId]),
            CancellationToken.None);

        result.FailedUserIds.Should().Contain(missingId);
        result.RestoredUsers.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_RestoreThrows_AddsToFailed()
    {
        var user = User.Create("f@test.com", "FF");
        user.Version = 1;
        user.MarkDeleted();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([user]);
        repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("fail"));
        var publisher = new Mock<IPublisher>();

        var handler = new BulkRestoreUsersCommandHandler(repo.Object, publisher.Object);
        var result = await handler.Handle(new BulkRestoreUsersCommand([user.Id]), CancellationToken.None);

        result.FailedUserIds.Should().Contain(user.Id);
    }
}

public class BulkUpdateUsersCommandHandlerAdditionalTests
{
    [Fact]
    public async Task Handle_ValidUpdates_UpdatesUsers()
    {
        var user = User.Create("g@test.com", "Original");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([user]);
        repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new BulkUpdateUsersCommandHandler(repo.Object);
        var result = await handler.Handle(
            new BulkUpdateUsersCommand([
                new UpdateUserRequestItem(user.Id, "Updated", "+1234567890")
            ]),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        user.Name.Should().Be("Updated");
        user.PhoneNumber.Should().Be("+1234567890");
        repo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UserNotInDictionary_Skips()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var handler = new BulkUpdateUsersCommandHandler(repo.Object);
        var result = await handler.Handle(
            new BulkUpdateUsersCommand([
                new UpdateUserRequestItem(Guid.NewGuid(), "Name")
            ]),
            CancellationToken.None);

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
            .ReturnsAsync([user1, user2]);
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
            new BulkUpdateUsersCommand([
                new UpdateUserRequestItem(user1.Id, "XX"),
                new UpdateUserRequestItem(user2.Id, "YY")
            ]),
            CancellationToken.None);

        result.Should().Be(Unit.Value);
        repo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
