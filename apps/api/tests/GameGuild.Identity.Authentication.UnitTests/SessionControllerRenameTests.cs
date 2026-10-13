using System.Security.Claims;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication.UnitTests.Infrastructure;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests;

/// <summary>
///     Session custom naming endpoint (issue #278): PATCH /v1/auth/sessions/{id}/name.
///     The acting user is bound from the authenticated principal, never the body.
/// </summary>
public class SessionControllerRenameTests
{
    private readonly Mock<ISessionManagementService> _sessionService = new();
    private readonly SessionController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public SessionControllerRenameTests()
    {
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(value => value.ActorContext).Returns(ActorContext.Anonymous with
        {
            ActorKind = ActorKind.User, IsAuthenticated = true, SubjectId = _userId.ToString()
        });
        var users = new Mock<IUserRepository>();
        users.Setup(value => value.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _userId });
        _controller = new SessionController(_sessionService.Object, IdentityCommandTestSender.ForSessions(_sessionService.Object));
        SetUser();
    }

    private void SetUser()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString()),
            new(ClaimTypes.Email, "test@test.com")
        };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
                Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
            }
        };
    }

    [Fact]
    public async Task RenameSession_ValidName_ReturnsOk()
    {
        _sessionService
            .Setup(service => service.RenameSessionAsync(_userId, It.IsAny<Guid>(), "Family desktop", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.RenameSession(Guid.NewGuid(), new RenameSessionRequest { Name = " Family desktop " }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _sessionService.Verify(service => service.RenameSessionAsync(
            _userId, It.IsAny<Guid>(), "Family desktop", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenameSession_NotTheOwnersSessionOrInactive_ReturnsNotFound()
    {
        _sessionService
            .Setup(service => service.RenameSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.RenameSession(Guid.NewGuid(), new RenameSessionRequest { Name = "somewhere else" }, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("over-sixty-four-characters-over-sixty-four-characters-over-sixty-four-characters!!")]
    public async Task RenameSession_InvalidName_ReturnsBadRequestWithoutTouchingTheService(string name)
    {
        var result = await _controller.RenameSession(Guid.NewGuid(), new RenameSessionRequest { Name = name }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _sessionService.Verify(service => service.RenameSessionAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSessions_ReflectsPersistedDeviceAndLocationMetadata()
    {
        var sessionId = Guid.NewGuid();
        var session = new UserSession
        {
            Id = sessionId, UserId = _userId, IsActive = true,
            IpAddress = "203.0.113.10",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastUsedAt = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            DeviceInfo = SessionEnrichmentJson.SerializeDeviceInfo(new DeviceInfo
            {
                DeviceName = "Family desktop", Browser = "Chrome", OperatingSystem = "Windows", DeviceType = "Desktop"
            }),
            Location = SessionEnrichmentJson.SerializeLocation(new LocationInfo
            {
                IpAddress = "203.0.113.10", Country = "Brazil", City = "Sao Paulo"
            })
        };
        _sessionService
            .Setup(service => service.GetUserSessionsAsync(_userId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString()),
            new("session_id", sessionId.ToString())
        };
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var result = await _controller.GetSessions(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var sessions = ok.Value.Should().BeAssignableTo<List<SessionResponse>>().Subject;
        var response = sessions.Should().ContainSingle().Subject;
        response.IsCurrent.Should().BeTrue();
        response.DeviceInfo!.DeviceName.Should().Be("Family desktop");
        response.DeviceInfo.Browser.Should().Be("Chrome");
        response.Location!.Country.Should().Be("Brazil");
        response.Location.DisplayLocation.Should().Be("Sao Paulo, Brazil");
    }
}
