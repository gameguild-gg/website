using FluentAssertions;
using GameGuild.Email;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class AccountRecoveryRequestHandlersTests
{
    private static User CreateUser(string email) =>
        new() { Id = Guid.NewGuid(), Email = email, Username = "recovery-user", TenantId = Guid.NewGuid(), TokenVersion = 1 };

    private static TrustedContact CreateConfirmedContact(Guid ownerId) => new()
    {
        Id = Guid.NewGuid(),
        OwnerUserId = ownerId,
        Email = "contact@test.com",
        Status = TrustedContactStatus.Confirmed,
        InvitedAt = SystemClock.UtcNow.AddDays(-3),
        ConfirmedAt = SystemClock.UtcNow.AddDays(-2)
    };

    private static RequestAccountRecoveryCommandHandler CreateHandler(
        Mock<IUserRepository> users,
        Mock<ITrustedContactStore> contacts,
        Mock<IAccountRecoveryStore> recoveries,
        Mock<IWebAuthnCredentialRepository> webAuthn,
        Mock<IEmailSender> email,
        IMemoryCache cache,
        AccountRecoveryOptions? options = null) => new(
            users.Object, contacts.Object, recoveries.Object, webAuthn.Object, email.Object, cache,
            Options.Create(options ?? new AccountRecoveryOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            Mock.Of<IAuthenticationAuditEventSink>(),
            NullLogger<RequestAccountRecoveryCommandHandler>.Instance);

    [Fact]
    public async Task RequestRecovery_UnknownEmailReturnsGenericSuccessWithoutWork()
    {
        var users = new Mock<IUserRepository>();
        var contacts = new Mock<ITrustedContactStore>();
        var recoveries = new Mock<IAccountRecoveryStore>();
        var webAuthn = new Mock<IWebAuthnCredentialRepository>();
        var email = new Mock<IEmailSender>();
        users.Setup(r => r.GetByEmailAsync("ghost@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = CreateHandler(users, contacts, recoveries, webAuthn, email, new MemoryCache(new MemoryCacheOptions()));
        var result = await handler.Handle(new RequestAccountRecoveryCommand { Email = "ghost@test.com" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        recoveries.Verify(s => s.AddWithApprovalsAsync(It.IsAny<AccountRecoveryRequest>(), It.IsAny<IReadOnlyList<RecoveryApproval>>(), It.IsAny<CancellationToken>()), Times.Never);
        email.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestRecovery_WithoutFactorsSkipsRequestCreation()
    {
        var user = CreateUser("locked@test.com");
        var users = new Mock<IUserRepository>();
        var contacts = new Mock<ITrustedContactStore>();
        var recoveries = new Mock<IAccountRecoveryStore>();
        var webAuthn = new Mock<IWebAuthnCredentialRepository>();
        var email = new Mock<IEmailSender>();
        users.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        contacts.Setup(s => s.GetActiveByOwnerAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrustedContact> { new() { Id = Guid.NewGuid(), OwnerUserId = user.Id, Status = TrustedContactStatus.Invited } });
        webAuthn.Setup(w => w.HasActiveCredentialsAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = CreateHandler(users, contacts, recoveries, webAuthn, email, new MemoryCache(new MemoryCacheOptions()));
        var result = await handler.Handle(new RequestAccountRecoveryCommand { Email = user.Email }, CancellationToken.None);

        result.Success.Should().BeTrue();
        recoveries.Verify(s => s.AddWithApprovalsAsync(It.IsAny<AccountRecoveryRequest>(), It.IsAny<IReadOnlyList<RecoveryApproval>>(), It.IsAny<CancellationToken>()), Times.Never);
        email.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestRecovery_IssuesCodesOnlyToConfirmedContactsAndClampsThreshold()
    {
        var user = CreateUser("locked@test.com");
        var confirmed = CreateConfirmedContact(user.Id);
        var invited = new TrustedContact { Id = Guid.NewGuid(), OwnerUserId = user.Id, Status = TrustedContactStatus.Invited, Email = "invited@test.com" };
        var users = new Mock<IUserRepository>();
        var contacts = new Mock<ITrustedContactStore>();
        var recoveries = new Mock<IAccountRecoveryStore>();
        var webAuthn = new Mock<IWebAuthnCredentialRepository>();
        var email = new Mock<IEmailSender>();
        users.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        contacts.Setup(s => s.GetActiveByOwnerAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrustedContact> { confirmed, invited });
        webAuthn.Setup(w => w.HasActiveCredentialsAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        recoveries.Setup(s => s.FindLiveByOwnerAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountRecoveryRequest?)null);

        AccountRecoveryRequest? persistedRequest = null;
        IReadOnlyList<RecoveryApproval> persistedApprovals = Array.Empty<RecoveryApproval>();
        recoveries.Setup(s => s.AddWithApprovalsAsync(It.IsAny<AccountRecoveryRequest>(), It.IsAny<IReadOnlyList<RecoveryApproval>>(), It.IsAny<CancellationToken>()))
            .Callback<AccountRecoveryRequest, IReadOnlyList<RecoveryApproval>, CancellationToken>((request, approvals, _) => { persistedRequest = request; persistedApprovals = approvals; })
            .Returns(Task.CompletedTask);

        // Configured K=5 must clamp to the 2 available factors (1 contact + passkey).
        var handler = CreateHandler(users, contacts, recoveries, webAuthn, email,
            new MemoryCache(new MemoryCacheOptions()), new AccountRecoveryOptions { RequiredApprovals = 5 });
        var result = await handler.Handle(new RequestAccountRecoveryCommand { Email = user.Email }, CancellationToken.None);

        result.Success.Should().BeTrue();
        persistedRequest.Should().NotBeNull();
        persistedRequest!.RequiredApprovals.Should().Be(2);
        persistedRequest.Status.Should().Be(AccountRecoveryStatus.PendingApprovals);
        persistedRequest.CooldownEndsAt.Should().BeAfter(persistedRequest.RequestedAt);
        persistedRequest.ApprovalWindowEndsAt.Should().BeAfter(persistedRequest.CooldownEndsAt);
        persistedApprovals.Should().ContainSingle();
        persistedApprovals[0].TrustedContactId.Should().Be(confirmed.Id);
        persistedApprovals[0].ApprovalCodeHash.Should().MatchRegex("^[a-f0-9]{64}$");
        persistedApprovals[0].ApprovedAt.Should().BeNull();
        // One code email to the confirmed contact and one notice to the owner.
        email.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RequestRecovery_SecondCallWithinRateLimitWindowSkipsLookup()
    {
        var user = CreateUser("throttled@test.com");
        var users = new Mock<IUserRepository>();
        var handler = CreateHandler(
            users,
            new Mock<ITrustedContactStore>(),
            new Mock<IAccountRecoveryStore>(),
            new Mock<IWebAuthnCredentialRepository>(),
            new Mock<IEmailSender>(),
            new MemoryCache(new MemoryCacheOptions()));

        var first = await handler.Handle(new RequestAccountRecoveryCommand { Email = user.Email }, CancellationToken.None);
        var second = await handler.Handle(new RequestAccountRecoveryCommand { Email = user.Email }, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        users.Verify(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestRecovery_LiveRequestPreventsANewOne()
    {
        var user = CreateUser("locked@test.com");
        var users = new Mock<IUserRepository>();
        var contacts = new Mock<ITrustedContactStore>();
        var recoveries = new Mock<IAccountRecoveryStore>();
        var webAuthn = new Mock<IWebAuthnCredentialRepository>();
        var email = new Mock<IEmailSender>();
        users.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        contacts.Setup(s => s.GetActiveByOwnerAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrustedContact> { CreateConfirmedContact(user.Id) });
        webAuthn.Setup(w => w.HasActiveCredentialsAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        recoveries.Setup(s => s.FindLiveByOwnerAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountRecoveryRequest { Id = Guid.NewGuid(), OwnerUserId = user.Id, Status = AccountRecoveryStatus.PendingApprovals });

        var handler = CreateHandler(users, contacts, recoveries, webAuthn, email, new MemoryCache(new MemoryCacheOptions()));
        var result = await handler.Handle(new RequestAccountRecoveryCommand { Email = user.Email }, CancellationToken.None);

        result.Success.Should().BeTrue();
        recoveries.Verify(s => s.AddWithApprovalsAsync(It.IsAny<AccountRecoveryRequest>(), It.IsAny<IReadOnlyList<RecoveryApproval>>(), It.IsAny<CancellationToken>()), Times.Never);
        email.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelRecovery_CancelsLiveRequestAndKillsResetToken()
    {
        var owner = Guid.NewGuid();
        var recoveries = new Mock<IAccountRecoveryStore>();
        var live = new AccountRecoveryRequest
        {
            Id = Guid.NewGuid(),
            OwnerUserId = owner,
            Status = AccountRecoveryStatus.ResetUnlocked,
            ResetTokenHash = AccountRecoveryTokensTestHelpers.Hash("reset-token"),
            ResetTokenExpiresAt = SystemClock.UtcNow.AddHours(1)
        };
        recoveries.Setup(s => s.FindLiveByOwnerAsync(owner, It.IsAny<CancellationToken>())).ReturnsAsync(live);

        var handler = new CancelAccountRecoveryCommandHandler(
            recoveries.Object, Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<CancelAccountRecoveryCommandHandler>.Instance);
        var result = await handler.Handle(new CancelAccountRecoveryCommand { OwnerUserId = owner }, CancellationToken.None);

        result.Success.Should().BeTrue();
        live.Status.Should().Be(AccountRecoveryStatus.Cancelled);
        live.CancelledAt.Should().NotBeNull();
        live.ResetTokenHash.Should().BeNull();
        live.ResetTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task CancelRecovery_WithoutLiveRequestReportsFailure()
    {
        var recoveries = new Mock<IAccountRecoveryStore>();
        recoveries.Setup(s => s.FindLiveByOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountRecoveryRequest?)null);

        var handler = new CancelAccountRecoveryCommandHandler(
            recoveries.Object, Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<CancelAccountRecoveryCommandHandler>.Instance);
        var result = await handler.Handle(new CancelAccountRecoveryCommand { OwnerUserId = Guid.NewGuid() }, CancellationToken.None);

        result.Success.Should().BeFalse();
    }
}
