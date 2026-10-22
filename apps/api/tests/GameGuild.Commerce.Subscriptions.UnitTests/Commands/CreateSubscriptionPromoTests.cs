using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Commands;

/// <summary>
///     Issue #418: promo application during subscription creation. Codes are quoted through
///     the unified promo engine (ISubscriptionPromoCoordinator), the subscription is created
///     on the discounted amount, trial-extension and free-month grants are consumed, and one
///     usage row per applied code is recorded against the created subscription.
/// </summary>
public class CreateSubscriptionPromoTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepository = new();
    private readonly Mock<ISubscriptionPlanRepository> _mockPlanRepository = new();
    private readonly Mock<GameGuild.Commerce.ISubscriptionPromoCoordinator> _mockPromoCoordinator = new();
    private readonly CreateSubscriptionCommandHandler _handler;

    public CreateSubscriptionPromoTests()
    {
        _mockPlanRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new SubscriptionPlan("Pro", "pro", 2999, "USD") { Type = SubscriptionPlanType.Paid });

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => Captured = s)
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        _handler = new CreateSubscriptionCommandHandler(
            _mockRepository.Object,
            _mockPlanRepository.Object,
            NullLogger<CreateSubscriptionCommandHandler>.Instance,
            _mockPromoCoordinator.Object);
    }

    private Subscription? Captured { get; set; }

    private CreateSubscriptionCommand CommandWithPromo(decimal amount = 100m, IReadOnlyList<string>? codes = null)
    {
        return new CreateSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            PlanId: Guid.NewGuid(),
            CreatedByUserId: Guid.NewGuid(),
            BillingCycle: BillingCycle.Monthly,
            Amount: amount,
            FulfilledOrderId: Guid.NewGuid(),
            StartDate: SystemClock.UtcNow,
            TrialDays: null,
            PromoCodes: codes ?? new List<string> { "SAVE20" },
            PromoCodeIdempotencyKey: "checkout-1",
            ProductId: null);
    }

    private static GameGuild.Commerce.SubscriptionPromoQuoteResult Quote(
        decimal original,
        decimal final,
        IReadOnlyList<GameGuild.Commerce.SubscriptionPromoCodeApplication> applied,
        IReadOnlyList<GameGuild.Commerce.SubscriptionPromoRejectedCode>? rejected = null)
    {
        return new GameGuild.Commerce.SubscriptionPromoQuoteResult(
            original, final, original - final, applied, rejected ?? [], "USD");
    }

    [Fact]
    public async Task Handle_WithApplyingPromo_CreatesSubscriptionOnDiscountedAmount()
    {
        var command = CommandWithPromo(amount: 100m);
        _mockPromoCoordinator
            .Setup(c => c.QuoteForSubscriptionCreationAsync(
                It.Is<GameGuild.Commerce.SubscriptionPromoQuoteRequest>(r =>
                    r.PlanId == command.PlanId && r.Amount == 100m && r.UserId == command.CreatedByUserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Quote(100m, 80m, new[]
            {
                new GameGuild.Commerce.SubscriptionPromoCodeApplication(Guid.NewGuid(), "SAVE20", 20m, "PercentageOff")
            }));
        _mockPromoCoordinator
            .Setup(c => c.RecordSubscriptionUsageAsync(
                It.IsAny<GameGuild.Commerce.SubscriptionPromoUsageRecordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        await _handler.Handle(command, CancellationToken.None);

        Captured.Should().NotBeNull();
        Captured!.Amount.Amount.Should().Be(80m);

        _mockPromoCoordinator.Verify(
            c => c.RecordSubscriptionUsageAsync(
                It.Is<GameGuild.Commerce.SubscriptionPromoUsageRecordRequest>(r =>
                    r.SubscriptionId == Captured.Id &&
                    r.UserId == command.CreatedByUserId &&
                    r.IdempotencyKey == "checkout-1" &&
                    r.AppliedCodes.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithTrialExtensionPromo_ConsumesTrialExtensionDays()
    {
        var command = CommandWithPromo(amount: 100m);
        _mockPromoCoordinator
            .Setup(c => c.QuoteForSubscriptionCreationAsync(It.IsAny<GameGuild.Commerce.SubscriptionPromoQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Quote(100m, 100m, new[]
            {
                new GameGuild.Commerce.SubscriptionPromoCodeApplication(Guid.NewGuid(), "EXTRATRIAL", 0m, "FreeTrialExtension", TrialExtensionDays: 14)
            }));
        _mockPromoCoordinator
            .Setup(c => c.RecordSubscriptionUsageAsync(It.IsAny<GameGuild.Commerce.SubscriptionPromoUsageRecordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        await _handler.Handle(command, CancellationToken.None);

        Captured.Should().NotBeNull();
        // The extension granted a trial the subscription did not have.
        Captured!.Status.Should().Be(SubscriptionStatus.Trialing);
        Captured.TrialEndDate.Should().NotBeNull();
        Captured.DomainEvents.OfType<TrialExtendedEvent>().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithFreeMonthsPromo_SkipsBillingPeriods()
    {
        var command = CommandWithPromo(amount: 100m);
        _mockPromoCoordinator
            .Setup(c => c.QuoteForSubscriptionCreationAsync(It.IsAny<GameGuild.Commerce.SubscriptionPromoQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Quote(100m, 0m, new[]
            {
                new GameGuild.Commerce.SubscriptionPromoCodeApplication(Guid.NewGuid(), "FREEMONTHS", 100m, "FreeMonths", FreeMonthsGranted: 2)
            }));
        _mockPromoCoordinator
            .Setup(c => c.RecordSubscriptionUsageAsync(It.IsAny<GameGuild.Commerce.SubscriptionPromoUsageRecordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        await _handler.Handle(command, CancellationToken.None);

        Captured.Should().NotBeNull();
        Captured!.DomainEvents.OfType<BillingPeriodsSkippedEvent>()
            .Should().ContainSingle().Which.SkippedPeriods.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenEveryCodeIsRejected_FailsClosedWithReasons()
    {
        var command = CommandWithPromo();
        _mockPromoCoordinator
            .Setup(c => c.QuoteForSubscriptionCreationAsync(It.IsAny<GameGuild.Commerce.SubscriptionPromoQuoteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Quote(100m, 100m, [], new[]
            {
                new GameGuild.Commerce.SubscriptionPromoRejectedCode("SAVE20", "Promo code is not valid for this subscription plan")
            }));

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not valid for this subscription plan*");
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutPromoCodes_NeverCallsTheCoordinator()
    {
        var command = new CreateSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            PlanId: Guid.NewGuid(),
            CreatedByUserId: Guid.NewGuid(),
            BillingCycle: BillingCycle.Monthly,
            Amount: 29.99m);

        await _handler.Handle(command, CancellationToken.None);

        _mockPromoCoordinator.Verify(c => c.QuoteForSubscriptionCreationAsync(
            It.IsAny<GameGuild.Commerce.SubscriptionPromoQuoteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Captured!.Amount.Amount.Should().Be(29.99m);
    }

    [Fact]
    public async Task Handle_WithPromoCodesButNoCoordinator_Throws()
    {
        var handler = new CreateSubscriptionCommandHandler(
            _mockRepository.Object,
            _mockPlanRepository.Object,
            Mock.Of<ILogger<CreateSubscriptionCommandHandler>>());
        var command = CommandWithPromo();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no promo coordinator*");
    }
}
