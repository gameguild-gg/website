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
        typeof(ResolveFraudReviewCommand)
    };

    private async Task<Payment> ReviewHeldPaymentAsync(Guid tenantId, DateTime? createdAt = null)
    {
        var payment = CreatePayment(tenantId);
        payment.MarkAsProcessing();
        payment.RecordFraudAssessment(FraudDecision.Review, 55, "queue test review", "{}");
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

    private static Payment CreatePayment(Guid tenantId) => Payment.Create(
        tenantId,
        25m,
        "USD",
        $"queue-{Guid.NewGuid():N}",
        provider: "stripe",
        subscriptionId: Guid.NewGuid(),
        externalCustomerId: $"cus_{Guid.NewGuid():N}",
        paymentMethodId: "pm_queue");

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
