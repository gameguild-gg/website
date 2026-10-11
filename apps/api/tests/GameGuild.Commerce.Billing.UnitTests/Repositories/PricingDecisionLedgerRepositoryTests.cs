using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Commerce.Billing.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Repositories;

/// <summary>
/// Issue #400: repository-level ledger behavior — exact-code filtering (no substring
/// false positives), window/tenant/correlation filters, effectiveness aggregation and
/// drift sampling that prefers never-validated entries.
/// </summary>
public sealed class PricingDecisionLedgerRepositoryTests
{
    [Fact]
    public async Task SearchAsync_FiltersByExactPromoCodeWithoutSubstringMatches()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var repository = new PricingDecisionLedgerRepository(context);

        context.Set<PricingDecisionLedgerEntry>().AddRange(
            Entry(idempotencyKey: "1", appliedPromoCodes: ",WELCOME,"),
            Entry(idempotencyKey: "2", appliedPromoCodes: ",WELCOME10,"),
            Entry(idempotencyKey: "3", appliedPromoCodes: ",WELCOME,VIP,"),
            Entry(idempotencyKey: "4", appliedPromoCodes: null));
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.SearchAsync(
            new PricingDecisionLedgerSearchCriteria(PromoCode: "welcome"),
            skip: 0,
            take: 10);

