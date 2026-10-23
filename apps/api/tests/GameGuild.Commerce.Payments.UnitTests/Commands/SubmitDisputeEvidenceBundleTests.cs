using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Commands;

/// <summary>
///     Evidence-bundle submission (issue #419 "submit evidence bundle to gateway" / "track
///     submission status"): the handler assembles every attached evidence row into one bundle,
///     delivers it through the gateway's challenge surface, stamps the submission on the dispute
///     and each evidence row, moves a submitted dispute under review and fails closed when the
///     dispute has no gateway identity, no evidence or is already resolved.
/// </summary>
public class SubmitDisputeEvidenceBundleTests
{
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IRevenueAuditService> _revenueAudit = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly ActorContext _actor;

    public SubmitDisputeEvidenceBundleTests()
    {
        _actor = AuthenticatedActor();
        _gateway.SetupGet(gateway => gateway.ProviderId).Returns("stripe");
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(_actor);
        _revenueAudit
            .Setup(service => service.RecordAuditTrailAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gateway
            .Setup(gateway => gateway.ChallengeDisputeAsync(It.IsAny<GatewayDisputeChallengeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayDisputeActionResult(true, null, null));
    }

    [Fact]
    public async Task SubmitBundle_DeliversAssembledEvidence_AndTracksSubmissionStatus()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: "dp_9001", evidenceCount: 3);
        await using var db = fixture.Db;

        var handler = CreateHandler(db);
        var result = await handler.Handle(
            new SubmitDisputeEvidenceCommand(fixture.Dispute.Id, "merchant evidence follows"),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        // The bundle carries the summary plus one entry per evidence row.
        _gateway.Verify(
            gateway => gateway.ChallengeDisputeAsync(
                It.Is<GatewayDisputeChallengeRequest>(request =>
                    request.ExternalDisputeId == "dp_9001" &&
                    request.IdempotencyKey == $"dispute_evidence_{fixture.Dispute.Id}" &&
                    request.EvidenceSummary!.StartsWith("merchant evidence follows", StringComparison.Ordinal) &&
                    request.EvidenceSummary.Contains("[Invoice] Invoice evidence #1") &&
                    request.EvidenceSummary.Contains("[Receipt] Receipt evidence #2") &&
                    request.EvidenceSummary.Contains("[Communication] Communication evidence #3") &&
                    request.EvidenceSummary.Contains("(file: invoice.pdf, https://files/invoice.pdf)")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Submission tracking: the dispute and every evidence row are stamped.
        fixture.Dispute.EvidenceSubmittedAt.Should().NotBeNull();
        (await db.Set<DisputeEvidence>()
                .Where(evidence => evidence.DisputeId == fixture.Dispute.Id)
                .CountAsync(evidence => evidence.GatewaySubmittedAt != null))
            .Should().Be(3);

        // A dispute in Submitted moves under review once its evidence reached the gateway.
        fixture.Dispute.Status.Should().Be(DisputeStatus.UnderReview);

        _revenueAudit.Verify(
            service => service.RecordAuditTrailAsync(
                nameof(PaymentDispute),
                fixture.Dispute.Id,
                "DisputeEvidenceSubmitted",
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
    public async Task SubmitBundle_ReSubmission_KeepsTheFirstEvidenceSubmissionTimestamp()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: "dp_9002", evidenceCount: 1);
        await using var db = fixture.Db;

        var handler = CreateHandler(db);
        await handler.Handle(new SubmitDisputeEvidenceCommand(fixture.Dispute.Id), CancellationToken.None);

        var firstStamp = (await db.Set<DisputeEvidence>()
            .SingleAsync(evidence => evidence.DisputeId == fixture.Dispute.Id)).GatewaySubmittedAt;
        firstStamp.Should().NotBeNull();

        // A later re-submission refreshes the dispute-level timestamp but never rewrites the
        // per-evidence history.
        await handler.Handle(new SubmitDisputeEvidenceCommand(fixture.Dispute.Id), CancellationToken.None);
        var evidenceRow = await db.Set<DisputeEvidence>()
            .SingleAsync(evidence => evidence.DisputeId == fixture.Dispute.Id);
        evidenceRow.GatewaySubmittedAt.Should().Be(firstStamp);
        _gateway.Verify(
            gateway => gateway.ChallengeDisputeAsync(It.IsAny<GatewayDisputeChallengeRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SubmitBundle_WithoutEvidence_FailsClosed()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: "dp_9003", evidenceCount: 0);
        await using var db = fixture.Db;

        var handler = CreateHandler(db);
        var result = await handler.Handle(new SubmitDisputeEvidenceCommand(fixture.Dispute.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("no evidence");
        _gateway.Verify(
            gateway => gateway.ChallengeDisputeAsync(It.IsAny<GatewayDisputeChallengeRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Dispute.EvidenceSubmittedAt.Should().BeNull();
    }

    [Fact]
    public async Task SubmitBundle_WithoutGatewayIdentity_FailsClosed()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: null, evidenceCount: 2);
        await using var db = fixture.Db;

        var handler = CreateHandler(db);
        var result = await handler.Handle(new SubmitDisputeEvidenceCommand(fixture.Dispute.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("gateway webhook");
    }

    [Fact]
    public async Task SubmitBundle_WhenGatewayRefuses_FailsClosedWithoutSubmissionTracking()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: "dp_9004", evidenceCount: 1);
        await using var db = fixture.Db;
        _gateway
            .Setup(gateway => gateway.ChallengeDisputeAsync(It.IsAny<GatewayDisputeChallengeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayDisputeActionResult(false, "evidence_too_large", "bundle exceeds the size limit"));

        var handler = CreateHandler(db);
        var result = await handler.Handle(new SubmitDisputeEvidenceCommand(fixture.Dispute.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("refused the evidence bundle");
        fixture.Dispute.EvidenceSubmittedAt.Should().BeNull();
        (await db.Set<DisputeEvidence>().CountAsync(evidence => evidence.GatewaySubmittedAt != null))
            .Should().Be(0);
        fixture.Dispute.Status.Should().Be(DisputeStatus.Submitted);
    }

    [Fact]
    public async Task SubmitBundle_ForResolvedDispute_FailsClosed()
    {
        var fixture = await NewDatabaseWithDisputeAsync(gatewayDisputeId: "dp_9005", evidenceCount: 1);
        await using var db = fixture.Db;
        fixture.Dispute.MarkAsLost("cardholder won", Guid.NewGuid());
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new SubmitDisputeEvidenceCommand(fixture.Dispute.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("already resolved");
        _gateway.Verify(
            gateway => gateway.ChallengeDisputeAsync(It.IsAny<GatewayDisputeChallengeRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private SubmitDisputeEvidenceCommandHandler CreateHandler(RefundsTestDbContext db) => new(
        db,
        new PaymentRepository(db, NullLogger<PaymentRepository>.Instance),
        _gateway.Object,
        _revenueAudit.Object,
        _actorAccessor.Object,
        NullLogger<SubmitDisputeEvidenceCommandHandler>.Instance);

    private async Task<DisputeFixture> NewDatabaseWithDisputeAsync(string? gatewayDisputeId, int evidenceCount)
    {
        var options = new DbContextOptionsBuilder<RefundsTestDbContext>()
            .UseInMemoryDatabase($"evidence-bundle-{Guid.NewGuid():N}")
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

        var evidenceTypes = new[] { EvidenceType.Invoice, EvidenceType.Receipt, EvidenceType.Communication };
        for (var index = 1; index <= evidenceCount; index++)
        {
            db.Add(new DisputeEvidence
            {
                DisputeId = dispute.Id,
                EvidenceType = evidenceTypes[(index - 1) % evidenceTypes.Length],
                Title = $"{evidenceTypes[(index - 1) % evidenceTypes.Length]} evidence #{index}",
                Description = "documents the merchant position",
                FileName = index == 1 ? "invoice.pdf" : null,
                FileUrl = index == 1 ? "https://files/invoice.pdf" : null,
                SubmittedBy = Guid.NewGuid(),
                IsFromMerchant = true,
                SubmittedAt = SystemClock.UtcNow.AddMinutes(index)
            });
        }

        await db.SaveChangesAsync();

        // Re-read through the same context so assertions observe the tracked entities.
        var tracked = await db.Set<PaymentDispute>()
            .Include(d => d.Evidence)
            .FirstAsync(d => d.Id == dispute.Id);
        return new DisputeFixture(db, payment, tracked);
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
