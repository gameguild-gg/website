using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public sealed class RootReversalFenceRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<SourceStampId, RootFenceState> _states = [];

    public RootFenceSnapshot Capture(IEnumerable<SourceStampId> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        lock (_gate)
        {
            return new RootFenceSnapshot(roots
                .Distinct()
                .ToDictionary(root => root, CurrentState));
        }
    }

    public long BeginReversal(SourceStampId root)
    {
        lock (_gate)
        {
            var current = CurrentState(root);
            if (current.IsReversalActive)
                throw new InvalidOperationException("A reversal is already active for this root.");

            var next = new RootFenceState(checked(current.Epoch + 1), true);
            _states[root] = next;
            return next.Epoch;
        }
    }

    public void CompleteReversal(SourceStampId root, long epoch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(epoch);
        lock (_gate)
        {
            var current = CurrentState(root);
            if (current.Epoch != epoch) throw new StaleRootFenceException(root, epoch, current.Epoch);
            if (!current.IsReversalActive) throw new InvalidOperationException("No reversal is active for this root.");
            _states[root] = current with { IsReversalActive = false };
        }
    }

    public void EnsureAllocatable(RootFenceSnapshot snapshot, IEnumerable<SourceStampId> roots)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(roots);
        lock (_gate)
        {
            foreach (var root in roots.Distinct())
            {
                var current = CurrentState(root);
                if (!snapshot.TryGet(root, out var captured) || captured.Epoch != current.Epoch)
                    throw new StaleRootFenceException(root, captured.Epoch, current.Epoch);
                if (current.IsReversalActive)
                    throw new RootReversalInProgressException(root, current.Epoch);
            }
        }
    }

    public T WithAllocationFence<T>(RootFenceSnapshot snapshot, IEnumerable<SourceStampId> roots, Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            var materializedRoots = roots.Distinct().ToArray();
            EnsureAllocatable(snapshot, materializedRoots);
            return action();
        }
    }

    private RootFenceState CurrentState(SourceStampId root) =>
        _states.TryGetValue(root, out var state) ? state : new RootFenceState(0, false);
}

public sealed class RootFenceSnapshot
{
    private readonly IReadOnlyDictionary<SourceStampId, RootFenceState> _states;

    internal RootFenceSnapshot(IReadOnlyDictionary<SourceStampId, RootFenceState> states) => _states = states;

    internal bool TryGet(SourceStampId root, out RootFenceState state) => _states.TryGetValue(root, out state);
}

internal readonly record struct RootFenceState(long Epoch, bool IsReversalActive);

public sealed class StaleRootFenceException : InvalidOperationException
{
    public StaleRootFenceException(SourceStampId root, long capturedEpoch, long currentEpoch)
        : base($"Root {root.Value:N} fence epoch {capturedEpoch} is stale; current epoch is {currentEpoch}.")
    {
    }
}

public sealed class RootReversalInProgressException : InvalidOperationException
{
    public RootReversalInProgressException(SourceStampId root, long epoch)
        : base($"Root {root.Value:N} is fenced for reversal at epoch {epoch}.")
    {
    }
}
