
namespace GameGuild;

/// <summary>
///     Base class for entities with state machine behavior.
///     Provides common infrastructure for validating and transitioning between states.
/// </summary>
/// <typeparam name="TStatus">The enum type representing valid states</typeparam>
public abstract class StatefulEntity<TStatus> : EntityBase
    where TStatus : struct, Enum
{
    /// <summary>
    ///     Dictionary defining valid state transitions.
    ///     Key is the current state, value is the set of states that can be transitioned to.
    /// </summary>
    protected abstract IReadOnlyDictionary<TStatus, IReadOnlySet<TStatus>> ValidTransitions { get; }

    /// <summary>
    ///     Gets the current status of the entity.
    /// </summary>
    public abstract TStatus Status { get; protected set; }

    /// <summary>
    ///     Validates if a state transition is allowed from the current state.
    /// </summary>
    /// <param name="newStatus">The target state</param>
    /// <returns>True if the transition is valid, false otherwise</returns>
    public bool CanTransitionTo(TStatus newStatus)
    {
        if (!ValidTransitions.TryGetValue(Status, out var allowed))
        {
            return false;
        }

        return allowed.Contains(newStatus);
    }

    /// <summary>
    ///     Transitions to a new status with validation.
    /// </summary>
    /// <param name="newStatus">The target state</param>
    /// <exception cref="InvalidStateTransitionException">Thrown when transition is not allowed</exception>
    protected void TransitionTo(TStatus newStatus)
    {
        if (!CanTransitionTo(newStatus))
        {
            throw new InvalidStateTransitionException(
                GetType().Name,
                Status.ToString()!,
                newStatus.ToString()!);
        }

        var oldStatus = Status;
        Status = newStatus;
        Touch();
        OnStatusChanged(oldStatus, newStatus);
    }

    /// <summary>
    ///     Called after a successful state transition.
    ///     Override to add custom behavior (e.g., raising domain events).
    /// </summary>
    /// <param name="oldStatus">The previous state</param>
    /// <param name="newStatus">The new state</param>
    protected virtual void OnStatusChanged(TStatus oldStatus, TStatus newStatus)
    {
        // Override in derived classes to handle state change events
    }
}