        totalCount.Should().Be(2);
        items.Select(entry => entry.IdempotencyKey).Should().BeEquivalentTo(["1", "3"],
            "the WELCOME filter must match the exact code only, never WELCOME10");
    }

    [Fact]
    public async Task SearchAsync_FiltersByTenantOrderSubscriptionAndWindow()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var repository = new PricingDecisionLedgerRepository(context);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var orderA = Guid.NewGuid();
        var subscription = Guid.NewGuid();
        var cutoff = DateTime.UtcNow;

        context.Set<PricingDecisionLedgerEntry>().AddRange(
            Entry(idempotencyKey: "t-a", tenantId: tenantA, createdAtUtc: cutoff.AddMinutes(30)),
            Entry(idempotencyKey: "t-b", tenantId: tenantB, createdAtUtc: cutoff.AddMinutes(30)),
            Entry(idempotencyKey: "o-a", orderId: orderA, createdAtUtc: cutoff.AddMinutes(30)),
            Entry(idempotencyKey: "s-1", subscriptionId: subscription, createdAtUtc: cutoff.AddMinutes(30)),
            Entry(idempotencyKey: "old", createdAtUtc: cutoff.AddHours(-2)),
            Entry(idempotencyKey: "new", createdAtUtc: cutoff.AddHours(2)));
        await context.SaveChangesAsync();

        (await repository.SearchAsync(new PricingDecisionLedgerSearchCriteria(TenantId: tenantA), 0, 10))
            .TotalCount.Should().Be(1);
        (await repository.SearchAsync(new PricingDecisionLedgerSearchCriteria(OrderId: orderA), 0, 10))
            .TotalCount.Should().Be(1);
        (await repository.SearchAsync(new PricingDecisionLedgerSearchCriteria(SubscriptionId: subscription), 0, 10))
            .TotalCount.Should().Be(1);
        (await repository.SearchAsync(new PricingDecisionLedgerSearchCriteria(FromUtc: cutoff), 0, 10))
            .TotalCount.Should().Be(5, "only the entry decided before the cutoff is excluded by FromUtc");
        (await repository.SearchAsync(new PricingDecisionLedgerSearchCriteria(ToUtc: cutoff), 0, 10))
            .TotalCount.Should().Be(1, "only the entry decided before the cutoff is included by ToUtc");
    }

    [Fact]
    public async Task GetEffectivenessAsync_AggregatesDiscountVersusBaselineRevenue()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var repository = new PricingDecisionLedgerRepository(context);
        var tenant = Guid.NewGuid();
        var rule = Guid.NewGuid();

        context.Set<PricingDecisionLedgerEntry>().AddRange(
            Entry("e1", tenantId: tenant, baseAmount: 100m, finalAmount: 80m, promoTotal: 20m, appliedPromoCodes: ",WELCOME,", ruleId: rule, ruleTotal: 0m, stackingRule: "stack-a"),
            Entry("e2", tenantId: tenant, baseAmount: 50m, finalAmount: 50m, promoTotal: 0m, appliedPromoCodes: null, ruleId: null),
            Entry("e3", tenantId: tenant, baseAmount: 30m, finalAmount: 15m, promoTotal: 15m, appliedPromoCodes: ",WELCOME,", ruleId: rule, ruleTotal: 0m, stackingRule: "stack-a"),
            Entry("e4", tenantId: Guid.NewGuid(), baseAmount: 999m, finalAmount: 1m, promoTotal: 998m, appliedPromoCodes: ",OTHER,")); // other tenant
        await context.SaveChangesAsync();

        var aggregate = await repository.GetEffectivenessAsync(null, null, tenant, CancellationToken.None);

        aggregate.TenantId.Should().Be(tenant);
        aggregate.Totals.EntryCount.Should().Be(3);
        aggregate.Totals.BaselineRevenue.Should().Be(180m);
        aggregate.Totals.FinalRevenue.Should().Be(145m);
        aggregate.Totals.DiscountGiven.Should().Be(35m);
        aggregate.Totals.EntriesWithPromo.Should().Be(2);
        aggregate.Totals.EntriesWithRule.Should().Be(2);
        aggregate.Totals.PromoRedemptions.Should().Be(2);

        var welcome = aggregate.TopPromoCodes.Single(row => row.Code == "WELCOME");
        welcome.Redemptions.Should().Be(2);
        welcome.DiscountGiven.Should().Be(35m);
        welcome.BaselineRevenue.Should().Be(130m);
        welcome.RevenueInfluenced.Should().Be(95m);

        aggregate.TopRules.Should().ContainSingle()
            .Which.RuleId.Should().Be(rule);

        aggregate.StackingOutcomes.Should().Contain(
            row => row.StackingRuleName == "stack-a" && row.Decisions == 2);
        aggregate.StackingOutcomes.Should().Contain(
            row => row.StackingRuleName == "(no stacking rule)" && row.Decisions == 1);

        aggregate.DriftOutcomes.Should().ContainSingle()
            .Which.DriftStatus.Should().Be(PricingDecisionDriftStatus.NotValidated);
    }

    [Fact]
    public async Task SampleForValidationAsync_PrefersNeverValidatedOldestFirst()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var repository = new PricingDecisionLedgerRepository(context);

        var oldUnvalidated = Entry("old-unvalidated");
        oldUnvalidated.CreatedAt = DateTime.UtcNow.AddHours(-3);

        var newUnvalidated = Entry("new-unvalidated");
        newUnvalidated.CreatedAt = DateTime.UtcNow.AddHours(-1);

        var verified = Entry("verified");
        verified.StampValidation(PricingDecisionDriftStatus.Verified, verified.FinalAmount, null);

        context.Set<PricingDecisionLedgerEntry>().AddRange(oldUnvalidated, newUnvalidated, verified);
        await context.SaveChangesAsync();

        var sample = await repository.SampleForValidationAsync(null, null, 2, CancellationToken.None);

        sample.Should().HaveCount(2);
        sample.Select(entry => entry.IdempotencyKey).Should().BeEquivalentTo(["old-unvalidated", "new-unvalidated"],
            o => o.WithoutStrictOrdering(),
            "never-validated entries are sampled first");
        sample.Select(entry => entry.IdempotencyKey).Should().NotContain("verified");
    }

    private static PricingDecisionLedgerEntry Entry(
        string idempotencyKey,
        Guid? tenantId = null,
        Guid? orderId = null,
        Guid? subscriptionId = null,
        decimal baseAmount = 100m,
        decimal finalAmount = 90m,
        decimal promoTotal = 10m,
        string? appliedPromoCodes = null,
        Guid? ruleId = null,
        decimal ruleTotal = 0m,
        string? stackingRule = null,
        DateTime? createdAtUtc = null) => new()
    {
        Id = Guid.NewGuid(),
        DecisionType = PricingDecisionType.CheckoutLineItem,
        TenantId = tenantId,
        OrderId = orderId,
        SubscriptionId = subscriptionId,
        Quantity = 1,
        Currency = "USD",
        BaseAmount = baseAmount,
        RuleAdjustmentTotal = ruleTotal,
        PromoDiscountTotal = promoTotal,
        FinalAmount = finalAmount,
        BreakdownJson = (PricingDecisionBreakdown.Empty("USD")
            with
        {
            BaseUnitPrice = baseAmount,
            BaseAmount = baseAmount,
            PromoDiscountTotal = promoTotal,
            FinalAmount = finalAmount
        }).ToJson(),
        AppliedPromoCodes = appliedPromoCodes,
        AppliedPricingRuleId = ruleId,
        AppliedStackingRuleName = stackingRule,
        IdempotencyKey = idempotencyKey,
        CreatedAt = createdAtUtc ?? DateTime.UtcNow
    };
}
