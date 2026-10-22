using FluentAssertions;
using GameGuild.Commerce.Products;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Tests.Commerce.Products.Unit.Services;

/// <summary>
/// Issue #418: the coordinator bridges the promo engine into subscription creation
/// (plan-scoped quote) and invoice materialization (uninvoiced usage lookup + stamping).
/// </summary>
public class SubscriptionPromoCoordinatorTests
{
    private readonly Mock<IPricingEngineService> _engine = new();
    private readonly Mock<IPromoCodeRepository> _promoRepo = new();
    private readonly Mock<IPromoCodeUsageRecorder> _recorder = new();

    private SubscriptionPromoCoordinator CreateCoordinator()
        => new(_engine.Object, _promoRepo.Object, _recorder.Object, NullLogger<SubscriptionPromoCoordinator>.Instance);

    private static PromoCode Promo(string code, PromoCodeType type = PromoCodeType.PercentageOff) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        Type = type,
        DiscountPercentage = type == PromoCodeType.PercentageOff ? 20m : null,
        FreeTrialExtensionDays = type == PromoCodeType.FreeTrialExtension ? 10 : null,
        DurationMonths = type == PromoCodeType.FreeMonths ? 1 : null,
        Currency = "USD",
        IsActive = true
    };

    [Fact]
    public async Task Quote_PassesPlanIdToEngine_AndMapsAppliedCodes()
    {
        var planId = Guid.NewGuid();
        var promo = Promo("PLAN20");
        _engine
            .Setup(e => e.ApplyPromoCodesAsync(100m, It.IsAny<IEnumerable<string>>(), null, It.IsAny<Guid?>(), planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeApplicationResult(
                100m, 80m, 20m,
                [new AppliedPromoCode("PLAN20", 20m, 20m, PromoCodeType.PercentageOff)],
                []));
        _promoRepo
            .Setup(r => r.GetByCodeAsync("PLAN20", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);

        var quote = await CreateCoordinator().QuoteForSubscriptionCreationAsync(
            new GameGuild.Commerce.SubscriptionPromoQuoteRequest(planId, 100m, ["PLAN20"]));

        _engine.Verify(
            e => e.ApplyPromoCodesAsync(100m, It.IsAny<IEnumerable<string>>(), null, It.IsAny<Guid?>(), planId, It.IsAny<CancellationToken>()),
            Times.Once);
        quote.FinalAmount.Should().Be(80m);
        quote.Currency.Should().Be("USD");
        quote.AppliedCodes.Should().ContainSingle(a => a.PromoCodeId == promo.Id && a.DiscountAmount == 20m);
    }

    [Fact]
    public async Task Record_PassesSubscriptionIdThroughToTheRecorder()
    {
        var promo = Promo("PLAN20");
        var subscriptionId = Guid.NewGuid();
        var usageId = Guid.NewGuid();
        _promoRepo
            .Setup(r => r.GetByCodeAsync("PLAN20", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);
        _recorder
            .Setup(r => r.RecordAsync(promo, It.IsAny<Guid>(), 20m, "USD", It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoUsageRecordingStatus.Recorded, new PromoCodeUse { Id = usageId, PromoCodeId = promo.Id }));

        var recordedIds = await CreateCoordinator().RecordSubscriptionUsageAsync(
            new GameGuild.Commerce.SubscriptionPromoUsageRecordRequest(
                subscriptionId,
                Guid.NewGuid(),
                [new GameGuild.Commerce.SubscriptionPromoCodeApplication(promo.Id, "PLAN20", 20m)]));

        recordedIds.Should().ContainSingle(id => id == usageId);
        _recorder.Verify(
            r => r.RecordAsync(promo, It.IsAny<Guid>(), 20m, "USD", $"subscription:{subscriptionId:N}:promo:code:PLAN20", It.IsAny<Guid?>(), It.IsAny<Guid?>(), subscriptionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUninvoicedPromoContext_AggregatesDiscountAndUsageIds()
    {
        var subscriptionId = Guid.NewGuid();
        var promo = Promo("PLAN20");
        var usageIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _promoRepo
            .Setup(r => r.GetUninvoicedUsesForSubscriptionAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageIds.Select(id => new PromoCodeUse
            {
                Id = id,
                PromoCodeId = promo.Id,
                UserId = Guid.NewGuid(),
                DiscountApplied = 10m,
                Currency = "USD"
            }).ToList());
        _promoRepo
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([promo]);

        var context = await CreateCoordinator().GetUninvoicedPromoContextAsync(subscriptionId);

        context.Should().NotBeNull();
        context!.TotalDiscount.Should().Be(20m);
        context.UsageIds.Should().BeEquivalentTo(usageIds);
        context.Codes.Should().OnlyContain(c => c.Code == "PLAN20");
    }

    [Fact]
    public async Task MarkPromoUsesInvoiced_DelegatesToRepository()
    {
        var usageIds = new List<Guid> { Guid.NewGuid() };
        var invoiceId = Guid.NewGuid();
        _promoRepo
            .Setup(r => r.MarkUsesInvoicedAsync(usageIds, invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var stamped = await CreateCoordinator().MarkPromoUsesInvoicedAsync(usageIds, invoiceId);

        stamped.Should().Be(1);
    }
}
