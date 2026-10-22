using FluentAssertions;
using GameGuild.Commerce.Payments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Infrastructure;

/// <summary>
///     Data-backed currency usage probe (issue #412 spec A): the DisableCurrency
///     warn-and-require-confirmation guard must count non-terminal subscriptions whose price
///     is denominated in the queried currency — per tenant — and exclude cancelled/expired
///     subscriptions and other currencies.
/// </summary>
public class CurrencySubscriptionUsageProbeTests
{
    [Fact]
    public async Task Count_IncludesActiveSubscriptions_InTheQueriedCurrency()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDatabase();
        db.Add(NewSubscription(tenantId, "EUR"));
        db.Add(NewSubscription(tenantId, "EUR", cancel: true));
        db.Add(NewSubscription(tenantId, "USD"));
        db.Add(NewSubscription(Guid.NewGuid(), "EUR")); // another tenant's subscription
        await db.SaveChangesAsync();

        var probe = new CurrencySubscriptionUsageProbe(db);

        var count = await probe.CountActiveSubscriptionsUsingCurrencyAsync(tenantId, "EUR");

        count.Should().Be(1);
    }

    [Fact]
    public async Task Count_WithNoMatchingSubscriptions_IsZero()
    {
        var tenantId = Guid.NewGuid();
        await using var db = NewDatabase();
        db.Add(NewSubscription(tenantId, "USD"));
        await db.SaveChangesAsync();

        var probe = new CurrencySubscriptionUsageProbe(db);

        (await probe.CountActiveSubscriptionsUsingCurrencyAsync(tenantId, "JPY")).Should().Be(0);
    }

    private static SubscriptionUsageTestDbContext NewDatabase()
    {
        var options = new DbContextOptionsBuilder<SubscriptionUsageTestDbContext>()
            .UseInMemoryDatabase($"currency-usage-{Guid.NewGuid():N}")
            .Options;
        var db = new SubscriptionUsageTestDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static Subscription NewSubscription(Guid tenantId, string currency, bool cancel = false)
    {
        var subscription = new Subscription(
            tenantId,
            planId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            BillingCycle.Monthly,
            new Money(10m, currency),
            startDate: SystemClock.UtcNow.AddDays(-1));
        if (cancel)
        {
            subscription.Cancel(CancellationReason.UserRequested);
        }

        return subscription;
    }
}

/// <summary>In-memory context mapping the Subscription entity for the probe tests.</summary>
internal sealed class SubscriptionUsageTestDbContext(DbContextOptions<SubscriptionUsageTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("InMemory test context does not support transactions");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
