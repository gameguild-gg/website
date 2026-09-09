using System.Security.Claims;
using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Commerce.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Courses;

[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses")]
[Authorize]
public sealed class CourseCheckoutController(
    ISender sender) : ControllerBase
{
    [HttpPost("{courseId:guid}/checkout/complete")]
    public async Task<ActionResult<CompleteCourseCheckoutResponse>> CompleteCheckout(
        Guid courseId,
        [FromBody] CompleteCourseCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var outcome = await sender.Send(new CompleteCourseCheckoutCommand(
            courseId,
            userId.Value,
            request.ProductId,
            request.PaymentProviderReference), cancellationToken).ConfigureAwait(false);

        if (outcome.Response is not null) return Ok(outcome.Response);
        return outcome.StatusCode switch
        {
            StatusCodes.Status404NotFound => NotFound(outcome.Problem),
            StatusCodes.Status409Conflict => Conflict(outcome.Problem),
            _ => BadRequest(outcome.Problem),
        };
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("userId")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public sealed record CompleteCourseCheckoutRequest(
    Guid ProductId,
    string? PaymentProviderReference = null,
    string? PaymentMethod = null);

public sealed record CompleteCourseCheckoutResponse(
    Guid CourseId,
    Guid ProductId,
    Guid EntitlementId,
    IReadOnlyList<Guid> EnrollmentIds,
    bool AlreadyHadAccess,
    decimal Amount,
    string Currency,
    string LearningUrl,
    string? PaymentProviderReference);
