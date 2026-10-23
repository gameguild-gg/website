using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Commands;

public class RetryWebhookEventHandlerTests
{
    private readonly Mock<IBillingWebhookRepository> _repository = new();
    private readonly Mock<IBillingWebhookReplayHandler> _replayHandler = new();
    private readonly Mock<IBillingWebhookDeadLetterService> _deadLetterService = new();
    private readonly BillingWebhookReplayDispatcher _dispatcher;

    public RetryWebhookEventHandlerTests()
    {
        _replayHandler.Setup(handler => handler.Provider).Returns(PaymentProviders.Stripe);
        _dispatcher = new BillingWebhookReplayDispatcher([_replayHandler.Object]);
    }

    private RetryWebhookEventHandler CreateHandler() => new(
        _repository.Object,
        _dispatcher,
        _deadLetterService.Object,
        Options.Create(new BillingConfiguration()),
        NullLogger<RetryWebhookEventHandler>.Instance);

    [Fact]
    public async Task Handle_Should_Return_Error_For_Invalid_Guid()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new RetryWebhookEventCommand("not-a-guid"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid event ID");
    }

    [Fact]
    public async Task Handle_Should_Return_NotFound_When_Event_Missing()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent?)null);

        var result = await CreateHandler().Handle(new RetryWebhookEventCommand(Guid.NewGuid().ToString()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Should_Return_AlreadyProcessed_When_Event_Succeeded()
    {
        var webhookEvent = new BillingWebhookEvent
        {
            IsProcessed = true,
            IsFailed = false,
            ProcessingAttempts = 2
        };

        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhookEvent);

        var result = await CreateHandler().Handle(new RetryWebhookEventCommand(Guid.NewGuid().ToString()), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AttemptNumber.Should().Be(2);
        result.Message.Should().Contain("already processed");
        _replayHandler.Verify(handler => handler.ReplayAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_DeadLetter_When_Max_Attempts_Exceeded()
    {
        var webhookEvent = new BillingWebhookEvent
        {
            IsProcessed = false,
            IsFailed = true,
            ProcessingAttempts = 5,
            Provider = PaymentProviders.Stripe
        };

        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhookEvent);

        var result = await CreateHandler().Handle(new RetryWebhookEventCommand(Guid.NewGuid().ToString()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Maximum retry attempts");
        result.ErrorMessage.Should().Contain("dead-lettered");
        _deadLetterService.Verify(service => service.DeadLetterAsync(
            webhookEvent,
            It.Is<string>(reason => reason.Contains("Maximum retry attempts")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Fail_Explicitly_When_Provider_Has_No_Replay_Handler()
    {
        var webhookEvent = new BillingWebhookEvent
        {
            IsProcessed = false,
            IsFailed = true,
            ProcessingAttempts = 1,
            Provider = "unsupported-provider"
        };

        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhookEvent);
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        _deadLetterService
            .Setup(service => service.DeadLetterAsync(webhookEvent, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<BillingWebhookEvent, string, CancellationToken>((row, reason, _) => row.MarkAsDeadLettered(reason));

        var result = await CreateHandler().Handle(new RetryWebhookEventCommand(Guid.NewGuid().ToString()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No replay handler registered");
        result.ErrorMessage.Should().Contain("dead-lettered");
        webhookEvent.DeadLetteredAt.Should().NotBeNull();
        _deadLetterService.Verify(service => service.DeadLetterAsync(
            webhookEvent,
            It.Is<string>(reason => reason.Contains("No replay handler registered for provider 'unsupported-provider'")),
            It.IsAny<CancellationToken>()), Times.Once);
        _replayHandler.Verify(handler => handler.ReplayAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Execute_Replay_Of_The_Stored_Delivery()
    {
        // The retry must EXECUTE the provider handler on the stored row (issue #408),
        // not just reset the failure flags and report a queued retry.
        var webhookEvent = new BillingWebhookEvent
        {
            IsProcessed = false,
            IsFailed = true,
            ProcessingAttempts = 2,
            Provider = PaymentProviders.Stripe
        };

        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhookEvent);
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        _replayHandler
            .Setup(handler => handler.ReplayAsync(webhookEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebhookProcessingResult.Success(webhookEvent.ExternalEventId));

        var result = await CreateHandler().Handle(new RetryWebhookEventCommand(Guid.NewGuid().ToString()), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AttemptNumber.Should().Be(3);
        result.Message.Should().Contain("replayed successfully");
        _replayHandler.Verify(handler => handler.ReplayAsync(webhookEvent, It.IsAny<CancellationToken>()), Times.Once);
        _deadLetterService.Verify(service => service.DeadLetterAsync(
            It.IsAny<BillingWebhookEvent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_DeadLetter_When_Replay_Fails_At_The_Ceiling()
    {
        var webhookEvent = new BillingWebhookEvent
        {
            IsProcessed = false,
            IsFailed = true,
            ProcessingAttempts = 4, // next attempt (5) is the last one below the ceiling of 5
            Provider = PaymentProviders.Stripe
        };

        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhookEvent);
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        _replayHandler
            .Setup(handler => handler.ReplayAsync(webhookEvent, It.IsAny<CancellationToken>()))
            .ReturnsAsync(WebhookProcessingResult.Failed(webhookEvent.ExternalEventId, "downstream still unavailable"));

        var result = await CreateHandler().Handle(new RetryWebhookEventCommand(Guid.NewGuid().ToString()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("downstream still unavailable");
        _deadLetterService.Verify(service => service.DeadLetterAsync(
            webhookEvent,
            "downstream still unavailable",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Leave_Row_Retrievable_When_Replay_Fails_Below_The_Ceiling()
    {
        var webhookEvent = new BillingWebhookEvent
        {
            IsProcessed = false,
            IsFailed = true,
            ProcessingAttempts = 2,
            Provider = PaymentProviders.Stripe
        };

        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(webhookEvent);
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingWebhookEvent e, CancellationToken _) => e);
        _replayHandler
            .Setup(handler => handler.ReplayAsync(webhookEvent, It.IsAny<CancellationToken>()))
            .Callback<BillingWebhookEvent, CancellationToken>((row, _) => row.MarkAsFailed("downstream still unavailable"))
            .ReturnsAsync(WebhookProcessingResult.Failed(webhookEvent.ExternalEventId, "downstream still unavailable"));

        var result = await CreateHandler().Handle(new RetryWebhookEventCommand(Guid.NewGuid().ToString()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("retry worker will requeue");
        _deadLetterService.Verify(service => service.DeadLetterAsync(
            It.IsAny<BillingWebhookEvent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
