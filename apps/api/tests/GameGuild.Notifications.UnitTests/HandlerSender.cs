using GameGuild.CQRS;

namespace GameGuild.Notifications.UnitTests;

/// <summary>
/// Controller unit tests use the real command handlers while retaining mocked service boundaries.
/// Transaction/outbox behavior is exercised separately by the host integration tests.
/// </summary>
internal sealed class HandlerSender(params object[] handlers) : ISender
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var contract = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        var handler = handlers.Single(contract.IsInstanceOfType);
        return (Task<TResponse>)contract.GetMethod("Handle")!.Invoke(handler, [request, cancellationToken])!;
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        => throw new NotSupportedException("These controller tests use typed command responses.");

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("These controller tests use typed command responses.");
}
