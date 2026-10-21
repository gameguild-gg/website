using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public sealed class BillingWebhookRetryWorkerTests
{
    private readonly Mock<IBillingWebhookRepository> _webhookRepository = new();
    private readonly Mock<ISender> _sender = new();
    private readonly Mock<IBillingWebhookDeadLetterService> _deadLetterService = new();

    private static BillingConfiguration Configuration(
        bool enabled = true,
        int maxRetryAttempts = 5,
        int batchSize = 20,
        int[]? retryScheduleMinutes = null,
        int terminalRetryScheduleMinutes = 1440)
    {
        var configuration = new BillingConfiguration();
        configuration.Webhook.RetryWorker.Enabled = enabled;
        configuration.Webhook.RetryWorker.MaxRetryAttempts = maxRetryAttempts;
        configuration.Webhook.RetryWorker.BatchSize = batchSize;
        configuration.Webhook.RetryWorker.RetryScheduleMinutes = retryScheduleMinutes ?? [1, 5, 15, 60, 360];
        configuration.Webhook.RetryWorker.TerminalRetryScheduleMinutes = terminalRetryScheduleMinutes;
        return configuration;
    }

    private BillingWebhookRetryWorker CreateWorker(BillingConfiguration configuration) => new(
        _webhookRepository.Object,
        _sender.Object,
        _deadLetterService.Object,
        Options.Create(configuration),
        NullLogger<BillingWebhookRetryWorker>.Instance);

    private static BillingWebhookEvent FailedEvent(int attempts, DateTime? updatedAt = null)
    {
        var webhookEvent = new BillingWebhookEvent
        {
            Provider = PaymentProviders.Stripe,
            ExternalEventId = $"evt_{Guid.NewGuid():N}",
            EventType = "invoice.payment_succeeded",
            ProcessingAttempts = attempts
        };
        webhookEvent.MarkAsFailed("transient downstream failure");
        // MarkAsFailed refreshes UpdatedAt to now; the explicit assignment simulates the
        // row having been in the failed state for the requested duration.
        webhookEvent.UpdatedAt = updatedAt ?? SystemClock.UtcNow.AddHours(-2);
        return webhookEvent;
    }

    [Fact]
    public async Task ProcessDueAsync_ReturnsZeroWithoutPolling_WhenDisabled()
    {
        var worker = CreateWorker(Configuration(enabled: false));

        var requeued = await worker.ProcessDueAsync();

        requeued.Should().Be(0);
        _webhookRepository.Verify(repository => repository.GetRetryCandidatesAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never,
            "a disabled worker must not touch the inbox");
        _webhookRepository.Verify(repository => repository.GetExhaustedEventsAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDueAsync_RequeuesEventsPastBackoffWindow()
    {
        var due = FailedEvent(attempts: 1, updatedAt: SystemClock.UtcNow.AddHours(-2));
        SetupNoExhaustedEvents();
        _webhookRepository
            .Setup(repository => repository.GetRetryCandidatesAsync(5, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { due });
        _sender
            .Setup(sender => sender.Send(It.Is<RetryWebhookEventCommand>(command => command.EventId == due.Id.ToString()),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookRetryResult { Success = true, AttemptNumber = 2 });

        var requeued = await CreateWorker(Configuration()).ProcessDueAsync();

        requeued.Should().Be(1);
        _sender.VerifyAll();
    }

    [Fact]
    public async Task ProcessDueAsync_SkipsEventsStillInsideBackoffWindow()
    {
        // Updated ten seconds ago; the smallest schedule delay is 1 minute for a first
        // failure, so the event must not be requeued on this cycle.
        var notDue = FailedEvent(attempts: 1, updatedAt: SystemClock.UtcNow.AddSeconds(-10));
        SetupNoExhaustedEvents();
        _webhookRepository
            .Setup(repository => repository.GetRetryCandidatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { notDue });

        var requeued = await CreateWorker(Configuration()).ProcessDueAsync();

        requeued.Should().Be(0);
        _sender.Verify(sender => sender.Send(It.IsAny<RetryWebhookEventCommand>(), It.IsAny<CancellationToken>()), Times.Never,
            "events inside the exponential backoff window must be skipped");
    }

    [Fact]
    public async Task ProcessDueAsync_BackoffGrowsWithAttemptNumber()
    {
        // Attempt 3 on the default schedule waits 15 minutes: an update 10 minutes ago is
        // not due, an update 20 minutes ago is.
        var recentHighAttempt = FailedEvent(attempts: 3, updatedAt: SystemClock.UtcNow.AddMinutes(-10));
        var dueHighAttempt = FailedEvent(attempts: 3, updatedAt: SystemClock.UtcNow.AddMinutes(-20));
        SetupNoExhaustedEvents();
        _webhookRepository
            .Setup(repository => repository.GetRetryCandidatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { recentHighAttempt, dueHighAttempt });
        _sender
            .Setup(sender => sender.Send(It.IsAny<RetryWebhookEventCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookRetryResult { Success = true });

        var requeued = await CreateWorker(Configuration()).ProcessDueAsync();

        requeued.Should().Be(1,
            "only the attempt-3 event past the 15-minute schedule delay is requeued");
        _sender.Verify(sender => sender.Send(
            It.Is<RetryWebhookEventCommand>(command => command.EventId == dueHighAttempt.Id.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDueAsync_HonorsConfiguredMaxAttemptsAndBatchSize()
    {
        SetupNoExhaustedEvents();
        _webhookRepository
            .Setup(repository => repository.GetRetryCandidatesAsync(5, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateWorker(Configuration(maxRetryAttempts: 5, batchSize: 20)).ProcessDueAsync();

        _webhookRepository.VerifyAll();
    }

    [Fact]
    public async Task ProcessDueAsync_DoesNotRequeueWhenExistingRetryPathRefusesAtCeiling()
    {
        var exhausted = FailedEvent(attempts: 4);
        SetupNoExhaustedEvents();
        _webhookRepository
            .Setup(repository => repository.GetRetryCandidatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { exhausted });
        _sender
            .Setup(sender => sender.Send(It.IsAny<RetryWebhookEventCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookRetryResult
            {
                Success = false,
                ErrorMessage = "Maximum retry attempts (5) exceeded"
            });

        var requeued = await CreateWorker(Configuration()).ProcessDueAsync();

        requeued.Should().Be(0);
    }

    [Fact]
    public async Task ProcessDueAsync_DeadLettersExhaustedEvents_LeftBehindByTheRequeuePath()
    {
        // A row that failed its final (5th) attempt no longer matches the retry-candidates
        // filter; the worker's dead-letter intake must terminally park it (issue #408).
        var exhaustedRow = FailedEvent(attempts: 5);
        _webhookRepository
            .Setup(repository => repository.GetExhaustedEventsAsync(5, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { exhaustedRow });
        _webhookRepository
            .Setup(repository => repository.GetRetryCandidatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var requeued = await CreateWorker(Configuration()).ProcessDueAsync();

        requeued.Should().Be(0);
        _deadLetterService.Verify(service => service.DeadLetterAsync(
            exhaustedRow,
            It.Is<string>(reason => reason.Contains("Retry budget exhausted")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDueAsync_ContinuesWhenOneExhaustedDeadLetterThrows()
    {
        var throwing = FailedEvent(attempts: 5);
        var parked = FailedEvent(attempts: 5);
        _webhookRepository
            .Setup(repository => repository.GetExhaustedEventsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { throwing, parked });
        _deadLetterService
            .SetupSequence(service => service.DeadLetterAsync(It.IsAny<BillingWebhookEvent>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient persistence failure"))
            .Returns(Task.CompletedTask);
        _webhookRepository
            .Setup(repository => repository.GetRetryCandidatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var requeued = await CreateWorker(Configuration()).ProcessDueAsync();

        requeued.Should().Be(0);
        _deadLetterService.Verify(service => service.DeadLetterAsync(
            parked, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once,
            "one failing dead-letter transition must not abort the remaining rows");
    }

    [Fact]
    public async Task ProcessDueAsync_ContinuesWhenOneEventRetryThrows()
    {
        var failing = FailedEvent(attempts: 1);
        var succeeding = FailedEvent(attempts: 1);
        SetupNoExhaustedEvents();
        _webhookRepository
            .Setup(repository => repository.GetRetryCandidatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { failing, succeeding });
        _sender
            .SetupSequence(sender => sender.Send(It.IsAny<RetryWebhookEventCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient send failure"))
            .ReturnsAsync(new WebhookRetryResult { Success = true });

        var requeued = await CreateWorker(Configuration()).ProcessDueAsync();

        requeued.Should().Be(1,
            "one failing event must not abort the remaining due events in the cycle");
    }

    private void SetupNoExhaustedEvents() =>
        _webhookRepository
            .Setup(repository => repository.GetExhaustedEventsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
}

public sealed class WebhookRetryScheduleTests
{
    [Fact]
    public void DelayFor_MatchesTheIssueBackoffSchedule()
    {
        // Issue #408: exponential backoff of 1, 5, 15, 60 and 360 minutes, then 24 hours.
        var schedule = new[] { 1, 5, 15, 60, 360 };

        WebhookRetrySchedule.DelayFor(1, schedule, 1440).Should().Be(TimeSpan.FromMinutes(1));
        WebhookRetrySchedule.DelayFor(2, schedule, 1440).Should().Be(TimeSpan.FromMinutes(5));
        WebhookRetrySchedule.DelayFor(3, schedule, 1440).Should().Be(TimeSpan.FromMinutes(15));
        WebhookRetrySchedule.DelayFor(4, schedule, 1440).Should().Be(TimeSpan.FromMinutes(60));
        WebhookRetrySchedule.DelayFor(5, schedule, 1440).Should().Be(TimeSpan.FromMinutes(360));
        WebhookRetrySchedule.DelayFor(6, schedule, 1440).Should().Be(TimeSpan.FromMinutes(1440),
            "attempt counts beyond the schedule fall back to the terminal cadence");
    }

    [Fact]
    public void DelayFor_FallsBackToTerminalCadence_WhenScheduleIsEmpty()
    {
        WebhookRetrySchedule.DelayFor(1, Array.Empty<int>(), 1440).Should().Be(TimeSpan.FromMinutes(1440));
    }
}
