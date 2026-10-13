using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.AccountCredits;

/// <summary>
///     Issue #410 v1 referral tracking: per-user code idempotency, funnel tracking, the
///     first-paid-invoice conversion with credit rewards for both parties, and the
///     fail-closed fraud blocks (self-referral, same-IP repeat, referral cap).
/// </summary>
public class ReferralServiceTests
{
    private static async Task<(ReferralService Service, PaymentsPersistenceTestDbContext Context)> CreateAsync(
        Action<AccountCreditOptions>? configure = null)
    {
        var context = new PaymentsPersistenceTestDbContext(
            new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
                .UseInMemoryDatabase($"referrals-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        var options = Options.Create(new AccountCreditOptions());
        configure?.Invoke(options.Value);
        return (new ReferralService(context, new AccountCreditService(context), options), context);
    }

    [Fact]
    public async Task GetOrCreateCode_Is_Idempotent_Per_User_And_Builds_Trackable_Link()
    {
        var (service, _) = await CreateAsync(options => options.ReferralLinkBaseUrl = "https://join.example.com/");
        var tenantId = Guid.NewGuid();
        var referrer = Guid.NewGuid();

        var first = await service.GetOrCreateCodeAsync(tenantId, referrer);
        var second = await service.GetOrCreateCodeAsync(tenantId, referrer);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Code.Should().Be(first.Value.Code);
        second.Value.Link.Should().Be($"https://join.example.com/?ref={first.Value.Code}");
    }

    [Fact]
    public async Task RecordClick_Tracks_The_First_Click_Only()
    {
        var (service, _) = await CreateAsync();
        var tenantId = Guid.NewGuid();
        var code = (await service.GetOrCreateCodeAsync(tenantId, Guid.NewGuid())).Value.Code;

        var click = await service.RecordClickAsync(tenantId, code, "clicker@example.test", "ip-1");
        var replay = await service.RecordClickAsync(tenantId, code, null, "ip-2");

        click.IsSuccess.Should().BeTrue();
        replay.IsSuccess.Should().BeTrue();
        replay.Value.ClickedAt.Should().Be(click.Value.ClickedAt);
        replay.Value.Status.Should().Be(ReferralStatus.Clicked);
        var unknown = await service.RecordClickAsync(tenantId, "nope", null, null);
        unknown.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleInvoicePaid_Issues_Credit_Rewards_Once_After_First_Paid_Invoice()
    {
        var (service, context) = await CreateAsync(options =>
        {
            options.ReferrerRewardAmount = 11m;
            options.RefereeRewardAmount = 4m;
        });
        var tenantId = Guid.NewGuid();
        var referrer = Guid.NewGuid();
        var referee = Guid.NewGuid();
        var code = (await service.GetOrCreateCodeAsync(tenantId, referrer)).Value.Code;

        (await service.RecordSignupAsync(tenantId, code, referee, ipHash: "ip-1")).IsSuccess.Should().BeTrue();

        var subscriptionId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var first = await service.HandleInvoicePaidAsync(tenantId, referee, subscriptionId, invoiceId);
        first.IsSuccess.Should().BeTrue();

        // replay of the same paid invoice event: no double rewards
        var replay = await service.HandleInvoicePaidAsync(tenantId, referee, subscriptionId, invoiceId);
        replay.IsSuccess.Should().BeTrue();

        var referral = await context.Set<Referral>().SingleAsync(row => row.RefereeUserId == referee);
        referral.Status.Should().Be(ReferralStatus.RewardIssued);
        referral.ConversionInvoiceId.Should().Be(invoiceId);

        var grants = await context.Set<AccountCreditGrant>().ToListAsync();
        grants.Should().HaveCount(2);
        grants.Should().Contain(grant => grant.UserId == referrer && grant.Amount == 11m
                                         && grant.Source == AccountCreditSource.Referral
                                         && grant.IdempotencyKey == $"referral:{referral.Id}:referrer");
        grants.Should().Contain(grant => grant.UserId == referee && grant.Amount == 4m
                                         && grant.Source == AccountCreditSource.Referral
                                         && grant.IdempotencyKey == $"referral:{referral.Id}:referee");
    }

    [Fact]
    public async Task RecordSignup_Blocks_Self_Referral_Fraud()
    {
        var (service, context) = await CreateAsync();
        var tenantId = Guid.NewGuid();
        var referrer = Guid.NewGuid();
        var code = (await service.GetOrCreateCodeAsync(tenantId, referrer)).Value.Code;

        var result = await service.RecordSignupAsync(tenantId, code, referrer, ipHash: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Blocked.Should().BeTrue();
        result.Value.Reason.Should().Contain("Self-referral");
        (await context.Set<Referral>().SingleAsync()).Status.Should().Be(ReferralStatus.Fraudulent);
    }

    [Fact]
    public async Task RecordSignup_Blocks_Same_Ip_Repeat_Under_The_Same_Referrer()
    {
        var (service, _) = await CreateAsync();
        var tenantId = Guid.NewGuid();
        var referrer = Guid.NewGuid();
        var code = (await service.GetOrCreateCodeAsync(tenantId, referrer)).Value.Code;

        var first = await service.RecordSignupAsync(tenantId, code, Guid.NewGuid(), ipHash: "shared-ip");
        first.IsSuccess.Should().BeTrue();
        first.Value.Blocked.Should().BeFalse();

        var second = await service.RecordSignupAsync(tenantId, code, Guid.NewGuid(), ipHash: "shared-ip");
        second.IsSuccess.Should().BeTrue();
        second.Value.Blocked.Should().BeTrue();
        second.Value.Referral.Status.Should().Be(ReferralStatus.Fraudulent);
    }

    [Fact]
    public async Task RecordSignup_Enforces_The_Referrer_Cap_Fail_Closed()
    {
        var (service, _) = await CreateAsync(options => options.MaxReferralsPerUser = 1);
        var tenantId = Guid.NewGuid();
        var referrer = Guid.NewGuid();
        var code = (await service.GetOrCreateCodeAsync(tenantId, referrer)).Value.Code;

        (await service.RecordSignupAsync(tenantId, code, Guid.NewGuid(), ipHash: "ip-a")).IsSuccess.Should().BeTrue();

        var over = await service.RecordSignupAsync(tenantId, code, Guid.NewGuid(), ipHash: "ip-b");
        over.IsFailure.Should().BeTrue();
        over.Error.Code.Should().Be("Referrals.CapReached");
    }

    [Fact]
    public async Task Multiple_Referees_Share_The_Referrer_Code_Each_With_Their_Own_Row()
    {
        var (service, _) = await CreateAsync(options => options.MaxReferralsPerUser = null);
        var tenantId = Guid.NewGuid();
        var referrer = Guid.NewGuid();
        var code = (await service.GetOrCreateCodeAsync(tenantId, referrer)).Value.Code;

        (await service.RecordSignupAsync(tenantId, code, Guid.NewGuid(), ipHash: "ip-1")).IsSuccess.Should().BeTrue();
        (await service.RecordSignupAsync(tenantId, code, Guid.NewGuid(), ipHash: "ip-2")).IsSuccess.Should().BeTrue();

        var stats = await service.GetStatsAsync(tenantId, referrer);
        stats.IsSuccess.Should().BeTrue();
        stats.Value.ReferralCode.Should().Be(code);
        stats.Value.SignedUp.Should().Be(2);
    }
}
