using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Commerce.Billing.UnitTests.Support;
using GameGuild.Compliance.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

/// <summary>
/// Issue #400: the pricing-decision ledger writer must persist every valid decision
/// transactionally, replay idempotently on a repeated key, fail closed on invalid
/// decisions, and publish a Compliance.Audit event for each recorded row.
/// </summary>
public sealed class PricingDecisionLedgerWriterTests
{
    private readonly Mock<IAuditService> _auditService = new();

    private PricingDecisionLedgerWriter CreateWriter(PricingLedgerSqliteTestSupport.PricingLedgerTestContext context)
        => new(context, _auditService.Object, NullLogger<PricingDecisionLedgerWriter>.Instance);

    private static PricingDecisionRecord ValidDecision(string idempotencyKey, decimal? finalAmount = null)
    {
        var breakdown = PricingDecisionBreakdown.Empty("USD", 2)
            with
        {
            BaseUnitPrice = 10m,
            BaseAmount = 20m,
            PromoApplications = [new PromoApplicationBreakdown("WELCOME", null, "PercentageOff", 10m, 2m, 1)],
            PromoDiscountTotal = 2m,
            FinalAmount = finalAmount ?? 18m
        };

        return new PricingDecisionRecord(
            DecisionType: PricingDecisionType.CheckoutLineItem,
            TenantId: Guid.NewGuid(),
            ActorUserId: Guid.NewGuid(),
            ActorIpAddress: "203.0.113.10",
            Order: Guid.NewGuid(),
            OrderLineItem: Guid.NewGuid(),
            PaymentId: null,
            Subscription: null,
            BillingCycleNumber: null,
            Product: Guid.NewGuid(),
            Breakdown: breakdown,
            AppliedPricingRuleId: null,
            AppliedStackingRuleName: "default-stack",
            IdempotencyKey: idempotencyKey);
    }

    [Fact]
    public async Task RecordAsync_PersistsEntryWithBreakdownAndCorrelation()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var writer = CreateWriter(context);
        var decision = ValidDecision("checkout:1");

        var (status, entry) = await writer.RecordAsync(decision, CancellationToken.None);

        status.Should().Be(PricingDecisionRecordingStatus.Recorded);
        entry.Should().NotBeNull();

        var persisted = await context.Set<PricingDecisionLedgerEntry>()
            .SingleAsync(row => row.IdempotencyKey == "checkout:1");
        persisted.DecisionType.Should().Be(PricingDecisionType.CheckoutLineItem);
        persisted.TenantId.Should().Be(decision.TenantId);
        persisted.OrderId.Should().Be(decision.Order);
        persisted.OrderLineItemId.Should().Be(decision.OrderLineItem);
        persisted.ProductId.Should().Be(decision.Product);
        persisted.Currency.Should().Be("USD");
        persisted.BaseAmount.Should().Be(20m);
        persisted.PromoDiscountTotal.Should().Be(2m);
        persisted.FinalAmount.Should().Be(18m);
        persisted.ActorUserId.Should().Be(decision.ActorUserId);
        persisted.ActorIpAddress.Should().Be("203.0.113.10");
        persisted.PricingEngineVersion.Should().Be(PricingDecisionLedgerDefaults.EngineVersion);
        persisted.DriftStatus.Should().Be(PricingDecisionDriftStatus.NotValidated);
        persisted.AppliedStackingRuleName.Should().Be("default-stack");

        // The delimiter-joined code column powers exact-code filtering.
        persisted.AppliedPromoCodes.Should().Be(",WELCOME,");

