using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
///     Automated retry-queue worker behavior (issue #415): opt-in gating, batch cap, dispatch
///     through the existing retry path with per-item error isolation, and system-actor
///     attribution for queue dispatches.
/// </summary>
public class PaymentRetryQueueWorkerTests
{
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<ISender> _sender = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    [Fact]
    public async Task ProcessDueAsync_WhenDisabled_DispatchesNothing()
    {
        var worker = CreateWorker(enabled: false);
        _paymentRepository
            .Setup(repository => repository.GetDueForRetryAsync(It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .Throws(new InvalidOperationException("must not be called"));

        var dispatched = await worker.ProcessDueAsync();

        dispatched.Should().Be(0);
        _sender.Verify(
            sender => sender.Send(It.IsAny<RetryPaymentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessDueAsync_DispatchesEachDuePaymentThroughTheRetryCommand()
    {
        var due = new[] { DuePayment(), DuePayment(), DuePayment() };
        var worker = CreateWorker(enabled: true);
        SetupDuePayments(due);
        SetupRetryResults(success: true);

        var dispatched = await worker.ProcessDueAsync();

        dispatched.Should().Be(3);
        foreach (var payment in due)
        {
            _sender.Verify(
                sender => sender.Send(
                    It.Is<RetryPaymentCommand>(command => command.PaymentId == payment.Id),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task ProcessDueAsync_CapsTheBatchAtMaxBatchSize()
    {
        var worker = CreateWorker(enabled: true, maxBatchSize: 2);
        SetupDuePayments([DuePayment(), DuePayment(), DuePayment()]);

        await worker.ProcessDueAsync();

        _paymentRepository.Verify(
            repository => repository.GetDueForRetryAsync(It.IsAny<CancellationToken>(), 2),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDueAsync_IsolatesPerItemFailures_AndContinuesTheBatch()
    {
        var failing = DuePayment();
        var succeeding = DuePayment();
        var worker = CreateWorker(enabled: true);
        SetupDuePayments([failing, succeeding]);
        SetupRetryResults(success: true, except: failing.Id);

        var dispatched = await worker.ProcessDueAsync();

        dispatched.Should().Be(1);
        _sender.Verify(
            sender => sender.Send(
                It.Is<RetryPaymentCommand>(command => command.PaymentId == failing.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _sender.Verify(
            sender => sender.Send(
                It.Is<RetryPaymentCommand>(command => command.PaymentId == succeeding.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDueAsync_RunsDispatchesAsTheRetryQueueSystemActor_AndRestoresTheContext()
    {
        var payment = DuePayment();
        var worker = CreateWorker(enabled: true);
        SetupDuePayments([payment]);
        SetupRetryResults(success: true);

        // A mutable accessor fake so the test observes the SetActorContext swap and restore
        // (a static Moq setup would keep returning the ambient context regardless).
        var ambientContext = ActorContextBuilder.ForUser(Guid.NewGuid()).Build();
        var accessor = new MutableActorContextAccessor(ambientContext);
        var workerWithMutableAccessor = new PaymentRetryQueueWorker(
            _paymentRepository.Object,
            _sender.Object,
            accessor,
            Microsoft.Extensions.Options.Options.Create(new PaymentRetryQueueOptions
            {
                Enabled = true,
                MaxBatchSize = 100,
                PollIntervalMinutes = 60
            }),
            NullLogger<PaymentRetryQueueWorker>.Instance);

        ActorContext? observedDuringDispatch = null;
        _sender
            .Setup(sender => sender.Send(It.IsAny<RetryPaymentCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => observedDuringDispatch = accessor.ActorContext)
            .ReturnsAsync(new PaymentRetryResult { Success = true, RetryAttempt = 1 });

        await workerWithMutableAccessor.ProcessDueAsync();

        observedDuringDispatch!.ActorKind.Should().Be(ActorKind.System);
        accessor.ActorContext.Should().Be(ambientContext); // Context restored after the dispatch.
    }

    private sealed class MutableActorContextAccessor(ActorContext initial) : IActorContextAccessor
    {
        private ActorContext _current = initial;

        public ActorContext ActorContext => _current;

        public void SetActorContext(ActorContext context) => _current = context;

        public void ClearActorContext() => _current = ActorContext.Anonymous;
    }

    private PaymentRetryQueueWorker CreateWorker(bool enabled, int maxBatchSize = 100) =>
        new(
            _paymentRepository.Object,
            _sender.Object,
            _actorAccessor.Object,
            Options.Create(new PaymentRetryQueueOptions
            {
                Enabled = enabled,
                MaxBatchSize = maxBatchSize,
                PollIntervalMinutes = 60
            }),
            NullLogger<PaymentRetryQueueWorker>.Instance);

    private void SetupDuePayments(IEnumerable<Payment> payments)
    {
        _paymentRepository
            .Setup(repository => repository.GetDueForRetryAsync(It.IsAny<CancellationToken>(), It.IsAny<int?>()))
            .ReturnsAsync(payments.ToList());
    }

    private void SetupRetryResults(bool success, Guid? except = null)
    {
        _sender
            .Setup(sender => sender.Send(It.IsAny<RetryPaymentCommand>(), It.IsAny<CancellationToken>()))
            .Returns((RetryPaymentCommand command, CancellationToken _) =>
            {
                if (except.HasValue && command.PaymentId == except.Value)
                {
                    throw new InvalidOperationException("gateway unavailable");
                }

                return Task.FromResult(new PaymentRetryResult
                {
                    Success = success,
                    RetryAttempt = 1,
                    MaxRetriesReached = !success
                });
            });
    }

    private static Payment DuePayment()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            25m,
            "USD",
            $"idem-queue-{Guid.NewGuid():N}",
            subscriptionId: Guid.NewGuid(),
            maxRetries: 4);
        payment.MarkAsProcessing();
        payment.MarkAsFailed("insufficient funds", "insufficient_funds");
        return payment;
    }
}
