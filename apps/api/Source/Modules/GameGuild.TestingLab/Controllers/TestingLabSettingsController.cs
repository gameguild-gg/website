using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;


namespace GameGuild.TestingLab;

/// <summary> Controller for TestingLabSettings operations </summary>
[Route("api/testing-lab/settings")]
[Authorize]
public class TestingLabSettingsController(
  ITestingLabSettingsService settingsService,
  ISender sender,
  IActorContextAccessor actorContextAccessor,
  ILogger<TestingLabSettingsController> _logger
) : BaseApiController {
  private ActorContext Actor => actorContextAccessor.ActorContext;
  /// <summary> Get testing lab settings for the current tenant or global settings if no tenant context Creates default settings if none exist </summary>
  [HttpGet]
  [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Settings)]
  public async Task<ActionResult<TestingLabSettingsDto>> GetSettings() {
    var userId = Actor.SubjectIdAsGuid;
    if (userId == null) { return Unauthorized(new { message = "User ID claim not found in token" }); }

    // Use middleware-provided tenant context (nullable for global)
    var tenantId = Actor.TenantId;

    var settings = await settingsService.GetTestingLabSettingsDtoAsync(tenantId);

    return Ok(settings);
  }

  /// <summary> Create or update testing lab settings for the current tenant </summary>
  [HttpPut]
  [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Settings)]
  public async Task<ActionResult<TestingLabSettingsDto>> CreateOrUpdateSettings([FromBody] CreateTestingLabSettingsDto dto) {
    try {
      // Allow null tenant (global) – pass through nullable context
      var tenantId = Actor.TenantId; // may be null for global settings
      var settingsDto = await sender.Send(new CreateOrUpdateTestingLabSettingsEndpointCommand(tenantId, dto));

      return Ok(settingsDto);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning(ex, "Invalid settings data provided for tenant {TenantId}", Actor.TenantId);
      return BadRequest(new { message = "The provided settings data is invalid." });
    }
  }

  /// <summary> Update testing lab settings for the current tenant (partial update) </summary>
  [HttpPatch]
  [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Settings)]
  public async Task<ActionResult<TestingLabSettingsDto>> UpdateSettings([FromBody] UpdateTestingLabSettingsDto dto) {
    try {
      var tenantId = Actor.TenantId; // nullable allowed
      var settingsDto = await sender.Send(new UpdateTestingLabSettingsEndpointCommand(tenantId, dto));

      return Ok(settingsDto);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning(ex, "Invalid settings update data for tenant {TenantId}", Actor.TenantId);
      return BadRequest(new { message = "The provided settings update data is invalid." });
    }
  }

  /// <summary> Reset testing lab settings to default values for the current tenant </summary>
  [HttpPost("reset")]
  [RequireTestingLabPermission(TestingLabActions.Edit, TestingLabResourceTypes.Settings)]
  public async Task<ActionResult<TestingLabSettingsDto>> ResetSettings() {
    try {
      var tenantId = Actor.TenantId;
      var settingsDto = await sender.Send(new ResetTestingLabSettingsEndpointCommand(tenantId));

      return Ok(settingsDto);
    }
    catch (ArgumentException ex)
    {
      _logger.LogWarning(ex, "Error resetting settings for tenant {TenantId}", Actor.TenantId);
      return BadRequest(new { message = "An error occurred while resetting the settings." });
    }
  }

  /// <summary> Check if testing lab settings exist for the current tenant </summary>
  [HttpGet("exists")]
  [RequireTestingLabPermission(TestingLabActions.Read, TestingLabResourceTypes.Settings)]
  public async Task<ActionResult<bool>> SettingsExist() {
    var tenantId = Actor.TenantId;
    var exists = await settingsService.TestingLabSettingsExistAsync(tenantId);

    return Ok(exists);
  }
}
