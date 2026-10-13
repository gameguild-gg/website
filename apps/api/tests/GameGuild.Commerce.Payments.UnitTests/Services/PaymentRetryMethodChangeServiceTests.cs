using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
///     Pause/resume of automatic retries around a payment-method change (issue #415):
///     pausing clears the schedule only on retriable failed payments, resuming re-arms them
///     pointed at the confirmed method, and closed recoveries are never resurrected.
/// </summary>
public class PaymentRetryMethodChangeServiceTests
{
    private readonly Mock<IRevenueAuditService> _auditService = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    public PaymentRetryMethodChangeServiceTests()
    {
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        });
        _auditService
            .Setup(service => service.RecordAuditTrailAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task PauseForMethodChange_ClearsTheScheduleOnFailedRetryingPayments()
    {
        await using var context = await CreateContextAsync();
        var (tenantId, subscriptionId, scheduled, _) = await SeedAsync(context);

        var paused = await CreateService(context).PauseForMethodChangeAsync(tenantId, subscriptionId);

        paused.Should().Be(1);
        scheduled.Status.Should().Be(PaymentStatus.Failed);
        scheduled.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public async Task PauseForMethodChange_IgnoresExhaustedNonRetriableAndOtherSubscriptions()
    {
        await using var context = await CreateContextAsync();
        var (tenantId, subscriptionId, scheduled, exhausted) = await SeedAsync(context);
        var nonRetriable = Payment.Create(
            tenantId,
            30m,
            "USD",
            $"idem-method-change-{Guid.NewGuid():N}",
            subscriptionId: subscriptionId,
            paymentMethodId: "pm_old_card");
        nonRetriable.MarkAsProcessing();
        nonRetriable.MarkAsFailed("card expired", "expired_card", nextRetryAt: null, isRetriable: false);
        context.Set<Payment>().Add(nonRetriable);
        var otherSubscription = FailedPayment(tenantId, Guid.NewGuid());
        context.Set<Payment>().Add(otherSubscription);
        await context.SaveChangesAsync();

        var paused = await CreateService(context).PauseForMethodChangeAsync(tenantId, subscriptionId);

        paused.Should().Be(1); // Only the retriable payment of this subscription.
        exhausted.MaxRetriesReached.Should().BeTrue();
        otherSubscription.NextRetryAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResumeAfterMethodChange_ReArmsPausedPaymentsAgainstTheNewMethod()
    {
        await using var context = await CreateContextAsync();
        var (tenantId, subscriptionId, scheduled, _) = await SeedAsync(context);
        var service = CreateService(context);
        await service.PauseForMethodChangeAsync(tenantId, subscriptionId);
        var before = SystemClock.UtcNow;

        var resumed = await service.ResumeAfterMethodChangeAsync(tenantId, subscriptionId, "pm_new_card");

        resumed.Should().Be(1);
        scheduled.NextRetryAt.Should().BeOnOrAfter(before);
        scheduled.PaymentMethodId.Should().Be("pm_new_card");
        scheduled.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task ResumeAfterMethodChange_DoesNotTouchStillScheduledPayments()
    {
        await using var context = await CreateContextAsync();
        var (tenantId, subscriptionId, scheduled, _) = await SeedAsync(context);
        var originalSchedule = scheduled.NextRetryAt;

        var resumed = await CreateService(context).ResumeAfterMethodChangeAsync(tenantId, subscriptionId, "pm_new_card");

        resumed.Should().Be(0);
        scheduled.NextRetryAt.Should().Be(originalSchedule);
        scheduled.PaymentMethodId.Should().Be("pm_old_card");
    }

    [Fact]
    public async Task PauseThenResume_WritesAuditTrailEntriesForBothTransitions()
    {
        await using var context = await CreateContextAsync();
        var (tenantId, subscriptionId, _, _) = await SeedAsync(context);
        var service = CreateService(context);

        await service.PauseForMethodChangeAsync(tenantId, subscriptionId);
        await service.ResumeAfterMethodChangeAsync(tenantId, subscriptionId, "pm_new_card");

        _auditService.Verify(
            service => service.RecordAuditTrailAsync(
                "Payment",
                It.IsAny<Guid>(),
                "RetryPaused",
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _auditService.Verify(
            service => service.RecordAuditTrailAsync(
                "Payment",
                It.IsAny<Guid>(),
                "RetryResumed",
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResumeAfterMethodChange_WithoutActor_FailsClosed()
    {
        await using var context = await CreateContextAsync();
        var (tenantId, subscriptionId, _, _) = await SeedAsync(context);
        var service = CreateService(context);
        await service.PauseForMethodChangeAsync(tenantId, subscriptionId);
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(ActorContext.Anonymous);

        var act = () => service.ResumeAfterMethodChangeAsync(tenantId, subscriptionId, "pm_new_card");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private PaymentRetryMethodChangeService CreateService(PaymentsPersistenceTestDbContext context) =>
        new(
            context,
            _auditService.Object,
            _actorAccessor.Object,
            NullLogger<PaymentRetryMethodChangeService>.Instance);

    private static async Task<PaymentsPersistenceTestDbContext> CreateContextAsync()
    {
        var context = new PaymentsPersistenceTestDbContext(
            new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
                .UseInMemoryDatabase($"method-change-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    /// <returns>(tenantId, subscriptionId, scheduledPayment, exhaustedPayment)</returns>
    private static async Task<(Guid TenantId, Guid SubscriptionId, Payment Scheduled, Payment Exhausted)> SeedAsync(
        PaymentsPersistenceTestDbContext context)
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();

        var scheduled = FailedPayment(tenantId, subscriptionId);
        var exhausted = FailedPayment(tenantId, subscriptionId, maxRetries: 1);
        exhausted.PrepareForRetry(); // RetryCount 1 == MaxRetries 1
        exhausted.MarkAsProcessing();
        exhausted.MarkAsFailed("declined again", maxRetries: 1);
        exhausted.NextRetryAt.Should().BeNull();

        context.Set<Payment>().AddRange(scheduled, exhausted);
        await context.SaveChangesAsync();
        return (tenantId, subscriptionId, scheduled, exhausted);
    }

    private static Payment FailedPayment(Guid tenantId, Guid subscriptionId, int maxRetries = 4)
    {
        var payment = Payment.Create(
            tenantId,
            25m,
            "USD",
            $"idem-method-change-{Guid.NewGuid():N}",
            subscriptionId: subscriptionId,
            paymentMethodId: "pm_old_card",
            maxRetries: maxRetries);
        payment.MarkAsProcessing();
        payment.MarkAsFailed("insufficient funds", "insufficient_funds");
        return payment;
    }
}
