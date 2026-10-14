using FluentAssertions;
using GameGuild.Email;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class MfaRecoveryCommandHandlersTests
{
    private static User CreateUser(string email) =>
        new() { Id = Guid.NewGuid(), Email = email, Username = "recovery-user", TokenVersion = 1 };

    private static (Mock<IUserRepository> Users, Mock<IMfaAttemptTrackingService> Tracking,
        Mock<IMfaRecoveryRequestStore> Store, Mock<IEmailSender> Email, IMemoryCache Cache,
        IConfiguration Config) CreateMocks()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        return (new Mock<IUserRepository>(), new Mock<IMfaAttemptTrackingService>(),
            new Mock<IMfaRecoveryRequestStore>(), new Mock<IEmailSender>(), new MemoryCache(new MemoryCacheOptions()), config);
    }

    private static RequestMfaRecoveryCommandHandler CreateRequestHandler(
        Mock<IUserRepository> users, Mock<IMfaAttemptTrackingService> tracking, Mock<IMfaRecoveryRequestStore> store,
        Mock<IEmailSender> email, IMemoryCache cache, IConfiguration config) =>
        new(users.Object, tracking.Object, store.Object, email.Object, cache, config,
            NullLogger<RequestMfaRecoveryCommandHandler>.Instance);

    [Fact]
    public async Task RequestMfaRecoveryCommandHandler_UnknownEmailReturnsGenericSuccessAndCreatesNothing()
    {
        var (users, tracking, store, email, cache, config) = CreateMocks();
        users.Setup(r => r.GetByEmailAsync("ghost@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = CreateRequestHandler(users, tracking, store, email, cache, config);
        var result = await handler.Handle(new RequestMfaRecoveryCommand { Email = "ghost@test.com" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        store.Verify(s => s.AddAsync(It.IsAny<MfaRecoveryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        email.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        tracking.Verify(t => t.GetMfaStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestMfaRecoveryCommandHandler_MfaUserWithNoLiveRequestCreatesPendingRequestAndSendsEmail()
    {
        var user = CreateUser("locked-out@test.com");
        var (users, tracking, store, email, cache, config) = CreateMocks();
        users.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        tracking.Setup(t => t.GetMfaStatusAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        store.Setup(s => s.FindLiveByUserAsync(user.Id.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MfaRecoveryRequest?)null);

        MfaRecoveryRequest? persisted = null;
        store.Setup(s => s.AddAsync(It.IsAny<MfaRecoveryRequest>(), It.IsAny<CancellationToken>()))
            .Callback<MfaRecoveryRequest, CancellationToken>((request, _) => persisted = request)
            .Returns(Task.CompletedTask);

        var handler = CreateRequestHandler(users, tracking, store, email, cache, config);
        var result = await handler.Handle(new RequestMfaRecoveryCommand { Email = user.Email }, CancellationToken.None);

        result.Success.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(MfaRecoveryRequestStatus.Pending);
        persisted.UserId.Should().Be(user.Id.ToString());
        persisted.RequestTokenHash.Should().MatchRegex("^[a-f0-9]{64}$");
        persisted.CooldownEndsAt.Should().BeAfter(persisted.RequestedAt);
        email.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.ToEmail == user.Email && m.PlainTextContent.Contains("token=")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestMfaRecoveryCommandHandler_SecondCallWithinRateLimitWindowReturnsGenericSuccessWithoutWork()
    {
        var user = CreateUser("throttled@test.com");
        var (users, tracking, store, email, cache, config) = CreateMocks();
        users.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = CreateRequestHandler(users, tracking, store, email, cache, config);
        var first = await handler.Handle(new RequestMfaRecoveryCommand { Email = user.Email }, CancellationToken.None);
        var second = await handler.Handle(new RequestMfaRecoveryCommand { Email = user.Email }, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        users.Verify(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.AddAsync(It.IsAny<MfaRecoveryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ContinueMfaRecoveryCommandHandler_CooldownNotElapsedDeniesWithRemainingTime()
    {
        var (users, _, store, email, _, config) = CreateMocks();
        var now = SystemClock.UtcNow;
        var recovery = new MfaRecoveryRequest
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid().ToString(),
            RequestedAt = now.AddDays(-1),
            CooldownEndsAt = now.AddDays(6),
            Status = MfaRecoveryRequestStatus.Pending,
            RequestTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("request-token"))).ToLowerInvariant()
        };
        store.Setup(s => s.ExpireOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        store.Setup(s => s.FindLiveByRequestTokenHashAsync(recovery.RequestTokenHash!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recovery);

        var handler = new ContinueMfaRecoveryCommandHandler(store.Object, users.Object, email.Object, config,
            NullLogger<ContinueMfaRecoveryCommandHandler>.Instance);
        var result = await handler.Handle(new ContinueMfaRecoveryCommand { RequestToken = "request-token" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.RetryAfterSeconds.Should().HaveValue().And.BeGreaterThan(0);
        recovery.Status.Should().Be(MfaRecoveryRequestStatus.Pending);
        email.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ContinueMfaRecoveryCommandHandler_UnknownTokenReturnsGenericDenial()
    {
        var (users, _, store, email, _, config) = CreateMocks();
        store.Setup(s => s.ExpireOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        store.Setup(s => s.FindLiveByRequestTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MfaRecoveryRequest?)null);

        var handler = new ContinueMfaRecoveryCommandHandler(store.Object, users.Object, email.Object, config,
            NullLogger<ContinueMfaRecoveryCommandHandler>.Instance);
        var result = await handler.Handle(new ContinueMfaRecoveryCommand { RequestToken = "unknown-token" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.RetryAfterSeconds.Should().BeNull();
        result.Message.Should().Be("Invalid or expired recovery request.");
        email.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ContinueMfaRecoveryCommandHandler_CooldownElapsedMintsConfirmationAndSendsEmail()
    {
        var user = CreateUser("ready@test.com");
        var (users, _, store, email, _, config) = CreateMocks();
        var now = SystemClock.UtcNow;
        var requestTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("request-token"))).ToLowerInvariant();
        var recovery = new MfaRecoveryRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id.ToString(),
            TenantId = Guid.NewGuid().ToString(),
            RequestedAt = now.AddDays(-8),
            CooldownEndsAt = now.AddDays(-1),
            Status = MfaRecoveryRequestStatus.Pending,
            RequestTokenHash = requestTokenHash
        };
        store.Setup(s => s.ExpireOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        store.Setup(s => s.FindLiveByRequestTokenHashAsync(requestTokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recovery);
        store.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new ContinueMfaRecoveryCommandHandler(store.Object, users.Object, email.Object, config,
            NullLogger<ContinueMfaRecoveryCommandHandler>.Instance);
        var result = await handler.Handle(new ContinueMfaRecoveryCommand { RequestToken = "request-token" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        recovery.Status.Should().Be(MfaRecoveryRequestStatus.AwaitingConfirmation);
        recovery.RequestTokenHash.Should().Be(requestTokenHash); // constraint: stays set
        recovery.ConfirmationTokenHash.Should().MatchRegex("^[a-f0-9]{64}$");
        recovery.ConfirmationTokenExpiresAt.Should().BeAfter(now);
        email.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.ToEmail == user.Email && m.PlainTextContent.Contains("confirm=")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestMfaRecoveryCommandHandler_UnknownEmailReturnsSamePayloadAsKnownEmail()
    {
        var user = CreateUser("enumeration-safe@test.com");
        var (users, tracking, store, email, cache, config) = CreateMocks();
        users.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        users.Setup(r => r.GetByEmailAsync("ghost@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        tracking.Setup(t => t.GetMfaStatusAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        store.Setup(s => s.FindLiveByUserAsync(user.Id.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MfaRecoveryRequest?)null);
        store.Setup(s => s.AddAsync(It.IsAny<MfaRecoveryRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateRequestHandler(users, tracking, store, email, cache, config);
        var known = await handler.Handle(new RequestMfaRecoveryCommand { Email = user.Email }, CancellationToken.None);
        var unknown = await handler.Handle(new RequestMfaRecoveryCommand { Email = "ghost@test.com" }, CancellationToken.None);

        // Enumeration safety: the wire payload (Success + Message map 1:1 to the 200 body) is identical.
        unknown.Should().BeEquivalentTo(known);
    }

    [Fact]
    public async Task ContinueMfaRecoveryCommandHandler_RequestOlderThanRetentionIsExpiredOnTouchAndDenied()
    {
        var (users, _, store, email, _, config) = CreateMocks();
        var now = SystemClock.UtcNow;
        var recovery = new MfaRecoveryRequest
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid().ToString(),
            RequestedAt = now.AddDays(-31),
            CooldownEndsAt = now.AddDays(-24),
            Status = MfaRecoveryRequestStatus.Pending,
            RequestTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("request-token"))).ToLowerInvariant()
        };
        store.Setup(s => s.ExpireOlderThanAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, CancellationToken>((_, _) => recovery.Status = MfaRecoveryRequestStatus.Expired)
            .ReturnsAsync(1);
        // Mirrors the store contract: only Pending/AwaitingConfirmation rows are live.
        store.Setup(s => s.FindLiveByRequestTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(recovery.Status is MfaRecoveryRequestStatus.Pending or MfaRecoveryRequestStatus.AwaitingConfirmation
                ? recovery
                : null));

        var handler = new ContinueMfaRecoveryCommandHandler(store.Object, users.Object, email.Object, config,
            NullLogger<ContinueMfaRecoveryCommandHandler>.Instance);
        var result = await handler.Handle(new ContinueMfaRecoveryCommand { RequestToken = "request-token" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.RetryAfterSeconds.Should().BeNull();
        result.Message.Should().Be("Invalid or expired recovery request.");
        recovery.Status.Should().Be(MfaRecoveryRequestStatus.Expired);
        store.Verify(s => s.ExpireOlderThanAsync(
            It.Is<DateTimeOffset>(cutoff => Math.Abs((cutoff - now.AddDays(-30)).TotalSeconds) < 60),
            It.IsAny<CancellationToken>()), Times.Once);
        email.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmMfaRecoveryCommandHandler_SecondConfirmWithSameTokenIsDenied()
    {
        var (_, tracking, store, _, _, _) = CreateMocks();
        var userId = Guid.NewGuid();
        var confirmationTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("confirmation-token"))).ToLowerInvariant();
        var recovery = new MfaRecoveryRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId.ToString(),
            TenantId = Guid.NewGuid().ToString(),
            RequestedAt = SystemClock.UtcNow.AddDays(-8),
            CooldownEndsAt = SystemClock.UtcNow.AddDays(-1),
            Status = MfaRecoveryRequestStatus.AwaitingConfirmation,
            RequestTokenHash = new string('a', 64),
            ConfirmationTokenHash = confirmationTokenHash,
            ConfirmationTokenExpiresAt = SystemClock.UtcNow.AddMinutes(30)
        };
        // Mirrors the store contract: only AwaitingConfirmation rows match a confirmation token.
        store.Setup(s => s.FindAwaitingByConfirmationTokenHashAsync(confirmationTokenHash, It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(recovery.Status == MfaRecoveryRequestStatus.AwaitingConfirmation ? recovery : null));
        store.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tracking.Setup(t => t.DisableMfaAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new ConfirmMfaRecoveryCommandHandler(store.Object, tracking.Object,
            NullLogger<ConfirmMfaRecoveryCommandHandler>.Instance);
        var first = await handler.Handle(new ConfirmMfaRecoveryCommand { ConfirmationToken = "confirmation-token" }, CancellationToken.None);
        var second = await handler.Handle(new ConfirmMfaRecoveryCommand { ConfirmationToken = "confirmation-token" }, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Success.Should().BeFalse();
        second.Message.Should().Be("Invalid or expired recovery confirmation.");
        tracking.Verify(t => t.DisableMfaAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmMfaRecoveryCommandHandler_HappyPathMarksCompletedAndDisablesMfa()
    {
        var (_, tracking, store, _, _, _) = CreateMocks();
        var userId = Guid.NewGuid();
        var confirmationTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("confirmation-token"))).ToLowerInvariant();
        var recovery = new MfaRecoveryRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId.ToString(),
            TenantId = Guid.NewGuid().ToString(),
            RequestedAt = SystemClock.UtcNow.AddDays(-8),
            CooldownEndsAt = SystemClock.UtcNow.AddDays(-1),
            Status = MfaRecoveryRequestStatus.AwaitingConfirmation,
            RequestTokenHash = new string('a', 64),
            ConfirmationTokenHash = confirmationTokenHash,
            ConfirmationTokenExpiresAt = SystemClock.UtcNow.AddMinutes(30)
        };
        store.Setup(s => s.FindAwaitingByConfirmationTokenHashAsync(confirmationTokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recovery);
        store.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tracking.Setup(t => t.DisableMfaAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new ConfirmMfaRecoveryCommandHandler(store.Object, tracking.Object,
            NullLogger<ConfirmMfaRecoveryCommandHandler>.Instance);
        var result = await handler.Handle(new ConfirmMfaRecoveryCommand { ConfirmationToken = "confirmation-token" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        recovery.Status.Should().Be(MfaRecoveryRequestStatus.Completed);
        recovery.ConsumedAt.Should().NotBeNull();
        tracking.Verify(t => t.DisableMfaAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmMfaRecoveryCommandHandler_UnknownTokenReturnsGenericDenialWithoutDisabling()
    {
        var (_, tracking, store, _, _, _) = CreateMocks();
        store.Setup(s => s.FindAwaitingByConfirmationTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MfaRecoveryRequest?)null);

        var handler = new ConfirmMfaRecoveryCommandHandler(store.Object, tracking.Object,
            NullLogger<ConfirmMfaRecoveryCommandHandler>.Instance);
        var result = await handler.Handle(new ConfirmMfaRecoveryCommand { ConfirmationToken = "unknown-token" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid or expired recovery confirmation.");
        tracking.Verify(t => t.DisableMfaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmMfaRecoveryCommandHandler_ExpiredConfirmationTokenDenies()
    {
        var (_, tracking, store, _, _, _) = CreateMocks();
        var confirmationTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("confirmation-token"))).ToLowerInvariant();
        var recovery = new MfaRecoveryRequest
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid().ToString(),
            RequestedAt = SystemClock.UtcNow.AddDays(-10),
            CooldownEndsAt = SystemClock.UtcNow.AddDays(-3),
            Status = MfaRecoveryRequestStatus.AwaitingConfirmation,
            RequestTokenHash = new string('a', 64),
            ConfirmationTokenHash = confirmationTokenHash,
            ConfirmationTokenExpiresAt = SystemClock.UtcNow.AddMinutes(-5)
        };
        store.Setup(s => s.FindAwaitingByConfirmationTokenHashAsync(confirmationTokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recovery);

        var handler = new ConfirmMfaRecoveryCommandHandler(store.Object, tracking.Object,
            NullLogger<ConfirmMfaRecoveryCommandHandler>.Instance);
        var result = await handler.Handle(new ConfirmMfaRecoveryCommand { ConfirmationToken = "confirmation-token" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        recovery.Status.Should().Be(MfaRecoveryRequestStatus.AwaitingConfirmation);
        recovery.ConsumedAt.Should().BeNull();
        tracking.Verify(t => t.DisableMfaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
