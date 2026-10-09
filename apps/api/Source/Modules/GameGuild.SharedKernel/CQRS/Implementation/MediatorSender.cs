using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using GameGuild.CQRS.Infrastructure;

namespace GameGuild.CQRS.Implementation;

/// <summary>
///     Handles request dispatch (Send + CreateStream) with O(1) handler lookup, compiled delegates, and pipeline behavior support.
/// </summary>
internal class MediatorSender : ISender
{
    // O(1) lookup caches for performance
    // ReSharper disable InconsistentNaming
#pragma warning disable IDE1006 // Naming Styles - Static readonly fields intentionally use camelCase for cache fields
    private static readonly ConcurrentDictionary<Type, HandlerMetadata> s_handlerCache = new();
    private static readonly ConcurrentDictionary<(Type requestType, Type responseType), Type> s_handlerTypeCache = new();
    private static readonly ConcurrentDictionary<(Type requestType, Type responseType), Type> s_pipelineBehaviorTypeCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_sendMethodCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_handleMethodCache = new();
#pragma warning restore IDE1006
    // ReSharper restore InconsistentNaming

    private readonly ServiceFactory _serviceFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MediatorSender" /> class.
    /// </summary>
    /// <param name="serviceFactory">Service factory</param>
    public MediatorSender(ServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
    }

    // ── Send (typed with response) ─────────────────────────────────────────

    /// <summary>
    ///     Send a request through the mediator pipeline with pipeline behavior support and O(1) handler lookup.
    /// </summary>
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerDelegate = BuildPipeline<TResponse>(
            requestType,
            () => ExecuteHandler<TResponse>(request, requestType, cancellationToken),
            request,
            cancellationToken);

