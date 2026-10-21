using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.FraudDetection;

/// <summary>
///     Issue #409: the fraud-review queue query and the fail-closed authorization contract
///     of the fraud admin surface (<c>payments:admin</c>).
/// </summary>
public class FraudReviewQueueAndAuthorizationTests : IAsyncDisposable
{
    private readonly PaymentsPersistenceTestDbContext _context = new(
        new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"fraud-reviews-{Guid.NewGuid():N}")
            .Options);

    [Fact]
    public async Task GetFraudReviewsQuery_ShouldReturnOnlyPendingReviewsForTheTenant()
    {
        var tenantId = Guid.NewGuid();
        var expected = await ReviewHeldPaymentAsync(tenantId);
        await ReviewHeldPaymentAsync(tenantId); // second pending review for the same tenant
        await ReviewHeldPaymentAsync(Guid.NewGuid()); // other tenant: must be excluded
        await BlockHeldPaymentAsync(tenantId); // blocked payments are not reviewable
        await ApprovedReviewPaymentAsync(tenantId); // resolved reviews leave the queue
        await PlainFailedPaymentAsync(tenantId); // non-fraud failures are not reviews

        var handler = new GetFraudReviewsQueryHandler(_context);
        var reviews = await handler.Handle(new GetFraudReviewsQuery(tenantId), CancellationToken.None);

        reviews.Should().HaveCount(2);
        reviews.Should().OnlyContain(review => review.TenantId == tenantId);
        reviews.Should().Contain(review => review.PaymentId == expected.Id);
        reviews.Should().OnlyContain(review =>
            review.Score >= 0 && review.Score <= 100
            && !string.IsNullOrWhiteSpace(review.Reason));
    }

    [Fact]
    public async Task GetFraudReviewsQuery_ShouldPageOldestFirst()
    {
        var tenantId = Guid.NewGuid();
        var oldest = await ReviewHeldPaymentAsync(tenantId, createdAt: SystemClock.UtcNow.AddMinutes(-30));
        var middle = await ReviewHeldPaymentAsync(tenantId, createdAt: SystemClock.UtcNow.AddMinutes(-20));
        var newest = await ReviewHeldPaymentAsync(tenantId, createdAt: SystemClock.UtcNow.AddMinutes(-10));

        var handler = new GetFraudReviewsQueryHandler(_context);

        var firstPage = await handler.Handle(new GetFraudReviewsQuery(tenantId, Page: 1, PageSize: 2), CancellationToken.None);
        var secondPage = await handler.Handle(new GetFraudReviewsQuery(tenantId, Page: 2, PageSize: 2), CancellationToken.None);

        firstPage.Select(review => review.PaymentId).Should().Equal(oldest.Id, middle.Id);
        secondPage.Select(review => review.PaymentId).Should().Equal(newest.Id);
    }

    [Fact]
    public async Task GetFraudReviewsQuery_ShouldFilterByMinimumScore()
    {
        var tenantId = Guid.NewGuid();
        await ReviewHeldPaymentAsync(tenantId, score: 45);
        var high = await ReviewHeldPaymentAsync(tenantId, score: 80);

        var handler = new GetFraudReviewsQueryHandler(_context);
        var reviews = await handler.Handle(new GetFraudReviewsQuery(tenantId, MinScore: 60), CancellationToken.None);

        reviews.Select(review => review.PaymentId).Should().Equal(high.Id);
    }

    [Fact]
    public async Task GetFraudReviewsQuery_ShouldFilterByRiskBand()
    {
        var tenantId = Guid.NewGuid();
        await ReviewHeldPaymentAsync(tenantId, score: 45);  // Medium (31-60)
        var critical = await ReviewHeldPaymentAsync(tenantId, score: 92); // Critical (86+)

        var handler = new GetFraudReviewsQueryHandler(_context);
        var reviews = await handler.Handle(new GetFraudReviewsQuery(tenantId, RiskLevel: FraudRiskLevel.Critical), CancellationToken.None);

        reviews.Select(review => review.PaymentId).Should().Equal(critical.Id);
        reviews.Single().RiskLevel.Should().Be(FraudRiskLevel.Critical);
    }

    [Fact]
    public async Task GetFraudReviewsQuery_ShouldFilterByMinimumAmount()
    {
        var tenantId = Guid.NewGuid();
        await ReviewHeldPaymentAsync(tenantId, amount: 25m);
        var large = await ReviewHeldPaymentAsync(tenantId, amount: 500m);

        var handler = new GetFraudReviewsQueryHandler(_context);
        var reviews = await handler.Handle(new GetFraudReviewsQuery(tenantId, MinAmount: 100m), CancellationToken.None);

        reviews.Select(review => review.PaymentId).Should().Equal(large.Id);
    }

    [Fact]
    public async Task GetFraudReviewsQuery_ShouldFilterByCreationDateRange()
    {
        var tenantId = Guid.NewGuid();
        await ReviewHeldPaymentAsync(tenantId, createdAt: SystemClock.UtcNow.AddDays(-5));
        var recent = await ReviewHeldPaymentAsync(tenantId, createdAt: SystemClock.UtcNow.AddHours(-1));

        var handler = new GetFraudReviewsQueryHandler(_context);
        var reviews = await handler.Handle(
            new GetFraudReviewsQuery(tenantId, FromUtc: SystemClock.UtcNow.AddDays(-1), ToUtc: SystemClock.UtcNow),
            CancellationToken.None);

        reviews.Select(review => review.PaymentId).Should().Equal(recent.Id);
    }

    [Theory]
    [MemberData(nameof(FraudAdminRequests))]
    public void FraudAdminRequests_ShouldRequireThePaymentsAdminPermission(Type requestType)
    {
        var attribute = requestType.GetCustomAttributes(typeof(AuthorizeRequestAttribute), inherit: true)
            .Cast<AuthorizeRequestAttribute>()
            .Single();

        attribute.Permission.Should().Be(PaymentsPermission.Keys.Admin);
    }

    [Fact]
    public void PaymentsAdminPermission_ShouldBeRegisteredUnderTheFailClosedKey()
    {
        PaymentsPermission.Keys.Admin.Should().Be("payments:admin");
        PermissionRegistry.GetByKey(PaymentsPermission.Keys.Admin).Should().NotBeNull(
            "the payments:admin permission must be part of the permission registry so unknown-permission checks fail closed");
    }

    [Fact]
    public void FraudReviewsController_ShouldRequireAuthentication()
    {
        typeof(FraudReviewsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Should().NotBeEmpty("the fraud review surface must be authentication-gated at the controller level");
    }

    public static TheoryData<Type> FraudAdminRequests => new()
    {
        typeof(GetFraudReviewsQuery),
        typeof(ResolveFraudReviewCommand),
        typeof(ResolveFraudReviewsBulkCommand)
    };

    private async Task<Payment> ReviewHeldPaymentAsync(Guid tenantId, DateTime? createdAt = null, int score = 55, decimal amount = 25m)
    {
        var payment = CreatePayment(tenantId, amount);
        payment.MarkAsProcessing();
        payment.RecordFraudAssessment(FraudDecision.Review, score, "queue test review", "{}");
        if (createdAt.HasValue)
        {
            payment.CreatedAt = createdAt.Value;
        }

        _context.Set<Payment>().Add(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    private async Task<Payment> BlockHeldPaymentAsync(Guid tenantId)
    {
        var payment = CreatePayment(tenantId);
        payment.MarkAsProcessing();
        payment.RecordFraudAssessment(FraudDecision.Block, 90, "queue test block", "{}");
        _context.Set<Payment>().Add(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    private async Task<Payment> ApprovedReviewPaymentAsync(Guid tenantId)
    {
        var payment = CreatePayment(tenantId);
        payment.MarkAsProcessing();
        payment.RecordFraudAssessment(FraudDecision.Review, 55, "queue test review", "{}");
        payment.ResolveFraudReview(approved: true, Guid.NewGuid(), "approved");
        _context.Set<Payment>().Add(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    private async Task<Payment> PlainFailedPaymentAsync(Guid tenantId)
    {
        var payment = CreatePayment(tenantId);
        payment.MarkAsProcessing();
        payment.MarkAsFailed("declined", "card_declined");
        _context.Set<Payment>().Add(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    private static Payment CreatePayment(Guid tenantId, decimal amount = 25m) => Payment.Create(
        tenantId,
        amount,
        "USD",
        $"queue-{Guid.NewGuid():N}",
        provider: "stripe",
        subscriptionId: Guid.NewGuid(),
        externalCustomerId: $"cus_{Guid.NewGuid():N}",
        paymentMethodId: "pm_queue");

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
