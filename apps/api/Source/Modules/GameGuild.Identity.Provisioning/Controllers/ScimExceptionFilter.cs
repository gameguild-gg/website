using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Converts <see cref="ScimException"/> into RFC 7644 §3.12 error bodies. Applied to
///     every SCIM controller so the global RFC 7807 middleware never masks a protocol
///     error as a 500.
/// </summary>
public sealed class ScimExceptionFilter : IExceptionFilter, IOrderedFilter
{
    // Run before the global exception handler so SCIM protocol errors keep their bodies.
    public int Order => int.MinValue;

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ScimException scimException)
        {
            return;
        }

        context.Result = new ObjectResult(ScimErrorBody.From(scimException))
        {
            StatusCode = scimException.Status
        };
        context.ExceptionHandled = true;
    }
}
