using System.Net;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace GameGuild;

/// <summary>
///     Middleware for handling unhandled exceptions globally.
///     Implements secure error handling with proper 401/403 distinction, validation error reporting,
///     domain exception handling, and information leakage prevention.
///     All responses use typed <see cref="ProblemDetails"/> per RFC 7807 for consistency with
///     <see cref="ProblemDetailsMapper"/>.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (SecurityException securityException)
        {
            logger.LogWarning(securityException,
                "Security exception occurred. StatusCode: {StatusCode}, Path: {Path}, TraceId: {TraceId}, InternalMessage: {InternalMessage}",
                (int)securityException.StatusCode,
                LogRedaction.Sanitize(context.Request.Path.Value),
                context.TraceIdentifier,
                LogRedaction.Sanitize(securityException.InternalMessage));

            if (context.Response.HasStarted)
            {
                logger.LogWarning("Response has already started, cannot write error response for SecurityException");
                return;
            }

            await HandleSecurityExceptionAsync(context, securityException).ConfigureAwait(false);
        }
        catch (RequestValidationException validationException)
        {
            logger.LogWarning(validationException,
                "Validation failed. Path: {Path}, TraceId: {TraceId}, ErrorCount: {ErrorCount}",
                LogRedaction.Sanitize(context.Request.Path.Value),
                context.TraceIdentifier,
                validationException.Errors.Count);

            if (context.Response.HasStarted)
            {
                logger.LogWarning("Response has already started, cannot write error response for RequestValidationException");
                return;
            }

            await HandleRequestValidationExceptionAsync(context, validationException).ConfigureAwait(false);
        }
        catch (DomainException domainException)
        {
            logger.LogWarning(domainException,
                "Domain exception occurred. Path: {Path}, TraceId: {TraceId}",
                LogRedaction.Sanitize(context.Request.Path.Value),
                context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                logger.LogWarning("Response has already started, cannot write error response for DomainException");
                return;
            }

            await HandleDomainExceptionAsync(context, domainException).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An unhandled exception occurred");

            if (context.Response.HasStarted)
            {
                logger.LogWarning("Response has already started, cannot write error response for unhandled exception");
                return;
            }

            await HandleExceptionAsync(context, exception).ConfigureAwait(false);
        }
    }

    private static async Task HandleSecurityExceptionAsync(HttpContext context, SecurityException exception)
    {
        var statusCode = (int)exception.StatusCode;
        var problemDetails = new ProblemDetails
        {
            Type = GetProblemTypeUrl(exception.StatusCode),
            Title = GetProblemTitle(exception.StatusCode),
            Status = statusCode,
            Detail = exception.PublicMessage
        };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        await WriteProblemDetailsAsync(context, statusCode, problemDetails, exception).ConfigureAwait(false);
    }

    private static async Task HandleRequestValidationExceptionAsync(HttpContext context, RequestValidationException exception)
    {
        const int statusCode = StatusCodes.Status400BadRequest;
        var problemDetails = new ProblemDetails
        {
            Type = RfcUrls.BadRequest,
            Title = "Validation Failed",
            Status = statusCode,
            Detail = exception.Message
        };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        problemDetails.Extensions["errors"] = exception.Errors.Select(e => new
        {
            property = e.PropertyName,
            message = e.ErrorMessage,
            attemptedValue = e.AttemptedValue
        });

        await WriteProblemDetailsAsync(context, statusCode, problemDetails, exception).ConfigureAwait(false);
    }

    private static async Task HandleDomainExceptionAsync(HttpContext context, DomainException exception)
    {
        var statusCode = exception is EntityNotFoundException
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status422UnprocessableEntity;
        var problemDetails = new ProblemDetails
        {
            Type = statusCode == StatusCodes.Status404NotFound ? RfcUrls.NotFound : RfcUrls.UnprocessableEntity,
            Title = statusCode == StatusCodes.Status404NotFound ? "Resource Not Found" : "Domain Rule Violation",
            Status = statusCode,
            Detail = exception.Message
        };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        await WriteProblemDetailsAsync(context, statusCode, problemDetails, exception).ConfigureAwait(false);
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        const int statusCode = StatusCodes.Status500InternalServerError;
        // SECURITY: Never expose exception details to clients
        var problemDetails = new ProblemDetails
        {
            Type = RfcUrls.InternalServerError,
            Title = "Internal Server Error",
            Status = statusCode,
            Detail = "An unexpected error occurred. Please try again later."
        };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        await WriteProblemDetailsAsync(context, statusCode, problemDetails, exception).ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes a typed <see cref="ProblemDetails"/> as JSON using the shared serializer options (no per-call allocation).
    /// </summary>
    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        ProblemDetails problemDetails,
        Exception exception)
    {
        var response = context.Response;
        response.StatusCode = statusCode;

        // Use the configured ASP.NET Core Problem Details pipeline when available so exception
        // mappings, language, correlation IDs, detail levels, and custom extensions also apply to
        // exceptions handled by this middleware.
        var problemDetailsService = context.RequestServices?.GetService<IProblemDetailsService>();
        if (problemDetailsService is not null && await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problemDetails,
                Exception = exception,
            }).ConfigureAwait(false))
        {
            return;
        }

        response.ContentType = "application/problem+json";
        var jsonResponse = JsonSerializer.Serialize(problemDetails, SharedJsonOptions.Api);
        await response.WriteAsync(jsonResponse).ConfigureAwait(false);
    }

    private static string GetProblemTypeUrl(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => RfcUrls.Unauthorized,
        HttpStatusCode.Forbidden => RfcUrls.Forbidden,
        _ => RfcUrls.InternalServerError
    };

    private static string GetProblemTitle(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Unauthorized",
        HttpStatusCode.Forbidden => "Forbidden",
        _ => "Internal Server Error"
    };
}
