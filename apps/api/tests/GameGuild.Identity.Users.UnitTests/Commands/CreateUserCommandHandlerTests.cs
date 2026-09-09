using FluentAssertions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Users.UnitTests.Commands;

/// <summary>
/// Unit tests for CreateUserCommandHandler
/// </summary>
public class CreateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new CreateUserCommandHandler(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateUser()
    {
        // Arrange
        var command = new CreateUserCommand(
            Email: "test@example.com",
            Name: "Test User",
            PhoneNumber: "+1234567890"
        );

        _userRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(command.Email);
        result.Name.Should().Be(command.Name);
        result.PhoneNumber.Should().Be(command.PhoneNumber);
        result.IsActive.Should().BeTrue();
        result.Id.Should().NotBe(Guid.Empty);

        _userRepositoryMock.Verify(
            x => x.AddAsync(It.Is<User>(u => 
                u.Email == command.Email && 
                u.Name == command.Name && 
                u.PhoneNumber == command.PhoneNumber), 
            It.IsAny<CancellationToken>()), 
            Times.Once);

        _userRepositoryMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutPhoneNumber_ShouldCreateUser()
    {
        // Arrange
        var command = new CreateUserCommand(
            Email: "test@example.com",
            Name: "Test User"
        );

        _userRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(command.Email);
        result.Name.Should().Be(command.Name);
        result.PhoneNumber.Should().BeNull();
        result.IsActive.Should().BeTrue();

        _userRepositoryMock.Verify(
            x => x.AddAsync(It.Is<User>(u => 
                u.Email == command.Email && 
                u.Name == command.Name && 
                u.PhoneNumber == null), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Handle_RegistersDurableUserCreatedEvent()
    {
        User? persistedUser = null;
        _userRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => persistedUser = user)
            .Returns(Task.CompletedTask);
        _userRepositoryMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new CreateUserCommand("event@example.com", "Event User"), CancellationToken.None);

        persistedUser.Should().NotBeNull();
        persistedUser!.IntegrationEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserCreatedEvent>()
            .Which.UserId.Should().Be(persistedUser.Id);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var command = new CreateUserCommand(
            Email: "test@example.com",
            Name: "Test User"
        );

        _userRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldCallRepositoryMethodsInCorrectOrder()
    {
        // Arrange
        var command = new CreateUserCommand(
            Email: "test@example.com",
            Name: "Test User"
        );

        var callSequence = new List<string>();

        _userRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback(() => callSequence.Add("AddAsync"))
            .Returns(Task.CompletedTask);

        _userRepositoryMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callSequence.Add("SaveChangesAsync"))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        callSequence.Should().ContainInOrder("AddAsync", "SaveChangesAsync");
    }
}
