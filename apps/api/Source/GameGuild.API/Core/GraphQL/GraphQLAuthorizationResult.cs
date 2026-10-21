using HotChocolate;
using HotChocolate.Execution;

namespace GameGuild.API.Core.GraphQL;

/// <summary>
///     A delegating <see cref="IOperationResult"/> whose authorization errors carry no
///     response path (issue #335 error hygiene). HotChocolate attaches the response path
///     to resolver-raised errors AFTER the error-filter stage
///     (<c>MiddlewareContext.ReportError</c> → <c>EnsurePathAndLocation</c> re-adds a
///     missing path to every error that reaches the result), so an <see cref="IErrorFilter"/>
///     alone cannot keep denied-field paths out of the response. The path is therefore
///     stripped on the finished result: the HTTP transport serializes operation results
///     through the interface properties (<c>JsonResultFormatter</c>), so the sanitized
///     error list is exactly what clients see.
/// </summary>
/// <remarks>
///     <para>
///         Bounded <c>AUTHORIZATION_DENIED</c> / <c>AUTHORIZATION_UNAVAILABLE</c> errors
///         keep their generic message and code; only the response <c>path</c> is removed,
///         so an attacker probing a protected field learns that something was denied but
///         never which field was denied. Incremental patches are wrapped recursively.
///     </para>
///     <para>
///         <see cref="ToDictionary"/> delegates to the wrapped result and is not used by
///         the HTTP transport; the transport path is the sanitized surface.
///     </para>
/// </remarks>
internal sealed class GraphQLAuthorizationResult(IOperationResult result) : IOperationResult
{
    private readonly IOperationResult _result = result ?? throw new ArgumentNullException(nameof(result));

    private IReadOnlyList<IError>? _sanitizedErrors;
    private IReadOnlyList<IOperationResult>? _sanitizedIncremental;

    public ExecutionResultKind Kind => _result.Kind;

    public IReadOnlyDictionary<string, object?>? ContextData => _result.ContextData;

    public int? RequestIndex => _result.RequestIndex;

    public int? VariableIndex => _result.VariableIndex;

    public string? Label => _result.Label;

    // Fully qualified: System.IO.Path (implicit usings) collides with HotChocolate.Path.
    public HotChocolate.Path? Path => _result.Path;

    public IReadOnlyDictionary<string, object?>? Data => _result.Data;

    public IReadOnlyList<object?>? Items => _result.Items;

    public IReadOnlyList<IError>? Errors => _sanitizedErrors ??= SanitizeErrors(_result.Errors);

    public IReadOnlyDictionary<string, object?>? Extensions => _result.Extensions;

    public IReadOnlyList<IOperationResult>? Incremental
        => _sanitizedIncremental ??= _result.Incremental is { } incremental
            ? incremental.Select(patch => new GraphQLAuthorizationResult(patch)).ToList()
            : null;

    public bool? HasNext => _result.HasNext;

    public bool IsDataSet => _result.IsDataSet;

    public IReadOnlyDictionary<string, object?> ToDictionary() => _result.ToDictionary();

    public void RegisterForCleanup(Func<ValueTask> clean) => _result.RegisterForCleanup(clean);

    public ValueTask DisposeAsync() => _result.DisposeAsync();

    /// <summary>
    ///     True when at least one authorization error in the result (or an incremental
    ///     patch) still carries a revealing response path and the result needs wrapping.
    /// </summary>
    internal static bool NeedsSanitizing(IOperationResult result)
        => (result.Errors is { } errors && errors.Any(HasRevealingPath))
            || (result.Incremental is { } incremental && incremental.Any(NeedsSanitizing));

    private static bool HasRevealingPath(IError error)
        => error.Code is (GraphQLAuthorizationSupport.DeniedErrorCode
                or GraphQLAuthorizationSupport.UnavailableErrorCode)
            && error.Path is not null;

    private static IReadOnlyList<IError>? SanitizeErrors(IReadOnlyList<IError>? errors)
    {
        if (errors is null)
        {
            return null;
        }

        IError? sanitized = null;
        var result = new IError[errors.Count];
        for (var index = 0; index < errors.Count; index++)
        {
            var error = errors[index];
            if (HasRevealingPath(error))
            {
                sanitized = ErrorBuilder.FromError(error)
                    .SetPath((HotChocolate.Path?)null)
                    .Build();
                result[index] = sanitized;
            }
            else
            {
                result[index] = error;
            }
        }

        // Avoid allocating a distinct array when there was nothing to strip.
        return sanitized is null ? errors : result;
    }
}
