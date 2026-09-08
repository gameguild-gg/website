using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameGuild.Learning.Courses;

/// <summary>
/// REST API controller for program CRUD operations, search, filtering, analytics, monetization, and product integration.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses")]
[Authorize]
public class ProgramCrudController(
    IProgramCrudService programService,
    IActorContextAccessor actorContextAccessor,
    IPermissionQueryService permissionQueryService,
    ISender sender) : BaseApiController
{
  // ===== CONTENT-TYPE LEVEL OPERATIONS =====

  /// <summary> Get all courses with optional filtering (content-type level read permission). Non-manage actors are DAC-scoped to their own courses. </summary>
  [HttpGet]
  [RequireContentTypePermission<Program>(PermissionType.Read)]
  public async Task<ActionResult<IEnumerable<ProgramDto>>> GetPrograms(
      [FromQuery] string? status = null,
      [FromQuery] ProgramCategory? category = null,
      [FromQuery] ProgramDifficulty? difficulty = null,
      [FromQuery] Guid? creatorId = null,
      [FromQuery] string? q = null,
      [FromQuery] string? sort = null,
      [FromQuery] int skip = 0,
      [FromQuery] int take = 50)
  {
    var actor = actorContextAccessor.ActorContext;
    var canManageCatalog = await CanManageCatalogAsync(actor).ConfigureAwait(false);
    var selfId = actor.SubjectIdAsGuid;

    if (!canManageCatalog && selfId.HasValue)
    {
      // Non-manage actors are scoped to their own courses in every filter branch. An explicit
      // ?creatorId= pointing at someone else is ignored (conservative option: scoping to self
      // never confirms whether the probed id exists, unlike a 403 would).
      creatorId = selfId;
    }

    IEnumerable<Program> programs;
    if (!string.IsNullOrEmpty(q))
    {
      programs = await programService.SearchProgramsAsync(q, skip, take).ConfigureAwait(false);
    }
    else if (status == "published")
    {
      programs = await programService.GetPublishedProgramsAsync(skip, take).ConfigureAwait(false);
    }
    else if (category.HasValue)
    {
      programs = await programService.GetProgramsByCategoryAsync(category.Value, skip, take).ConfigureAwait(false);
    }
    else if (difficulty.HasValue)
    {
      programs = await programService.GetProgramsByDifficultyAsync(difficulty.Value, skip, take).ConfigureAwait(false);
    }
    else if (creatorId.HasValue)
    {
      programs = await programService.GetProgramsByCreatorAsync(creatorId.Value, skip, take).ConfigureAwait(false);
    }
    else if (sort == "popular")
    {
      programs = await programService.GetPopularProgramsAsync(take).ConfigureAwait(false);
    }
    else if (sort == "recent")
    {
      programs = await programService.GetRecentProgramsAsync(take).ConfigureAwait(false);
    }
    else
    {
      programs = await programService.GetProgramsAsync(skip, take).ConfigureAwait(false);
    }

    // Belt-and-suspenders for branches the service cannot scope query-level (q/status/category/difficulty/sort).
    if (!canManageCatalog)
    {
      programs = selfId.HasValue
          ? programs.Where(p => p.CreatorId == selfId.Value)
          : Enumerable.Empty<Program>();
    }

    return Ok(programs.ToDtos());
  }

  /// <summary>
  /// Catalog-wide manage capability: system admin, or a Program content-type Manage grant.
  /// Same actors and permission-name scheme as <see cref="ResourcePermissionAuthorizationFilter"/>
  /// content-type checks ("{ContentTypeName}.{PermissionType}") and AssessmentsController.CanManageCourseAsync.
  /// </summary>
  private async Task<bool> CanManageCatalogAsync(ActorContext actor)
  {
    if (actor.IsSystemAdmin) return true;
    if (!actor.SubjectIdAsGuid.HasValue || !actor.TenantId.HasValue) return false;

    return await permissionQueryService.HasTenantPermissionAsync(
        actor.SubjectIdAsGuid.Value,
        actor.TenantId,
        $"{nameof(Program)}.{PermissionType.Manage}").ConfigureAwait(false);
  }

  /// <summary> Get published public courses for the public catalog. </summary>
  [HttpGet("public")]
  [AllowAnonymous]
  public async Task<ActionResult<IEnumerable<ProgramDto>>> GetPublicPrograms(
      [FromQuery] int skip = 0,
      [FromQuery] int take = 50)
  {
    var programs = await programService.GetPublicPublishedProgramsAsync(skip, take).ConfigureAwait(false);
    return Ok(programs.ToDtos());
  }

  /// <summary> Get every course in which the current user has an active enrollment. </summary>
  [HttpGet("me")]
  public async Task<ActionResult<IEnumerable<ProgramDto>>> GetMyPrograms()
  {
    var currentUserId = GetCurrentUserId();
    if (!currentUserId.HasValue) return Unauthorized();

    var programs = await programService.GetUserProgramsAsync(currentUserId.Value).ConfigureAwait(false);
    return Ok(programs.ToDtos());
  }
  /// <summary> Create a new program (content-type level draft permission) </summary>
  [HttpPost]
  [RequireContentTypePermission<Program>(PermissionType.Draft)]
  public async Task<ActionResult<ProgramDto>> CreateProgram([FromBody] CreateProgramDto createDto)
  {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var currentUserId = GetCurrentUserId();
    if (!currentUserId.HasValue) return Unauthorized();

    var program = await sender.Send(new CreateProgramEndpointCommand(
      createDto with { CreatorId = currentUserId.Value })).ConfigureAwait(false);

    return CreatedAtAction(nameof(GetProgram), new { id = program.Id }, program.ToDto());
  }

  // ===== RESOURCE-LEVEL OPERATIONS =====

  /// <summary> Get a specific program by ID (resource-level read permission) </summary>
  [HttpGet("{id}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read)]
  public async Task<ActionResult<ProgramDto>> GetProgram(Guid id)
  {
    var program = await programService.GetProgramByIdAsync(id).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Get a specific program with all content included (resource-level read permission) </summary>
  [HttpGet("{id}/with-content")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read)]
  public async Task<ActionResult<ProgramDto>> GetProgramWithContent(Guid id)
  {
    var program = await programService.GetProgramWithContentAsync(id).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Update a program (resource-level edit permission) </summary>
  [HttpPut("{id}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult<ProgramDto>> UpdateProgram(Guid id, [FromBody] UpdateProgramDto updateDto)
  {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var program = await sender.Send(new UpdateProgramEndpointCommand(id, updateDto)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Delete a program (resource-level delete permission) </summary>
  [HttpDelete("{id}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Delete)]
  public async Task<ActionResult> DeleteProgram(Guid id)
  {
    var existingProgram = await programService.GetProgramByIdAsync(id).ConfigureAwait(false);

    if (existingProgram == null) return NotFound();

    await sender.Send(new DeleteProgramEndpointCommand(id)).ConfigureAwait(false);

    return NoContent();
  }

  /// <summary> Clone/duplicate a program (resource-level clone permission) </summary>
  [HttpPost("{id}:clone")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Clone)]
  public async Task<ActionResult<ProgramDto>> CloneProgram(Guid id, [FromBody] CloneProgramDto cloneDto)
  {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var program = await sender.Send(new CloneProgramEndpointCommand(id, cloneDto.NewTitle)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return CreatedAtAction(nameof(GetProgram), new { id = program.Id }, program.ToDto());
  }

  /// <summary> Get a specific program by slug (public access for published programs) </summary>
  [HttpGet("slug/{slug}")]
  [AllowAnonymous]
  public async Task<ActionResult<ProgramDto>> GetProgramBySlug(string slug)
  {
    var isAuthenticated = HttpContext.User.Identity?.IsAuthenticated == true;

    Program? program;

    if (isAuthenticated)
    {
      program = await programService.GetProgramBySlugAsync(slug).ConfigureAwait(false);
    }
    else
    {
      program = await programService.GetPublishedProgramBySlugAsync(slug).ConfigureAwait(false);
    }

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Self-enroll the current authenticated user in a published public course. </summary>
  [HttpPost("{id}:self-enroll")]
  public async Task<ActionResult<UserProgressDto>> SelfEnroll(Guid id)
  {
    var userId = GetCurrentUserId();
    if (!userId.HasValue) return Unauthorized();

    var program = await programService.GetProgramByIdAsync(id).ConfigureAwait(false);

    if (program == null || program.Status != ContentStatus.Published || program.Visibility != ContentVisibility.Public)
    {
      return NotFound();
    }

    if (!program.IsEnrollmentOpen)
    {
      return Conflict(new ProblemDetails
      {
        Title = "Enrollment closed",
        Detail = "This course is not currently open for self-enrollment."
      });
    }

    var linkedProducts = await programService.GetLinkedProductsAsync(id).ConfigureAwait(false);
    var pricing = await programService.GetProgramPricingAsync(id).ConfigureAwait(false);
    if (linkedProducts.Any() || pricing is { IsMonetizationEnabled: true, Price: > 0 })
    {
      return StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
      {
        Title = "Payment required",
        Detail = "This course requires checkout before enrollment."
      });
    }

    var progress = await sender.Send(new AddUserToProgramEndpointCommand(id, userId.Value)).ConfigureAwait(false);

    if (progress == null) return NotFound();

    return Ok(progress);
  }

  // ===== USER PARTICIPATION ENDPOINTS =====

  /// <summary> Add a user to a program (resource-level edit permission) </summary>
  [HttpPost("{id}/users/{userId}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult<UserProgressDto>> AddUserToProgram(Guid id, Guid userId)
  {
    var progress = await sender.Send(new AddUserToProgramEndpointCommand(id, userId)).ConfigureAwait(false);

    if (progress == null) return NotFound();

    return Ok(progress);
  }

  /// <summary> Remove a user from a program (resource-level edit permission) </summary>
  [HttpDelete("{id}/users/{userId}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult> RemoveUserFromProgram(Guid id, Guid userId)
  {
    var success = await sender.Send(new RemoveUserFromProgramEndpointCommand(id, userId)).ConfigureAwait(false);

    if (!success) return NotFound();

    return NoContent();
  }

  /// <summary> Get all users in a program (resource-level read permission) </summary>
  [HttpGet("{id}/users")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read)]
  public async Task<ActionResult<IEnumerable<UserProgressDto>>> GetProgramUsers(Guid id, [FromQuery] int skip = 0, [FromQuery] int take = 50)
  {
    var users = await programService.GetProgramUsersAsync(id, skip, take).ConfigureAwait(false);

    return Ok(users);
  }

  /// <summary> Get a specific user's progress in a program (resource-level read permission) </summary>
  [HttpGet("{id}/users/{userId}/progress")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read)]
  public async Task<ActionResult<UserProgressDto>> GetUserProgress(Guid id, Guid userId)
  {
    var progress = await programService.GetUserProgressDtoAsync(id, userId).ConfigureAwait(false);

    if (progress == null) return NotFound();

    return Ok(progress);
  }

  /// <summary> Get the current learner's progress in a program. </summary>
  [HttpGet("{id}/me/progress")]
  public async Task<ActionResult<UserProgressDto>> GetMyProgress(Guid id)
  {
    var currentUserId = GetCurrentUserId();
    if (currentUserId == null) return Unauthorized();

    var progress = await programService.GetUserProgressDtoAsync(id, currentUserId.Value).ConfigureAwait(false);

    if (progress == null) return NotFound();

    return Ok(progress);
  }

  /// <summary> Update a user's progress in a program (resource-level edit permission) </summary>
  [HttpPut("{id}/users/{userId}/progress")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult<UserProgressDto>> UpdateUserProgress(Guid id, Guid userId, [FromBody] UpdateProgressDto progressDto)
  {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var progress = await sender.Send(new UpdateUserProgressEndpointCommand(id, userId, progressDto)).ConfigureAwait(false);

    if (progress == null) return NotFound();

    return Ok(progress);
  }

  /// <summary> Update the current learner's progress in a program. </summary>
  [HttpPut("{id}/me/progress")]
  public async Task<ActionResult<UserProgressDto>> UpdateMyProgress(Guid id, [FromBody] UpdateProgressDto progressDto)
  {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var currentUserId = GetCurrentUserId();
    if (currentUserId == null) return Unauthorized();

    var progress = await sender.Send(new UpdateUserProgressEndpointCommand(id, currentUserId.Value, progressDto)).ConfigureAwait(false);

    if (progress == null) return NotFound();

    return Ok(progress);
  }

  /// <summary> Mark content as completed for a user (resource-level edit permission) </summary>
  [HttpPost("{id}/users/{userId}/content/{contentId}:complete")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult> MarkContentCompleted(Guid id, Guid userId, Guid contentId)
  {
    var success = await sender.Send(new MarkProgramContentCompletedEndpointCommand(id, userId, contentId)).ConfigureAwait(false);

    if (!success) return NotFound();

    return NoContent();
  }

  /// <summary> Mark content as completed for the current learner. </summary>
  [HttpPost("{id}/me/content/{contentId}:complete")]
  public async Task<ActionResult> MarkMyContentCompleted(Guid id, Guid contentId)
  {
    var currentUserId = GetCurrentUserId();
    if (currentUserId == null) return Unauthorized();

    var success = await sender.Send(new MarkProgramContentCompletedEndpointCommand(id, currentUserId.Value, contentId)).ConfigureAwait(false);

    if (!success) return NotFound();

    return NoContent();
  }

  /// <summary> Reset user progress in a program (resource-level edit permission) </summary>
  [HttpPost("{id}/users/{userId}:reset")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult> ResetUserProgress(Guid id, Guid userId)
  {
    var success = await sender.Send(new ResetUserProgressEndpointCommand(id, userId)).ConfigureAwait(false);

    if (!success) return NotFound();

    return NoContent();
  }

  private Guid? GetCurrentUserId()
  {
    var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("userId")?.Value;

    return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
  }

  // ===== MONETIZATION ENDPOINTS =====

  /// <summary> Enable monetization for a program (resource-level monetize permission) </summary>
  [HttpPost("{id}:monetize")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult<ProgramDto>> EnableMonetization(Guid id, [FromBody] MonetizationDto monetizationDto)
  {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var program = await sender.Send(new EnableProgramMonetizationEndpointCommand(id, monetizationDto)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Disable monetization for a program (resource-level monetize permission) </summary>
  [HttpPost("{id}:disable-monetization")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult<ProgramDto>> DisableMonetization(Guid id)
  {
    var program = await sender.Send(new DisableProgramMonetizationEndpointCommand(id)).ConfigureAwait(false);

    if (program == null) return NotFound();

    return Ok(program.ToDto());
  }

  /// <summary> Get program pricing information (resource-level read permission) </summary>
  [HttpGet("{id}/pricing")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read)]
  public async Task<ActionResult<PricingDto>> GetProgramPricing(Guid id)
  {
    var pricing = await programService.GetProgramPricingAsync(id).ConfigureAwait(false);

    if (pricing == null) return NotFound();

    return Ok(pricing);
  }

  /// <summary> Update program pricing (resource-level pricing permission) </summary>
  [HttpPut("{id}/pricing")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult<PricingDto>> UpdateProgramPricing(Guid id, [FromBody] UpdatePricingDto pricingDto)
  {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var pricing = await sender.Send(new UpdateProgramPricingEndpointCommand(id, pricingDto)).ConfigureAwait(false);

    if (pricing == null) return NotFound();

    return Ok(pricing);
  }

  // ===== ANALYTICS ENDPOINTS =====

  /// <summary> Get program analytics (resource-level analytics permission) </summary>
  [HttpGet("{id}/analytics")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Analytics)]
  public async Task<ActionResult<ProgramAnalyticsDto>> GetProgramAnalytics(Guid id)
  {
    var analytics = await programService.GetProgramAnalyticsAsync(id).ConfigureAwait(false);

    if (analytics == null) return NotFound();

    return Ok(analytics);
  }

  /// <summary> Get user completion rates for a program (resource-level analytics permission) </summary>
  [HttpGet("{id}/analytics/completion-rates")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Analytics)]
  public async Task<ActionResult<CompletionRatesDto>> GetCompletionRates(Guid id)
  {
    var rates = await programService.GetCompletionRatesAsync(id).ConfigureAwait(false);

    if (rates == null) return NotFound();

    return Ok(rates);
  }

  /// <summary> Get program engagement metrics (resource-level analytics permission) </summary>
  [HttpGet("{id}/analytics/engagement")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Analytics)]
  public async Task<ActionResult<EngagementMetricsDto>> GetEngagementMetrics(Guid id)
  {
    var metrics = await programService.GetEngagementMetricsAsync(id).ConfigureAwait(false);

    if (metrics == null) return NotFound();

    return Ok(metrics);
  }

  /// <summary> Get program revenue analytics (resource-level revenue permission) </summary>
  [HttpGet("{id}/analytics/revenue")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Read)]
  public async Task<ActionResult<RevenueAnalyticsDto>> GetRevenueAnalytics(Guid id)
  {
    var revenue = await programService.GetRevenueAnalyticsAsync(id).ConfigureAwait(false);

    if (revenue == null) return NotFound();

    return Ok(revenue);
  }

  // ===== PRODUCT INTEGRATION ENDPOINTS =====

  /// <summary> Create a product from a program (resource-level edit permission for program, content-type level draft permission for product) </summary>
  [HttpPost("{id}:create-product")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult<Guid>> CreateProductFromProgram(Guid id, [FromBody] CreateProductFromProgramDto productDto)
  {
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var productId = await sender.Send(new CreateProductFromProgramEndpointCommand(id, productDto)).ConfigureAwait(false);

    if (productId == null) return NotFound();

    return Ok(productId.Value);
  }

  /// <summary> Link a program to an existing product (resource-level edit permission) </summary>
  [HttpPost("{id}:link-product/{productId}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult> LinkProgramToProduct(Guid id, Guid productId)
  {
    var success = await sender.Send(new LinkProgramToProductEndpointCommand(id, productId)).ConfigureAwait(false);

    if (!success) return NotFound();

    return NoContent();
  }

  /// <summary> Unlink a program from a product (resource-level edit permission) </summary>
  [HttpDelete("{id}:unlink-product/{productId}")]
  [RequireResourcePermission<PermissionType, Program>(PermissionType.Edit)]
  public async Task<ActionResult> UnlinkProgramFromProduct(Guid id, Guid productId)
  {
    var success = await sender.Send(new UnlinkProgramFromProductEndpointCommand(id, productId)).ConfigureAwait(false);

    if (!success) return NotFound();

    return NoContent();
  }

  /// <summary> Get all products linked to a program (resource-level read permission) </summary>
  [HttpGet("{id}/products")]
  [AllowAnonymous]
  public async Task<ActionResult<IEnumerable<Guid>>> GetLinkedProducts(Guid id)
  {
    var program = await programService.GetProgramByIdAsync(id).ConfigureAwait(false);

    if (program == null || program.Status != ContentStatus.Published || program.Visibility != ContentVisibility.Public)
    {
      return NotFound();
    }

    var productIds = await programService.GetLinkedProductsAsync(id).ConfigureAwait(false);

    return Ok(productIds);
  }
}
