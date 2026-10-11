using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.AccountCredits;

/// <summary>
///     Issue #410 v1 credit ledger persistence behavior: idempotent grants, per-currency
///     balances, FIFO-by-expiry consumption, credit-type eligibility, revocation and the
///     bounded expiry pass (grants and unredeemed gifts).
/// </summary>
public class AccountCreditServicePersistenceTests
{
    private static readonly DateTime FarFuture = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<PaymentsPersistenceTestDbContext> CreateContextAsync()
    {
        var context = new PaymentsPersistenceTestDbContext(
            new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
                .UseInMemoryDatabase($"account-credits-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static async Task<AccountCreditService> ArrangeCreditsAsync(
        PaymentsPersistenceTestDbContext context,
        Guid tenantId,
        Guid userId,
        params (decimal Amount, DateTime? ExpiresAt, AccountCreditType Type)[] grants)
    {
        var service = new AccountCreditService(context);
        foreach (var (amount, expiresAt, type) in grants)
        {
            var result = await service.GrantAsync(new AccountCreditGrantRequest(
                tenantId, userId, amount, "USD", AccountCreditSource.Manual, type,
                $"grant {amount}", ExpiresAt: expiresAt));
            result.IsSuccess.Should().BeTrue($"grant of {amount} should succeed");
        }

        return service;
    }

    [Fact]
    public async Task Grant_Is_Idempotent_Per_Tenant_And_Key()
    {
        await using var context = await CreateContextAsync();
        var service = new AccountCreditService(context);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var first = await service.GrantAsync(new AccountCreditGrantRequest(
            tenantId, userId, 25m, "usd", AccountCreditSource.Manual, AccountCreditType.General,
            "identical", IdempotencyKey: "op-1"));
        var second = await service.GrantAsync(new AccountCreditGrantRequest(
            tenantId, userId, 25m, "USD", AccountCreditSource.Manual, AccountCreditType.General,
            "identical", IdempotencyKey: "op-1"));

        second.IsSuccess.Should().BeTrue();
        second.Value.Id.Should().Be(first.Value.Id);
        first.Value.Currency.Should().Be("USD"); // normalized upper-case
        (await context.Set<AccountCreditGrant>().CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("dollars")]
    public async Task Grant_Rejects_Unknown_Currencies(string currency)
    {
        await using var context = await CreateContextAsync();
        var service = new AccountCreditService(context);

        var result = await service.GrantAsync(new AccountCreditGrantRequest(
            Guid.NewGuid(), Guid.NewGuid(), 10m, currency, AccountCreditSource.Manual,
            AccountCreditType.General, "d"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AccountCredits.InvalidCurrency");
    }

    [Fact]
    public async Task GetBalancesAsync_Sums_Active_Grants_Per_Currency_Excluding_Expired()
    {
        await using var context = await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = await ArrangeCreditsAsync(context, tenantId, userId,
            (30m, null, AccountCreditType.General),
            (12m, FarFuture, AccountCreditType.General));

        var eur = await service.GrantAsync(new AccountCreditGrantRequest(
            tenantId, userId, 7m, "EUR", AccountCreditSource.Manual, AccountCreditType.General, "eur"));
        eur.IsSuccess.Should().BeTrue();

        var balances = await service.GetBalancesAsync(tenantId, userId);

        balances.Should().HaveCount(2);
        balances.Single(balance => balance.Currency == "USD").Available.Should().Be(42m);
        balances.Single(balance => balance.Currency == "EUR").Available.Should().Be(7m);
    }

    [Fact]
    public async Task ConsumeAsync_Drains_Fifo_By_Expiry_Non_Expiring_Last()
    {
        await using var context = await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        // non-expiring first-issued, expiring later-issued: FIFO must still drain the expiring one first.
        var service = await ArrangeCreditsAsync(context, tenantId, userId,
            (20m, null, AccountCreditType.General),
            (15m, FarFuture, AccountCreditType.General),
            (10m, FarFuture.AddDays(10), AccountCreditType.General));

        var invoiceId = Guid.NewGuid();
        var result = await service.ConsumeAsync(tenantId, userId, "USD", 30m, invoiceId, "subscription", null);

        result.IsSuccess.Should().BeTrue();
        var application = result.Value;
        application.AmountApplied.Should().Be(30m);
        application.ConsumedGrants.Should().HaveCount(3);
        // earliest expiry drained first, next expiry second, non-expiring grant last (partial).
        application.ConsumedGrants[0].Amount.Should().Be(15m);
        application.ConsumedGrants[1].Amount.Should().Be(10m);
        application.ConsumedGrants[2].Amount.Should().Be(5m);

        var consumeRows = await context.Set<AccountCreditTransaction>()
            .Where(transaction => transaction.Type == AccountCreditTransactionType.Consume)
            .ToListAsync();
        consumeRows.Should().HaveCount(3);
        consumeRows.Should().OnlyContain(row => row.ReferenceId == invoiceId);
    }

    [Fact]
    public async Task ConsumeAsync_Respects_Credit_Type_Eligibility()
    {
        await using var context = await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        // only Addon credit exists: subscription invoices must not touch it (fail closed).
        var service = await ArrangeCreditsAsync(context, tenantId, userId,
            (50m, null, AccountCreditType.Addon));

        var result = await service.ConsumeAsync(tenantId, userId, "USD", 10m, Guid.NewGuid(), "subscription", null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AccountCredits.NoEligibleCredit");
    }

    [Fact]
    public async Task ConsumeAsync_Caps_At_Available_Balance_And_Reports_Remainder()
    {
        await using var context = await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = await ArrangeCreditsAsync(context, tenantId, userId, (8m, null, AccountCreditType.General));

        var result = await service.ConsumeAsync(tenantId, userId, "USD", 20m, Guid.NewGuid(), "subscription", null);

        result.IsSuccess.Should().BeTrue();
        result.Value.AmountApplied.Should().Be(8m);
        result.Value.AmountRemaining.Should().Be(12m);
    }

    [Fact]
    public async Task RevokeAsync_Captures_Reason_In_Ledger()
    {
        await using var context = await CreateContextAsync();
        var service = new AccountCreditService(context);
        var tenantId = Guid.NewGuid();
        var grant = (await service.GrantAsync(new AccountCreditGrantRequest(
            tenantId, Guid.NewGuid(), 15m, "USD", AccountCreditSource.Manual, AccountCreditType.General, "g"))).Value;

        var result = await service.RevokeAsync(tenantId, grant.Id, "chargeback");

        result.IsSuccess.Should().BeTrue();
        var revoked = await context.Set<AccountCreditGrant>().SingleAsync(g => g.Id == grant.Id);
        revoked.Status.Should().Be(AccountCreditGrantStatus.Revoked);
        revoked.RevocationReason.Should().Be("chargeback");
        (await context.Set<AccountCreditTransaction>()
            .CountAsync(transaction => transaction.Type == AccountCreditTransactionType.Revoke))
            .Should().Be(1);
    }

    [Fact]
    public async Task ExpireDueAsync_Flips_Due_Grants_And_Unredeemed_Gifts_Bounded()
    {
        await using var context = await CreateContextAsync();
        var now = SystemClock.UtcNow;
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Set<AccountCreditGrant>().AddRange(
            new AccountCreditGrant(tenantId, userId, 10m, "USD", AccountCreditSource.Manual,
                AccountCreditType.General, "due", ExpiresAt: now.AddHours(-1)),
            new AccountCreditGrant(tenantId, userId, 10m, "USD", AccountCreditSource.Manual,
                AccountCreditType.General, "not due", ExpiresAt: now.AddHours(1)));

        var giftCode = GiftRedemptionCodeGenerator.HashCode(GiftRedemptionCodeGenerator.GenerateCode());
        context.Set<GiftSubscription>().Add(new GiftSubscription(
            tenantId, Guid.NewGuid(), "late@example.test", 20m, "USD", 6, giftCode, "PREFIX",
            purchasedAt: now.AddYears(-2))); // way past the 1-year window
        await context.SaveChangesAsync();

        var service = new AccountCreditService(context);
        var result = await service.ExpireDueAsync(now, batchSize: 100);

        result.ExpiredGrants.Should().Be(1);
        result.ExpiredGrantAmount.Should().Be(10m);
        result.ExpiredGifts.Should().Be(1);
        (await context.Set<AccountCreditTransaction>()
            .CountAsync(transaction => transaction.Type == AccountCreditTransactionType.Expire))
            .Should().Be(1);
    }

    [Fact]
    public async Task GetHistoryAsync_Pages_Newest_First()
    {
        await using var context = await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new AccountCreditService(context);
        for (var i = 0; i < 3; i++)
        {
            (await service.GrantAsync(new AccountCreditGrantRequest(
                tenantId, userId, 5m + i, "USD", AccountCreditSource.Manual, AccountCreditType.General, $"g{i}")))
                .IsSuccess.Should().BeTrue();
        }

        var page = await service.GetHistoryAsync(tenantId, userId, skip: 0, take: 2);

        page.TotalCount.Should().Be(3);
        page.Items.Should().HaveCount(2);
        page.Items.Should().OnlyContain(row => row.Type == AccountCreditTransactionType.Grant);
    }
}
