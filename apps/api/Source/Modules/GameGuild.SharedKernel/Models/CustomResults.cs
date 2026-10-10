using Microsoft.AspNetCore.Http;

namespace GameGuild;

/// <summary>
///     Maps <see cref="Result" /> failures to RFC 7807 ProblemDetails responses.
///     Centralizes the error-to-HTTP mapping so controllers never need ad-hoc translation.
/// </summary>
public static class CustomResults
{
    /// <summary>
    ///     Converts a failed <see cref="Result" /> into an <see cref="IResult" /> ProblemDetails response.
    ///     Delegates to <see cref="ProblemDetailsMapper" /> for consistent error mapping (DRY).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the result is a success.</exception>
    public static IResult Problem(Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot create a problem result from a successful result.");
        }

        var pd = ProblemDetailsMapper.ToProblemDetails(result.Error);

        return Results.Problem(
            title: pd.Title,
            detail: pd.Detail,
            type: pd.Type,
            statusCode: pd.Status,
            extensions: pd.Extensions.Count > 0 ? pd.Extensions : null);
    }

    /// <summary>Maps an <see cref="ErrorType" /> to an HTTP status code.</summary>
    public static int GetStatusCode(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Problem => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.None => throw new InvalidOperationException("Cannot map ErrorType.None to an HTTP status code."),
            _ => StatusCodes.Status500InternalServerError
        };
    }

}
