using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Courses;

[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses/{courseId:guid}/access")]
[Authorize]
public sealed class CourseAccessController(ICourseAccessEvaluator accessEvaluator) : BaseApiController
{
    [HttpGet("capabilities")]
    [ProducesResponseType<CourseAccessCapabilities>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseAccessCapabilities>> GetCapabilities(
        Guid courseId,
        CancellationToken cancellationToken)
    {
        var capabilities = await accessEvaluator
            .GetCapabilitiesAsync(courseId, cancellationToken)
            .ConfigureAwait(false);

        return capabilities.CourseExists ? Ok(capabilities) : NotFound();
    }
}
