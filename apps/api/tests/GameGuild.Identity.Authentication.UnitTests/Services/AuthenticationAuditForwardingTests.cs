using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class AuthenticationAuditForwardingTests
{
    [Fact]
    public async Task MfaAttempt_IsForwardedToCentralAuditWithRequestContext()
    {
        var repository = new Mock<IMfaAttemptRepository>();
        var auditEventSink = new Mock<IAuthenticationAuditEventSink>();
        repository
            .Setup(x => x.CreateAsync(It.IsAny<MfaAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MfaAttempt attempt, CancellationToken _) => attempt);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.25");
        httpContext.Request.Headers.UserAgent = "MfaUnitTestAgent";
        var service = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            repository.Object,
            new HttpContextAccessor { HttpContext = httpContext },
            auditEventSink: auditEventSink.Object);
        var userId = Guid.NewGuid();

        await service.RecordMfaAttemptAsync(userId, MfaMethod.Totp, false, "InvalidCode", "device-hash", CancellationToken.None);

        auditEventSink.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent =>
                auditEvent.ActionType == "Authentication.MfaFailed" &&
                auditEvent.UserId == userId &&
                auditEvent.Method == nameof(MfaMethod.Totp) &&
                auditEvent.IpAddress == "198.51.100.25" &&
                auditEvent.UserAgent == "MfaUnitTestAgent" &&
                auditEvent.ErrorMessage == "InvalidCode"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MfaAttempt_IsForwardedWhenLocalAttemptPersistenceFails()
    {
        var repository = new Mock<IMfaAttemptRepository>();
        var auditEventSink = new Mock<IAuthenticationAuditEventSink>();
        repository
            .Setup(x => x.CreateAsync(It.IsAny<MfaAttempt>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Attempt store unavailable"));
        auditEventSink
            .Setup(x => x.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            repository.Object,
            new HttpContextAccessor(),
            auditEventSink: auditEventSink.Object);
        var userId = Guid.NewGuid();

        await service.RecordMfaAttemptAsync(userId, MfaMethod.BackupCode, false, "InvalidCode", null, CancellationToken.None);

        auditEventSink.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent =>
                auditEvent.ActionType == "Authentication.MfaFailed" &&
                auditEvent.UserId == userId &&
                auditEvent.Method == nameof(MfaMethod.BackupCode) &&
                auditEvent.ErrorMessage == "InvalidCode"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FailedMfaAttempt_IsAuditedWhenConfigurationPersistenceFails()
    {
        var configurationRepository = new Mock<IUserMfaConfigurationRepository>();
        configurationRepository
            .Setup(x => x.UpdateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("MFA configuration store unavailable"));
        var attemptRepository = new Mock<IMfaAttemptRepository>();
        attemptRepository
            .Setup(x => x.CreateAsync(It.IsAny<MfaAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MfaAttempt attempt, CancellationToken _) => attempt);
        var auditEventSink = new Mock<IAuthenticationAuditEventSink>();
        auditEventSink
            .Setup(x => x.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            configurationRepository.Object,
            attemptRepository.Object,
            new HttpContextAccessor(),
            auditEventSink: auditEventSink.Object);
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordFailedMfaAttemptAsync(
            new UserMfaConfiguration { UserId = userId },
            MfaMethod.Totp,
            "InvalidCode",
            null,
            CancellationToken.None));

        auditEventSink.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent =>
                auditEvent.ActionType == "Authentication.MfaFailed" &&
                auditEvent.UserId == userId &&
                auditEvent.ErrorMessage == "InvalidCode"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
