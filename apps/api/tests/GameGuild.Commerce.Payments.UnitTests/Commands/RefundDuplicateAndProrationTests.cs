using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Commands;

/// <summary>
///     Refund safeguards and extensions (issue #419): the duplicate-open-refund guard, prorated
///     refund computation, the refund confirmation email, the opt-in wallet credit for partial
///     refunds and the subscription adjustment after a full refund.
/// </summary>
public class RefundDuplicateAndProrationTests
{
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IRevenueAuditService> _revenueAudit = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IPlanProrationCalculator> _prorationCalculator = new();
    private readonly Mock<IPaymentSubscriptionSyncService> _subscriptionSync = new();
    private readonly Mock<IWalletService> _walletService = new();
    private readonly Mock<IPaymentMethodRepository> _paymentMethodRepository = new();
    private readonly RefundApprovalOptions _options = new();

    public RefundDuplicateAndProrationTests()
    {
        _gateway.SetupGet(gateway => gateway.ProviderId).Returns("stripe");
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(AuthenticatedActor());
        _revenueAudit
            .Setup(service => service.RecordRevenueEventAsync(
                It.IsAny<RevenueEventType>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<RevenueSource>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueEvent { Id = Guid.NewGuid(), EventType = RevenueEventType.RefundProcessed });
        _notifications
            .Setup(service => service.SendAsync(
                It.IsAny<Guid?>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationChannel>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Notification.Create(
                Guid.NewGuid(),
                NotificationType.RefundCompleted,
                NotificationChannel.Email,
                "Refund processed",
                "Your refund was processed.",
                null)));
    }

    [Fact]
    public async Task RequestRefundAsync_RejectsASecondRefundWhileOneIsOpen()
    {
        var payment = SucceededPayment(50m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        SetupGatewayRefundSuccess();

        var first = await service.RequestRefundAsync(new RefundRequestInput(payment.Id, 20m, "goodwill"), CancellationToken.None);
        first.Success.Should().BeTrue();
        first.Refund!.Status.Should().Be(RefundStatus.Completed);

        // Force a second, still-open refund onto the payment (the request path itself guards
        // against this; a manual/legacy row must be guarded against as well).
        var open = Refund.Create(
            payment.TenantId,
            payment.Id,
            10m,
            "USD",
            "duplicate",
            null,
            Guid.NewGuid(),
            requiresApproval: true,
            $"idem-open-{Guid.NewGuid():N}");
        db.Add(open);
        await db.SaveChangesAsync();

        var second = await service.RequestRefundAsync(new RefundRequestInput(payment.Id, 10m, "duplicate"), CancellationToken.None);

        second.Success.Should().BeFalse();
        second.FailureReason.Should().Contain($"already has refund {open.Id}");
        second.FailureReason.Should().Contain(RefundStatus.Pending.ToString());
    }

    [Fact]
    public async Task RequestRefundAsync_AllowsANewRefundAfterThePreviousOneCompleted()
    {
        var payment = SucceededPayment(50m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        SetupGatewayRefundSuccess();

        var first = await service.RequestRefundAsync(new RefundRequestInput(payment.Id, 20m, "goodwill"), CancellationToken.None);
        first.Success.Should().BeTrue();
        var second = await service.RequestRefundAsync(new RefundRequestInput(payment.Id, 30m, "duplicate"), CancellationToken.None);

        second.Success.Should().BeTrue();
        second.Refund!.Status.Should().Be(RefundStatus.Completed);
        payment.RefundedAmount.Should().Be(50m);
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task RequestRefundAsync_ProratedKind_ComputesTheAmountFromTheBillingPeriod()
    {
        var payment = SucceededPayment(100m, subscriptionId: Guid.NewGuid());
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        SetupGatewayRefundSuccess();
        var period = new SubscriptionBillingPeriodContext(
            SystemClock.UtcNow.AddDays(-10),
            SystemClock.UtcNow.AddDays(20),
            OwnerUserId: Guid.NewGuid());
        _subscriptionSync
            .Setup(sync => sync.GetBillingPeriodAsync(payment.Id, payment.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(period);
        _prorationCalculator
            .Setup(calculator => calculator.CalculateProrationAsync(It.IsAny<PlanProrationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanProrationResult(
                CreditForUnused: 66.67m,
                ChargeForNew: 0m,
                NetAdjustment: -66.67m,
                EffectiveDate: SystemClock.UtcNow,
                TotalDays: 30,
                DaysRemaining: 20));

        var outcome = await service.RequestRefundAsync(
            new RefundRequestInput(payment.Id, Amount: null, Reason: "subscription_cancellation", Kind: RefundKind.Prorated),
            CancellationToken.None);

        outcome.Success.Should().BeTrue();
        outcome.Refund!.Kind.Should().Be(RefundKind.Prorated);
        outcome.Refund.Amount.Should().Be(66.67m);
        outcome.Refund.ReasonDetails.Should().Contain("20/30 days unused");
        _prorationCalculator.Verify(calculator => calculator.CalculateProrationAsync(
            It.Is<PlanProrationInput>(input =>
                input.PeriodStart == period.PeriodStart
                && input.PeriodEnd == period.PeriodEnd
                && input.OldAmount.Amount == 100m
                && input.NewAmount.Amount == 100m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestRefundAsync_ProratedKind_FailsClosedWithoutASubscriptionPeriod()
    {
        var payment = SucceededPayment(100m); // no subscription link
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        _subscriptionSync
            .Setup(sync => sync.GetBillingPeriodAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionBillingPeriodContext?)null);

        var outcome = await service.RequestRefundAsync(
            new RefundRequestInput(payment.Id, Amount: null, Reason: "subscription_cancellation", Kind: RefundKind.Prorated),
            CancellationToken.None);

        outcome.Success.Should().BeFalse();
        outcome.FailureReason.Should().Contain("Prorated refunds require a subscription-linked payment");
        (await db.Set<Refund>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RequestRefundAsync_ExplicitAmountOnAFullRequest_IsRecordedAsPartial()
    {
        var payment = SucceededPayment(50m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        SetupGatewayRefundSuccess();

        var outcome = await service.RequestRefundAsync(new RefundRequestInput(payment.Id, 20m, "goodwill"), CancellationToken.None);

        outcome.Refund!.Kind.Should().Be(RefundKind.Partial);
        outcome.Refund.Amount.Should().Be(20m);
    }

    [Fact]
    public async Task CompletedRefund_SendsTheConfirmationEmailToTheRequester()
    {
        var payment = SucceededPayment(50m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        SetupGatewayRefundSuccess();

        var outcome = await service.RequestRefundAsync(new RefundRequestInput(payment.Id, 20m, "goodwill"), CancellationToken.None);

        outcome.Success.Should().BeTrue();
        _notifications.Verify(service => service.SendAsync(
            outcome.Refund!.RequestedByUserId,
            NotificationType.RefundCompleted,
            It.IsAny<string>(),
            It.Is<string>(message => message.Contains("20") && message.Contains("USD")),
            NotificationChannel.Email,
            payment.TenantId,
            It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(),
            outcome.Refund.Id,
            nameof(Refund),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PartialRefund_CreditsTheWalletOnlyWhenTheOptionIsEnabled()
    {
        var ownerId = Guid.NewGuid();
        var payment = SucceededPayment(50m, paymentMethodId: "pm_wallet_1");
        _paymentMethodRepository
            .Setup(repository => repository.GetByGatewayIdAsync(payment.TenantId, "stripe", "pm_wallet_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentMethod(payment.TenantId, ownerId, "stripe", "pm_wallet_1", PaymentMethodType.Card, isDefault: false));
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();

        var disabledService = CreateService(db, workflowOptions: new RefundWorkflowOptions());
        SetupGatewayRefundSuccess();
        var disabledOutcome = await disabledService.RequestRefundAsync(
            new RefundRequestInput(payment.Id, 20m, "goodwill"), CancellationToken.None);
        disabledOutcome.Success.Should().BeTrue();
        _walletService.Verify(wallet => wallet.AddFundsAsync(
            It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var secondPayment = SucceededPayment(50m, paymentMethodId: "pm_wallet_2");
        _paymentMethodRepository
            .Setup(repository => repository.GetByGatewayIdAsync(secondPayment.TenantId, "stripe", "pm_wallet_2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentMethod(secondPayment.TenantId, ownerId, "stripe", "pm_wallet_2", PaymentMethodType.Card, isDefault: false));
        db.Add(secondPayment);
        await db.SaveChangesAsync();

        var walletCreditingService = CreateService(
            db,
            workflowOptions: new RefundWorkflowOptions { CreditWalletOnPartialRefund = true });
        var outcome = await walletCreditingService.RequestRefundAsync(
            new RefundRequestInput(secondPayment.Id, 20m, "goodwill"), CancellationToken.None);

        outcome.Success.Should().BeTrue();
        _walletService.Verify(wallet => wallet.AddFundsAsync(
            ownerId,
            20m,
            It.Is<string>(description => description.Contains(secondPayment.Id.ToString())),
            outcome.Refund!.Id.ToString(),
            "USD",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FullRefund_AdjustsTheSubscriptionThroughTheSyncSeam()
    {
        var payment = SucceededPayment(50m, subscriptionId: Guid.NewGuid());
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        SetupGatewayRefundSuccess();

        var outcome = await service.RequestRefundAsync(new RefundRequestInput(payment.Id, null, "duplicate"), CancellationToken.None);

        outcome.Success.Should().BeTrue();
        _subscriptionSync.Verify(sync => sync.AdjustForRefundAsync(
            payment.Id,
            payment.SubscriptionId!.Value,
            50m,
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestRefundAsync_FailsClosed_WhenAConcurrentRequestWinsTheSingleOpenRefundGuard()
    {
        var payment = SucceededPayment(50m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();

        var repository = new Mock<IRefundRepository>();
        repository
            .Setup(repo => repo.GetByPaymentAndIdempotencyKeyAsync(payment.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Refund?)null);
        repository
            .Setup(repo => repo.GetOpenRefundForPaymentAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Refund?)null);
        repository
            .Setup(repo => repo.AddAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OpenRefundAlreadyExistsException(payment.Id, new InvalidOperationException("unique violation")));

        var service = new RefundProcessingService(
            repository.Object,
            new PaymentRepository((IApplicationDbContext)db, NullLogger<PaymentRepository>.Instance),
            _gateway.Object,
            _revenueAudit.Object,
            _actorAccessor.Object,
            Options.Create(_options),
            _notifications.Object,
            NullLogger<RefundProcessingService>.Instance,
            _prorationCalculator.Object,
            _subscriptionSync.Object,
            _walletService.Object,
            _paymentMethodRepository.Object);

        var outcome = await service.RequestRefundAsync(new RefundRequestInput(payment.Id, null, "duplicate"), CancellationToken.None);

        outcome.Success.Should().BeFalse();
        outcome.FailureReason.Should().Contain("open refund");
        _gateway.Verify(gateway => gateway.ProcessRefundAsync(
            It.IsAny<GatewayRefundRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private IRefundProcessingService CreateService(
        RefundsTestDbContext db,
        RefundWorkflowOptions? workflowOptions = null) => new RefundProcessingService(
        new RefundRepository((IApplicationDbContext)db, NullLogger<RefundRepository>.Instance),
        new PaymentRepository((IApplicationDbContext)db, NullLogger<PaymentRepository>.Instance),
        _gateway.Object,
        _revenueAudit.Object,
        _actorAccessor.Object,
        Options.Create(_options),
        _notifications.Object,
        NullLogger<RefundProcessingService>.Instance,
        _prorationCalculator.Object,
        _subscriptionSync.Object,
        _walletService.Object,
        _paymentMethodRepository.Object,
        Options.Create(workflowOptions ?? new RefundWorkflowOptions()));

    private RefundsTestDbContext NewDatabase()
    {
        var options = new DbContextOptionsBuilder<RefundsTestDbContext>()
            .UseInMemoryDatabase($"refunds-{Guid.NewGuid():N}")
            .Options;
        return new RefundsTestDbContext(options);
    }

    private void SetupGatewayRefundSuccess()
    {
        _gateway
            .Setup(gateway => gateway.ProcessRefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayRefundResult(
                Success: true,
                RefundId: "re_123",
                AmountRefunded: 10m,
                ErrorCode: null,
                ErrorMessage: null,
                ProcessedAt: SystemClock.UtcNow));
    }

    private static Payment SucceededPayment(decimal amount = 25m, Guid? subscriptionId = null, string? paymentMethodId = null)
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            amount,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            subscriptionId: subscriptionId,
            paymentMethodId: paymentMethodId);
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_123", "ch_123");
        return payment;
    }

    private static ActorContext AuthenticatedActor() => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = Guid.NewGuid().ToString(),
        TenantId = Guid.NewGuid(),
        Roles = new HashSet<string>(),
        Permissions = new HashSet<string>(),
        TypedAttributes = ActorAttributes.Empty,
        AuthScheme = "Test",
        IsAuthenticated = true
    };
}
