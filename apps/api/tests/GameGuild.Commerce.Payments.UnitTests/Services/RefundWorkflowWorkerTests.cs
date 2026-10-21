using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
///     Automated refund-workflow worker behavior (issues #403/#419): opt-in gating, execution of
///     approved refunds and bounded retries of failed ones through the shared execution core,
///     per-refund error isolation and system-actor attribution.
/// </summary>
public class RefundWorkflowWorkerTests
{
    private readonly Mock<IRefundRepository> _refundRepository = new();
    private readonly Mock<IRefundProcessingService> _processingService = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    [Fact]
    public async Task ProcessDueAsync_WhenDisabled_ExecutesNothing()
    {
        var worker = CreateWorker(enabled: false);
        _refundRepository
            .Setup(repository => repository.ListDueForWorkflowAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("must not be called"));

        var executed = await worker.ProcessDueAsync();

        executed.Should().Be(0);
        _processingService.Verify(
            service => service.ExecuteAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessDueAsync_ExecutesEachDueRefundThroughTheSharedCore()
    {
        var due = new[] { ApprovedRefund(), FailedRefund(), ApprovedRefund() };
        var worker = CreateWorker(enabled: true);
        SetupDueRefunds(due);

        var executed = await worker.ProcessDueAsync();

        executed.Should().Be(3);
        foreach (var refund in due)
        {
            _processingService.Verify(
                service => service.ExecuteAsync(
                    It.Is<Refund>(candidate => candidate.Id == refund.Id),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task ProcessDueAsync_PassesTheAttemptBudgetAndRetryCooldownToTheQuery()
    {
        var worker = CreateWorker(enabled: true, maxExecutionAttempts: 3, failedRetryDelayMinutes: 60, maxBatchSize: 25);
        SetupDueRefunds([]);

        await worker.ProcessDueAsync();

        _refundRepository.Verify(
            repository => repository.ListDueForWorkflowAsync(
                3,
                It.Is<DateTime>(cutoff => cutoff <= SystemClock.UtcNow.AddMinutes(-60).AddSeconds(5)
                    && cutoff >= SystemClock.UtcNow.AddMinutes(-61)),
                25,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDueAsync_IsolatesPerRefundFailures_AndContinuesTheBatch()
    {
        var failing = ApprovedRefund();
        var succeeding = ApprovedRefund();
        var worker = CreateWorker(enabled: true);
        SetupDueRefunds([failing, succeeding]);
        _processingService
            .Setup(service => service.ExecuteAsync(It.Is<Refund>(candidate => candidate.Id == failing.Id), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gateway unavailable"));

        var executed = await worker.ProcessDueAsync();

        executed.Should().Be(1);
        _processingService.Verify(
            service => service.ExecuteAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessDueAsync_RunsExecutionsAsTheWorkflowSystemActor_AndRestoresTheContext()
    {
        var refund = ApprovedRefund();
        var worker = CreateWorker(enabled: true);
        SetupDueRefunds([refund]);

        var ambientContext = ActorContextBuilder.ForUser(Guid.NewGuid()).Build();
        var accessor = new MutableActorContextAccessor(ambientContext);
        var workerWithMutableAccessor = new RefundWorkflowWorker(
            _refundRepository.Object,
            _processingService.Object,
            accessor,
            Options.Create(new RefundWorkflowOptions
            {
                Enabled = true,
                PollIntervalMinutes = 5,
                MaxExecutionAttempts = 3,
                FailedRetryDelayMinutes = 60,
                MaxBatchSize = 50
            }),
            NullLogger<RefundWorkflowWorker>.Instance);

        ActorContext? observedDuringExecution = null;
        _processingService
            .Setup(service => service.ExecuteAsync(It.IsAny<Refund>(), It.IsAny<CancellationToken>()))
            .Callback(() => observedDuringExecution = accessor.ActorContext)
            .ReturnsAsync(new RefundExecutionOutcome { Success = true, Refund = refund });

        await workerWithMutableAccessor.ProcessDueAsync();

        observedDuringExecution!.ActorKind.Should().Be(ActorKind.System);
        accessor.ActorContext.Should().Be(ambientContext); // Context restored after the execution.
    }

    private RefundWorkflowWorker CreateWorker(
        bool enabled,
        int maxExecutionAttempts = 3,
        int failedRetryDelayMinutes = 60,
        int maxBatchSize = 50) =>
        new(
            _refundRepository.Object,
            _processingService.Object,
            _actorAccessor.Object,
            Options.Create(new RefundWorkflowOptions
            {
                Enabled = enabled,
                PollIntervalMinutes = 5,
                MaxExecutionAttempts = maxExecutionAttempts,
                FailedRetryDelayMinutes = failedRetryDelayMinutes,
                MaxBatchSize = maxBatchSize
            }),
            NullLogger<RefundWorkflowWorker>.Instance);

    private void SetupDueRefunds(IEnumerable<Refund> refunds)
    {
        _refundRepository
            .Setup(repository => repository.ListDueForWorkflowAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refunds.ToList());
    }

    private static Refund ApprovedRefund()
    {
        var refund = Refund.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10m,
            "USD",
            "goodwill",
            null,
            Guid.NewGuid(),
            requiresApproval: true,
            $"idem-{Guid.NewGuid():N}");
        refund.MarkApproved(Guid.NewGuid());
        return refund;
    }

    private static Refund FailedRefund()
    {
        var refund = Refund.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10m,
            "USD",
            "duplicate",
            null,
            Guid.NewGuid(),
            requiresApproval: false,
            $"idem-{Guid.NewGuid():N}");
        refund.MarkProcessing();
        refund.MarkFailed("transient gateway error");
        return refund;
    }

    private sealed class MutableActorContextAccessor(ActorContext initial) : IActorContextAccessor
    {
        private ActorContext _current = initial;

        public ActorContext ActorContext => _current;

        public void SetActorContext(ActorContext context) => _current = context;

        public void ClearActorContext() => _current = ActorContext.Anonymous;

        public IActorContextScope BeginActorContextScope(ActorContext context)
        {
            var previous = _current;
            SetActorContext(context);
            return new RestoreScope(this, previous);
        }

        private sealed class RestoreScope(MutableActorContextAccessor accessor, ActorContext previous) : IActorContextScope
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                accessor._current = previous;
            }
        }
    }
}
