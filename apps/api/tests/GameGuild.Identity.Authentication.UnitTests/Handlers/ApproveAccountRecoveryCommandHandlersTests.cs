using FluentAssertions;
using GameGuild.Email;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class ApproveAccountRecoveryCommandHandlersTests
{
    private static User CreateUser(string email = "owner@test.com") =>
        new() { Id = Guid.NewGuid(), Email = email, Username = "owner", TenantId = Guid.NewGuid(), TokenVersion = 1 };

    private static AccountRecoveryRequest CreateLiveRequest(Guid ownerUserId, int requiredApprovals = 2, DateTimeOffset? now = null)
    {
        now ??= SystemClock.UtcNow;
        return new AccountRecoveryRequest
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            TenantId = Guid.NewGuid(),
            Status = AccountRecoveryStatus.PendingApprovals,
            RequestedAt = now.Value.AddDays(-2),
            CooldownEndsAt = now.Value.AddDays(-1),
            ApprovalWindowEndsAt = now.Value.AddDays(1),
            RequiredApprovals = requiredApprovals
        };
    }

    private static (ApproveAccountRecoveryCommandHandler Handler, Mock<IAccountRecoveryStore> Recoveries,
        Mock<ITrustedContactStore> Contacts) CreateHandler(
            Mock<IUserRepository>? users = null,
            Mock<IEmailSender>? email = null,
            Mock<IAccountRecoveryStore>? recoveries = null,
            Mock<ITrustedContactStore>? contacts = null)
    {
        users ??= new Mock<IUserRepository>();
        email ??= new Mock<IEmailSender>();
        recoveries ??= new Mock<IAccountRecoveryStore>();
        contacts ??= new Mock<ITrustedContactStore>();
        recoveries.Setup(s => s.ExpireElapsedAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var evaluator = new AccountRecoveryThresholdEvaluator(
            recoveries.Object, users.Object, email.Object,
            Options.Create(new AccountRecoveryOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            Mock.Of<IAuthenticationAuditEventSink>(),
            NullLogger<AccountRecoveryThresholdEvaluator>.Instance);
        return (new ApproveAccountRecoveryCommandHandler(
            recoveries.Object, contacts.Object, evaluator,
            Mock.Of<IAuthenticationAuditEventSink>(),
            NullLogger<ApproveAccountRecoveryCommandHandler>.Instance), recoveries, contacts);
    }

    private static RecoveryApproval CreateCodeApproval(AccountRecoveryRequest recovery, TrustedContact contact, string code) => new()
    {
        Id = Guid.NewGuid(),
        RecoveryRequestId = recovery.Id,
        OwnerUserId = recovery.OwnerUserId,
        TenantId = recovery.TenantId,
        Factor = RecoveryApprovalFactor.TrustedContact,
        TrustedContactId = contact.Id,
        ApprovalCodeHash = AccountRecoveryTokensTestHelpers.Hash(code),
        IssuedAt = recovery.RequestedAt
    };

    [Fact]
    public async Task Approve_WithinCooldownReportsRemainingWaitWithoutRecording()
    {
        var owner = Guid.NewGuid();
        var recovery = CreateLiveRequest(owner);
        recovery.CooldownEndsAt = SystemClock.UtcNow.AddHours(12);
        var contact = new TrustedContact { Id = Guid.NewGuid(), OwnerUserId = owner, Status = TrustedContactStatus.Confirmed };
        var approval = CreateCodeApproval(recovery, contact, "code-one");

        var (handler, recoveries, contacts) = CreateHandler();
        recoveries.Setup(s => s.FindRedeemableByApprovalCodeHashAsync(approval.ApprovalCodeHash!, It.IsAny<CancellationToken>())).ReturnsAsync(approval);
        recoveries.Setup(s => s.FindByIdAsync(recovery.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recovery);
        contacts.Setup(s => s.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contact);

        var result = await handler.Handle(new ApproveAccountRecoveryCommand { ApprovalCode = "code-one" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.RetryAfterSeconds.Should().HaveValue().And.BeGreaterThan(0);
        approval.ApprovedAt.Should().BeNull();
        recoveries.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Approve_ValidCodeRecordsApprovalBelowThreshold()
    {
        var owner = Guid.NewGuid();
        var recovery = CreateLiveRequest(owner, requiredApprovals: 2);
        var contact = new TrustedContact { Id = Guid.NewGuid(), OwnerUserId = owner, Status = TrustedContactStatus.Confirmed };
        var approval = CreateCodeApproval(recovery, contact, "code-one");

        var (handler, recoveries, contacts) = CreateHandler();
        recoveries.Setup(s => s.FindRedeemableByApprovalCodeHashAsync(approval.ApprovalCodeHash!, It.IsAny<CancellationToken>())).ReturnsAsync(approval);
        recoveries.Setup(s => s.FindByIdAsync(recovery.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recovery);
        contacts.Setup(s => s.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contact);
        recoveries.Setup(s => s.CountDistinctApprovalsAsync(recovery.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await handler.Handle(new ApproveAccountRecoveryCommand { ApprovalCode = "code-one" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ResetUnlocked.Should().BeFalse();
        result.Message.Should().Contain("1 of 2");
        approval.ApprovedAt.Should().NotBeNull();
        recovery.Status.Should().Be(AccountRecoveryStatus.PendingApprovals);
    }

    [Fact]
    public async Task Approve_ReachingThresholdUnlocksResetAndEmailsOwner()
    {
        var owner = Guid.NewGuid();
        var ownerUser = CreateUser();
        var recovery = CreateLiveRequest(ownerUser.Id, requiredApprovals: 2);
        var contact = new TrustedContact { Id = Guid.NewGuid(), OwnerUserId = ownerUser.Id, Status = TrustedContactStatus.Confirmed };
        var approval = CreateCodeApproval(recovery, contact, "code-two");

        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(ownerUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ownerUser);
        var email = new Mock<IEmailSender>();
        var (handler, recoveries, contacts) = CreateHandler(users, email);
        recoveries.Setup(s => s.FindRedeemableByApprovalCodeHashAsync(approval.ApprovalCodeHash!, It.IsAny<CancellationToken>())).ReturnsAsync(approval);
        recoveries.Setup(s => s.FindByIdAsync(recovery.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recovery);
        contacts.Setup(s => s.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contact);
        recoveries.Setup(s => s.CountDistinctApprovalsAsync(recovery.Id, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await handler.Handle(new ApproveAccountRecoveryCommand { ApprovalCode = "code-two" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ResetUnlocked.Should().BeTrue();
        recovery.Status.Should().Be(AccountRecoveryStatus.ResetUnlocked);
        recovery.ResetUnlockedAt.Should().NotBeNull();
        recovery.ResetTokenHash.Should().MatchRegex("^[a-f0-9]{64}$");
        recovery.ResetTokenExpiresAt.Should().BeAfter(SystemClock.UtcNow);
        email.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.ToEmail == ownerUser.Email && m.PlainTextContent.Contains("token=")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Approve_ConsumedCodeReplayFailsClosed()
    {
        var (handler, recoveries, _) = CreateHandler();
        // After redemption the lookup no longer matches (ApprovedAt != null filtered out).
        recoveries.Setup(s => s.FindRedeemableByApprovalCodeHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecoveryApproval?)null);

        var result = await handler.Handle(new ApproveAccountRecoveryCommand { ApprovalCode = "code-one" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid or expired recovery approval.");
    }

    [Fact]
    public async Task Approve_RevokedContactFailsClosed()
    {
        var owner = Guid.NewGuid();
        var recovery = CreateLiveRequest(owner);
        var contact = new TrustedContact { Id = Guid.NewGuid(), OwnerUserId = owner, Status = TrustedContactStatus.Revoked };
        var approval = CreateCodeApproval(recovery, contact, "code-one");

        var (handler, recoveries, contacts) = CreateHandler();
        recoveries.Setup(s => s.FindRedeemableByApprovalCodeHashAsync(approval.ApprovalCodeHash!, It.IsAny<CancellationToken>())).ReturnsAsync(approval);
        recoveries.Setup(s => s.FindByIdAsync(recovery.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recovery);
        contacts.Setup(s => s.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contact);

        var result = await handler.Handle(new ApproveAccountRecoveryCommand { ApprovalCode = "code-one" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        approval.ApprovedAt.Should().BeNull();
    }

    [Fact]
    public async Task Approve_WindowElapsedFailsClosed()
    {
        var owner = Guid.NewGuid();
        var recovery = CreateLiveRequest(owner);
        recovery.ApprovalWindowEndsAt = SystemClock.UtcNow.AddHours(-1);
        var contact = new TrustedContact { Id = Guid.NewGuid(), OwnerUserId = owner, Status = TrustedContactStatus.Confirmed };
        var approval = CreateCodeApproval(recovery, contact, "code-one");

        var (handler, recoveries, contacts) = CreateHandler();
        recoveries.Setup(s => s.FindRedeemableByApprovalCodeHashAsync(approval.ApprovalCodeHash!, It.IsAny<CancellationToken>())).ReturnsAsync(approval);
        recoveries.Setup(s => s.FindByIdAsync(recovery.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recovery);
        contacts.Setup(s => s.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contact);

        var result = await handler.Handle(new ApproveAccountRecoveryCommand { ApprovalCode = "code-one" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        approval.ApprovedAt.Should().BeNull();
    }

    [Fact]
    public async Task ApprovePasskey_VerifiedAssertionRecordsApproval()
    {
        var ownerUser = CreateUser();
        var recovery = CreateLiveRequest(ownerUser.Id, requiredApprovals: 2);
        var webAuthn = new Mock<IWebAuthnAuthenticationService>();
        webAuthn.Setup(w => w.CompleteAuthenticationAsync("assertion", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = true, UserId = ownerUser.Id, CredentialId = Guid.NewGuid() });

        var users = new Mock<IUserRepository>();
        var email = new Mock<IEmailSender>();
        var recoveries = new Mock<IAccountRecoveryStore>();
        recoveries.Setup(s => s.ExpireElapsedAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        recoveries.Setup(s => s.FindLiveByOwnerAsync(ownerUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recovery);
        recoveries.Setup(s => s.TryAddPasskeyApprovalAsync(It.Is<RecoveryApproval>(a =>
            a.Factor == RecoveryApprovalFactor.Passkey && a.ApprovedAt != null), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        recoveries.Setup(s => s.CountDistinctApprovalsAsync(recovery.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var evaluator = new AccountRecoveryThresholdEvaluator(
            recoveries.Object, users.Object, email.Object,
            Options.Create(new AccountRecoveryOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            Mock.Of<IAuthenticationAuditEventSink>(),
            NullLogger<AccountRecoveryThresholdEvaluator>.Instance);
        var handler = new ApproveAccountRecoveryWithPasskeyCommandHandler(
            webAuthn.Object, recoveries.Object, evaluator,
            Mock.Of<IAuthenticationAuditEventSink>(),
            NullLogger<ApproveAccountRecoveryWithPasskeyCommandHandler>.Instance);

        var result = await handler.Handle(new ApproveAccountRecoveryWithPasskeyCommand { AssertionResponse = "assertion" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ResetUnlocked.Should().BeFalse();
        result.Message.Should().Contain("1 of 2");
        recovery.Status.Should().Be(AccountRecoveryStatus.PendingApprovals);
    }

    [Fact]
    public async Task ApprovePasskey_ReplayOfSameCredentialFailsClosed()
    {
        var ownerUser = CreateUser();
        var recovery = CreateLiveRequest(ownerUser.Id);
        var webAuthn = new Mock<IWebAuthnAuthenticationService>();
        webAuthn.Setup(w => w.CompleteAuthenticationAsync("assertion", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = true, UserId = ownerUser.Id, CredentialId = Guid.NewGuid() });

        var recoveries = new Mock<IAccountRecoveryStore>();
        recoveries.Setup(s => s.ExpireElapsedAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        recoveries.Setup(s => s.FindLiveByOwnerAsync(ownerUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recovery);
        recoveries.Setup(s => s.TryAddPasskeyApprovalAsync(It.IsAny<RecoveryApproval>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var evaluator = new AccountRecoveryThresholdEvaluator(
            recoveries.Object, new Mock<IUserRepository>().Object, new Mock<IEmailSender>().Object,
            Options.Create(new AccountRecoveryOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            Mock.Of<IAuthenticationAuditEventSink>(),
            NullLogger<AccountRecoveryThresholdEvaluator>.Instance);
        var handler = new ApproveAccountRecoveryWithPasskeyCommandHandler(
            webAuthn.Object, recoveries.Object, evaluator,
            Mock.Of<IAuthenticationAuditEventSink>(),
            NullLogger<ApproveAccountRecoveryWithPasskeyCommandHandler>.Instance);

        var result = await handler.Handle(new ApproveAccountRecoveryWithPasskeyCommand { AssertionResponse = "assertion" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid or expired recovery approval.");
    }

    [Fact]
    public async Task ApprovePasskey_FailedAssertionFailsClosed()
    {
        var webAuthn = new Mock<IWebAuthnAuthenticationService>();
        webAuthn.Setup(w => w.CompleteAuthenticationAsync("bad", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = false, Error = "no" });

        var recoveries = new Mock<IAccountRecoveryStore>();
        recoveries.Setup(s => s.ExpireElapsedAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var evaluator = new AccountRecoveryThresholdEvaluator(
            recoveries.Object, new Mock<IUserRepository>().Object, new Mock<IEmailSender>().Object,
            Options.Create(new AccountRecoveryOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            Mock.Of<IAuthenticationAuditEventSink>(),
            NullLogger<AccountRecoveryThresholdEvaluator>.Instance);
        var handler = new ApproveAccountRecoveryWithPasskeyCommandHandler(
            webAuthn.Object, recoveries.Object, evaluator,
            Mock.Of<IAuthenticationAuditEventSink>(),
            NullLogger<ApproveAccountRecoveryWithPasskeyCommandHandler>.Instance);

        var result = await handler.Handle(new ApproveAccountRecoveryWithPasskeyCommand { AssertionResponse = "bad" }, CancellationToken.None);

        result.Success.Should().BeFalse();
    }
}
