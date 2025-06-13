using System.Net;
using System.Text.Json;

namespace GameGuild.Common.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        HttpResponse response = context.Response;
        response.ContentType = "application/json";

        var errorResponse = new
        {
            message = exception.Message, statusCode = (int)HttpStatusCode.InternalServerError, timestamp = DateTime.UtcNow
        };

        response.StatusCode = (int)HttpStatusCode.InternalServerError;

        string jsonResponse = JsonSerializer.Serialize(errorResponse);
        await response.WriteAsync(jsonResponse);
    }
}
