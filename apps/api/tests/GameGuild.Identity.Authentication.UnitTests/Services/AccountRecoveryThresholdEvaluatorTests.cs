using FluentAssertions;
using GameGuild.Email;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authentication.UnitTests.Handlers;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class AccountRecoveryThresholdEvaluatorTests
{
    private static AccountRecoveryThresholdEvaluator CreateEvaluator(
        Mock<IAccountRecoveryStore> recoveries,
        Mock<IUserRepository>? users = null,
        Mock<IEmailSender>? email = null)
    {
        users ??= new Mock<IUserRepository>();
        email ??= new Mock<IEmailSender>();
        return new AccountRecoveryThresholdEvaluator(
            recoveries.Object, users.Object, email.Object,
            Options.Create(new AccountRecoveryOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            Mock.Of<IAuthenticationAuditEventSink>(),
            NullLogger<AccountRecoveryThresholdEvaluator>.Instance);
    }

    private static AccountRecoveryRequest CreatePending(Guid? ownerUserId = null, int requiredApprovals = 3) => new()
    {
        Id = Guid.NewGuid(),
        OwnerUserId = ownerUserId ?? Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Status = AccountRecoveryStatus.PendingApprovals,
        RequestedAt = SystemClock.UtcNow.AddDays(-1),
        CooldownEndsAt = SystemClock.UtcNow.AddHours(-1),
        ApprovalWindowEndsAt = SystemClock.UtcNow.AddDays(1),
        RequiredApprovals = requiredApprovals
    };

    [Theory]
    [InlineData(2, 3, false)]   // below threshold
    [InlineData(3, 3, true)]    // exactly at threshold
    [InlineData(4, 3, true)]    // above threshold
    public async Task Evaluate_UnlocksOnlyWhenDistinctApprovalsMeetThreshold(int distinctApprovals, int required, bool expectUnlock)
    {
        var owner = Guid.NewGuid();
        var ownerUser = new User { Id = owner, Email = "owner@test.com", Username = "owner" };
        var recovery = CreatePending(owner, required);
        var recoveries = new Mock<IAccountRecoveryStore>();
        recoveries.Setup(s => s.CountDistinctApprovalsAsync(recovery.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(distinctApprovals);
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(owner, It.IsAny<CancellationToken>())).ReturnsAsync(ownerUser);
        var email = new Mock<IEmailSender>();
        var evaluator = CreateEvaluator(recoveries, users, email);

        var result = await evaluator.EvaluateAsync(recovery, null, null, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ResetUnlocked.Should().Be(expectUnlock);
        if (expectUnlock)
        {
            recovery.Status.Should().Be(AccountRecoveryStatus.ResetUnlocked);
            recovery.ResetTokenHash.Should().MatchRegex("^[a-f0-9]{64}$");
            recovery.ResetTokenExpiresAt.Should().BeAfter(SystemClock.UtcNow);
            email.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            recovery.Status.Should().Be(AccountRecoveryStatus.PendingApprovals);
            recovery.ResetTokenHash.Should().BeNull();
        }
    }

    [Fact]
    public async Task Evaluate_AlreadyUnlockedRequestIdempotentAndDoesNotRemint()
    {
        var recovery = CreatePending(requiredApprovals: 2);
        recovery.Status = AccountRecoveryStatus.ResetUnlocked;
        recovery.ResetTokenHash = AccountRecoveryTokensTestHelpers.Hash("existing-token");
        var recoveries = new Mock<IAccountRecoveryStore>();
        recoveries.Setup(s => s.CountDistinctApprovalsAsync(recovery.Id, It.IsAny<CancellationToken>())).ReturnsAsync(5);
        var evaluator = CreateEvaluator(recoveries);

        var result = await evaluator.EvaluateAsync(recovery, null, null, CancellationToken.None);

        result.ResetUnlocked.Should().BeFalse();
        recovery.ResetTokenHash.Should().Be(AccountRecoveryTokensTestHelpers.Hash("existing-token"));
        recoveries.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

public sealed class AccountRecoveryOptionsTests
{
    [Theory]
    [InlineData(2, 3, 2)]   // configured K below factors: unchanged
    [InlineData(5, 2, 2)]   // configured K above factors: clamped down
    [InlineData(0, 3, 1)]   // configured K below one: clamped up to 1
    [InlineData(-4, 3, 1)]  // negative: clamped up to 1
    [InlineData(3, 3, 3)]   // exact match
    public void EffectiveRequiredApprovals_ClampsIntoSafeRange(int configured, int factors, int expected)
    {
        var options = new AccountRecoveryOptions { RequiredApprovals = configured };
        options.EffectiveRequiredApprovals(factors).Should().Be(expected);
    }

    [Fact]
    public void Defaults_AreFailClosed()
    {
        var options = new AccountRecoveryOptions();
        options.RequiredApprovals.Should().Be(2);
        options.Cooldown.Should().BePositive();
        options.ApprovalWindow.Should().BeGreaterThan(options.Cooldown);
        options.InviteLifetime.Should().BePositive();
        options.ResetTokenLifetime.Should().BePositive();
        options.MaxTrustedContacts.Should().BeGreaterThanOrEqualTo(2);
    }
}
