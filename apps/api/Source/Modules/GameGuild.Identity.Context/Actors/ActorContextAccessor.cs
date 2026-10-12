namespace GameGuild.Identity.Context.Actors;

/// <summary>
///     Default implementation of <see cref="IActorContextAccessor"/> using <see cref="AsyncLocal{T}"/>.
/// </summary>
/// <remarks>
///     <para>
///         Uses AsyncLocal to maintain the actor context across async/await boundaries.
///         This ensures the context flows correctly through the entire request processing pipeline.
///     </para>
///     <para>
///         This implementation has no dependencies on ASP.NET Core and can be used in any
///         .NET application (console apps, background workers, tests, etc.).
///     </para>
/// </remarks>
public sealed class ActorContextAccessor : IActorContextAccessor
{
    private static readonly AsyncLocal<ActorContextHolder> ActorContextCurrent = new();

    /// <inheritdoc />
    public ActorContext ActorContext
    {
        get => ActorContextCurrent.Value?.Context ?? ActorContext.Anonymous;
    }

    /// <inheritdoc />
    public void SetActorContext(ActorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Use a holder object to ensure the value flows correctly across async boundaries
        // and avoids issues with value types being copied
        var holder = ActorContextCurrent.Value;
        if (holder != null)
        {
            // Clear the old context to prevent leaking
            holder.Context = null;
        }

        ActorContextCurrent.Value = new ActorContextHolder { Context = context };
    }

    /// <inheritdoc />
    public void ClearActorContext()
    {
        var holder = ActorContextCurrent.Value;
        if (holder != null)
        {
            holder.Context = null;
        }
    }

    /// <inheritdoc />
    public IActorContextScope BeginActorContextScope(ActorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previousHolder = ActorContextCurrent.Value;
        var previousContext = previousHolder?.Context;
        SetActorContext(context);
        return new RestoreHolderScope(this, previousHolder, previousContext);
    }

    /// <summary>
    ///     Restores the ambient actor context by repairing the holder object that was
    ///     active before the scope started. <see cref="SetActorContext"/> deliberately
    ///     clears the previous holder (leak protection), which makes a plain
    ///     set-previous-restore invisible to execution contexts upstack that still
    ///     reference the original holder — a nested swap in an event or pipeline handler
    ///     would leave the caller observing an anonymous context. Repairing the original
    ///     holder keeps every observer consistent.
    /// </summary>
    private sealed class RestoreHolderScope(
        ActorContextAccessor accessor,
        ActorContextHolder? previousHolder,
        ActorContext? previousContext) : IActorContextScope
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (previousHolder is not null)
            {
                // Repair the original holder so upstack observers see the previous
                // context again, and re-install it in the current flow.
                previousHolder.Context = previousContext;
                ActorContextCurrent.Value = previousHolder;
            }
            else
            {
                accessor.ClearActorContext();
            }
        }
    }

    /// <summary>
    ///     Holder class to ensure proper AsyncLocal behavior with reference semantics.
    /// </summary>
    private sealed class ActorContextHolder
    {
        public ActorContext? Context { get; set; }
    }
}
