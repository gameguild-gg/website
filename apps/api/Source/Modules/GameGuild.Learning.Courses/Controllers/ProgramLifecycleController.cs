using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Courses;

/// <summary>
/// REST API controller for program lifecycle management:
/// submit, approve, reject, withdraw, archive, restore, publish, unpublish, schedule.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses")]
[Authorize]
public class ProgramLifecycleController(ISender sender) : BaseApiController {

  /// <summary> Submit a program for review (resource-level submit permission) </summary>
  [HttpPost("{id}:submit")]
  [RequireCourseCapability(CourseCapability.Publish)]
  public async Task<ActionResult<ProgramDto>> SubmitProgram(Guid id) {
    var program = await sender.Send(new SubmitProgramLifecycleCommand(id)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Approve a program (resource-level approve permission) </summary>
  [HttpPost("{id}:approve")]
  [RequireCourseCapability(CourseCapability.Publish)]
  public async Task<ActionResult<ProgramDto>> ApproveProgram(Guid id) {
    var program = await sender.Send(new ApproveProgramLifecycleCommand(id)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Reject a program (resource-level reject permission) </summary>
  [HttpPost("{id}:reject")]
  [RequireCourseCapability(CourseCapability.Publish)]
  public async Task<ActionResult<ProgramDto>> RejectProgram(Guid id, [FromBody] RejectProgramDto rejectDto) {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var program = await sender.Send(new RejectProgramLifecycleCommand(id, rejectDto.Reason)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Withdraw a program from review (resource-level withdraw permission) </summary>
  [HttpPost("{id}:withdraw")]
  [RequireCourseCapability(CourseCapability.Publish)]
  public async Task<ActionResult<ProgramDto>> WithdrawProgram(Guid id) {
    var program = await sender.Send(new WithdrawProgramLifecycleCommand(id)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Archive a program (resource-level archive permission) </summary>
  [HttpPost("{id}:archive")]
  [RequireCourseCapability(CourseCapability.Publish)]
  public async Task<ActionResult<ProgramDto>> ArchiveProgram(Guid id) {
    var program = await sender.Send(new ArchiveProgramLifecycleCommand(id)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Restore an archived program (resource-level restore permission) </summary>
  [HttpPost("{id}:restore")]
  [RequireCourseCapability(CourseCapability.Publish)]
  public async Task<ActionResult<ProgramDto>> RestoreProgram(Guid id) {
    var program = await sender.Send(new RestoreProgramLifecycleCommand(id)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Publish a program (resource-level publish permission) </summary>
  [HttpPost("{id}:publish")]
  [RequireCourseCapability(CourseCapability.Publish)]
  public async Task<ActionResult<ProgramDto>> PublishProgram(Guid id) {
    var program = await sender.Send(new PublishProgramLifecycleCommand(id)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Unpublish a program (resource-level unpublish permission) </summary>
  [HttpPost("{id}:unpublish")]
  [RequireCourseCapability(CourseCapability.Publish)]
  public async Task<ActionResult<ProgramDto>> UnpublishProgram(Guid id) {
    var program = await sender.Send(new UnpublishProgramLifecycleCommand(id)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Schedule a program for publishing (resource-level schedule permission) </summary>
  [HttpPost("{id}:schedule")]
  [RequireCourseCapability(CourseCapability.Publish)]
  public async Task<ActionResult<ProgramDto>> ScheduleProgram(Guid id, [FromBody] ScheduleProgramDto scheduleDto) {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var program = await sender.Send(new ScheduleProgramLifecycleCommand(id, scheduleDto.PublishAt)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }
}
