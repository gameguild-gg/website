using FluentAssertions;
using GameGuild.Commerce.Products;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Tests.Commerce.Products.Unit.Commands;

/// <summary>
/// The redemption endpoint wiring: <see cref="ApplyPromoCodesCommandHandler"/> must record
/// one usage row per applied code (idempotent, actual discount) and fail closed by dropping
/// codes whose usage cannot be recorded.
/// </summary>
public class ApplyPromoCodesRecordingTests
{
    private readonly Mock<IPromoCodeRepository> _promoRepo = new();
    private readonly Mock<IPromoCodeUsageRecorder> _recorder = new();
    private readonly Mock<IPromoCodeAuditLogger> _audit = new();
    private readonly Mock<GameGuild.Commerce.IPricingDecisionLedgerWriter> _pricingLedger = new();

    private ApplyPromoCodesCommandHandler CreateHandler(Mock<IPricingEngineService>? engine = null)
    {
        engine ??= new Mock<IPricingEngineService>();
        return new ApplyPromoCodesCommandHandler(
            engine.Object,
            _promoRepo.Object,
            _recorder.Object,
            _audit.Object,
            _pricingLedger.Object,
            NullLogger<ApplyPromoCodesCommandHandler>.Instance);
    }

    private static PromoCode Promo(string code, PromoCodeType type = PromoCodeType.PercentageOff, decimal pct = 20m) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        Type = type,
        DiscountPercentage = pct == 0 ? null : pct,
        Currency = "USD",
        IsActive = true
    };

    private static PromoCodeApplicationResult EngineResult(params (string Code, decimal Discount)[] applied)
    {
        return new PromoCodeApplicationResult(
            100m,
            100m - applied.Sum(a => a.Discount),
            applied.Sum(a => a.Discount),
            applied.Select(a => new AppliedPromoCode(a.Code, a.Discount, null)).ToList(),
            new List<RejectedPromoCode>());
    }

    [Fact]
    public async Task Handle_RecordsUsagePerAppliedCode_WithActualDiscountAndKey()
    {
        var promo = Promo("SAVE20");
        var engine = new Mock<IPricingEngineService>();
        engine.Setup(e => e.ApplyPromoCodesAsync(100m, It.IsAny<IEnumerable<string>>(), null, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngineResult(("SAVE20", 20m)));
        _promoRepo.Setup(r => r.GetByCodeAsync("SAVE20", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);
        var recordedUsage = new PromoCodeUse { Id = Guid.NewGuid(), PromoCodeId = promo.Id };
        _recorder.Setup(r => r.RecordAsync(promo, It.IsAny<Guid>(), 20m, "USD", It.IsAny<string?>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoUsageRecordingStatus.Recorded, recordedUsage));

        var command = new ApplyPromoCodesCommand(100m, ["SAVE20"], UserId: Guid.NewGuid(), IdempotencyKey: "checkout-1");
        var result = await CreateHandler(engine).Handle(command, CancellationToken.None);

        result.UsageRecorded.Should().BeTrue();
        result.AppliedCodes.Should().ContainSingle(a => a.Code == "SAVE20" && a.DiscountAmount == 20m);
        _recorder.Verify(r => r.RecordAsync(
            promo,
            command.UserId!.Value,
            20m,
            "USD",
            "checkout-1:code:SAVE20",
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(a => a.LogRedemptionAsync(command.UserId!.Value, "SAVE20", 20m, "USD", recordedUsage.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReplayedKey_KeepsCodeApplied_WithoutSecondCount()
    {
        var promo = Promo("SAVE20");
        var engine = new Mock<IPricingEngineService>();
        engine.Setup(e => e.ApplyPromoCodesAsync(100m, It.IsAny<IEnumerable<string>>(), null, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngineResult(("SAVE20", 20m)));
        _promoRepo.Setup(r => r.GetByCodeAsync("SAVE20", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);
        _recorder.Setup(r => r.RecordAsync(It.IsAny<PromoCode>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoUsageRecordingStatus.Replay, new PromoCodeUse()));

        var command = new ApplyPromoCodesCommand(100m, ["SAVE20"], UserId: Guid.NewGuid(), IdempotencyKey: "checkout-1");
        var result = await CreateHandler(engine).Handle(command, CancellationToken.None);

        result.UsageRecorded.Should().BeTrue();
        result.AppliedCodes.Should().ContainSingle(a => a.Code == "SAVE20");
        result.RejectedCodes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LimitReached_FailsClosed_AndDropsTheCodeFromTheResult()
    {
        var promo = Promo("SAVED-OUT");
        var engine = new Mock<IPricingEngineService>();
        engine.Setup(e => e.ApplyPromoCodesAsync(100m, It.IsAny<IEnumerable<string>>(), null, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngineResult(("SAVED-OUT", 30m)));
        _promoRepo.Setup(r => r.GetByCodeAsync("SAVED-OUT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);
        _recorder.Setup(r => r.RecordAsync(It.IsAny<PromoCode>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoUsageRecordingStatus.RejectedTotalLimit, (PromoCodeUse?)null));

        var command = new ApplyPromoCodesCommand(100m, ["SAVED-OUT"], UserId: Guid.NewGuid(), IdempotencyKey: "checkout-2");
        var result = await CreateHandler(engine).Handle(command, CancellationToken.None);

        result.UsageRecorded.Should().BeFalse();
        result.AppliedCodes.Should().BeEmpty("a code whose usage cannot be recorded must not discount");
        result.TotalDiscount.Should().Be(0m);
        result.FinalAmount.Should().Be(100m);
        result.RejectedCodes.Should().ContainSingle(r => r.Code == "SAVED-OUT" && r.Reason.Contains("maximum uses", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_PerUserLimitReached_FailsClosed()
    {
        var promo = Promo("ONCEONLY");
        var engine = new Mock<IPricingEngineService>();
        engine.Setup(e => e.ApplyPromoCodesAsync(100m, It.IsAny<IEnumerable<string>>(), null, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngineResult(("ONCEONLY", 10m)));
        _promoRepo.Setup(r => r.GetByCodeAsync("ONCEONLY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);
        _recorder.Setup(r => r.RecordAsync(It.IsAny<PromoCode>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoUsageRecordingStatus.RejectedPerUserLimit, (PromoCodeUse?)null));

        var command = new ApplyPromoCodesCommand(100m, ["ONCEONLY"], UserId: Guid.NewGuid(), IdempotencyKey: "checkout-3");
        var result = await CreateHandler(engine).Handle(command, CancellationToken.None);

        result.AppliedCodes.Should().BeEmpty();
        result.RejectedCodes.Should().ContainSingle(r => r.Code == "ONCEONLY" && r.Reason.Contains("maximum number of times", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_WithoutUserId_ComputesOnly_DoesNotRecord()
    {
        var engine = new Mock<IPricingEngineService>();
        engine.Setup(e => e.ApplyPromoCodesAsync(100m, It.IsAny<IEnumerable<string>>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngineResult(("ANON", 5m)));

        var command = new ApplyPromoCodesCommand(100m, ["ANON"]);
        var result = await CreateHandler(engine).Handle(command, CancellationToken.None);

        result.UsageRecorded.Should().BeFalse();
        result.AppliedCodes.Should().ContainSingle(a => a.Code == "ANON");
        _recorder.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_GeneratedKeyUsed_WhenClientProvidesNone()
    {
        var promo = Promo("NOKEY");
        var engine = new Mock<IPricingEngineService>();
        engine.Setup(e => e.ApplyPromoCodesAsync(100m, It.IsAny<IEnumerable<string>>(), null, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngineResult(("NOKEY", 5m)));
        _promoRepo.Setup(r => r.GetByCodeAsync("NOKEY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);
        _recorder.Setup(r => r.RecordAsync(It.IsAny<PromoCode>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoUsageRecordingStatus.Recorded, new PromoCodeUse()));

        var command = new ApplyPromoCodesCommand(100m, ["NOKEY"], UserId: Guid.NewGuid(), OrderId: Guid.NewGuid());
        var result = await CreateHandler(engine).Handle(command, CancellationToken.None);

        result.UsageRecorded.Should().BeTrue();
        _recorder.Verify(r => r.RecordAsync(
            promo,
            command.UserId!.Value,
            5m,
            "USD",
            It.Is<string>(key => key.StartsWith("apply:", StringComparison.Ordinal) && key.EndsWith(":code:NOKEY")),
            command.OrderId,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RecordsPricingDecisionLedgerEntry_WhenKeyOrOrderCorrelationExists()
    {
        var promo = Promo("SAVE20");
        var engine = new Mock<IPricingEngineService>();
        engine.Setup(e => e.ApplyPromoCodesAsync(100m, It.IsAny<IEnumerable<string>>(), null, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngineResult(("SAVE20", 20m)));
        _promoRepo.Setup(r => r.GetByCodeAsync("SAVE20", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);
        _recorder.Setup(r => r.RecordAsync(It.IsAny<PromoCode>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoUsageRecordingStatus.Recorded, new PromoCodeUse()));

        var command = new ApplyPromoCodesCommand(100m, ["SAVE20"], UserId: Guid.NewGuid(), IdempotencyKey: "checkout-9", OrderId: Guid.NewGuid());
        await CreateHandler(engine).Handle(command, CancellationToken.None);

        _pricingLedger.Verify(writer => writer.RecordAsync(
            It.Is<GameGuild.Commerce.PricingDecisionRecord>(decision =>
                decision.DecisionType == GameGuild.Commerce.PricingDecisionType.OrderPromoApply &&
                decision.Order == command.OrderId &&
                decision.ActorUserId == command.UserId &&
                decision.IdempotencyKey == "checkout-9:pricing" &&
                decision.Breakdown.BaseAmount == 100m &&
                decision.Breakdown.PromoApplications.Count == 1 &&
                decision.Breakdown.PromoApplications[0].Code == "SAVE20" &&
                decision.Breakdown.PromoApplications[0].Amount == 20m &&
                decision.Breakdown.PromoDiscountTotal == 20m &&
                decision.Breakdown.FinalAmount == 80m),
            It.IsAny<CancellationToken>()), Times.Once,
            "an order-level promo application that produces a final price must append a ledger row (issue #400)");
    }

    [Fact]
    public async Task Handle_SkipsLedgerRecording_WhenNoStableCorrelationKeyExists()
    {
        var engine = new Mock<IPricingEngineService>();
        engine.Setup(e => e.ApplyPromoCodesAsync(100m, It.IsAny<IEnumerable<string>>(), null, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EngineResult(("ANON", 5m)));

        var command = new ApplyPromoCodesCommand(100m, ["ANON"]);
        await CreateHandler(engine).Handle(command, CancellationToken.None);

        _pricingLedger.Verify(writer => writer.RecordAsync(It.IsAny<GameGuild.Commerce.PricingDecisionRecord>(), It.IsAny<CancellationToken>()), Times.Never,
            "anonymous preview calls without a key or order linkage never write the ledger");
    }
}
