using HotChocolate;

namespace GameGuild.API.Core.GraphQL;

/// <summary>
///     Error hygiene for the GraphQL authorization framework (issue #335): execution-time
///     authorization failures are normalized so they never reveal which field was denied.
///     The bounded <c>AUTHORIZATION_DENIED</c> / <c>AUTHORIZATION_UNAVAILABLE</c> errors
///     keep their generic message and code, but their response <c>path</c> is stripped: an
///     attacker probing a protected field learns only that the request was denied, not the
///     identity of the denied field. Root operations denied at planning time additionally
///     receive the nonexistent-field error shape from the operation gate middleware.
/// </summary>
public sealed class GraphQLAuthorizationErrorFilter : IErrorFilter
{
    /// <inheritdoc />
    public IError OnError(IError error)
    {
        if (error.Code is not (GraphQLAuthorizationSupport.DeniedErrorCode
            or GraphQLAuthorizationSupport.UnavailableErrorCode)
            || error.Path is null)
        {
            return error;
        }

        return ErrorBuilder.FromError(error)
            .SetPath((HotChocolate.Path?)null)
            .Build();
    }
}
