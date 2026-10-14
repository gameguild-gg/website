using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Push MFA v1 lifecycle tests (issue #259): issue → push → approve/deny within TTL → verify,
///     with timeout falling back to TOTP and every closed state failing closed.
/// </summary>
public sealed class PushMfaServiceTests : IDisposable
{
    private readonly Mock<IUserMfaConfigurationRepository> mfaConfigRepository = new();
    private readonly Mock<IPublisher> publisher = new();
    private readonly ServiceProvider services = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();

    private PushMfaService CreateService() =>
        new(
            NullLogger<PushMfaService>.Instance,
            services.GetRequiredService<IDistributedCache>(),
            mfaConfigRepository.Object,
            publisher.Object);

    public void Dispose() => services.Dispose();

    private void SetupEnrolledDevice(Guid userId, string deviceToken = "device-token-0123456789abcdef")
    {
        mfaConfigRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserMfaConfiguration { UserId = userId, PushDeviceToken = deviceToken, IsEnabled = true });
    }

    [Fact]
    public async Task RegisterDevice_PersistsToken()
    {
        var userId = Guid.NewGuid();
        mfaConfigRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserMfaConfiguration { UserId = userId });
        UserMfaConfiguration? saved = null;
        mfaConfigRepository
            .Setup(r => r.UpdateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback<UserMfaConfiguration, CancellationToken>((c, _) => saved = c)
            .ReturnsAsync((UserMfaConfiguration c, CancellationToken _) => c);
        var service = CreateService();

        var result = await service.RegisterDeviceAsync(userId, "  device-token-0123456789abcdef  ");

        result.Should().BeTrue();
        saved!.PushDeviceToken.Should().Be("device-token-0123456789abcdef");
    }

    [Fact]
    public async Task RegisterDevice_RejectsMalformedTokens()
    {
        var service = CreateService();

        (await service.RegisterDeviceAsync(Guid.NewGuid(), "short")).Should().BeFalse();
        (await service.RegisterDeviceAsync(Guid.NewGuid(), "")).Should().BeFalse();
    }

    [Fact]
    public async Task IssueChallenge_RequiresEnrolledDevice()
    {
        var userId = Guid.NewGuid();
        mfaConfigRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserMfaConfiguration { UserId = userId, PushDeviceToken = null });
        var service = CreateService();

        var result = await service.IssueChallengeAsync(userId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No push MFA device");
    }

    [Fact]
    public async Task Challenge_ApproveWithinTtl_VerifiesAndConsumes()
    {
        var userId = Guid.NewGuid();
        SetupEnrolledDevice(userId);
        var service = CreateService();

        var challenge = await service.IssueChallengeAsync(userId);
        challenge.Success.Should().BeTrue();
        challenge.ExpiresInSeconds.Should().Be((int)PushMfaService.ChallengeTtl.TotalSeconds);

        var respond = await service.RespondAsync(Guid.ParseExact(challenge.ChallengeId, "N"), approved: true);
        respond.Success.Should().BeTrue();

        var verify = await service.VerifyAsync(userId);
        verify.Success.Should().BeTrue();

        // Consumed: a second verification cannot replay the approval.
        var replay = await service.VerifyAsync(userId);
        replay.Success.Should().BeFalse();
        replay.RequiresAdditionalVerification.Should().BeTrue();
    }

    [Fact]
    public async Task Challenge_Deny_FailsVerification()
    {
        var userId = Guid.NewGuid();
        SetupEnrolledDevice(userId);
        var service = CreateService();

        var challenge = await service.IssueChallengeAsync(userId);
        await service.RespondAsync(Guid.ParseExact(challenge.ChallengeId, "N"), approved: false);

        var verify = await service.VerifyAsync(userId);
        verify.Success.Should().BeFalse();
        verify.Message.Should().Contain("denied");
    }

    [Fact]
    public async Task Challenge_Unanswered_AtVerifyTime_SignalsTotpFallback()
    {
        var userId = Guid.NewGuid();
        SetupEnrolledDevice(userId);
        var service = CreateService();

        var challenge = await service.IssueChallengeAsync(userId);

        // Simulate timeout by aging the stored state past the TTL.
        var cache = services.GetRequiredService<IDistributedCache>();
        var key = "auth:push-mfa:challenge:" + challenge.ChallengeId;
        var aged = System.Text.Json.JsonSerializer.Serialize(new
        {
            userId = userId,
            createdAt = DateTime.UtcNow.AddMinutes(-5),
            expiresAt = DateTime.UtcNow.AddMinutes(-4),
            status = 0
        }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        await cache.SetStringAsync(key, aged);

        var verify = await service.VerifyAsync(userId);
        verify.Success.Should().BeFalse();
        verify.RequiresAdditionalVerification.Should().BeTrue();
        verify.Message.Should().Contain("TOTP");
    }

    [Fact]
    public async Task Challenge_StillPending_AtVerifyTime_SignalsFallback()
    {
        var userId = Guid.NewGuid();
        SetupEnrolledDevice(userId);
        var service = CreateService();

        await service.IssueChallengeAsync(userId);

        var verify = await service.VerifyAsync(userId);
        verify.Success.Should().BeFalse();
        verify.RequiresAdditionalVerification.Should().BeTrue();
        verify.Message.Should().Contain("pending");
    }

    [Fact]
    public async Task Respond_RejectsUnknownChallenge()
    {
        var service = CreateService();

        var respond = await service.RespondAsync(Guid.NewGuid(), approved: true);

        respond.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Respond_IsSingleShot()
    {
        var userId = Guid.NewGuid();
        SetupEnrolledDevice(userId);
        var service = CreateService();
        var challenge = await service.IssueChallengeAsync(userId);

        var first = await service.RespondAsync(Guid.ParseExact(challenge.ChallengeId, "N"), approved: true);
        var second = await service.RespondAsync(Guid.ParseExact(challenge.ChallengeId, "N"), approved: false);

        first.Success.Should().BeTrue();
        second.Success.Should().BeFalse();
        second.Message.Should().Contain("already answered");
    }

    [Fact]
    public async Task Verify_FailsClosed_WhenChallengeBelongsToAnotherUser()
    {
        var owner = Guid.NewGuid();
        SetupEnrolledDevice(owner);
        var service = CreateService();
        var challenge = await service.IssueChallengeAsync(owner);
        await service.RespondAsync(Guid.ParseExact(challenge.ChallengeId, "N"), approved: true);

        var attacker = Guid.NewGuid();
        SetupEnrolledDevice(attacker);
        var verify = await service.VerifyAsync(attacker);

        verify.Success.Should().BeFalse();
        verify.RequiresAdditionalVerification.Should().BeTrue();
    }

    [Fact]
    public async Task Verify_WithoutChallenge_SignalsTotpFallback()
    {
        var userId = Guid.NewGuid();
        var service = CreateService();

        var verify = await service.VerifyAsync(userId);

        verify.Success.Should().BeFalse();
        verify.RequiresAdditionalVerification.Should().BeTrue();
        verify.Message.Should().Contain("TOTP");
    }

    [Fact]
    public async Task PushPublishFailure_CleansUpTheChallenge()
    {
        var userId = Guid.NewGuid();
        SetupEnrolledDevice(userId);
        publisher
            .Setup(p => p.Publish(It.IsAny<PushMfaChallengeNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus down"));
        var service = CreateService();

        var act = () => service.IssueChallengeAsync(userId);
        await act.Should().ThrowAsync<InvalidOperationException>();

        var verify = await service.VerifyAsync(userId);
        verify.Success.Should().BeFalse();
        verify.Message.Should().Contain("TOTP");
    }
}