        // The breakdown document round-trips and recomputes to the stored final.
        var breakdown = PricingDecisionBreakdown.FromJson(persisted.BreakdownJson);
        breakdown.Should().NotBeNull();
        breakdown!.PromoApplications.Should().ContainSingle().Which.Code.Should().Be("WELCOME");
        breakdown.RecomputeFinalAmount().Should().Be(18m);
    }

    [Fact]
    public async Task RecordAsync_ReplaysIdempotentlyOnRepeatedKey()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var writer = CreateWriter(context);
        var first = await writer.RecordAsync(ValidDecision("replay-key"), CancellationToken.None);

        var second = await writer.RecordAsync(ValidDecision("replay-key", finalAmount: 5m), CancellationToken.None);

        second.Status.Should().Be(PricingDecisionRecordingStatus.Replay);
        second.Entry!.Id.Should().Be(first.Entry!.Id, "a replayed key returns the original row untouched");
        (await context.Set<PricingDecisionLedgerEntry>().CountAsync()).Should().Be(1);
        second.Entry.FinalAmount.Should().Be(18m, "the replay must not overwrite the original decision");
    }

    [Fact]
    public async Task RecordAsync_FailsClosedOnInvalidDecisions()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var writer = CreateWriter(context);

        var unknownType = await writer.RecordAsync(ValidDecision("k1") with { DecisionType = "Nonsense" }, CancellationToken.None);
        unknownType.Status.Should().Be(PricingDecisionRecordingStatus.Rejected);

        var negativeFinal = await writer.RecordAsync(ValidDecision("k2", finalAmount: -1m), CancellationToken.None);
        negativeFinal.Status.Should().Be(PricingDecisionRecordingStatus.Rejected);

        var missingKey = await writer.RecordAsync(ValidDecision("  ") with { IdempotencyKey = " " }, CancellationToken.None);
        missingKey.Status.Should().Be(PricingDecisionRecordingStatus.Rejected);

        var zeroQuantity = await writer.RecordAsync(
            ValidDecision("k3") with
            {
                Breakdown = PricingDecisionBreakdown.Empty("USD") with { Quantity = 0, FinalAmount = 1m }
            },
            CancellationToken.None);
        zeroQuantity.Status.Should().Be(PricingDecisionRecordingStatus.Rejected);

        (await context.Set<PricingDecisionLedgerEntry>().CountAsync()).Should().Be(0,
            "rejected decisions must never append a ledger row");
    }

    [Fact]
    public async Task RecordAsync_PublishesComplianceAuditEvent()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var writer = CreateWriter(context);
        var decision = ValidDecision("audit-key");

        await writer.RecordAsync(decision, CancellationToken.None);

        _auditService.Verify(
            mock => mock.LogAdminActionAsync(
                decision.ActorUserId!.Value,
                "PricingDecision.Recorded",
                It.Is<string>(description => description.Contains("CheckoutLineItem")),
                It.IsAny<object?>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordAsync_SurvivesAuditPublishingFailure()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        _auditService
            .Setup(mock => mock.LogAdminActionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>()))
            .ThrowsAsync(new InvalidOperationException("audit transport down"));
        var writer = CreateWriter(context);

        var (status, _) = await writer.RecordAsync(ValidDecision("audit-fail-key"), CancellationToken.None);

        status.Should().Be(PricingDecisionRecordingStatus.Recorded,
            "audit publishing is best-effort; the durable ledger row is the source of truth");
        (await context.Set<PricingDecisionLedgerEntry>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RecordAsync_RecordsSystemActorRenewalQuote()
    {
        await using var context = PricingLedgerSqliteTestSupport.CreateContext();
        var writer = CreateWriter(context);
        var subscriptionId = Guid.NewGuid();

        var breakdown = PricingDecisionBreakdown.Empty("EUR", 1)
            with
        {
            BaseUnitPrice = 15m,
            BaseAmount = 15m,
            FinalAmount = 15m
        };

        var (status, entry) = await writer.RecordAsync(
            new PricingDecisionRecord(
                DecisionType: PricingDecisionType.RenewalQuote,
                TenantId: Guid.NewGuid(),
                ActorUserId: null,
                ActorIpAddress: null,
                Order: null,
                OrderLineItem: null,
                PaymentId: null,
                Subscription: subscriptionId,
                BillingCycleNumber: 4,
                Product: null,
                Breakdown: breakdown,
                IdempotencyKey: $"renewal:{subscriptionId}:4:20261011:pricing"),
            CancellationToken.None);

        status.Should().Be(PricingDecisionRecordingStatus.Recorded);
        entry!.ActorUserId.Should().BeNull();
        entry.SubscriptionId.Should().Be(subscriptionId);
        entry.BillingCycleNumber.Should().Be(4);
        entry.Currency.Should().Be("EUR");
    }
}
