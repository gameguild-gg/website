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

    private static BillingConfiguration Configuration(
        bool enabled = true,
        int maxRetryAttempts = 5,
        int batchSize = 20,
        int initialDelaySeconds = 5,
        bool jitter = false)
    {
        var configuration = new BillingConfiguration();
        configuration.Webhook.RetryWorker.Enabled = enabled;
        configuration.Webhook.RetryWorker.MaxRetryAttempts = maxRetryAttempts;
        configuration.Webhook.RetryWorker.BatchSize = batchSize;
        configuration.Webhook.RetryPolicy.InitialDelaySeconds = initialDelaySeconds;
        configuration.Webhook.RetryPolicy.BackoffMultiplier = 2.0;
        configuration.Webhook.RetryPolicy.MaxDelaySeconds = 300;
        configuration.Webhook.RetryPolicy.AddJitter = jitter;
        return configuration;
    }

    private BillingWebhookRetryWorker CreateWorker(BillingConfiguration configuration) => new(
        _webhookRepository.Object,
        _sender.Object,
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
    }

    [Fact]
    public async Task ProcessDueAsync_RequeuesEventsPastBackoffWindow()
    {
        var due = FailedEvent(attempts: 1, updatedAt: SystemClock.UtcNow.AddHours(-2));
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
        // Updated one second ago; the smallest configured delay is InitialDelaySeconds (5s),
        // so the event must not be requeued on this cycle.
        var notDue = FailedEvent(attempts: 1, updatedAt: SystemClock.UtcNow.AddSeconds(-1));
        _webhookRepository
            .Setup(repository => repository.GetRetryCandidatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { notDue });

        var requeued = await CreateWorker(Configuration(initialDelaySeconds: 5)).ProcessDueAsync();

        requeued.Should().Be(0);
        _sender.Verify(sender => sender.Send(It.IsAny<RetryWebhookEventCommand>(), It.IsAny<CancellationToken>()), Times.Never,
            "events inside the exponential backoff window must be skipped");
    }

    [Fact]
    public async Task ProcessDueAsync_BackoffGrowsWithAttemptNumber()
    {
        // Attempt 3 with a 10s initial delay and 2x multiplier: due only after 40s.
        var recentHighAttempt = FailedEvent(attempts: 3, updatedAt: SystemClock.UtcNow.AddSeconds(-30));
        var dueHighAttempt = FailedEvent(attempts: 3, updatedAt: SystemClock.UtcNow.AddMinutes(-5));
        _webhookRepository
            .Setup(repository => repository.GetRetryCandidatesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { recentHighAttempt, dueHighAttempt });
        _sender
            .Setup(sender => sender.Send(It.IsAny<RetryWebhookEventCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookRetryResult { Success = true });

        var requeued = await CreateWorker(Configuration(initialDelaySeconds: 10)).ProcessDueAsync();

        requeued.Should().Be(1,
            "only the attempt-3 event past the 40s backoff window is requeued");
        _sender.Verify(sender => sender.Send(
            It.Is<RetryWebhookEventCommand>(command => command.EventId == dueHighAttempt.Id.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDueAsync_HonorsConfiguredMaxAttemptsAndBatchSize()
    {
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
    public async Task ProcessDueAsync_ContinuesWhenOneEventRetryThrows()
    {
        var failing = FailedEvent(attempts: 1);
        var succeeding = FailedEvent(attempts: 1);
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
}
