using GameGuild.Identity.Authentication;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests;

public sealed class TrustedDeviceSessionCommandTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("foreign-owner")]
    [InlineData("wrong-session")]
    [InlineData("inactive")]
    [InlineData("expired")]
    [InlineData("termination-marker")]
    [InlineData("empty-fingerprint")]
    [InlineData("whitespace-fingerprint")]
    public async Task TrustRejectsAnUnavailableOrUnownedSessionWithoutCallingTheTrustMutation(string boundary)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        UserSession? session = new()
        {
            Id = sessionId, UserId = userId, IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddHours(1), DeviceFingerprint = "persisted-device"
        };
        switch (boundary)
        {
            case "missing": session = null; break;
            case "foreign-owner": session.UserId = Guid.NewGuid(); break;
            case "wrong-session": session.Id = Guid.NewGuid(); break;
            case "inactive": session.IsActive = false; break;
            case "expired": session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); break;
            case "termination-marker": session.TerminatedAt = DateTime.UtcNow; break;
            case "empty-fingerprint": session.DeviceFingerprint = string.Empty; break;
            case "whitespace-fingerprint": session.DeviceFingerprint = " "; break;
        }
        var service = new Mock<ISessionManagementService>(MockBehavior.Strict);
        service.Setup(s => s.GetSessionAsync(sessionId, CancellationToken.None)).ReturnsAsync(session);
        var handler = new SessionMutationCommandHandler(service.Object);
        var result = await handler.Handle(new TrustDeviceCommand(userId, sessionId, "Browser"), CancellationToken.None);
        Assert.False(result);
        service.Verify(s => s.GetSessionAsync(sessionId, CancellationToken.None), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmptyPrincipalIdentifiersAreRejectedBeforeAnySessionRead(bool emptyUser)
    {
        var service = new Mock<ISessionManagementService>(MockBehavior.Strict);
        var handler = new SessionMutationCommandHandler(service.Object);
        var result = await handler.Handle(new TrustDeviceCommand(
            emptyUser ? Guid.Empty : Guid.NewGuid(),
            emptyUser ? Guid.NewGuid() : Guid.Empty, "Browser"), CancellationToken.None);
        Assert.False(result);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidBindingUsesOnlyPersistedFingerprintAndPropagatesCancellationAndMutationOutcome(bool outcome)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var service = new Mock<ISessionManagementService>(MockBehavior.Strict);
        service.Setup(s => s.GetSessionAsync(sessionId, cancellation.Token)).ReturnsAsync(new UserSession
        {
            Id = sessionId, UserId = userId, IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddHours(1), DeviceFingerprint = "persisted-device"
        });
        service.Setup(s => s.TrustDeviceAsync(userId, "persisted-device", "Browser", cancellation.Token)).ReturnsAsync(outcome);
        var result = await new SessionMutationCommandHandler(service.Object)
            .Handle(new TrustDeviceCommand(userId, sessionId, "Browser"), cancellation.Token);
        Assert.Equal(outcome, result);
        service.VerifyAll();
        service.VerifyNoOtherCalls();
    }
}
