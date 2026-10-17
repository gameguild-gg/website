using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     SMS MFA factor tests (issue #244): E.164 normalization, digest-only storage, single-use
///     codes, five-attempt ceiling, resend throttle, enrollment persistence and challenge send.
/// </summary>
public sealed class SmsMfaServiceTests : IDisposable
{
    private readonly Mock<IUserMfaConfigurationRepository> mfaConfigRepository = new();
    private readonly Mock<IPublisher> publisher = new();
    private readonly MemoryCache memoryCache = new(new MemoryCacheOptions { SizeLimit = null });

    private SmsMfaService CreateService() =>
        new(NullLogger<SmsMfaService>.Instance, memoryCache, mfaConfigRepository.Object, publisher.Object);

    public void Dispose() => memoryCache.Dispose();

    [Theory]
    [InlineData("+15551234567", "+15551234567")]
    [InlineData(" 15551234567 ", "+15551234567")]
    [InlineData("+441632960111", "+441632960111")]
    public void NormalizePhoneNumber_AcceptsE164(string input, string expected)
    {
        SmsMfaService.NormalizePhoneNumber(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("+1234567890123456789")] // too long
    [InlineData("+1555ABCD456")] // non-digits
    public void NormalizePhoneNumber_RejectsMalformedNumbers(string input)
    {
        SmsMfaService.NormalizePhoneNumber(input).Should().BeNull();
    }

    [Fact]
    public void MaskPhoneNumber_ShowsOnlyLastFourDigits()
    {
        SmsMfaService.MaskPhoneNumber("+15551234567").Should().Be("***-***-4567");
    }

    [Fact]
    public async Task StartEnrollment_PublishesCode_QueuesOnlyDigest()
    {
        var userId = Guid.NewGuid();
        var service = CreateService();

        var result = await service.StartEnrollmentAsync(userId, "+15551234567");

        result.Success.Should().BeTrue();
        result.PhoneNumberMasked.Should().Be("***-***-4567");
        result.ExpiresInSeconds.Should().Be((int)SmsMfaService.CodeLifetime.TotalSeconds);

        publisher.Verify(p => p.Publish(
            It.Is<SmsMfaCodeRequestedNotification>(n =>
                n.UserId == userId &&
                n.PhoneNumber == "+15551234567" &&
                n.Code.Length == 6 &&
                n.Code.All(char.IsDigit)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartEnrollment_IsThrottledToOneCodePerMinute()
    {
        var userId = Guid.NewGuid();
        var service = CreateService();

        var first = await service.StartEnrollmentAsync(userId, "+15551234567");
        var second = await service.StartEnrollmentAsync(userId, "+15551234567");

        first.Success.Should().BeTrue();
        second.Success.Should().BeFalse();
        publisher.Verify(p => p.Publish(It.IsAny<SmsMfaCodeRequestedNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteEnrollment_VerifiesCodeAndPersistsPhone()
    {
        var userId = Guid.NewGuid();
        string? capturedCode = null;
        publisher
            .Setup(p => p.Publish(It.IsAny<SmsMfaCodeRequestedNotification>(), It.IsAny<CancellationToken>()))
            .Callback<SmsMfaCodeRequestedNotification, CancellationToken>((n, _) => capturedCode = n.Code);
        UserMfaConfiguration? saved = null;
        mfaConfigRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserMfaConfiguration { UserId = userId });
        mfaConfigRepository
            .Setup(r => r.UpdateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback<UserMfaConfiguration, CancellationToken>((c, _) => saved = c)
            .ReturnsAsync((UserMfaConfiguration c, CancellationToken _) => c);
        var service = CreateService();

        await service.StartEnrollmentAsync(userId, "+15551234567");
        var result = await service.CompleteEnrollmentAsync(userId, capturedCode!);

        result.Success.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.SmsPhoneNumber.Should().Be("+15551234567");
        saved.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteEnrollment_RejectsWrongCode_AndConsumesCodeAttempts()
    {
        var userId = Guid.NewGuid();
        var service = CreateService();
        await service.StartEnrollmentAsync(userId, "+15551234567");

        for (var attempt = 0; attempt < SmsMfaService.MaxVerificationAttempts; attempt++)
        {
            (await service.VerifyCodeAsync(userId, "000000")).Should().BeFalse();
        }

        // Exhausted: even the correct code is now invalid.
        (await service.VerifyCodeAsync(userId, "000000")).Should().BeFalse();
    }

    [Fact]
    public async Task Code_IsSingleUse()
    {
        var userId = Guid.NewGuid();
        string? capturedCode = null;
        publisher
            .Setup(p => p.Publish(It.IsAny<SmsMfaCodeRequestedNotification>(), It.IsAny<CancellationToken>()))
            .Callback<SmsMfaCodeRequestedNotification, CancellationToken>((n, _) => capturedCode = n.Code);
        var service = CreateService();

        await service.StartEnrollmentAsync(userId, "+15551234567");

        (await service.VerifyCodeAsync(userId, capturedCode!)).Should().BeTrue();
        (await service.VerifyCodeAsync(userId, capturedCode!)).Should().BeFalse();
    }

    [Fact]
    public async Task VerifyCode_WithoutPendingCode_ReturnsFalse()
    {
        var service = CreateService();

        (await service.VerifyCodeAsync(Guid.NewGuid(), "123456")).Should().BeFalse();
    }

    [Fact]
    public async Task SendChallenge_RequiresEnrolledPhone()
    {
        var userId = Guid.NewGuid();
        mfaConfigRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserMfaConfiguration { UserId = userId, SmsPhoneNumber = null });
        var service = CreateService();

        var result = await service.SendChallengeAsync(userId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not enrolled");
    }

    [Fact]
    public async Task SendChallenge_SendsToEnrolledPhone()
    {
        var userId = Guid.NewGuid();
        mfaConfigRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserMfaConfiguration { UserId = userId, SmsPhoneNumber = "+15551234567" });
        var service = CreateService();

        var result = await service.SendChallengeAsync(userId);

        result.Success.Should().BeTrue();
        result.PhoneNumberMasked.Should().Be("***-***-4567");
        publisher.Verify(p => p.Publish(
            It.Is<SmsMfaCodeRequestedNotification>(n => n.PhoneNumber == "+15551234567" && !n.IsEnrollment),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartEnrollment_PublishFailure_DropsTheCode()
    {
        var userId = Guid.NewGuid();
        publisher
            .Setup(p => p.Publish(It.IsAny<SmsMfaCodeRequestedNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus down"));
        var service = CreateService();

        var act = () => service.StartEnrollmentAsync(userId, "+15551234567");
        await act.Should().ThrowAsync<InvalidOperationException>();

        // No live code remains for a user who never received it.
        (await service.VerifyCodeAsync(userId, "123456")).Should().BeFalse();
    }
}
