using Fido2NetLib;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class WebAuthnAuthenticationAuditTests
{
    [Fact]
    public async Task BeginAuthenticationAsync_WhenCredentialsAreMissing_AuditsFailureContext()
    {
        var userId = Guid.NewGuid();
        var credentialRepository = new Mock<IWebAuthnCredentialRepository>();
        credentialRepository
            .Setup(repository => repository.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var auditSink = new Mock<IAuthenticationAuditEventSink>();
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.40");
        httpContext.Request.Headers.UserAgent = "WebAuthnTest/1.0";
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);
        var service = new WebAuthnAuthenticationSubService(
            Mock.Of<IFido2>(),
            credentialRepository.Object,
            Mock.Of<IUserRepository>(),
            NullLogger<WebAuthnAuthenticationSubService>.Instance,
            auditSink.Object,
            httpContextAccessor.Object);

        var result = await service.BeginAuthenticationAsync(userId: userId);

        Assert.False(result.Success);
        auditSink.Verify(sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.ChallengeFailed" &&
                    auditEvent.UserId == userId &&
                    !auditEvent.Success &&
                    auditEvent.Method == "WebAuthn" &&
                    auditEvent.IpAddress == "192.0.2.40" &&
                    auditEvent.UserAgent == "WebAuthnTest/1.0" &&
                    auditEvent.ErrorMessage == "NoWebAuthnCredentials" &&
                    auditEvent.Metadata == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteAuthenticationAsync_WhenAssertionCannotBeParsed_AuditsSafeFailureWithoutBlockingResponse()
    {
        var auditSink = new Mock<IAuthenticationAuditEventSink>();
        auditSink
            .Setup(sink => sink.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Audit transport unavailable"));
        var service = new WebAuthnAuthenticationSubService(
            Mock.Of<IFido2>(),
            Mock.Of<IWebAuthnCredentialRepository>(),
            Mock.Of<IUserRepository>(),
            NullLogger<WebAuthnAuthenticationSubService>.Instance,
            auditSink.Object,
            Mock.Of<IHttpContextAccessor>());

        var result = await service.CompleteAuthenticationAsync(
            "assertion-payload-must-not-be-recorded",
            "198.51.100.40",
            "WebAuthnTest/2.0");

        Assert.False(result.Success);
        Assert.Equal("Failed to complete WebAuthn authentication", result.Error);
        auditSink.Verify(sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.Failed" &&
                    auditEvent.UserId == null &&
                    !auditEvent.Success &&
                    auditEvent.Method == "WebAuthn" &&
                    auditEvent.IpAddress == "198.51.100.40" &&
                    auditEvent.UserAgent == "WebAuthnTest/2.0" &&
                    auditEvent.ErrorMessage == "AssertionVerificationFailed" &&
                    auditEvent.Metadata == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
