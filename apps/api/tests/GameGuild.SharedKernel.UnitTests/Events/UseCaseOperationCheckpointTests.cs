namespace GameGuild.Tests.SharedKernel.Unit;

public sealed class UseCaseOperationCheckpointTests
{
    [Fact]
    public void RollbackRestoresUncapturedStateAndDoesNotReuseTheRolledBackAggregate()
    {
        var context = NewContext();
        var before = context.CaptureCheckpoint();
        var rolledBack = context.GetOrCreateOperationEvent("Permission", "rolled-back");
        context.MarkBusinessMutationObserved();
        context.MarkOperationEventCaptured();
        context.RecordProducedEvents(new[] { rolledBack });

        context.RestoreCheckpoint(before);

        Assert.False(context.OperationEventCaptured);
        Assert.False(context.BusinessMutationObserved);
        Assert.Empty(context.ProducedEventTypes);
        var resumed = context.GetOrCreateOperationEvent("OuterAggregate", "retained");
        Assert.NotSame(rolledBack, resumed);
        Assert.Equal("retained", resumed.AggregateId);
    }

    [Fact]
    public void RollbackPreservesAnEarlierOuterEventAndItsProducedEventSet()
    {
        var context = NewContext();
        var outer = context.GetOrCreateOperationEvent("OuterAggregate", "retained");
        context.MarkBusinessMutationObserved();
        context.MarkOperationEventCaptured();
        context.RecordProducedEvents(new[] { outer });
        var before = context.CaptureCheckpoint();
        context.ResetForRetry();

        context.RestoreCheckpoint(before);

        Assert.True(context.OperationEventCaptured);
        Assert.True(context.BusinessMutationObserved);
        Assert.Equal(new[] { typeof(UseCaseOperationOccurredV1) }, context.ProducedEventTypes);
        Assert.Same(outer, context.GetOrCreateOperationEvent("Ignored", "ignored"));
    }

    [Fact]
    public void NullCheckpointIsRejected() => Assert.Throws<ArgumentNullException>(() => NewContext().RestoreCheckpoint(null!));

    private static UseCaseOperationContext NewContext() => new("test.permission.update", "PermissionUpdateTest",
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
}
