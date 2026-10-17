using GameGuild.CQRS;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Dual-mode dispatch surface for permission commands (issue #302): the same guarded,
///     versioned and audited command handlers can be executed either asynchronously (the
///     default request path) or synchronously.
/// </summary>
/// <remarks>
///     <para>
///         The asynchronous mode is the ordinary mediator pipeline. The synchronous mode
///         dispatches the identical command instance through the identical pipeline and
///         blocks until the handler completes — for callers that cannot await (synchronous
///         background jobs, legacy sync adapters, startup seeding). The underlying pipeline
///         awaits with <c>ConfigureAwait(false)</c>, so blocking is safe on worker threads;
///         it must NOT be called from a thread with an ASP.NET classic synchronization
///         context (ASP.NET Core has none).
///     </para>
///     <para>
///         <b>Guarantees preserved in both modes:</b> every permission mutation still runs
///         guarded (actor authorization), versioned (tenant security version bump) and
///         audited (<c>PermissionAuditLog</c>) — the modes differ only in how the caller
///     waits, never in the executed behavior.
///     </para>
/// </remarks>
public interface IPermissionCommandDispatcher
{
    /// <summary>
    ///     Asynchronously dispatches a permission command through the mediator pipeline.
    /// </summary>
    Task<TResponse> DispatchAsync<TResponse>(
        IRequest<TResponse> command,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Asynchronously dispatches a response-less permission command.
    /// </summary>
    Task DispatchAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : IRequest;

    /// <summary>
    ///     Synchronously dispatches a permission command through the same mediator
    ///     pipeline, blocking the calling thread until the handler completes.
    /// </summary>
    TResponse Dispatch<TResponse>(IRequest<TResponse> command);

    /// <summary>
    ///     Synchronously dispatches a response-less permission command.
    /// </summary>
    void Dispatch<TCommand>(TCommand command) where TCommand : IRequest;
}

/// <inheritdoc />
public sealed class PermissionCommandDispatcher(ISender sender) : IPermissionCommandDispatcher
{
    public Task<TResponse> DispatchAsync<TResponse>(
        IRequest<TResponse> command,
        CancellationToken cancellationToken = default) =>
        sender.Send(command, cancellationToken);

    public Task DispatchAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : IRequest =>
        sender.Send(command, cancellationToken);

    public TResponse Dispatch<TResponse>(IRequest<TResponse> command) =>
        sender.Send(command).ConfigureAwait(false).GetAwaiter().GetResult();

    public void Dispatch<TCommand>(TCommand command) where TCommand : IRequest =>
        sender.Send(command).ConfigureAwait(false).GetAwaiter().GetResult();
}
