using System.Reflection;
using System.Runtime.ExceptionServices;
using GameGuild.CQRS;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Analytics.UnitTests;

/// <summary>Routes controller unit tests through real command handlers and mocked service boundaries.</summary>
internal sealed class CommandHandlerSender(params object[] dependencies) : ISender
{
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var services = new ServiceCollection().AddLogging();
        foreach (var dependency in dependencies)
            foreach (var contract in dependency.GetType().GetInterfaces())
                services.AddSingleton(contract, dependency);
        using var provider = services.BuildServiceProvider();
        var handlerContract = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        var handlerType = request.GetType().Assembly.GetTypes().Single(type =>
            type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters && handlerContract.IsAssignableFrom(type));
        var handler = ActivatorUtilities.CreateInstance(provider, handlerType);
        try
        {
            return await (Task<TResponse>)handlerContract.GetMethod("Handle")!.Invoke(handler, [request, cancellationToken])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        => Send<Unit>(request, cancellationToken);

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
