
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Commands;

public class CreateSubscriptionCommandHandlerTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepository;
    private readonly CreateSubscriptionCommandHandler _handler;

    public CreateSubscriptionCommandHandlerTests()
    {
        _mockRepository = new Mock<ISubscriptionRepository>();
        Mock<ILogger<CreateSubscriptionCommandHandler>> mockLogger = new Mock<ILogger<CreateSubscriptionCommandHandler>>();
        _handler = new CreateSubscriptionCommandHandler(_mockRepository.Object, mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateSubscription_WithValidCommand()
    {
        // Arrange
        var command = new CreateSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            PlanId: Guid.NewGuid(),
            CreatedByUserId: Guid.NewGuid(),
            BillingCycle: BillingCycle.Monthly,
            Amount: 29.99m,
            StartDate: DateTime.UtcNow,
            TrialDays: null
        );

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        _mockRepository.Verify(r => r.AddAsync(It.Is<Subscription>(s => 
            s.PlanId == command.PlanId &&
            s.CreatedByUserId == command.CreatedByUserId &&
            s.BillingCycle == command.BillingCycle
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCreateSubscriptionWithTrial_WhenTrialDaysProvided()
    {
        // Arrange
        var startDate = DateTime.UtcNow;
        var trialDays = 14;
        var command = new CreateSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            PlanId: Guid.NewGuid(),
            CreatedByUserId: Guid.NewGuid(),
            BillingCycle: BillingCycle.Monthly,
            Amount: 29.99m,
            StartDate: startDate,
            TrialDays: trialDays
        );

        Subscription? capturedSubscription = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => capturedSubscription = s)
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedSubscription.Should().NotBeNull();
        capturedSubscription!.TrialEndDate.Should().NotBeNull();
        capturedSubscription.TrialEndDate!.Value.Date.Should().Be(startDate.AddDays(trialDays).Date);
    }

    [Fact]
    public async Task Handle_ShouldUseCurrentDate_WhenStartDateNotProvided()
    {
        // Arrange
        var beforeExecution = DateTime.UtcNow;
        var command = new CreateSubscriptionCommand(
            TenantId: Guid.NewGuid(),
            PlanId: Guid.NewGuid(),
            CreatedByUserId: Guid.NewGuid(),
            BillingCycle: BillingCycle.Monthly,
            Amount: 29.99m,
            StartDate: null,
            TrialDays: null
        );

        Subscription? capturedSubscription = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => capturedSubscription = s)
            .ReturnsAsync((Subscription s, CancellationToken _) => s);

        // Act
        await _handler.Handle(command, CancellationToken.None);
        var afterExecution = DateTime.UtcNow;

        // Assert
        capturedSubscription.Should().NotBeNull();
        capturedSubscription!.StartDate.Should().BeOnOrAfter(beforeExecution);
        capturedSubscription.StartDate.Should().BeOnOrBefore(afterExecution);
    }
}
