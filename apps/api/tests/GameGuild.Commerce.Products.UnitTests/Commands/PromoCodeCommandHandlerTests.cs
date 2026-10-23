using FluentAssertions;
using GameGuild.Commerce.Products;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Tests.Commerce.Products.Unit.Commands;

/// <summary>
/// Unit tests for ValidatePromoCodeCommandHandler
/// </summary>
public class ValidatePromoCodeCommandHandlerTests
{
    private readonly Mock<IPricingEngineService> _mockPricingEngine;
    private readonly Mock<IPromoCodeRepository> _mockPromoRepository;
    private readonly ValidatePromoCodeCommandHandler _handler;

    public ValidatePromoCodeCommandHandlerTests()
    {
        _mockPricingEngine = new Mock<IPricingEngineService>();
        _mockPromoRepository = new Mock<IPromoCodeRepository>();
        _handler = new ValidatePromoCodeCommandHandler(
            _mockPricingEngine.Object,
            _mockPromoRepository.Object,
            NullLogger<ValidatePromoCodeCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ValidPromoCode_ReturnsValidResult()
    {
        // Arrange
        var command = new ValidatePromoCodeCommand(
            Code: "DISCOUNT20",
            OrderAmount: 100m,
            ProductId: Guid.NewGuid(),
            UserId: Guid.NewGuid()
        );

        var promoCodeId = Guid.NewGuid();
        var expectedResult = new PromoCodeValidationResult(
            IsValid: true,
            Code: "DISCOUNT20",
            ErrorMessage: null,
            DiscountAmount: 20m,
            DiscountPercentage: 20,
            PromoCodeId: promoCodeId,
            Outcome: PromoCodeValidationOutcome.Valid
        );

        _mockPricingEngine
            .Setup(p => p.ValidatePromoCodeAsync(
                command.Code,
                command.OrderAmount,
                command.ProductId,
                command.UserId,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.DiscountAmount.Should().Be(20m);

        // Issue #418: the successful attempt is persisted as the conversion denominator.
        _mockPromoRepository.Verify(
            r => r.RecordValidationAttemptAsync(
                "DISCOUNT20",
                promoCodeId,
                command.UserId,
                PromoCodeValidationOutcome.Valid,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidPromoCode_ReturnsInvalidResult_AndRecordsAttempt()
    {
        // Arrange
        var command = new ValidatePromoCodeCommand(
            Code: "INVALID",
            OrderAmount: 100m
        );

        var expectedResult = new PromoCodeValidationResult(
            IsValid: false,
            Code: "INVALID",
            ErrorMessage: "Promo code not found",
            DiscountAmount: 0m,
            DiscountPercentage: 0,
            PromoCodeId: null,
            Outcome: PromoCodeValidationOutcome.NotFound
        );

        _mockPricingEngine
            .Setup(p => p.ValidatePromoCodeAsync(
                command.Code,
                command.OrderAmount,
                command.ProductId,
                command.UserId,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Promo code not found");

        // Unknown code strings are still recorded (PromoCodeId null) so code farming
        // stays observable (issue #418).
        _mockPromoRepository.Verify(
            r => r.RecordValidationAttemptAsync(
                "INVALID",
                null,
                command.UserId,
                PromoCodeValidationOutcome.NotFound,
                "Promo code not found",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAttemptPersistenceFails_ResultIsUnaffected()
    {
        // Arrange
        var command = new ValidatePromoCodeCommand(Code: "SAVE10", OrderAmount: 100m);

        _mockPricingEngine
            .Setup(p => p.ValidatePromoCodeAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeValidationResult(true, "SAVE10", null, 10m, 10, Outcome: PromoCodeValidationOutcome.Valid));

        _mockPromoRepository
            .Setup(r => r.RecordValidationAttemptAsync(
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<PromoCodeValidationOutcome>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: tracking is best effort; the validation result still flows through.
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Func<Task> act = async () => await _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_CallsPricingEngineWithCorrectParameters()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var command = new ValidatePromoCodeCommand(
            Code: "SUMMER2024",
            OrderAmount: 150m,
            ProductId: productId,
            UserId: userId,
            PlanId: planId
        );
        var cancellationToken = new CancellationToken();

        _mockPricingEngine
            .Setup(p => p.ValidatePromoCodeAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeValidationResult(true, "SUMMER2024", null, 15m, 10));

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockPricingEngine.Verify(
            p => p.ValidatePromoCodeAsync(
                "SUMMER2024",
                150m,
                productId,
                userId,
                planId,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullOptionalParameters_PassesNullToService()
    {
        // Arrange
        var command = new ValidatePromoCodeCommand(
            Code: "BASIC10",
            OrderAmount: 50m,
            ProductId: null,
            UserId: null
        );

        _mockPricingEngine
            .Setup(p => p.ValidatePromoCodeAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                null,
                null,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeValidationResult(true, "BASIC10", null, 5m, 10));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockPricingEngine.Verify(
            p => p.ValidatePromoCodeAsync(
                "BASIC10",
                50m,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task Handle_WithVariousOrderAmounts_PassesCorrectAmount(decimal orderAmount)
    {
        // Arrange
        var command = new ValidatePromoCodeCommand(
            Code: "TEST",
            OrderAmount: orderAmount
        );

        _mockPricingEngine
            .Setup(p => p.ValidatePromoCodeAsync(
                It.IsAny<string>(),
                orderAmount,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeValidationResult(true, "TEST", null, 0m, 0));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockPricingEngine.Verify(
            p => p.ValidatePromoCodeAsync(
                "TEST",
                orderAmount,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

/// <summary>
/// Unit tests for ApplyPromoCodesCommandHandler
/// </summary>
public class ApplyPromoCodesCommandHandlerTests
{
    private readonly Mock<IPricingEngineService> _mockPricingEngine;
    private readonly Mock<IPromoCodeRepository> _mockPromoRepository = new();
    private readonly Mock<IPromoCodeUsageRecorder> _mockRecorder = new();
    private readonly ApplyPromoCodesCommandHandler _handler;

    public ApplyPromoCodesCommandHandlerTests()
    {
        _mockPricingEngine = new Mock<IPricingEngineService>();
        _handler = new ApplyPromoCodesCommandHandler(
            _mockPricingEngine.Object,
            _mockPromoRepository.Object,
            _mockRecorder.Object,
            Mock.Of<IPromoCodeAuditLogger>(),
            Mock.Of<GameGuild.Commerce.IPricingDecisionLedgerWriter>(),
            NullLogger<ApplyPromoCodesCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_SinglePromoCode_ReturnsAppliedResult()
    {
        // Arrange
        var command = new ApplyPromoCodesCommand(
            OrderAmount: 100m,
            PromoCodes: new List<string> { "SAVE10" },
            ProductId: Guid.NewGuid(),
            UserId: Guid.NewGuid()
        );

        var expectedResult = new PromoCodeApplicationResult(
            OriginalAmount: 100m,
            FinalAmount: 90m,
            TotalDiscount: 10m,
            AppliedCodes: new List<AppliedPromoCode>
            {
                new AppliedPromoCode("SAVE10", 10m, 10m)
            },
            RejectedCodes: new List<RejectedPromoCode>()
        );

        _mockPricingEngine
            .Setup(p => p.ApplyPromoCodesAsync(
                command.OrderAmount,
                command.PromoCodes,
                command.ProductId,
                command.UserId,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // The handler resolves each applied code and records its usage atomically.
        var promo = new PromoCode
        {
            Id = Guid.NewGuid(),
            Code = "SAVE10",
            Name = "SAVE10",
            Type = PromoCodeType.PercentageOff,
            DiscountPercentage = 10m,
            Currency = "USD",
            IsActive = true,
            CreatedBy = Guid.NewGuid()
        };
        _mockPromoRepository
            .Setup(r => r.GetByCodeAsync("SAVE10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(promo);
        _mockRecorder
            .Setup(r => r.RecordAsync(promo, command.UserId!.Value, 10m, "USD", It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PromoUsageRecordingStatus.Recorded, new PromoCodeUse { Id = Guid.NewGuid(), PromoCodeId = promo.Id }));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalDiscount.Should().Be(10m);
        result.FinalAmount.Should().Be(90m);
        result.UsageRecorded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MultiplePromoCodes_ReturnsCombinedResult()
    {
        // Arrange
        var command = new ApplyPromoCodesCommand(
            OrderAmount: 100m,
            PromoCodes: new List<string> { "SAVE10", "EXTRA5" }
        );

        var expectedResult = new PromoCodeApplicationResult(
            OriginalAmount: 100m,
            FinalAmount: 85m,
            TotalDiscount: 15m,
            AppliedCodes: new List<AppliedPromoCode>
            {
                new AppliedPromoCode("SAVE10", 10m, 10m),
                new AppliedPromoCode("EXTRA5", 5m, 5m)
            },
            RejectedCodes: new List<RejectedPromoCode>()
        );

        _mockPricingEngine
            .Setup(p => p.ApplyPromoCodesAsync(
                command.OrderAmount,
                command.PromoCodes,
                command.ProductId,
                command.UserId,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalDiscount.Should().Be(15m);
        result.AppliedCodes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithRejectedCodes_ReturnsPartialResult()
    {
        // Arrange
        var command = new ApplyPromoCodesCommand(
            OrderAmount: 100m,
            PromoCodes: new List<string> { "VALID10", "EXPIRED" }
        );

        var expectedResult = new PromoCodeApplicationResult(
            OriginalAmount: 100m,
            FinalAmount: 90m,
            TotalDiscount: 10m,
            AppliedCodes: new List<AppliedPromoCode>
            {
                new AppliedPromoCode("VALID10", 10m, 10m)
            },
            RejectedCodes: new List<RejectedPromoCode>
            {
                new RejectedPromoCode("EXPIRED", "Promo code has expired")
            }
        );

        _mockPricingEngine
            .Setup(p => p.ApplyPromoCodesAsync(
                command.OrderAmount,
                command.PromoCodes,
                command.ProductId,
                command.UserId,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.AppliedCodes.Should().HaveCount(1);
        result.RejectedCodes.Should().HaveCount(1);
        result.RejectedCodes[0].Code.Should().Be("EXPIRED");
    }

    [Fact]
    public async Task Handle_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Func<Task> act = async () => await _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
