using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Commands;

/// <summary>
///     Refund approval workflow (issue #419): threshold auto-approve vs manual approval,
///     the approve/reject decisions and idempotent replay — exercised against an in-memory
///     refund ledger through the real <see cref="RefundProcessingService"/>.
/// </summary>
public class RefundApprovalWorkflowTests
{
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IRevenueAuditService> _revenueAudit = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<GameGuild.Notifications.Services.INotificationService> _notifications = new();
    private readonly RefundApprovalOptions _options = new();
    private readonly ActorContext _actor;

    public RefundApprovalWorkflowTests()
    {
        _actor = AuthenticatedActor();
        _gateway.SetupGet(gateway => gateway.ProviderId).Returns("stripe");
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(_actor);
        _revenueAudit
            .Setup(service => service.RecordRevenueEventAsync(
                It.IsAny<RevenueEventType>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<RevenueSource>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueEvent());
        _revenueAudit
            .Setup(service => service.CreateLedgerEntryAsync(
                It.IsAny<LedgerEntryType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialLedgerEntry());
        _revenueAudit
            .Setup(service => service.RecordAuditTrailAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task RequestRefund_BelowThreshold_AutoApprovesAndExecutesImmediately()
    {
        _options.ManualApprovalThresholdAmount = 100m;
        var payment = SucceededPayment(50m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        SetupGatewayRefundSuccess();
        var service = CreateService(db);

        var outcome = await service.RequestRefundAsync(
            new RefundRequestInput(payment.Id, 49m, "goodwill"), CancellationToken.None);

        outcome.Success.Should().BeTrue();
        outcome.RequiresApproval.Should().BeFalse();
        outcome.Refund!.Status.Should().Be(RefundStatus.Completed);
        outcome.Refund.RequiresApproval.Should().BeFalse();

        var stored = await db.Set<Refund>().SingleAsync();
        stored.Status.Should().Be(RefundStatus.Completed);
        stored.GatewayRefundId.Should().Be("re_123");
        payment.RefundedAmount.Should().Be(49m);
        payment.Status.Should().Be(PaymentStatus.PartiallyRefunded);

        _gateway.Verify(
            gateway => gateway.ProcessRefundAsync(
                It.Is<GatewayRefundRequest>(request => request.Amount == 49m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestRefund_AtOrAboveThreshold_QueuesForManualApprovalWithoutGatewayCall()
    {
        _options.ManualApprovalThresholdAmount = 100m;
        var payment = SucceededPayment(150m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var outcome = await service.RequestRefundAsync(
            new RefundRequestInput(payment.Id, 150m, "service_not_provided"), CancellationToken.None);

        outcome.Success.Should().BeTrue();
        outcome.RequiresApproval.Should().BeTrue();
        outcome.Refund!.Status.Should().Be(RefundStatus.Pending);
        outcome.Refund.RequiresApproval.Should().BeTrue();

        payment.RefundedAmount.Should().Be(0m);
        _gateway.Verify(
            gateway => gateway.ProcessRefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestRefund_NoThresholdConfigured_AutoApprovesEverything()
    {
        _options.ManualApprovalThresholdAmount = null;
        var payment = SucceededPayment(10_000m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        SetupGatewayRefundSuccess();
        var service = CreateService(db);

        var outcome = await service.RequestRefundAsync(
            new RefundRequestInput(payment.Id, null, "duplicate"), CancellationToken.None);

        outcome.Success.Should().BeTrue();
        outcome.RequiresApproval.Should().BeFalse();
        outcome.Refund!.Status.Should().Be(RefundStatus.Completed);
        outcome.Refund.Amount.Should().Be(10_000m); // null amount = full remaining refund
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task RequestRefund_WithClientKey_ReplaysTheRecordedRefundWithoutDoubleRefunding()
    {
        var payment = SucceededPayment(100m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        SetupGatewayRefundSuccess();
        var service = CreateService(db);
        var input = new RefundRequestInput(payment.Id, 30m, "duplicate", ClientIdempotencyKey: "order-42-refund");

        var first = await service.RequestRefundAsync(input, CancellationToken.None);
        var second = await service.RequestRefundAsync(input, CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        second.Refund!.Id.Should().Be(first.Refund!.Id);
        (await db.Set<Refund>().CountAsync()).Should().Be(1);
        payment.RefundedAmount.Should().Be(30m);

        _gateway.Verify(
            gateway => gateway.ProcessRefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestRefund_ExceedingRemainingAmount_FailsClosed()
    {
        var payment = SucceededPayment(100m);
        payment.ProcessRefund(80m, "re_prior", "earlier refund");
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var outcome = await service.RequestRefundAsync(
            new RefundRequestInput(payment.Id, 30m, "goodwill"), CancellationToken.None);

        outcome.Success.Should().BeFalse();
        outcome.FailureReason.Should().Contain("exceeds maximum refundable");
    }

    [Fact]
    public async Task ApproveRefundCommand_PendingRefund_ExecutesThroughTheGateway()
    {
        _options.ManualApprovalThresholdAmount = 100m;
        // The refund must belong to the approver's tenant for the fail-closed tenant guard
        // in ApproveRefundCommandHandler to pass: seed the payment with the actor's tenant.
        var payment = SucceededPayment(150m, _actor.TenantId);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var queued = await service.RequestRefundAsync(
            new RefundRequestInput(payment.Id, 150m, "service_not_provided"), CancellationToken.None);
        SetupGatewayRefundSuccess();

        var handler = new ApproveRefundCommandHandler(
            new RefundRepository(db, NullLogger<RefundRepository>.Instance),
            service,
            _revenueAudit.Object,
            _actorAccessor.Object,
            NullLogger<ApproveRefundCommandHandler>.Instance);
        var result = await handler.Handle(new ApproveRefundCommand(queued.Refund!.Id, "approved"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Refund!.Status.Should().Be(RefundStatus.Completed);
        result.Refund.DecidedByUserId.Should().Be(_actor.SubjectIdAsGuid);
        result.Refund.DecisionNotes.Should().Be("approved");
        payment.RefundedAmount.Should().Be(150m);
        payment.Status.Should().Be(PaymentStatus.Refunded);

        _gateway.Verify(
            gateway => gateway.ProcessRefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveRefundCommand_RefundFromAnotherTenant_FailsClosed()
    {
        // Queue the refund for manual approval so the arrange phase never touches the
        // (unstubbed) gateway: the test exercises the handler's tenant guard, not execution.
        _options.ManualApprovalThresholdAmount = 100m;
        var payment = SucceededPayment(150m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var queued = await service.RequestRefundAsync(
            new RefundRequestInput(payment.Id, 150m, "service_not_provided", RequestedByUserId: _actor.SubjectIdAsGuid),
            CancellationToken.None);

        var outsider = AuthenticatedActor(); // different tenant
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(outsider);
        var handler = new ApproveRefundCommandHandler(
            new RefundRepository(db, NullLogger<RefundRepository>.Instance),
            service,
            _revenueAudit.Object,
            _actorAccessor.Object,
            NullLogger<ApproveRefundCommandHandler>.Instance);

        var act = () => handler.Handle(new ApproveRefundCommand(queued.Refund!.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RejectRefundCommand_MarksRejectedWithoutMovingMoney()
    {
        _options.ManualApprovalThresholdAmount = 100m;
        // The refund must belong to the reviewer's tenant for the fail-closed tenant guard
        // in RejectRefundCommandHandler to pass: seed the payment with the actor's tenant.
        var payment = SucceededPayment(150m, _actor.TenantId);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var queued = await service.RequestRefundAsync(
            new RefundRequestInput(payment.Id, 150m, "service_not_provided"), CancellationToken.None);

        var handler = new RejectRefundCommandHandler(
            new RefundRepository(db, NullLogger<RefundRepository>.Instance),
            _revenueAudit.Object,
            _actorAccessor.Object,
            NullLogger<RejectRefundCommandHandler>.Instance);
        var result = await handler.Handle(new RejectRefundCommand(queued.Refund!.Id, "not eligible"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Refund!.Status.Should().Be(RefundStatus.Rejected);
        result.Refund.DecisionNotes.Should().Be("not eligible");
        payment.RefundedAmount.Should().Be(0m);
        _gateway.Verify(
            gateway => gateway.ProcessRefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestRefund_QueuedForApproval_NotifiesConfiguredApprovers()
    {
        _options.ManualApprovalThresholdAmount = 10m;
        _options.ApproverNotificationEmails = ["approver@example.com"];
        _notifications
            .Setup(service => service.SendAsync(
                It.IsAny<Guid?>(), It.IsAny<GameGuild.Notifications.NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GameGuild.Notifications.NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<GameGuild.Notifications.NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(GameGuild.Notifications.Notification.Create(null, GameGuild.Notifications.NotificationType.Billing, GameGuild.Notifications.NotificationChannel.Email, "Refund approval required", "body")));
        var payment = SucceededPayment(50m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var outcome = await service.RequestRefundAsync(
            new RefundRequestInput(payment.Id, 50m, "goodwill"), CancellationToken.None);

        outcome.RequiresApproval.Should().BeTrue();
        _notifications.Verify(
            service => service.SendAsync(
                null,
                GameGuild.Notifications.NotificationType.Billing,
                It.IsAny<string>(),
                It.IsAny<string>(),
                GameGuild.Notifications.NotificationChannel.Email,
                payment.TenantId,
                It.IsAny<string?>(),
                GameGuild.Notifications.NotificationPriority.High,
                It.Is<Guid?>(id => id == outcome.Refund!.Id),
                nameof(Refund),
                It.IsAny<string?>(),
                "approver@example.com",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("approver@example.com")]
    [InlineData("审批+refund@payments.example")]
    public async Task ApproverNotificationFailure_ProtectsDiagnostics_AndKeepsApprovalWorkflow(string firstRecipient)
    {
        const string secondRecipient = "backup-approver@example.com";
        const string privateMarker = "synthetic-private-notification-content";
        var failure = new InvalidOperationException($"Delivery to {firstRecipient} failed\r\n{privateMarker}");
        _options.ManualApprovalThresholdAmount = 10m;
        _options.ApproverNotificationEmails = [firstRecipient, secondRecipient];
        _notifications
            .SetupSequence(service => service.SendAsync(
                It.IsAny<Guid?>(), It.IsAny<GameGuild.Notifications.NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GameGuild.Notifications.NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<GameGuild.Notifications.NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure)
            .ReturnsAsync(Result.Success(GameGuild.Notifications.Notification.Create(
                null, GameGuild.Notifications.NotificationType.Billing, GameGuild.Notifications.NotificationChannel.Email,
                "Refund approval required", "body")));
        var logger = new Mock<ILogger<RefundProcessingService>>();
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var payment = SucceededPayment(50m);
        await using var db = NewDatabase();
        db.Add(payment);
        await db.SaveChangesAsync();

        var outcome = await CreateService(db, logger.Object).RequestRefundAsync(
            new RefundRequestInput(payment.Id, 50m, "goodwill"), CancellationToken.None);

        outcome.Success.Should().BeTrue();
        outcome.RequiresApproval.Should().BeTrue();
        outcome.Refund!.Status.Should().Be(RefundStatus.Pending);
        (await db.Set<Refund>().SingleAsync()).Status.Should().Be(RefundStatus.Pending);
        payment.RefundedAmount.Should().Be(0m);
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        _gateway.Verify(gateway => gateway.ProcessRefundAsync(
            It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        foreach (var recipient in new[] { firstRecipient, secondRecipient })
        {
            _notifications.Verify(service => service.SendAsync(
                It.IsAny<Guid?>(), GameGuild.Notifications.NotificationType.Billing, It.IsAny<string>(), It.IsAny<string>(),
                GameGuild.Notifications.NotificationChannel.Email, It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<GameGuild.Notifications.NotificationPriority>(), It.IsAny<Guid?>(), nameof(Refund),
                It.IsAny<string?>(), recipient, It.IsAny<CancellationToken>()), Times.Once);
        }

        var warning = logger.Invocations.Where(call => call.Method.Name == nameof(ILogger.Log)
            && (LogLevel)call.Arguments[0] == LogLevel.Warning).Should().ContainSingle().Which;
        var message = warning.Arguments[2].ToString()!;
        message.Should().NotContain(firstRecipient).And.NotContain(privateMarker);
        message.Should().NotMatchRegex("[\\x00-\\x1F\\x7F-\\x9F\\u2028\\u2029]");
        warning.Arguments[3].Should().BeNull("the original exception can contain the private notification payload");
        var properties = ((IEnumerable<KeyValuePair<string, object?>>)warning.Arguments[2])
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        properties.Should().NotContainKey("Email");
        properties.Values.Should().NotContain(LogRedaction.MaskEmail(firstRecipient));
        properties["ErrorType"].Should().Be(typeof(InvalidOperationException).FullName);
        properties["RefundId"].Should().Be(outcome.Refund.Id);
    }

    [Fact]
    public async Task GetPendingRefundApprovalsQuery_ReturnsOnlyQueuedRefundsForTheTenant()
    {
        _options.ManualApprovalThresholdAmount = 10m;
        var tenantId = Guid.NewGuid();
        var payment = SucceededPayment(50m, tenantId);
        await using var db = NewDatabase();
        db.Add(payment);
        var otherPayment = SucceededPayment(50m);
        db.Add(otherPayment);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await service.RequestRefundAsync(new RefundRequestInput(payment.Id, 50m, "goodwill"), CancellationToken.None);
        await service.RequestRefundAsync(new RefundRequestInput(otherPayment.Id, 50m, "goodwill"), CancellationToken.None);

        var handler = new GetPendingRefundApprovalsQueryHandler(db);
        var page = await handler.Handle(new GetPendingRefundApprovalsQuery(tenantId, Page: 1, PageSize: 20), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(item => item.TenantId == tenantId);
    }

    private IRefundProcessingService CreateService(
        RefundsTestDbContext db,
        ILogger<RefundProcessingService>? logger = null) => new RefundProcessingService(
        new RefundRepository((IApplicationDbContext)db, NullLogger<RefundRepository>.Instance),
        new PaymentRepository((IApplicationDbContext)db, NullLogger<PaymentRepository>.Instance),
        _gateway.Object,
        _revenueAudit.Object,
        _actorAccessor.Object,
        Options.Create(_options),
        _notifications.Object,
        logger ?? NullLogger<RefundProcessingService>.Instance);

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

    private static Payment SucceededPayment(decimal amount = 25m, Guid? tenantId = null)
    {
        var payment = Payment.Create(
            tenantId ?? Guid.NewGuid(),
            amount,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            subscriptionId: Guid.NewGuid());
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

internal sealed class RefundsTestDbContext(
    Microsoft.EntityFrameworkCore.DbContextOptions<RefundsTestDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options), IApplicationDbContext
{
    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>();
        modelBuilder.ApplyConfiguration(new RefundConfiguration());
        modelBuilder.Entity<PaymentDispute>(builder =>
        {
            builder.Property(dispute => dispute.Status).HasConversion<string>().IsRequired();
            builder.Property(dispute => dispute.Type).HasConversion<string>().IsRequired();
            builder.Property(dispute => dispute.Resolution).HasConversion<string>();
        });
        modelBuilder.Entity<DisputeEvidence>();
        base.OnModelCreating(modelBuilder);
    }
}
