using System.Reflection;
using GameGuild.CQRS;

namespace GameGuild.API.UnitTests;

internal sealed class DirectHandlerSender(params object[] handlers) : ISender
{
    public async Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default) =>
        (TResponse)(await InvokeAsync(request, cancellationToken).ConfigureAwait(false))!;

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest =>
        _ = await InvokeAsync(request, cancellationToken).ConfigureAwait(false);

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        InvokeAsync(request, cancellationToken);

    private async Task<object?> InvokeAsync(object request, CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        foreach (var handler in handlers)
        {
            var method = handler.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(candidate =>
                {
                    if (candidate.Name != "Handle") return false;
                    var parameters = candidate.GetParameters();
                    return parameters.Length == 2 && parameters[0].ParameterType == requestType;
                });
            if (method == null) continue;
            var invocation = method.Invoke(handler, [request, cancellationToken]);
            if (invocation is not Task task)
                throw new InvalidOperationException($"Handler {handler.GetType().FullName} did not return a Task.");
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        throw new InvalidOperationException($"No direct handler registered for {requestType.FullName}.");
    }
}
