using Xunit;
using Moq;
using MediatR;
using GameGuild.Modules.Auth.Commands;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.Auth.Handlers;
using GameGuild.Modules.Auth.Notifications;
using GameGuild.Modules.Auth.Services;

namespace GameGuild.Tests.Modules.Auth.Handlers;

public class LocalSignUpHandlerTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly LocalSignUpHandler _handler;

    public LocalSignUpHandlerTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _mockMediator = new Mock<IMediator>();
        _handler = new LocalSignUpHandler(_mockAuthService.Object, _mockMediator.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSignInResponse()
    {
        // Arrange
        var command = new LocalSignUpCommand
        {
            Email = "test@example.com",
            Password = "password123",
            Username = "testuser"
        };

        var expectedResponse = new SignInResponseDto
        {
            AccessToken = "mock-access-token",
            User = new UserDto
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                Username = "testuser"
            }
        };

        _mockAuthService.Setup(x => x.LocalSignUpAsync(It.IsAny<LocalSignUpRequestDto>()))
            .ReturnsAsync(expectedResponse);

        // Act
        SignInResponseDto result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("mock-access-token", result.AccessToken);
        Assert.Equal("test@example.com", result.User.Email);

        // Verify notification was published
        _mockMediator.Verify(x => x.Publish(It.IsAny<UserSignedUpNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AuthServiceThrows_PropagatesException()
    {
        // Arrange
        var command = new LocalSignUpCommand
        {
            Email = "test@example.com",
            Password = "password123",
            Username = "testuser"
        };

        _mockAuthService.Setup(x => x.LocalSignUpAsync(It.IsAny<LocalSignUpRequestDto>()))
            .ThrowsAsync(new InvalidOperationException("Email already exists"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        // Verify notification was not published when exception occurs
        _mockMediator.Verify(x => x.Publish(It.IsAny<UserSignedUpNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
