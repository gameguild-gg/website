using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class AuthAttemptServiceSuccessPathTests
{
    [Fact]
    public async Task RecordFailedAttemptAsync_ShouldSanitizeOnlyTheLoggedFailureReason()
    {
        var repository = new Mock<IAuthenticationAttemptRepository>();
        var enumerationProtection = new Mock<IUserEnumerationProtectionService>();
        var auditEventSink = new Mock<IAuthenticationAuditEventSink>();
        var logger = new CapturingLogger<AuthAttemptService>();
        AuthenticationAttempt? captured = null;
        var rawFailureReason = "Invalid\r\ncredentials\t" + new string('x', 300);

        repository
            .Setup(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<AuthenticationAttempt, CancellationToken>((attempt, _) => captured = attempt)
            .ReturnsAsync((AuthenticationAttempt attempt, CancellationToken _) => attempt);

        var sut = new AuthAttemptService(repository.Object, enumerationProtection.Object, logger, auditEventSink.Object);

        await sut.RecordFailedAttemptAsync(
            "user@example.com",
            null,
            "203.0.113.11",
            null,
            rawFailureReason,
            TimeSpan.FromMilliseconds(75));

        captured.Should().NotBeNull();
        captured!.FailureReason.Should().Be(rawFailureReason);
        auditEventSink.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent => auditEvent.ErrorMessage == rawFailureReason),
            It.IsAny<CancellationToken>()), Times.Once);

        var auditLog = logger.Messages.Single(message => message.Contains("AuthenticationFailed", StringComparison.Ordinal));
        var loggedFailureReason = auditLog.Split("FailureReason=", StringSplitOptions.None)[1]
            .Split(", ProcessingTimeMs=", StringSplitOptions.None)[0];

        loggedFailureReason.Should().StartWith("Invalid credentials ");
        loggedFailureReason.Should().NotContainAny("\r", "\n", "\t");
        loggedFailureReason.Length.Should().Be(256);
    }

    [Fact]
    public async Task RecordSuccessfulAttemptAsync_ShouldPersistSuccessfulAttempt()
    {
        var repository = new Mock<IAuthenticationAttemptRepository>();
        var auditEventSink = new Mock<IAuthenticationAuditEventSink>();
        AuthenticationAttempt? captured = null;

        repository
            .Setup(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<AuthenticationAttempt, CancellationToken>((attempt, _) => captured = attempt)
            .ReturnsAsync((AuthenticationAttempt attempt, CancellationToken _) => attempt);

        var sut = new AuthAttemptService(
            repository.Object,
            Mock.Of<IUserEnumerationProtectionService>(),
            NullLogger<AuthAttemptService>.Instance,
            auditEventSink.Object);

        var userId = Guid.NewGuid();

        await sut.RecordSuccessfulAttemptAsync(
            "user@example.com",
            userId,
            "198.51.100.1",
            "UnitTestAgent",
            TimeSpan.FromMilliseconds(42));

        captured.Should().NotBeNull();
        captured!.Email.Should().Be("user@example.com");
        captured.UserId.Should().Be(userId);
        captured.IpAddress.Should().Be("198.51.100.1");
        captured.UserAgent.Should().Be("UnitTestAgent");
        captured.IsSuccessful.Should().BeTrue();
        captured.FailureReason.Should().BeNull();
        captured.ProcessingTime.Should().Be(TimeSpan.FromMilliseconds(42));
        captured.AttemptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        repository.Verify(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()), Times.Once);
        auditEventSink.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent =>
                auditEvent.ActionType == "Authentication.Succeeded" &&
                auditEvent.UserId == userId &&
                auditEvent.IpAddress == "198.51.100.1" &&
                auditEvent.UserAgent == "UnitTestAgent"),
        It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordSuccessfulAttemptAsync_ShouldForwardAuditWhenAttemptPersistenceFails()
    {
        var repository = new Mock<IAuthenticationAttemptRepository>();
        var auditEventSink = new Mock<IAuthenticationAuditEventSink>();
        repository
            .Setup(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Attempt store unavailable"));

        var sut = new AuthAttemptService(
            repository.Object,
            Mock.Of<IUserEnumerationProtectionService>(),
            NullLogger<AuthAttemptService>.Instance,
            auditEventSink.Object);
        var userId = Guid.NewGuid();

        await sut.RecordSuccessfulAttemptAsync(
            "user@example.com",
            userId,
            "198.51.100.2",
            "UnitTestAgent",
            TimeSpan.FromMilliseconds(42));

        auditEventSink.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent =>
                auditEvent.ActionType == "Authentication.Succeeded" &&
                auditEvent.UserId == userId &&
                auditEvent.IpAddress == "198.51.100.2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_ShouldPersistFailureAndRecordEnumerationAttempt()
    {
        var repository = new Mock<IAuthenticationAttemptRepository>();
        var enumerationProtection = new Mock<IUserEnumerationProtectionService>();
        var auditEventSink = new Mock<IAuthenticationAuditEventSink>();
        AuthenticationAttempt? captured = null;

        repository
            .Setup(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .Callback<AuthenticationAttempt, CancellationToken>((attempt, _) => captured = attempt)
            .ReturnsAsync((AuthenticationAttempt attempt, CancellationToken _) => attempt);

        var sut = new AuthAttemptService(
            repository.Object,
            enumerationProtection.Object,
            NullLogger<AuthAttemptService>.Instance,
            auditEventSink.Object);

        await sut.RecordFailedAttemptAsync(
            "user@example.com",
            Guid.NewGuid(),
            "203.0.113.9",
            "UnitTestAgent",
            "InvalidCredentials",
            TimeSpan.FromMilliseconds(75));

        captured.Should().NotBeNull();
        captured!.IsSuccessful.Should().BeFalse();
        captured.FailureReason.Should().Be("InvalidCredentials");
        captured.IpAddress.Should().Be("203.0.113.9");
        captured.UserAgent.Should().Be("UnitTestAgent");
        captured.ProcessingTime.Should().Be(TimeSpan.FromMilliseconds(75));
        captured.AttemptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        enumerationProtection.Verify(x => x.RecordEnumerationAttemptAsync("203.0.113.9", "login"), Times.Once);
        auditEventSink.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent =>
                auditEvent.ActionType == "Authentication.Failed" &&
                !auditEvent.Success &&
                auditEvent.ErrorMessage == "InvalidCredentials"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_ShouldForwardAuditAndRecordEnumerationWhenAttemptPersistenceFails()
    {
        var repository = new Mock<IAuthenticationAttemptRepository>();
        var enumerationProtection = new Mock<IUserEnumerationProtectionService>();
        var auditEventSink = new Mock<IAuthenticationAuditEventSink>();
        repository
            .Setup(x => x.CreateAsync(It.IsAny<AuthenticationAttempt>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Attempt store unavailable"));

        var sut = new AuthAttemptService(
            repository.Object,
            enumerationProtection.Object,
            NullLogger<AuthAttemptService>.Instance,
            auditEventSink.Object);

        await sut.RecordFailedAttemptAsync(
            "user@example.com",
            null,
            "203.0.113.10",
            "UnitTestAgent",
            "InvalidCredentials",
            TimeSpan.FromMilliseconds(75));

        auditEventSink.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent =>
                auditEvent.ActionType == "Authentication.Failed" &&
                !auditEvent.Success &&
                auditEvent.ErrorMessage == "InvalidCredentials"),
            It.IsAny<CancellationToken>()), Times.Once);
        enumerationProtection.Verify(x => x.RecordEnumerationAttemptAsync("203.0.113.10", "login"), Times.Once);
    }
}

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }
}

public sealed class RevokeTokenHandlerTests
{
    [Fact]
    public async Task Handle_ShouldUseFallbackIpAndReturnUnitValue()
    {
        var authService = new Mock<IAuthService>();
        var ownerId = Guid.NewGuid();
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(value => value.ActorContext).Returns(ActorContext.Anonymous with
        {
            ActorKind = ActorKind.User, SubjectId = ownerId.ToString(), IsAuthenticated = true
        });
        var tokens = new Mock<IRefreshTokenRepository>();
        tokens.Setup(value => value.GetByTokenAsync("stored-hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken { UserId = ownerId });
        var hasher = new Mock<IRefreshTokenHasher>();
        hasher.Setup(value => value.HashToken("refresh-token")).Returns("stored-hash");
        var handler = new RevokeTokenHandler(authService.Object, NullLogger<RevokeTokenHandler>.Instance,
            actor.Object, tokens.Object, hasher.Object);
        var command = new RevokeTokenCommand
        {
            RefreshToken = "refresh-token",
            IpAddress = null
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().Be(Unit.Value);
        authService.Verify(x => x.RevokeRefreshTokenAsync("refresh-token", "Unknown", It.IsAny<CancellationToken>()), Times.Once);
    }
}
