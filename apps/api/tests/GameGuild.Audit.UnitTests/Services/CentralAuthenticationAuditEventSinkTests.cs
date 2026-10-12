using FluentAssertions;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class CentralAuthenticationAuditEventSinkTests
{
    private readonly IApplicationDbContext _context = Mock.Of<IApplicationDbContext>();
    [Theory]
    [InlineData("Authentication.MfaSignInRequired")]
    [InlineData("Authentication.MfaSignInDenied")]
    [InlineData("Authentication.MfaSignInEnrollmentStarted")]
    [InlineData("Authentication.MfaSignInVerified")]
    public async Task RequiredMfaAuditPersistenceFailuresReachTheOwningCommand(string action)
    {
        var audit = new Mock<ISecurityEventLogger>(MockBehavior.Strict);
        var failure = new InvalidOperationException("Synthetic required audit persistence failure.");
        audit.Setup(port => port.RecordInCommandAsync(_context, It.IsAny<CreateAuditLogRequest>(), CancellationToken.None)).ThrowsAsync(failure);
        var sink = new CentralAuthenticationAuditEventSink(audit.Object, NullLogger<CentralAuthenticationAuditEventSink>.Instance, _context);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sink.RecordAsync(
            new AuthenticationAuditEvent(action, Guid.NewGuid(), true, "Totp"), CancellationToken.None));
        Assert.Same(failure, exception);
        audit.Verify(port => port.RecordInCommandAsync(_context, It.IsAny<CreateAuditLogRequest>(), CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData("Authentication.MfaSignInRequired", SecurityEventCaptureOutcome.SpooledLocally)]
    [InlineData("Authentication.MfaSignInDenied", SecurityEventCaptureOutcome.SpooledLocally)]
    [InlineData("Authentication.MfaSignInEnrollmentStarted", SecurityEventCaptureOutcome.SpooledLocally)]
    [InlineData("Authentication.MfaSignInVerified", SecurityEventCaptureOutcome.SpooledLocally)]
    [InlineData("Authentication.MfaSignInRequired", SecurityEventCaptureOutcome.SpoolingDisabled)]
    [InlineData("Authentication.MfaSignInDenied", SecurityEventCaptureOutcome.SpoolingDisabled)]
    [InlineData("Authentication.MfaSignInEnrollmentStarted", SecurityEventCaptureOutcome.SpoolingDisabled)]
    [InlineData("Authentication.MfaSignInVerified", SecurityEventCaptureOutcome.SpoolingDisabled)]
    public async Task RequiredMfaAuditCannotCommitFromNonTransactionalCapture(string action, SecurityEventCaptureOutcome outcome)
    {
        var audit = new Mock<ISecurityEventLogger>(MockBehavior.Strict);
        audit.Setup(port => port.RecordInCommandAsync(_context, It.IsAny<CreateAuditLogRequest>(), CancellationToken.None))
            .ReturnsAsync((IApplicationDbContext _, CreateAuditLogRequest request, CancellationToken _) => Capture(request) with { Outcome = outcome });
        var sink = new CentralAuthenticationAuditEventSink(audit.Object, NullLogger<CentralAuthenticationAuditEventSink>.Instance, _context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sink.RecordAsync(
            new AuthenticationAuditEvent(action, Guid.NewGuid(), true, "Totp"), CancellationToken.None));
        audit.Verify(port => port.RecordInCommandAsync(_context, It.IsAny<CreateAuditLogRequest>(), CancellationToken.None), Times.Once);
    }

    private readonly Mock<ISecurityEventLogger> _securityEvents = new();

    private static SecurityEventCaptureResult Capture(CreateAuditLogRequest? request) =>
        new(
            SecurityEventCaptureOutcome.PersistedToDatabase,
            SecurityEventTaxonomy.Classify(
                request?.ActionType ?? string.Empty,
                request?.Category ?? AuditCategory.General,
                request?.Success ?? false,
                request?.RiskLevel),
            Guid.NewGuid());

    [Fact]
    public async Task RecordAsync_MapsAuthenticationContextToSecurityEventPipeline()
    {
        CreateAuditLogRequest? captured = null;
        _securityEvents
            .Setup(service => service.RecordAsync(It.IsAny<CreateAuditLogRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateAuditLogRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync((CreateAuditLogRequest request, CancellationToken _) => Capture(request));
        var sink = new CentralAuthenticationAuditEventSink(
            _securityEvents.Object,
            NullLogger<CentralAuthenticationAuditEventSink>.Instance, _context);
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await sink.RecordAsync(new AuthenticationAuditEvent(
            "Authentication.Failed",
            userId,
            false,
            "Password",
            "192.0.2.20",
            "UnitTestAgent",
            sessionId,
            tenantId,
            "InvalidCredentials",
            new { Attempt = 2 }),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.ActionType.Should().Be("Authentication.Failed");
        captured.Category.Should().Be(AuditCategory.Authentication);
        captured.UserId.Should().Be(userId);
        captured.SessionId.Should().Be(sessionId);
        captured.TenantId.Should().Be(tenantId);
        captured.IpAddress.Should().Be("192.0.2.20");
        captured.UserAgent.Should().Be("UnitTestAgent");
        captured.Success.Should().BeFalse();
        captured.ErrorMessage.Should().Be("InvalidCredentials");
        captured.Description.Should().Contain("Password");
    }

    [Fact]
    public async Task RecordAsync_MapsAssessedRiskLevelForSuccessfulThreatEvents()
    {
        CreateAuditLogRequest? captured = null;
        _securityEvents
            .Setup(service => service.RecordAsync(It.IsAny<CreateAuditLogRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateAuditLogRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync((CreateAuditLogRequest request, CancellationToken _) => Capture(request));
        var sink = new CentralAuthenticationAuditEventSink(
            _securityEvents.Object,
            NullLogger<CentralAuthenticationAuditEventSink>.Instance, _context);

        await sink.RecordAsync(new AuthenticationAuditEvent(
            "Authentication.ThreatDetected",
            Guid.NewGuid(),
            true,
            "Password",
            Metadata: new { RiskScore = 35 },
            AssessedRiskLevel: RiskLevel.Medium),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Success.Should().BeTrue();
        captured.ActionType.Should().Be("Authentication.ThreatDetected");
        captured.RiskLevel.Should().Be(AuditRiskLevel.Medium);
    }

    [Fact]
    public async Task RecordAsync_SwallowsExceptionsSoAuthenticationOperationsNeverFail()
    {
        _securityEvents
            .Setup(service => service.RecordAsync(It.IsAny<CreateAuditLogRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pipeline unavailable"));
        var sink = new CentralAuthenticationAuditEventSink(
            _securityEvents.Object,
            NullLogger<CentralAuthenticationAuditEventSink>.Instance, _context);

        var act = () => sink.RecordAsync(
            new AuthenticationAuditEvent("Authentication.Login", Guid.NewGuid(), true, "Password"),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordAsync_ForwardsCancellationToThePipeline()
    {
        var cancellationToken = new CancellationToken(canceled: true);
        CancellationToken forwarded = CancellationToken.None;
        _securityEvents
            .Setup(service => service.RecordAsync(It.IsAny<CreateAuditLogRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateAuditLogRequest, CancellationToken>((_, token) => forwarded = token)
            .ReturnsAsync((CreateAuditLogRequest request, CancellationToken _) => Capture(request));
        var sink = new CentralAuthenticationAuditEventSink(
            _securityEvents.Object,
            NullLogger<CentralAuthenticationAuditEventSink>.Instance, _context);

        await sink.RecordAsync(
            new AuthenticationAuditEvent("Authentication.Login", Guid.NewGuid(), true, "Password"),
            cancellationToken);

        forwarded.Should().Be(cancellationToken);
    }
}