        return await handlerDelegate().ConfigureAwait(false);
    }

    // ── Send (typed void / Unit) ───────────────────────────────────────────

    /// <summary>
    ///     Send a request through the mediator pipeline without expecting a response.
    /// </summary>
    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = typeof(TRequest);
        var handlerDelegate = BuildPipeline<Unit>(
            requestType,
            () => ExecuteHandler<Unit>(request, requestType, cancellationToken),
            request,
            cancellationToken);

        await handlerDelegate().ConfigureAwait(false);
    }

    // ── Send (object / dynamic dispatch) ───────────────────────────────────

    /// <summary>
    ///     Send a request through the mediator pipeline via dynamic dispatch.
    ///     Routes through the typed Send overload to ensure pipeline behaviors execute.
    /// </summary>
    public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();

        // Find the IRequest<TResponse> interface to determine the response type
        var targetInterface = FindRequestInterface(requestType);

        if (targetInterface != null)
        {
            var responseType = targetInterface.GetGenericArguments()[0];

            // Invoke Send<TResponse>(IRequest<TResponse>, CancellationToken) via reflection
            // to ensure the full pipeline (validation, logging, caching, etc.) executes
            var sendMethod = s_sendMethodCache.GetOrAdd(
                responseType,
                rt =>
                {
                    var methods = typeof(MediatorSender).GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var m in methods)
                    {
                        if (m.Name != "Send" || !m.IsGenericMethodDefinition || m.GetParameters().Length != 2)
                        {
                            continue;
                        }

                        var paramType = m.GetParameters()[0].ParameterType;
                        if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(IRequest<>))
                        {
                            return m.MakeGenericMethod(rt);
                        }
                    }

                    throw new InvalidOperationException($"Cannot find Send<TResponse> method for response type {rt}");
                });

            var compiledSend = ExpressionTreeCompiler.GetOrCompile(sendMethod);
            return await compiledSend(this, [request, cancellationToken]).ConfigureAwait(false);
        }

        // Try IRequest (Unit response) — also route through the typed pipeline
        if (request is IRequest)
        {
            // Build pipeline directly for Unit response — avoids second layer of reflection
            var handlerDelegate = BuildPipeline<Unit>(
                requestType,
                () => ExecuteHandler<Unit>(request, requestType, cancellationToken),
                request,
                cancellationToken);

            await handlerDelegate().ConfigureAwait(false);
            return Unit.Value;
        }

        throw new InvalidOperationException($"No handler found for request type {requestType}");
    }

    // ── Shared pipeline infrastructure ─────────────────────────────────────

    /// <summary>
    ///     Builds the pipeline chain for a request, wrapping behaviors around the terminal handler delegate.
    ///     This is the single pipeline construction method used by all Send overloads (DRY).
    /// </summary>
    private RequestHandlerDelegate<TResponse> BuildPipeline<TResponse>(
        Type requestType,
        RequestHandlerDelegate<TResponse> terminalHandler,
        object request,
        CancellationToken cancellationToken)
    {
        var behaviors = GetPipelineBehaviors(requestType, typeof(TResponse));

        if (behaviors.Count == 0)
        {
            return terminalHandler;
        }

        // Build the pipeline chain from innermost to outermost
        var handlerDelegate = terminalHandler;
        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = handlerDelegate;
            handlerDelegate = () => InvokeBehavior<TResponse>(behavior, request, next, cancellationToken);
        }

        return handlerDelegate;
    }

    /// <summary>
    ///     Gets pipeline behaviors for the specified request and response types.
    /// </summary>
    private List<object> GetPipelineBehaviors(Type requestType, Type responseType)
    {
        var behaviorType = s_pipelineBehaviorTypeCache.GetOrAdd(
            (requestType, responseType),
            key => typeof(IPipelineBehavior<,>).MakeGenericType(key.requestType, key.responseType));

        var enumerableType = typeof(IEnumerable<>).MakeGenericType(behaviorType);
        var behaviorsEnumerable = _serviceFactory(enumerableType) as IEnumerable;

        if (behaviorsEnumerable == null)
        {
            return [];
        }

        var behaviors = new List<object>();
        foreach (var behavior in behaviorsEnumerable)
        {
            if (behavior != null)
            {
                behaviors.Add(behavior);
            }
        }

        return behaviors;
    }

    /// <summary>
    ///     Invokes a pipeline behavior with the request and next delegate via cached compiled delegate.
    /// </summary>
    private static async Task<TResponse> InvokeBehavior<TResponse>(
        object behavior,
        object request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var behaviorType = behavior.GetType();
        var handleMethod = s_handleMethodCache.GetOrAdd(behaviorType, static bt =>
            bt.GetMethod("Handle")
            ?? throw new InvalidOperationException(
                $"Pipeline behavior {bt.Name} does not have a Handle method"));

        var compiledInvoker = ExpressionTreeCompiler.GetOrCompile(handleMethod);
        var result = await compiledInvoker(behavior, [request, next, cancellationToken]).ConfigureAwait(false);

        // Handle null results: nullable TResponse (e.g. Tenant?) returns null legitimately
        if (result is null)
        {
            return default!;
        }

        if (result is TResponse typedResult)
        {
            return typedResult;
        }

        throw new InvalidOperationException(
            $"Pipeline behavior {behaviorType.Name} returned unexpected type: {result?.GetType()}");
    }

    // ── Handler execution ──────────────────────────────────────────────────

    /// <summary>
    ///     Executes the actual request handler with O(1) cached lookup and compiled delegates.
    /// </summary>
    private async Task<TResponse> ExecuteHandler<TResponse>(object request, Type requestType, CancellationToken cancellationToken)
    {
        var handlerType = s_handlerTypeCache.GetOrAdd(
            (requestType, typeof(TResponse)),
            key => typeof(IRequestHandler<,>).MakeGenericType(key.requestType, key.responseType));

        var handler = GetHandler(handlerType);

        var metadata = s_handlerCache.GetOrAdd(
            handlerType,
            ht =>
            {
                var method = ht.GetMethod("Handle")
                             ?? throw new InvalidOperationException($"Handler method not found for {ht}");

                return new HandlerMetadata
                {
                    HandlerType = ht,
                    HandleMethod = method,
                    CachedInvoker = CreateCachedInvoker(method, typeof(TResponse))
                };
            });

        // Cached delegate invocation — wraps reflection for O(1) dispatch after first call
        if (metadata.CachedInvoker != null)
        {
            var result = await metadata.CachedInvoker(handler, [request, cancellationToken]).ConfigureAwait(false);
            return (TResponse)result!;
        }

        // Fallback to reflection (should rarely happen)
        return await HandleRequestFallback<TResponse>(handler, request, cancellationToken).ConfigureAwait(false);
    }

    private object GetHandler(Type handlerType)
    {
        return _serviceFactory(handlerType)
               ?? throw new InvalidOperationException($"Handler not found for {handlerType}");
    }

    // ── Cached invoker creation ────────────────────────────────────────────

    /// <summary>
    ///     Creates a cached delegate using a compiled expression tree for handler dispatch.
    ///     Works for both generic response types and Unit-returning handlers.
    /// </summary>
    [ExcludeFromCodeCoverage]
    private static Func<object, object[], Task<object?>>? CreateCachedInvoker(MethodInfo method, Type responseType)
    {
        try
        {
            return ExpressionTreeCompiler.GetOrCompile(method);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Fallback method using compiled expression delegate when the primary cached invoker is unavailable.
    /// </summary>
    private static async Task<TResponse> HandleRequestFallback<TResponse>(object handler, object request, CancellationToken cancellationToken)
    {
        var handlerType = handler.GetType();
        var method = s_handleMethodCache.GetOrAdd(handlerType, static ht =>
            ht.GetMethod("Handle")
            ?? throw new InvalidOperationException($"Handler method not found for {ht}"));

        var compiledInvoker = ExpressionTreeCompiler.GetOrCompile(method);
        var result = await compiledInvoker(handler, [request, cancellationToken]).ConfigureAwait(false);

        if (result is TResponse typedResult)
        {
            return typedResult;
        }

        throw new InvalidOperationException($"Handler returned unexpected type: {result?.GetType()}");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    ///     Finds the <c>IRequest&lt;TResponse&gt;</c> interface on a request type.
    /// </summary>
    private static Type? FindRequestInterface(Type requestType)
    {
        var interfaces = requestType.GetInterfaces();
        for (var i = 0; i < interfaces.Length; i++)
        {
            var @interface = interfaces[i];
            if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IRequest<>))
            {
                return @interface;
            }
        }

        return null;
    }
}
