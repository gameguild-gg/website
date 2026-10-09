using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication.UnitTests.Handlers;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EmailVerificationLoggingSecurityTests
{
    private const string SensitiveEmail = "private-person@example.invalid";
    private const string SensitiveToken = "private-verification-token";

    [Theory]
    [InlineData("verification")]
    [InlineData("reset")]
    [InlineData("magic")]
    public async Task TokenLifecycle_RedactsUserIdentityAndPreservesTokenResults(string kind)
    {
        var logger = new TestLogger<EmailVerificationService>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new EmailVerificationService(logger, cache, Publisher().Object);
        var userId = Guid.NewGuid();
        var token = kind switch
        {
            "verification" => await service.GenerateVerificationTokenAsync(userId, SensitiveEmail),
            "reset" => await service.GeneratePasswordResetTokenAsync(userId, SensitiveEmail),
            "magic" => await service.GenerateMagicLinkTokenAsync(userId, SensitiveEmail),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var result = kind switch
        {
            "verification" => await service.VerifyEmailTokenAsync(token),
            "reset" => await service.VerifyPasswordResetTokenAsync(token),
            "magic" => await service.VerifyMagicLinkTokenAsync(token),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        result.Success.Should().BeTrue();
        result.UserId.Should().Be(userId);
        result.Email.Should().Be(SensitiveEmail);
        (await service.IsTokenValidAsync(token)).Should().BeFalse();
        logger.Entries.Should().HaveCount(2);
        AssertPrivateLogs(logger, userId.ToString(), token);
        logger.Entries.Should().OnlyContain(entry => entry.Properties.Any(property =>
            property.Key == "UserId" && Equals(property.Value, LogRedaction.RedactId(userId, "uid"))));
    }

    [Fact]
    public async Task UserMismatch_LogsRedactedExpectedAndActualIdsWithoutConsumingToken()
    {
        var logger = new TestLogger<EmailVerificationService>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new EmailVerificationService(logger, cache, Publisher().Object);
        var actual = Guid.NewGuid();
        var expected = Guid.NewGuid();
        var token = await service.GenerateVerificationTokenAsync(actual, SensitiveEmail);

        (await service.VerifyEmailTokenAsync(expected, token)).Should().BeFalse();
        (await service.IsTokenValidAsync(token)).Should().BeTrue();
        AssertPrivateLogs(logger, actual.ToString(), expected.ToString(), token);
        var entry = logger.Entries.Last();
        entry.Properties.Single(property => property.Key == "ExpectedUserId").Value
            .Should().Be(LogRedaction.RedactId(expected, "uid"));
        entry.Properties.Single(property => property.Key == "ActualUserId").Value
            .Should().Be(LogRedaction.RedactId(actual, "uid"));
    }

    [Theory]
    [InlineData("generate")]
    [InlineData("send")]
    [InlineData("verify")]
    [InlineData("status")]
    [InlineData("resend-cache")]
    [InlineData("resend-publisher")]
    [InlineData("validity")]
    public async Task Failure_LogsOnlyExceptionTypeAndPreservesExistingFailureBehavior(string operation)
    {
        var userId = Guid.NewGuid();
        var original = new InvalidOperationException($"{SensitiveEmail} {SensitiveToken} {userId}\r\nforged");
        var logger = new TestLogger<EmailVerificationService>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var failingCache = new Mock<IMemoryCache>();
        failingCache.Setup(value => value.CreateEntry(It.IsAny<object>())).Throws(original);
        failingCache.Setup(value => value.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny)).Throws(original);
        var publisher = Publisher();
        publisher.Setup(value => value.Publish(It.IsAny<EmailVerificationRequestedNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(original);
        var service = new EmailVerificationService(logger,
            operation is "send" or "resend-publisher" ? cache : failingCache.Object, publisher.Object);

        switch (operation)
        {
            case "generate":
                var generation = await FluentActions.Awaiting(() => service.GenerateVerificationTokenAsync(userId, SensitiveEmail))
                    .Should().ThrowAsync<InvalidOperationException>();
                generation.Which.Should().BeSameAs(original);
                break;
            case "send":
                var sending = await FluentActions.Awaiting(() => service.SendVerificationEmailAsync(SensitiveEmail, SensitiveToken))
                    .Should().ThrowAsync<InvalidOperationException>();
                sending.Which.Should().BeSameAs(original);
                break;
            case "verify":
                (await service.VerifyEmailTokenAsync(SensitiveToken)).Success.Should().BeFalse();
                break;
            case "status":
                (await service.IsEmailVerifiedAsync(userId)).Should().BeFalse();
                break;
            case "resend-cache":
            case "resend-publisher":
                (await service.ResendVerificationEmailAsync(userId, SensitiveEmail)).Should().BeFalse();
                break;
            case "validity":
                (await service.IsTokenValidAsync(SensitiveToken)).Should().BeFalse();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }

        logger.Entries.Should().NotBeEmpty();
        AssertPrivateLogs(logger, userId.ToString());
        logger.Entries.Where(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Error)
            .Should().NotBeEmpty().And.OnlyContain(entry => entry.Properties.Any(property =>
                property.Key == "ErrorType" && Equals(property.Value, typeof(InvalidOperationException).FullName)));
    }

    [Fact]
    public async Task Resend_RateLimitsEachUserAndAddressIndependentlyWithoutEmailInCacheKeys()
    {
        var logger = new TestLogger<EmailVerificationService>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var publisher = Publisher();
        var service = new EmailVerificationService(logger, cache, publisher.Object);
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        const string secondEmail = "other-private-person@example.invalid";

        (await service.ResendVerificationEmailAsync(firstUser, SensitiveEmail)).Should().BeTrue();
        (await service.ResendVerificationEmailAsync(firstUser, SensitiveEmail)).Should().BeFalse();
        (await service.ResendVerificationEmailAsync(firstUser, secondEmail)).Should().BeTrue();
        (await service.ResendVerificationEmailAsync(firstUser, secondEmail)).Should().BeFalse();
        (await service.ResendVerificationEmailAsync(secondUser, SensitiveEmail)).Should().BeTrue();
        (await service.ResendVerificationEmailAsync(secondUser, SensitiveEmail)).Should().BeFalse();

        publisher.Verify(value => value.Publish(It.IsAny<EmailVerificationRequestedNotification>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        var rateLimitKeys = cache.Keys.OfType<string>().Where(key => key.StartsWith("emailverify:ratelimit:", StringComparison.Ordinal)).ToArray();
        rateLimitKeys.Should().HaveCount(3).And.OnlyHaveUniqueItems();
        rateLimitKeys.Should().OnlyContain(key => !key.Contains(SensitiveEmail) && !key.Contains(secondEmail));
        AssertPrivateLogs(logger, firstUser.ToString(), secondUser.ToString(), secondEmail);
    }

    [Fact]
    public async Task Resend_MixedCaseAndWhitespaceEmails_ShareOneRateLimitBucket()
    {
        var logger = new TestLogger<EmailVerificationService>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var publisher = Publisher();
        var service = new EmailVerificationService(logger, cache, publisher.Object);
        var userId = Guid.NewGuid();

        (await service.ResendVerificationEmailAsync(userId, SensitiveEmail)).Should().BeTrue();
        (await service.ResendVerificationEmailAsync(userId, SensitiveEmail.ToUpperInvariant())).Should().BeFalse();
        (await service.ResendVerificationEmailAsync(userId, $" {SensitiveEmail} ")).Should().BeFalse();

        var rateLimitKeys = cache.Keys.OfType<string>().Where(key => key.StartsWith("emailverify:ratelimit:", StringComparison.Ordinal)).ToArray();
        rateLimitKeys.Should().HaveCount(1);
    }

    private static Mock<IPublisher> Publisher()
    {
        var publisher = new Mock<IPublisher>();
        publisher.Setup(value => value.Publish(It.IsAny<EmailVerificationRequestedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return publisher;
    }

    private static void AssertPrivateLogs(TestLogger<EmailVerificationService> logger, params string[] additionalSensitiveValues)
    {
        foreach (var entry in logger.Entries)
        {
            entry.Exception.Should().BeNull();
            var text = entry.Message + "|" + string.Join("|", entry.Properties.Select(property => property.Value));
            foreach (var sensitive in additionalSensitiveValues.Prepend(SensitiveEmail).Prepend(SensitiveToken))
            {
                text.Should().NotContain(sensitive);
            }
            text.Should().NotContain("\r").And.NotContain("\n");
        }
    }
}
