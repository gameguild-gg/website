using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Commands;

/// <summary>
///     Accept-dispute-issues-refund chain (issue #419): accepting a gateway dispute records the
///     refund without a second gateway call (the chargeback already moved the funds), while
///     accepting a locally created dispute executes a real gateway refund. Challenging submits
///     evidence to the gateway and moves the dispute under review.
/// </summary>
public class AcceptDisputeRefundChainTests
{
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IRevenueAuditService> _revenueAudit = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly ActorContext _actor;

    public AcceptDisputeRefundChainTests()
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
        _gateway
            .Setup(gateway => gateway.AcceptDisputeAsync(It.IsAny<GatewayDisputeActionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayDisputeActionResult(true, null, null));
        _gateway
            .Setup(gateway => gateway.ChallengeDisputeAsync(It.IsAny<GatewayDisputeChallengeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayDisputeActionResult(true, null, null));
        _gateway
            .Setup(gateway => gateway.ProcessRefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayRefundResult(true, "re_chain", 10m, null, null, SystemClock.UtcNow));
    }

    [Fact]
    public async Task AcceptGatewayDispute_ClosesAtGateway_RecordsRefundWithoutSecondGatewayRefund()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: "dp_123");
        await using var db = fixture.Db;

        var handler = CreateAcceptHandler(db);
        var result = await handler.Handle(new AcceptDisputeCommand(fixture.Dispute.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Refund.Should().NotBeNull();
        result.Refund!.Reason.Should().Be("chargeback_accepted");
        result.Refund.Status.Should().Be(RefundStatus.Completed);
        result.Refund.DisputeId.Should().Be(fixture.Dispute.Id);

        // The chargeback already returned the funds: the refund is recorded for accounting,
        // never re-executed at the gateway.
        _gateway.Verify(
            gateway => gateway.AcceptDisputeAsync(
                It.Is<GatewayDisputeActionRequest>(request =>
                    request.ExternalDisputeId == "dp_123" &&
                    request.IdempotencyKey == $"dispute_accept_{fixture.Dispute.Id}"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _gateway.Verify(
            gateway => gateway.ProcessRefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        fixture.Payment.RefundedAmount.Should().Be(fixture.Payment.Amount);
        fixture.Payment.Status.Should().Be(PaymentStatus.Refunded);
        fixture.Dispute.Status.Should().Be(DisputeStatus.Lost);

        // The accounting parity: refund revenue event + balancing ledger entry recorded once.
        _revenueAudit.Verify(
            service => service.RecordRevenueEventAsync(
                RevenueEventType.RefundProcessed,
                fixture.Payment.Amount,
                "USD",
                It.IsAny<RevenueSource>(),
                fixture.Payment.Id.ToString(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcceptLocalDispute_ExecutesARealGatewayRefund()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: null);
        await using var db = fixture.Db;

        var handler = CreateAcceptHandler(db);
        var result = await handler.Handle(new AcceptDisputeCommand(fixture.Dispute.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Refund!.Status.Should().Be(RefundStatus.Completed);
        result.Refund.GatewayRefundId.Should().Be("re_chain");
        _gateway.Verify(
            gateway => gateway.AcceptDisputeAsync(It.IsAny<GatewayDisputeActionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _gateway.Verify(
            gateway => gateway.ProcessRefundAsync(
                It.Is<GatewayRefundRequest>(request => request.Amount == fixture.Payment.Amount),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcceptDispute_WhenGatewayRefuses_FailsClosedWithoutRefund()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: "dp_denied");
        await using var db = fixture.Db;
        _gateway
            .Setup(gateway => gateway.AcceptDisputeAsync(It.IsAny<GatewayDisputeActionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayDisputeActionResult(false, "dispute_already_closed", "already closed"));

        var handler = CreateAcceptHandler(db);
        var result = await handler.Handle(new AcceptDisputeCommand(fixture.Dispute.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("refused to accept");
        fixture.Payment.RefundedAmount.Should().Be(0m);
        (await db.Set<Refund>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ChallengeDispute_SubmitsEvidenceToGateway_AndMovesToReview()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: "dp_123");
        await using var db = fixture.Db;

        var handler = new ChallengeDisputeCommandHandler(
            db,
            new PaymentRepository(db, NullLogger<PaymentRepository>.Instance),
            _gateway.Object,
            _revenueAudit.Object,
            _actorAccessor.Object,
            NullLogger<ChallengeDisputeCommandHandler>.Instance);
        var result = await handler.Handle(
            new ChallengeDisputeCommand(fixture.Dispute.Id, "service was delivered"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        fixture.Dispute.Status.Should().Be(DisputeStatus.UnderReview);
        _gateway.Verify(
            gateway => gateway.ChallengeDisputeAsync(
                It.Is<GatewayDisputeChallengeRequest>(request =>
                    request.ExternalDisputeId == "dp_123" &&
                    request.EvidenceSummary == "service was delivered"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ChallengeLocalDispute_WithoutGatewayIdentity_FailsClosed()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: null);
        await using var db = fixture.Db;

        var handler = new ChallengeDisputeCommandHandler(
            db,
            new PaymentRepository(db, NullLogger<PaymentRepository>.Instance),
            _gateway.Object,
            _revenueAudit.Object,
            _actorAccessor.Object,
            NullLogger<ChallengeDisputeCommandHandler>.Instance);
        var result = await handler.Handle(new ChallengeDisputeCommand(fixture.Dispute.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("gateway webhook");
    }

    private AcceptDisputeCommandHandler CreateAcceptHandler(RefundsTestDbContext db)
    {
        var service = new RefundProcessingService(
            new RefundRepository(db, NullLogger<RefundRepository>.Instance),
            new PaymentRepository(db, NullLogger<PaymentRepository>.Instance),
            _gateway.Object,
            _revenueAudit.Object,
            _actorAccessor.Object,
            Options.Create(new RefundApprovalOptions()),
            Mock.Of<GameGuild.Notifications.Services.INotificationService>(),
            NullLogger<RefundProcessingService>.Instance);

        return new AcceptDisputeCommandHandler(
            db,
            new PaymentRepository(db, NullLogger<PaymentRepository>.Instance),
            _gateway.Object,
            service,
            _revenueAudit.Object,
            _actorAccessor.Object,
            NullLogger<AcceptDisputeCommandHandler>.Instance);
    }

    private async Task<DisputeFixture> NewDatabaseWithDisputeAsync(string? gatewayDisputeId)
    {
        var options = new DbContextOptionsBuilder<RefundsTestDbContext>()
            .UseInMemoryDatabase($"disputes-{Guid.NewGuid():N}")
            .Options;
        var db = new RefundsTestDbContext(options);

        var payment = Payment.Create(
            _actor.TenantId!.Value,
            75m,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            subscriptionId: Guid.NewGuid());
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_disputed", "ch_disputed");
        db.Add(payment);

        var dispute = new PaymentDispute
        {
            PaymentId = payment.Id,
            UserId = Guid.NewGuid(),
            Type = DisputeType.Fraudulent,
            Amount = 75m,
            Reason = "fraudulent",
            Status = DisputeStatus.Submitted
        };
        if (gatewayDisputeId is not null)
        {
            dispute.BindGatewayIdentity("stripe", gatewayDisputeId, SystemClock.UtcNow.AddDays(7));
        }

        db.Add(dispute);
        await db.SaveChangesAsync();

        return new DisputeFixture(db, payment, dispute);
    }

    private sealed record DisputeFixture(RefundsTestDbContext Db, Payment Payment, PaymentDispute Dispute);

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
