using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Identity.Users.UnitTests.Commands;

public class DeleteUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IActorContextAccessor> _actorContextAccessorMock;
    private readonly DeleteUserCommandHandler _handler;

    public DeleteUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _actorContextAccessorMock = new Mock<IActorContextAccessor>();
        _actorContextAccessorMock.Setup(x => x.ActorContext).Returns(ActorContext.Anonymous);
        _handler = new DeleteUserCommandHandler(
            _userRepositoryMock.Object,
            _actorContextAccessorMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldDeleteUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@example.com", Name = "Test User" };
        user.Version = 1;
        var command = new DeleteUserCommand(userId);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        // Handler uses soft delete via MarkDeleted() + UpdateAsync()
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u => u.IsDeleted), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldThrowUserNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new DeleteUserCommand(userId);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteUser_RecordsQuotaEventBeforePersistence()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@example.com", Name = "Test User" };
        user.Version = 1;
        user.SetProperties(new Dictionary<string, object?> { ["TenantId"] = tenantId });
        var command = new DeleteUserCommand(userId);

        var actorContext = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        };
        _actorContextAccessorMock.Setup(x => x.ActorContext).Returns(actorContext);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _userRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => user.IntegrationEvents.OfType<UserDeletedEvent>().Should().ContainSingle(e =>
                e.UserId == userId && e.TenantId == tenantId && e.ActorId == userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        // Handler uses soft delete via MarkDeleted() + UpdateAsync()
        _userRepositoryMock.Verify(x => x.UpdateAsync(It.Is<User>(u => u.IsDeleted), It.IsAny<CancellationToken>()), Times.Once);
        _userRepositoryMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
