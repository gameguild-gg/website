using Asp.Versioning;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;

namespace GameGuild.Learning.Courses;

/// <summary> Controller for managing program content with 3-layer DAC permissions Supports tenant-level, content-type-level, and resource-level permissions </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses/{programId}/content")]
[Authorize]
public class ProgramContentController(
  IProgramContentService contentService,
  IProgramCrudService programService,
  ICodingAssignmentContentService codingAssignmentService,
  IAuthorizationService authorizationService,
  IEnumerable<IProgramContentLearnerProjector> learnerProjectors,
  IEnumerable<IProgramContentAcademicMutationGuard> academicMutationGuards,
  ILogger<ProgramContentController> logger) : BaseApiController
{
  /// <summary> Get all content for a course with optional filtering (resource-level Read permission required on parent Program) </summary>
  /// <remarks>
  /// Supports filtering via query parameters:
  /// - level=top: Get only top-level content
  /// </remarks>
  [HttpGet(Name = "GetCoursesContent")]
  [AllowAnonymous]
  public async Task<ActionResult<IEnumerable<ProgramContentDto>>> GetProgramContent(Guid programId, [FromQuery] string? level = null)
  {
    var access = await ResolveContentAccessAsync(programId).ConfigureAwait(false);

    if (!access.HasAnyAccess)
    {
      return NotFound();
    }

    if (level == "top")
    {
      var topLevelContent = await contentService.GetTopLevelContentAsync(programId).ConfigureAwait(false);
      var topLevelDtos = topLevelContent.ToDtos().ToList();
      return Ok(ResolveProjectedContent(topLevelDtos, access));
    }

    var content = await contentService.GetContentByProgramAsync(programId).ConfigureAwait(false);
    var contentDtos = content.ToDtos().ToList();
    return Ok(ResolveProjectedContent(contentDtos, access));
  }

  /// <summary> Get specific program content by ID (resource-level Read permission required on parent Program) </summary>
  [HttpGet("{id}", Name = "GetCoursesContentById")]
  [AllowAnonymous]
  public async Task<ActionResult<ProgramContentDto>> GetContent(Guid programId, Guid id)
  {
    var access = await ResolveContentAccessAsync(programId).ConfigureAwait(false);
    if (!access.HasAnyAccess)
    {
      return NotFound();
    }

    var content = await contentService.GetContentByIdAsync(id).ConfigureAwait(false);

    if (content == null || content.ProgramId != programId) return NotFound();
    if (!access.CanManageContent && access.CanViewLearnerContent && content.Visibility == Visibility.Private) return NotFound();
    if (!access.CanManageContent && !access.CanViewLearnerContent && content.Visibility != Visibility.Public) return NotFound();

    var contentDto = content.ToDto();
    return Ok(ResolveProjectedContent([contentDto], access).Single());
  }

  /// <summary>Submit work for the current learner on a course content item.</summary>
  [HttpPost("{id}/submit")]
  public async Task<ActionResult<ContentInteractionDto>> SubmitContent(Guid programId, Guid id, [FromBody] SubmitUserContentDto submitDto)
  {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var currentUserId = GetCurrentUserId();
    if (currentUserId == null) return Unauthorized();
    if (!await AuthorizeCourseContentAsync(programId, Policies.CourseContentLearner).ConfigureAwait(false)) return Forbid();

    var content = await contentService.GetContentByIdAsync(id).ConfigureAwait(false);
    if (content is null || content.ProgramId != programId) return NotFound();
    try
    {
      ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Submit);
    }
    catch (InvalidOperationException exception)
    {
      return Conflict(exception.Message);
    }

    var interaction = await programService.SubmitUserContentAsync(programId, currentUserId.Value, id, submitDto.SubmissionData).ConfigureAwait(false);

    if (interaction == null) return NotFound();

    return Ok(interaction.ToDto());
  }

  /// <summary> Create new program content (resource-level Create permission required on parent Program) </summary>
  [HttpPost]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Create, "programId")]
  public async Task<ActionResult<ProgramContentDto>> CreateContent(Guid programId, [FromBody] CreateProgramContentDto createDto)
  {
    if (createDto.ProgramId != programId) return BadRequest("Program ID in URL must match Program ID in request body");

    var content = createDto.ToEntity();
    try
    {
      ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Authoring);
    }
    catch (InvalidOperationException exception)
    {
      return Conflict(exception.Message);
    }
    var createdContent = await contentService.CreateContentAsync(content).ConfigureAwait(false);
    var contentDto = createdContent.ToDto();

    return CreatedAtAction(nameof(GetContent), new { programId = createdContent.ProgramId, id = createdContent.Id }, contentDto);
  }

  /// <summary> Update program content (resource-level Edit permission required on parent Program) </summary>
  [HttpPut("{id}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "programId")]
  public async Task<ActionResult<ProgramContentDto>> UpdateContent(Guid programId, Guid id, [FromBody] UpdateProgramContentDto updateDto)
  {
    if (updateDto.Id != id) return BadRequest("Content ID in URL must match Content ID in request body");

    var existingContent = await contentService.GetContentByIdAsync(id).ConfigureAwait(false);

    if (existingContent == null || existingContent.ProgramId != programId) return NotFound();

    // Apply updates from DTO
    existingContent.ApplyUpdates(updateDto);
    try
    {
      ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, existingContent, ProgramContentAcademicMutation.Authoring);
    }
    catch (InvalidOperationException exception)
    {
      return Conflict(exception.Message);
    }

    var updatedContent = await contentService.UpdateContentAsync(existingContent).ConfigureAwait(false);
    var contentDto = updatedContent.ToDto();

    return Ok(contentDto);
  }

  /// <summary> Delete program content (resource-level Delete permission required on parent Program) </summary>
  [HttpDelete("{id}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Delete, "programId")]
  public async Task<ActionResult> DeleteContent(Guid programId, Guid id)
  {
    var content = await contentService.GetContentByIdAsync(id).ConfigureAwait(false);

    if (content == null || content.ProgramId != programId) return NotFound();

    try
    {
      ProgramContentAcademicMutationGuard.EnsureAllowed(academicMutationGuards, content, ProgramContentAcademicMutation.Delete);
    }
    catch (InvalidOperationException exception)
    {
      return Conflict(exception.Message);
    }

    var deleted = await contentService.DeleteContentAsync(id).ConfigureAwait(false);

    if (!deleted) return NotFound();

    return NoContent();
  }

  /// <summary> Get child content for a specific parent (resource-level Read permission required on parent Program) </summary>
  [HttpGet("{parentId}/children")]
  public async Task<ActionResult<IEnumerable<ProgramContentDto>>> GetChildContent(Guid programId, Guid parentId)
  {
    var access = await ResolveContentAccessAsync(programId).ConfigureAwait(false);
    if (!access.CanManageContent && !access.CanViewLearnerContent) return NotFound();

    // Verify parent belongs to the program
    var parent = await contentService.GetContentByIdAsync(parentId).ConfigureAwait(false);

    if (parent == null || parent.ProgramId != programId) return NotFound("Parent content not found or does not belong to this program");
    if (!access.CanManageContent && parent.Visibility == Visibility.Private) return NotFound();

    var children = await contentService.GetContentByParentAsync(parentId).ConfigureAwait(false);
    var childrenDtos = children.ToDtos();

    return Ok(ResolveProjectedContent(childrenDtos.ToList(), access));
  }

  /// <summary> Reorder content within a program (resource-level Edit permission required on parent Program) </summary>
  [HttpPost("reorder")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "programId")]
  public async Task<ActionResult> ReorderContent(Guid programId, [FromBody] ReorderContentDto reorderDto)
  {
    // Convert the simple list to (Id, SortOrder) tuples
    var newOrder = reorderDto.ContentIds.Select((id, index) => (id, index + 1)).ToList();
    var success = await contentService.ReorderContentAsync(programId, newOrder).ConfigureAwait(false);

    if (!success) return BadRequest("Failed to reorder content. Some content items may not exist.");

    return Ok();
  }

  /// <summary> Move content to a new parent/position (resource-level Edit permission required on parent Program) </summary>
  [HttpPost("{id}/move")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit, "programId")]
  public async Task<ActionResult> MoveContent(Guid programId, Guid id, [FromBody] MoveContentDto moveDto)
  {
    if (moveDto.ContentId != id) return BadRequest("Content ID in URL must match Content ID in request body");

    var content = await contentService.GetContentByIdAsync(id).ConfigureAwait(false);

    if (content == null || content.ProgramId != programId) return NotFound();

    var success = await contentService.MoveContentAsync(id, moveDto.NewParentId, moveDto.NewSortOrder).ConfigureAwait(false);

    if (!success) return BadRequest("Failed to move content");

    return Ok();
  }

  /// <summary> Get required content for a program (resource-level Read permission required on parent Program) </summary>
  [HttpGet("required")]
  public async Task<ActionResult<IEnumerable<ProgramContentDto>>> GetRequiredContent(Guid programId)
  {
    var access = await ResolveContentAccessAsync(programId).ConfigureAwait(false);
    if (!access.CanManageContent && !access.CanViewLearnerContent) return NotFound();

    var requiredContent = await contentService.GetRequiredContentAsync(programId).ConfigureAwait(false);
    var contentDtos = requiredContent.ToDtos();

    return Ok(ResolveProjectedContent(contentDtos.ToList(), access));
  }

  /// <summary> Get content by type (resource-level Read permission required on parent Program) </summary>
  [HttpGet("by-type/{type}")]
  public async Task<ActionResult<IEnumerable<ProgramContentDto>>> GetContentByType(Guid programId, ProgramContentType type)
  {
    var access = await ResolveContentAccessAsync(programId).ConfigureAwait(false);
    if (!access.CanManageContent && !access.CanViewLearnerContent) return NotFound();

    var content = await contentService.GetContentByTypeAsync(programId, type).ConfigureAwait(false);
    var contentDtos = content.ToDtos();

    return Ok(ResolveProjectedContent(contentDtos.ToList(), access));
  }

  /// <summary> Get content by visibility (resource-level Read permission required on parent Program) </summary>
  [HttpGet("by-visibility/{visibility}")]
  public async Task<ActionResult<IEnumerable<ProgramContentDto>>> GetContentByVisibility(Guid programId, Visibility visibility)
  {
    var access = await ResolveContentAccessAsync(programId).ConfigureAwait(false);
    if (!access.CanManageContent && !access.CanViewLearnerContent) return NotFound();

    // Learners cannot enumerate Private content even by explicit filter.
    if (!access.CanManageContent && visibility == Visibility.Private) return Ok(Array.Empty<ProgramContentDto>());

    var content = await contentService.GetContentByVisibilityAsync(programId, visibility).ConfigureAwait(false);
    var contentDtos = content.ToDtos();

    return Ok(ResolveProjectedContent(contentDtos.ToList(), access));
  }

  /// <summary> Search content within a program (resource-level Read permission required on parent Program) </summary>
  [HttpPost("search")]
  public async Task<ActionResult<IEnumerable<ProgramContentDto>>> SearchContent(Guid programId, [FromBody] SearchContentDto searchDto)
  {
    var access = await ResolveContentAccessAsync(programId).ConfigureAwait(false);
    if (!access.CanManageContent && !access.CanViewLearnerContent) return NotFound();
    if (searchDto.ProgramId != programId) return BadRequest("Program ID in URL must match Program ID in request body");

    var content = await contentService.SearchContentAsync(programId, searchDto.SearchTerm).ConfigureAwait(false);
    var contentDtos = content.ToDtos();

    return Ok(ResolveProjectedContent(contentDtos.ToList(), access));
  }

  /// <summary> Get content statistics for a program (resource-level Read permission required on parent Program) </summary>
  [HttpGet("stats")]
  public async Task<ActionResult<ContentStatsDto>> GetContentStats(Guid programId)
  {
    var access = await ResolveContentAccessAsync(programId).ConfigureAwait(false);
    if (!access.CanManageContent) return NotFound();

    var totalContent = await contentService.GetContentCountAsync(programId).ConfigureAwait(false);
    var requiredContent = await contentService.GetRequiredContentCountAsync(programId).ConfigureAwait(false);

    var stats = new ContentStatsDto { ProgramId = programId, TotalContent = totalContent, RequiredContent = requiredContent, OptionalContent = totalContent - requiredContent };

    return Ok(stats);
  }

  /// <summary> Student view of a coding assignment: Private tests stripped, Private files filtered out. </summary>
  [HttpGet("{id}/coding-assignment")]
  [Authorize]
  public async Task<ActionResult<CodingAssignmentContent>> GetCodingAssignmentPublic(Guid programId, Guid id)
  {
    var currentUserId = GetCurrentUserId();
    if (currentUserId == null) return Unauthorized();
    if (!await AuthorizeCourseContentAsync(programId, Policies.CourseContentLearner).ConfigureAwait(false)) return Forbid();

    var content = await codingAssignmentService.GetPublicAsync(programId, id, currentUserId.Value).ConfigureAwait(false);
    if (content == null) return NotFound();
    return Ok(content);
  }

  /// <summary> Instructor view of a coding assignment: full content including Private tests and files. </summary>
  [HttpGet("{id}/coding-assignment/full")]
  [Authorize]
  public async Task<ActionResult<CodingAssignmentContent>> GetCodingAssignmentFull(Guid programId, Guid id)
  {
    var currentUserId = GetCurrentUserId();
    if (currentUserId == null) return Unauthorized();
    if (!await AuthorizeCourseContentAsync(programId, Policies.CourseContentManage).ConfigureAwait(false)) return Forbid();

    var content = await codingAssignmentService.GetFullAsync(programId, id).ConfigureAwait(false);
    if (content == null) return NotFound();
    return Ok(content);
  }

  /// <summary> Author a coding assignment: UPSERT onto ProgramContent.JsonBody + sync grading to linked Assessment. </summary>
  [HttpPut("{id}/coding-assignment")]
  [Authorize]
  public async Task<ActionResult<CodingAssignmentContent>> PutCodingAssignment(
    Guid programId,
    Guid id,
    [FromBody] CodingAssignmentContent body)
  {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var currentUserId = GetCurrentUserId();
    if (currentUserId == null) return Unauthorized();
    if (!await AuthorizeCourseContentAsync(programId, Policies.CourseContentManage).ConfigureAwait(false)) return Forbid();

    var result = await codingAssignmentService.UpsertAsync(programId, id, body, currentUserId.Value).ConfigureAwait(false);
    if (!result.IsSuccess)
    {
      return result.Error.Type == ErrorType.NotFound
        ? NotFound(result.Error)
        : BadRequest(result.Error);
    }

    return Ok(result.Value);
  }

  private async Task<ContentAccessResolution> ResolveContentAccessAsync(Guid programId)
  {
    var program = await programService.GetProgramByIdAsync(programId).ConfigureAwait(false);
    if (program == null)
    {
      return ContentAccessResolution.None;
    }

    var viewAll = await authorizationService
      .AuthorizeAsync(User, program, Policies.CourseContentViewAll)
      .ConfigureAwait(false);
    if (viewAll.Succeeded)
    {
      return new ContentAccessResolution(true, true, true);
    }

    var learner = await authorizationService
      .AuthorizeAsync(User, program, Policies.CourseContentLearner)
      .ConfigureAwait(false);
    if (learner.Succeeded)
    {
      return new ContentAccessResolution(false, true, true);
    }

    var publicOutline = await authorizationService
      .AuthorizeAsync(User, program, Policies.CourseContentPublicOutline)
      .ConfigureAwait(false);

    return new ContentAccessResolution(false, false, publicOutline.Succeeded);
  }

  private async Task<bool> AuthorizeCourseContentAsync(Guid programId, string policy)
  {
    var program = new Program { Id = programId };
    var result = await authorizationService.AuthorizeAsync(User, program, policy).ConfigureAwait(false);
    return result.Succeeded;
  }

  private List<ProgramContentDto> ResolveProjectedContent(List<ProgramContentDto> content, ContentAccessResolution access)
  {
    if (access.CanManageContent) return content;
    if (access.CanViewLearnerContent) return ProjectLearnerContent(content);
    return SanitizePublicContent(content);
  }

  private List<ProgramContentDto> ProjectLearnerContent(IEnumerable<ProgramContentDto> content)
  {
    return content
      .Where(item => item.Visibility != Visibility.Private)
      .Select(item =>
      {
        var projector = learnerProjectors.SingleOrDefault(candidate => candidate.ContentType == item.Type);
        if (projector is not null && item.JsonBody.HasValue)
        {
          try
          {
            item.JsonBody = projector.Project(item.JsonBody.Value);
          }
          catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or InvalidOperationException)
          {
            logger.LogError(exception, "Learner-safe projection failed for content {ContentId}", item.Id);
            item.Body = null;
            item.JsonBody = null;
          }
        }
        item.Children = ProjectLearnerContent(item.Children);
        item.ChildrenCount = item.Children.Count;
        return item;
      })
      .ToList();
  }

  private static List<ProgramContentDto> ExcludePrivateContent(IEnumerable<ProgramContentDto> content)
  {
    return content
      .Where(item => item.Visibility != Visibility.Private)
      .Select(item =>
      {
        item.Children = ExcludePrivateContent(item.Children);
        item.ChildrenCount = item.Children.Count;
        return item;
      })
      .ToList();
  }

  private static List<ProgramContentDto> SanitizePublicContent(IEnumerable<ProgramContentDto> content)
  {
    return content
      .Where(item => item.Visibility == Visibility.Public)
      .Select(item =>
      {
        item.Body = null;
        item.JsonBody = null;
        item.Children = SanitizePublicContent(item.Children);
        item.ChildrenCount = item.Children.Count;
        return item;
      })
      .ToList();
  }

  private sealed record ContentAccessResolution(
    bool CanManageContent,
    bool CanViewLearnerContent,
    bool CanViewPublicOutline)
  {
    public static ContentAccessResolution None { get; } = new(false, false, false);
    public bool HasAnyAccess => CanManageContent || CanViewLearnerContent || CanViewPublicOutline;
  }

  private Guid? GetCurrentUserId()
  {
    var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("userId")?.Value;

    return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
  }
}
