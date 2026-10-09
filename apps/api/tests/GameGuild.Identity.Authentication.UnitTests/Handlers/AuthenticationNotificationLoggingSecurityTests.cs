using FluentAssertions;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public class AuthenticationNotificationLoggingSecurityTests
{
    private const string SensitiveEmail = "private-person@example.test";
    private const string Token = "private-reset-or-login-token";

    [Theory]
    [InlineData("verification", "success")]
    [InlineData("verification", "missing-user")]
    [InlineData("verification", "failure")]
    [InlineData("reset", "success")]
    [InlineData("reset", "missing-user")]
    [InlineData("reset", "failure")]
    [InlineData("magic", "success")]
    [InlineData("magic", "missing-user")]
    [InlineData("magic", "failure")]
    [InlineData("welcome", "success")]
    [InlineData("welcome", "failure")]
    public async Task QueueHandler_LogsNoPersonalDataOrToken_InAnyOutcome(string kind, string outcome)
    {
        var user = new User { Id = Guid.NewGuid(), Email = SensitiveEmail };
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByEmailAsync(SensitiveEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome == "missing-user" ? null : user);
        var service = NotificationQueueStub.Success();
        var queueFailure = new InvalidOperationException($"Queue rejected {SensitiveEmail}, token {Token}");
        if (outcome == "failure")
        {
            service.Setup(s => s.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
                    It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(queueFailure);
        }

        var entries = kind switch
        {
            "verification" => await CaptureAsync<SendEmailVerificationRequestedHandler>(
                logger => new SendEmailVerificationRequestedHandler(logger, service.Object, repository.Object)
                    .Handle(new EmailVerificationRequestedNotification { Email = SensitiveEmail, Token = Token }, CancellationToken.None),
                outcome, queueFailure),
            "reset" => await CaptureAsync<SendPasswordResetRequestedHandler>(
                logger => new SendPasswordResetRequestedHandler(logger, service.Object, repository.Object)
                    .Handle(new PasswordResetRequestedNotification { Email = SensitiveEmail, Token = Token }, CancellationToken.None),
                outcome, queueFailure),
            "magic" => await CaptureAsync<SendMagicLinkRequestedHandler>(
                logger => new SendMagicLinkRequestedHandler(logger, service.Object, repository.Object)
                    .Handle(new MagicLinkRequestedNotification { Email = SensitiveEmail, Token = Token }, CancellationToken.None),
                outcome, queueFailure),
            "welcome" => await CaptureAsync<SendWelcomeEmailHandler>(
                logger => new SendWelcomeEmailHandler(logger, service.Object, repository.Object)
                    .Handle(new UserSignedUpNotification { UserId = user.Id, Email = SensitiveEmail, Username = "private-name" }, CancellationToken.None),
                outcome, queueFailure),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var entry = entries.Should().ContainSingle().Subject;
        entry.Message.Should().Contain(LogRedaction.MaskEmail(SensitiveEmail))
            .And.MatchRegex("email:[0-9a-f]{64}").And.NotContain("p***@e***.test");
        AssertNoSensitiveData(entry);
        if (outcome == "failure")
        {
            entry.Message.Should().Contain(nameof(InvalidOperationException));
        }
        if (kind == "welcome")
        {
            entry.Message.Should().NotContain(user.Id.ToString());
            string.Join("|", entry.Properties.Select(p => p.Value)).Should().NotContain(user.Id.ToString());
        }
    }

    [Fact]
    public async Task SignInEvent_RedactsPersonalFieldsAndSanitizesLogSeparators()
    {
        var logger = new TestLogger<UserSignedInEventHandler>();
        var userId = Guid.NewGuid();
        const string ipAddress = "198.51.100.17";
        var handler = new UserSignedInEventHandler(logger);
        await handler.Handle(new TestUserSignedInEvent(userId, SensitiveEmail, "Password\r\nforged\u2028entry", ipAddress,
            "browser", DateTime.UtcNow), CancellationToken.None);

        var entry = logger.Entries.Should().ContainSingle().Subject;
        AssertNoSensitiveData(new LogOutput(entry.Message, entry.Exception, entry.Properties));
        entry.Message.Should().NotContain(userId.ToString()).And.NotContain(ipAddress)
            .And.NotContain("\r").And.NotContain("\n").And.NotContain("\u2028");
        string.Join("|", entry.Properties.Select(p => p.Value)).Should()
            .NotContain(userId.ToString()).And.NotContain(ipAddress).And.NotContain("\r").And.NotContain("\n");
    }

    private static async Task<IReadOnlyList<LogOutput>> CaptureAsync<T>(
        Func<TestLogger<T>, Task> invoke, string outcome, Exception queueFailure)
    {
        var logger = new TestLogger<T>();
        if (outcome == "failure")
        {
            var thrown = await FluentActions.Awaiting(() => invoke(logger)).Should().ThrowAsync<InvalidOperationException>();
            thrown.Which.Should().BeSameAs(queueFailure);
        }
        else
        {
            await invoke(logger);
        }

        return logger.Entries.Select(e => new LogOutput(e.Message, e.Exception, e.Properties)).ToArray();
    }

    private static void AssertNoSensitiveData(LogOutput entry)
    {
        entry.Message.Should().NotContain(SensitiveEmail).And.NotContain(Token);
        entry.Exception.Should().BeNull("exception messages may include personal data or bearer tokens");
        string.Join("|", entry.Properties.Select(p => p.Value)).Should().NotContain(SensitiveEmail).And.NotContain(Token);
    }

    private sealed record LogOutput(string Message, Exception? Exception, IReadOnlyList<KeyValuePair<string, object?>> Properties);

    private sealed record TestUserSignedInEvent(Guid UserId, string Email, string AuthMethod,
        string? IpAddress, string? UserAgent, DateTime Timestamp)
        : UserSignedInEvent(UserId, Email, AuthMethod, IpAddress, UserAgent, Timestamp);
}
