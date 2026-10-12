using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.AccountCredits;

/// <summary>
///     Issue #410 v1: daily expiry worker gating/options and the admin growth metrics
///     report (credit issuance/usage/expiry rates, referral funnel, top referrers).
/// </summary>
public class AccountCreditWorkerAndMetricsTests
{
    private static async Task<PaymentsPersistenceTestDbContext> CreateContextAsync()
    {
        var context = new PaymentsPersistenceTestDbContext(
            new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
                .UseInMemoryDatabase($"credits-metrics-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public void OptionsValidator_Rejects_Invalid_Configuration_Fail_Closed()
    {
        var validator = new AccountCreditOptionsValidator();

        var invalid = new AccountCreditOptions
        {
            ReferrerRewardAmount = -1m,
            ReferralLinkBaseUrl = "http://not-https.example.com",
            WorkerIntervalHours = 0
        };
        var result = validator.Validate(null, invalid);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(message => message.Contains("WorkerIntervalHours"));
        result.Failures.Should().Contain(message => message.Contains("ReferralLinkBaseUrl"));
        result.Failures.Should().Contain(message => message.Contains("cannot be negative"));

        var valid = validator.Validate(null, new AccountCreditOptions());
        valid.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Worker_Disabled_By_Configuration_Does_Nothing()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAccountCreditService>(_ => new AccountCreditService(CreateContextAsync().GetAwaiter().GetResult()));
        var provider = services.BuildServiceProvider();
        var options = Options.Create(new AccountCreditOptions { WorkerEnabled = false });

        using var worker = new AccountCreditExpiryBackgroundService(
            provider, options, NullLogger<AccountCreditExpiryBackgroundService>.Instance);

        // Disabled worker returns immediately; executing it to completion must not throw.
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Metrics_Report_Aggregates_Ledger_Funnel_And_Leaderboard()
    {
        await using var context = await CreateContextAsync();
        var creditService = new AccountCreditService(context);
        var referralService = new ReferralService(
            context, creditService,
            Options.Create(new AccountCreditOptions
            {
                ReferrerRewardAmount = 10m,
                RefereeRewardAmount = 0m,
                MaxReferralsPerUser = null
            }));

        var tenantId = Guid.NewGuid();
        var referrer = Guid.NewGuid();

        // issued 100, applied 40 → usage rate 0.4
        (await creditService.GrantAsync(new AccountCreditGrantRequest(
            tenantId, Guid.NewGuid(), 100m, "USD", AccountCreditSource.Manual,
            AccountCreditType.General, "g1"))).IsSuccess.Should().BeTrue();
        (await creditService.GrantAsync(new AccountCreditGrantRequest(
            tenantId, Guid.NewGuid(), 40m, "EUR", AccountCreditSource.Manual,
            AccountCreditType.General, "g2"))).IsSuccess.Should().BeTrue();
        var consume = await creditService.ConsumeAsync(
            tenantId, context.Set<AccountCreditGrant>().AsQueryable()
                .First(grant => grant.Currency == "EUR").UserId,
            "EUR", 40m, Guid.NewGuid(), "subscription", null);
        consume.IsSuccess.Should().BeTrue();

        // one fully rewarded referral for the referrer, one fraudulent block
        var code = (await referralService.GetOrCreateCodeAsync(tenantId, referrer)).Value.Code;
        var referee = Guid.NewGuid();
        (await referralService.RecordSignupAsync(tenantId, code, referee, "ip-1")).IsSuccess.Should().BeTrue();
        (await referralService.HandleInvoicePaidAsync(tenantId, referee, Guid.NewGuid(), Guid.NewGuid())).IsSuccess
            .Should().BeTrue();
        (await referralService.RecordSignupAsync(tenantId, code, Guid.NewGuid(), "ip-1")).IsSuccess.Should().BeTrue();

        var handler = new GetAccountCreditMetricsQueryHandler(context);
        var report = await handler.Handle(new GetAccountCreditMetricsQuery(tenantId), CancellationToken.None);

        report.Credit.PerCurrency.Should().HaveCount(2);
        var eur = report.Credit.PerCurrency.Single(row => row.Currency == "EUR");
        eur.Issued.Should().Be(40m);
        eur.Applied.Should().Be(40m);

        // Issued across currencies = 100 USD + 40 EUR + 10 USD referrer reward = 150;
        // applied 40 EUR → 40/150 = 0.2667.
        report.Credit.UsageRate.Should().Be(0.2667m);

        report.ReferralFunnel.Clicks.Should().Be(0);
        report.ReferralFunnel.Signups.Should().Be(1);
        report.ReferralFunnel.RewardsIssued.Should().Be(1);
        report.ReferralFunnel.Fraudulent.Should().Be(1);

        report.TopReferrers.Should().ContainSingle()
            .Which.Should().Match<ReferrerLeaderboardEntry>(entry =>
                entry.ReferrerUserId == referrer && entry.RewardedReferrals == 1 && entry.FraudulentReferrals == 1);
    }
}
