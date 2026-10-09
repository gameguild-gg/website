using FluentAssertions;
using GameGuild.Identity.Authentication.UnitTests.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class ThreatDetectionLoggingSecurityTests
{
    [Theory]
    [InlineData("private-person@private-organization.invalid", "p***@p***.invalid")]
    [InlineData("x@private-organization.invalid", "x***@p***.invalid")]
    [InlineData("private-person@private-organization.invalid\r\nFORGED", "p***@p***.invalid")]
    [InlineData("private-person@private-organization.invalid\u0085FORGED", "p***@p***.invalid")]
    [InlineData("private-person@private-organization.invalid\u2028FORGED", "p***@p***.invalid")]
    [InlineData("private-person@private-organization.invalid\u2029FORGED", "p***@p***.invalid")]
    [InlineData("private-identifier\r\nFORGED", "p***(26)")]
    [InlineData("", "none")]
    public async Task BruteForce_RedactsEveryLogRepresentationAndPreservesDetectionAndSiem(string identifier, string legacyMarker)
    {
        const int windowMinutes = 17;
        var repository = Repository(identifier, 5);
        var siem = new Mock<ISiemIntegrationService>();
        siem.Setup(value => value.SendBruteForceEventAsync(
                identifier, 5, TimeSpan.FromMinutes(windowMinutes), CancellationToken.None))
            .Returns(Task.CompletedTask);
        var logger = new TestLogger<ThreatDetectionService>();
        var service = Service(repository, siem, logger);

        (await service.DetectBruteForceAsync(identifier, windowMinutes)).Should().BeTrue();

        AssertPrivateLog(logger, identifier, legacyMarker);
        var entry = logger.Entries.Single();
        entry.Properties.Single(property => property.Key == "FailedCount").Value.Should().Be(5);
        entry.Properties.Single(property => property.Key == "TimeWindowMinutes").Value.Should().Be(windowMinutes);
        repository.Verify(value => value.GetFailedAttemptsAsync(
            identifier, It.IsAny<DateTime>(), CancellationToken.None), Times.Once);
        siem.Verify(value => value.SendBruteForceEventAsync(
            identifier, 5, TimeSpan.FromMinutes(windowMinutes), CancellationToken.None), Times.Once);
        siem.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task BruteForce_BelowThresholdDoesNotLogOrNotifySiem(int attemptCount)
    {
        const string identifier = "private-person@private-organization.invalid";
        var repository = Repository(identifier, attemptCount);
        var siem = new Mock<ISiemIntegrationService>();
        var logger = new TestLogger<ThreatDetectionService>();
        var service = Service(repository, siem, logger);

        (await service.DetectBruteForceAsync(identifier)).Should().BeFalse();

        logger.Entries.Should().BeEmpty();
        siem.VerifyNoOtherCalls();
        repository.Verify(value => value.GetFailedAttemptsAsync(
            identifier, It.IsAny<DateTime>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task BruteForce_SiemFailurePropagatesTheOriginalExceptionWithoutLoggingItsPayload()
    {
        const string identifier = "private-person@private-organization.invalid\r\nFORGED";
        var original = new InvalidOperationException(identifier);
        var repository = Repository(identifier, 6);
        var siem = new Mock<ISiemIntegrationService>();
        siem.Setup(value => value.SendBruteForceEventAsync(
                identifier, 6, TimeSpan.FromMinutes(15), CancellationToken.None))
            .ThrowsAsync(original);
        var logger = new TestLogger<ThreatDetectionService>();
        var service = Service(repository, siem, logger);

        var failure = await FluentActions.Awaiting(() => service.DetectBruteForceAsync(identifier))
            .Should().ThrowAsync<InvalidOperationException>();

        failure.Which.Should().BeSameAs(original);
        AssertPrivateLog(logger, identifier, "p***@p***.invalid");
        siem.Verify(value => value.SendBruteForceEventAsync(
            identifier, 6, TimeSpan.FromMinutes(15), CancellationToken.None), Times.Once);
    }

    private static Mock<IAuthenticationAttemptRepository> Repository(string identifier, int attemptCount)
    {
        var repository = new Mock<IAuthenticationAttemptRepository>();
        repository.Setup(value => value.GetFailedAttemptsAsync(
                identifier, It.IsAny<DateTime>(), CancellationToken.None))
            .ReturnsAsync(Enumerable.Range(0, attemptCount).Select(_ => new AuthenticationAttempt()).ToList());
        return repository;
    }

    private static ThreatDetectionService Service(
        Mock<IAuthenticationAttemptRepository> repository,
        Mock<ISiemIntegrationService> siem,
        TestLogger<ThreatDetectionService> logger)
    {
        return new ThreatDetectionService(repository.Object, logger, new ConfigurationBuilder().Build(), siem.Object);
    }

    private static void AssertPrivateLog(TestLogger<ThreatDetectionService> logger, string identifier, string legacyMarker)
    {
        var expectedMarker = LogRedaction.MaskIdentifier(identifier);
        if (identifier.Length > 0)
        {
            expectedMarker.Should().MatchRegex("^(email|username):[0-9a-f]{64}$").And.NotBe(legacyMarker);
        }
        else
        {
            expectedMarker.Should().Be("none");
        }

        logger.Entries.Should().ContainSingle();
        var entry = logger.Entries.Single();
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Exception.Should().BeNull();
        entry.Properties.Single(property => property.Key == "Identifier").Value.Should().Be(expectedMarker);
        var renderedAndStructured = entry.Message + "|" + string.Join("|", entry.Properties.Select(property => property.Value));
        renderedAndStructured.Should().Contain(expectedMarker)
            .And.NotContain("private-person").And.NotContain("private-organization.invalid")
            .And.NotContain("private-identifier").And.NotContain("FORGED")
            .And.NotContain("\r").And.NotContain("\n")
            .And.NotContain("\u0085").And.NotContain("\u2028").And.NotContain("\u2029");
    }
}
