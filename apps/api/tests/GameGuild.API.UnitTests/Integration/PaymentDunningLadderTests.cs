using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using GameGuild.API.Integration;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;

namespace GameGuild.API.UnitTests.Integration;

public sealed class PaymentDunningLadderTests
{
    private readonly Mock<ISubscriptionRepository> _subscriptionRepository = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IMonthlyStatementLinkBuilder> _linkBuilder = new();

    public PaymentDunningLadderTests()
    {
        _linkBuilder.Setup(builder => builder.GetBillingDashboardPath()).Returns("/billing");
    }

    [Fact]
    public void DefaultLadder_ReproducesTheTwoLegacyDunningSteps()
    {
        var options = new PaymentDunningOptions();

        var reminder = PaymentDunningLadder.Resolve(
            options.EscalationLadder,
            DunningStages.PaymentFailed,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 1, DateTimeKind.Utc));
        var finalNotice = PaymentDunningLadder.Resolve(
            options.EscalationLadder,
            DunningStages.FinalNotice,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 1, DateTimeKind.Utc));

        reminder.Title.Should().Be("Action required: your payment failed");
        reminder.Priority.Should().Be("High");
        reminder.DayOffset.Should().Be(0);
        reminder.Message.Should().Contain("{date}").And.Contain("{reason}");

        finalNotice.Title.Should().Be("Final notice: your subscription could not be renewed");
        finalNotice.Priority.Should().Be("Urgent");
        finalNotice.DayOffset.Should().Be(3);
    }

    [Fact]
    public void Resolve_WhenStageIsMissingFromConfig_FallsBackToBuiltInDefault()
    {
        var options = new PaymentDunningOptions
        {
            EscalationLadder = new List<DunningStepOptions>() // no FinalNotice rung
        };

        var step = PaymentDunningLadder.Resolve(
            options.EscalationLadder,
            DunningStages.FinalNotice,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 1, DateTimeKind.Utc));

        step.Title.Should().Be("Final notice: your subscription could not be renewed");
        step.Priority.Should().Be("Urgent");
    }

    [Fact]
    public void Resolve_WhenFailureIsOld_PicksTheHighestReachedRung()
    {
        var day0 = new DunningStepOptions { Stage = DunningStages.PaymentFailed, DayOffset = 0, Template = "t0", Title = "Day 0" };
        var day3 = new DunningStepOptions { Stage = DunningStages.PaymentFailed, DayOffset = 3, Template = "t3", Title = "Day 3" };
        var day7 = new DunningStepOptions { Stage = DunningStages.PaymentFailed, DayOffset = 7, Template = "t7", Title = "Day 7" };

        var atDay4 = PaymentDunningLadder.Resolve(
            new[] { day0, day3, day7 },
            DunningStages.PaymentFailed,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc));
        var atDay8 = PaymentDunningLadder.Resolve(
            new[] { day0, day3, day7 },
            DunningStages.PaymentFailed,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc));

        atDay4.Title.Should().Be("Day 3");
        atDay8.Title.Should().Be("Day 7");
    }

    [Fact]
    public void Resolve_WhenEventArrivesBeforeFirstRung_UsesFirstRung()
    {
        var day3 = new DunningStepOptions { Stage = DunningStages.FinalNotice, DayOffset = 3, Template = "final", Title = "Final" };

        var step = PaymentDunningLadder.Resolve(
            new[] { day3 },
            DunningStages.FinalNotice,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 1, DateTimeKind.Utc));

        step.Title.Should().Be("Final");
    }

    [Fact]
    public void Interpolate_FillsDateAndReasonPlaceholders()
    {
        var result = PaymentDunningLadder.Interpolate(
            "Payment on {date} failed: {reason}",
            new DateTime(2026, 8, 1, 14, 30, 0, DateTimeKind.Utc),
            "card declined");

        result.Should().Be("Payment on 2026-08-01 failed: card declined");
    }

    [Fact]
    public async Task PaymentFailedHandler_UsesConfiguredLadderStep()
    {
        var subscriptionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        ConfigureSubscription(subscriptionId, tenantId);
        ConfigureNotification(Result.Success(Notification.Create(
            Guid.NewGuid(),
            NotificationType.Billing,
            NotificationChannel.Email,
            "Configured title",
            "Configured body",
            tenantId)));
        var handler = new SubscriptionPaymentFailedEmailHandler(
            _subscriptionRepository.Object,
            _notificationService.Object,
            _linkBuilder.Object,
            Options.Create(new PaymentDunningOptions
            {
                EscalationLadder =
                [
                    new DunningStepOptions
                    {
                        Stage = DunningStages.PaymentFailed,
                        DayOffset = 0,
                        Template = "Custom",
                        Title = "Configured title",
                        Message = "Configured body",
                        Priority = "Low"
                    }
                ]
            }),
            Mock.Of<ILogger<SubscriptionPaymentFailedEmailHandler>>());

        await handler.Handle(
            new SubscriptionPaymentFailedEvent(
                subscriptionId,
                tenantId,
                "card declined",
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        _notificationService.Verify(service => service.SendAsync(
            It.IsAny<Guid>(),
            NotificationType.Billing,
            "Configured title",
            "Configured body",
            NotificationChannel.Email,
            tenantId,
            "/billing",
            NotificationPriority.Low,
            subscriptionId,
            nameof(Subscription),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void ConfigureSubscription(Guid subscriptionId, Guid tenantId)
    {
        _subscriptionRepository
            .Setup(repository => repository.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Subscription(
                tenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                BillingCycle.Monthly,
                new Money(49m),
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    private void ConfigureNotification(Result<Notification> result)
    {
        _notificationService
            .Setup(service => service.SendAsync(
                It.IsAny<Guid>(),
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
            .ReturnsAsync(result);
    }
}
