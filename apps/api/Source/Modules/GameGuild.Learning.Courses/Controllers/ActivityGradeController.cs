using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace GameGuild.Learning.Courses;

/// <summary> Controller for managing activity grades with Program permission inheritance Follows permission chain: ActivityGrade → ContentInteraction → ProgramContent → Program All operations require Program-level permissions </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses/{programId}/activity-grades")]
[Authorize]
public class ActivityGradeController(IActivityGradeService activityGradeService, ISender sender) : BaseApiController {
  /// <summary> Grade a content interaction (Program-level Edit permission required) </summary>
  [HttpPost]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "programId")]
  public async Task<ActionResult<ActivityGradeDto>> GradeActivity(Guid programId, [FromBody] CreateActivityGradeDto gradeDto) {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    try {
      var grade = await sender.Send(new GradeActivityEndpointCommand(
        gradeDto.ContentInteractionId,
        gradeDto.GraderProgramUserId,
        gradeDto.Grade,
        gradeDto.Feedback,
        gradeDto.GradingDetails)).ConfigureAwait(false);

      // Verify the grade belongs to the specified program
      await ValidateGradeBelongsToProgram(grade.Id, programId).ConfigureAwait(false);

      return Ok(grade.ToDto());
    }
    catch (ArgumentException ex) { return BadRequest(ex.Message); }
    catch (InvalidOperationException ex) { return Conflict(ex.Message); }
  }

  /// <summary> Get grade for a specific content interaction (Program-level Read permission required) </summary>
  [HttpGet("interaction/{contentInteractionId}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read, "programId")]
  public async Task<ActionResult<ActivityGradeDto>> GetGrade(Guid programId, Guid contentInteractionId) {
    var grade = await activityGradeService.GetGradeAsync(contentInteractionId).ConfigureAwait(false);

    if (grade == null) return NotFound("Grade not found for this content interaction");

    // Verify the grade belongs to the specified program
    await ValidateGradeBelongsToProgram(grade.Id, programId).ConfigureAwait(false);

    return Ok(grade.ToDto());
  }

  /// <summary> Get all grades given by a specific grader (Program-level Read permission required) </summary>
  [HttpGet("grader/{graderProgramUserId}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read, "programId")]
  public async Task<ActionResult<IEnumerable<ActivityGradeDto>>> GetGradesByGrader(Guid programId, Guid graderProgramUserId) {
    var grades = await activityGradeService.GetGradesByGraderAsync(graderProgramUserId).ConfigureAwait(false);

    // Filter to only grades for this program (additional validation)
    var programGrades = grades.Where(g => g.ContentInteraction.Content.ProgramId == programId);

    return Ok(programGrades.ToDto());
  }

  /// <summary> Get all grades received by a specific student (Program-level Read permission required) </summary>
  [HttpGet("student/{programUserId}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read, "programId")]
  public async Task<ActionResult<IEnumerable<ActivityGradeDto>>> GetGradesByStudent(Guid programId, Guid programUserId) {
    var grades = await activityGradeService.GetGradesByStudentAsync(programUserId).ConfigureAwait(false);

    // Filter to only grades for this program (additional validation)
    var programGrades = grades.Where(g => g.ContentInteraction.Content.ProgramId == programId);

    return Ok(programGrades.ToDto());
  }

  /// <summary> Update an existing grade (Program-level Edit permission required) </summary>
  [HttpPut("{gradeId}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "programId")]
  public async Task<ActionResult<ActivityGradeDto>> UpdateGrade(Guid programId, Guid gradeId, [FromBody] UpdateActivityGradeDto updateDto) {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    // Verify the grade belongs to the specified program
    await ValidateGradeBelongsToProgram(gradeId, programId).ConfigureAwait(false);

    var updatedGrade = await sender.Send(new UpdateActivityGradeEndpointCommand(
      gradeId,
      updateDto.Grade,
      updateDto.Feedback,
      updateDto.GradingDetails)).ConfigureAwait(false);

    if (updatedGrade == null) return NotFound("Grade not found");

    return Ok(updatedGrade.ToDto());
  }

  /// <summary> Delete a grade (Program-level Delete permission required) </summary>
  [HttpDelete("{gradeId}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Delete, "programId")]
  public async Task<ActionResult> DeleteGrade(Guid programId, Guid gradeId) {
    // Verify the grade belongs to the specified program
    await ValidateGradeBelongsToProgram(gradeId, programId).ConfigureAwait(false);

    var deleted = await sender.Send(new DeleteActivityGradeEndpointCommand(gradeId)).ConfigureAwait(false);

    if (!deleted) return NotFound("Grade not found");

    return NoContent();
  }

  /// <summary> Get pending grades for a program (content interactions needing grading) (Program-level Read permission required) </summary>
  [HttpGet("pending")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read, "programId")]
  public async Task<ActionResult<IEnumerable<ContentInteractionDto>>> GetPendingGrades(Guid programId) {
    var pendingInteractions = await activityGradeService.GetPendingGradesAsync(programId).ConfigureAwait(false);

    return Ok(pendingInteractions.ToDto());
  }

  /// <summary> Get grade statistics for a program (Program-level Read permission required) </summary>
  [HttpGet("statistics")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read, "programId")]
  public async Task<ActionResult<GradeStatisticsDto>> GetGradeStatistics(Guid programId) {
    var statistics = await activityGradeService.GetGradeStatisticsAsync(programId).ConfigureAwait(false);

    return Ok(statistics.ToDto());
  }

  /// <summary> Get all grades for a specific content item (Program-level Read permission required) </summary>
  [HttpGet("content/{contentId}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read, "programId")]
  public async Task<ActionResult<IEnumerable<ActivityGradeDto>>> GetGradesByContent(Guid programId, Guid contentId) {
    var grades = await activityGradeService.GetGradesByContentAsync(contentId).ConfigureAwait(false);

    // Filter to only grades for this program (additional validation)
    var programGrades = grades.Where(g => g.ContentInteraction.Content.ProgramId == programId);

    return Ok(programGrades.ToDto());
  }

  /// <summary> Helper method to validate that a grade belongs to the specified program Implements the permission inheritance chain validation </summary>
  private async Task ValidateGradeBelongsToProgram(Guid gradeId, Guid programId) {
    var grade = await activityGradeService.GetGradeByIdAsync(gradeId).ConfigureAwait(false);

    if (grade?.ContentInteraction?.Content?.ProgramId != programId) throw new UnauthorizedAccessException($"Grade {gradeId} does not belong to program {programId}");
  }
}
