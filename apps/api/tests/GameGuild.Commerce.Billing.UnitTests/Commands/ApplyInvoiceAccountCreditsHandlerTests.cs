using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Commerce.Subscriptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Commands;

/// <summary>
///     Issue #410 v1 apply-to-invoice settlement: idempotency key required, invoice
///     ownership enforced through the subscription owner, multi-tender settlement advances
///     the invoice (credit tender separated from provider tenders) and replays return the
///     recorded settlement without double-consuming credits.
/// </summary>
public class ApplyInvoiceAccountCreditsHandlerTests
{
    private sealed class TestSettlementDbContext(DbContextOptions<TestSettlementDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Invoice>();
            modelBuilder.Entity<Subscription>().Ignore(subscription => subscription.Plan);
            modelBuilder.Entity<Subscription>().OwnsOne(subscription => subscription.Amount);
            modelBuilder.Entity<AccountCreditGrant>();
            modelBuilder.Entity<AccountCreditTransaction>();
            modelBuilder.Entity<AccountCreditInvoiceSettlement>();
            base.OnModelCreating(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Mock.Of<IDbContextTransaction>());
    }

    private static async Task<TestSettlementDbContext> CreateContextAsync()
    {
        var context = new TestSettlementDbContext(
            new DbContextOptionsBuilder<TestSettlementDbContext>()
                .UseInMemoryDatabase($"invoice-credits-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static Mock<IActorContextAccessor> ActorAccessor(Guid tenantId, Guid userId)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Bearer",
            IsAuthenticated = true
        });
        return accessor;
    }

    private static async Task<(TestSettlementDbContext Context, ApplyInvoiceAccountCreditsHandler Handler, Guid TenantId, Guid OwnerId, Invoice Invoice)> ArrangeOpenInvoiceAsync(decimal invoiceTotal = 100m, decimal creditAmount = 70m)
    {
        var context = await CreateContextAsync();
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var subscription = new Subscription(
            tenantId: tenantId,
            planId: Guid.NewGuid(),
            createdByUserId: ownerId,
            billingCycle: BillingCycle.Monthly,
            amount: new GameGuild.Money(invoiceTotal, "USD"),
            startDate: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        context.Set<Subscription>().Add(subscription);
        await context.SaveChangesAsync();

        var invoice = new Invoice(tenantId, subscription.Id, invoiceTotal, "USD");
        invoice.Issue(SystemClock.UtcNow);
        context.Set<Invoice>().Add(invoice);
        await context.SaveChangesAsync();

        var creditService = new AccountCreditService(context);
        (await creditService.GrantAsync(new AccountCreditGrantRequest(
            tenantId, ownerId, creditAmount, "USD", AccountCreditSource.Gift,
            AccountCreditType.General, "gift credit"))).IsSuccess.Should().BeTrue();

        var handler = new ApplyInvoiceAccountCreditsHandler(context, creditService, ActorAccessor(tenantId, ownerId).Object);
        return (context, handler, tenantId, ownerId, invoice);
    }

    [Fact]
    public async Task Handle_Requires_An_Idempotency_Key()
    {
        var (_, handler, _, _, invoice) = await ArrangeOpenInvoiceAsync();

        var result = await handler.Handle(
            new ApplyInvoiceAccountCreditsCommand(invoice.Id, "  "), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("InvoiceCredits.IdempotencyKeyRequired");
    }

    [Fact]
    public async Task Handle_Rejects_Invoices_Of_Other_Users()
    {
        var (context, _, tenantId, ownerId, invoice) = await ArrangeOpenInvoiceAsync();
        var otherUserAccessor = ActorAccessor(tenantId, Guid.NewGuid());
        var handler = new ApplyInvoiceAccountCreditsHandler(
            context, new AccountCreditService(context), otherUserAccessor.Object);

        var result = await handler.Handle(
            new ApplyInvoiceAccountCreditsCommand(invoice.Id, "key-other"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("InvoiceCredits.NotOwned");
        _ = ownerId;
    }

    [Fact]
    public async Task Handle_Applies_Credit_Tender_And_Leaves_The_Remainder_To_Provider_Tenders()
    {
        var (context, handler, _, _, invoice) = await ArrangeOpenInvoiceAsync(invoiceTotal: 100m, creditAmount: 70m);

        var result = await handler.Handle(
            new ApplyInvoiceAccountCreditsCommand(invoice.Id, "idem-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AmountApplied.Should().Be(70m);
        result.Value.AmountRemaining.Should().Be(30m);
        result.Value.InvoiceStatus.Should().Be(InvoiceStatus.Open); // partially covered
        result.Value.ConsumedGrants.Should().ContainSingle();

        var settled = await context.Set<Invoice>().SingleAsync(entity => entity.Id == invoice.Id);
        settled.AmountPaid.Should().Be(70m);
        settled.PaymentId.Should().BeNull(); // provider tender untouched — multi-tender separation
        (await context.Set<AccountCreditInvoiceSettlement>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_Completes_The_Invoice_When_Credits_Cover_It_Fully()
    {
        var (_, handler, _, _, invoice) = await ArrangeOpenInvoiceAsync(invoiceTotal: 100m, creditAmount: 150m);

        var result = await handler.Handle(
            new ApplyInvoiceAccountCreditsCommand(invoice.Id, "idem-full"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AmountApplied.Should().Be(100m);
        result.Value.AmountRemaining.Should().Be(0m);
        result.Value.InvoiceStatus.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Handle_Is_Idempotent_Per_Key_Returning_The_Recorded_Settlement()
    {
        // Grant 150 so the 70 tender is a PARTIAL consumption — the replay must not touch
        // the surviving 80 (with the 70 default the grant would be fully used and the
        // "balance moved exactly once" assertion could not distinguish a replay from a
        // double application).
        var (context, handler, _, ownerId, invoice) = await ArrangeOpenInvoiceAsync(invoiceTotal: 100m, creditAmount: 150m);

        var first = await handler.Handle(
            new ApplyInvoiceAccountCreditsCommand(invoice.Id, "idem-replay", MaxAmount: 70m), CancellationToken.None);
        var second = await handler.Handle(
            new ApplyInvoiceAccountCreditsCommand(invoice.Id, "idem-replay", MaxAmount: 70m), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        first.Value.AmountApplied.Should().Be(70m);
        second.IsSuccess.Should().BeTrue();
        second.Value.IdempotentReplay.Should().BeTrue();
        second.Value.AmountApplied.Should().Be(first.Value.AmountApplied);

        // no double consumption: the credit balance moved exactly once
        var remaining = await context.Set<AccountCreditGrant>()
            .Where(grant => grant.UserId == ownerId)
            .SumAsync(grant => grant.RemainingAmount);
        remaining.Should().Be(80m); // 150 gift - 70 applied
    }

    [Fact]
    public async Task Handle_Fails_Closed_Without_An_Authenticated_Actor()
    {
        var (context, _, _, _, invoice) = await ArrangeOpenInvoiceAsync();
        var anonymous = new Mock<IActorContextAccessor>();
        anonymous.SetupGet(value => value.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = false
        });
        var handler = new ApplyInvoiceAccountCreditsHandler(context, new AccountCreditService(context), anonymous.Object);

        var result = await handler.Handle(
            new ApplyInvoiceAccountCreditsCommand(invoice.Id, "key-anon"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("InvoiceCredits.ActorRequired");
    }
}
