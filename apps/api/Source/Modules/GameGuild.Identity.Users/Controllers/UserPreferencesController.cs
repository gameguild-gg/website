using Asp.Versioning;
using GameGuild.Identity.Authorization;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Users;

/// <summary>
///     Controller for managing user preferences and settings
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("users/preferences")]
[Authorize]
public sealed class UserPreferencesController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Get user preferences
    /// </summary>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/preferences")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Get user preferences")]
    [ProducesResponseType<UserPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreferences(Guid userId, CancellationToken ct)
    {
        var query = new GetUserPreferencesQuery(userId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    ///     Partially update user preferences by user ID
    /// </summary>
    [HttpPatch("v{version:apiVersion}/users/{userId:guid}/preferences")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Partially update user preferences by user ID")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePreferences(Guid userId, [FromBody] UpdateUserPreferencesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var command = new UpdateUserPreferencesCommand(userId, body);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Replace user preferences by user ID
    /// </summary>
    [HttpPut("v{version:apiVersion}/users/{userId:guid}/preferences")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Replace user preferences by user ID")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplacePreferences(Guid userId, [FromBody] ReplaceUserPreferencesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var command = new ReplaceUserPreferencesCommand(userId, body);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Reset user preferences to defaults
    /// </summary>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/preferences:reset")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Reset user preferences to defaults")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPreferences(Guid userId, CancellationToken ct)
    {
        var command = new ResetUserPreferencesCommand(userId);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    // ========================================
    // NOTIFICATION PREFERENCES MANAGEMENT
    // ========================================

    /// <summary>
    ///     Check if notification preferences exist
    /// </summary>
    [HttpHead("v{version:apiVersion}/users/{userId:guid}/preferences/notifications")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Check if notification preferences exist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckNotificationPreferences(Guid userId, CancellationToken ct)
    {
        var query = new GetUserPreferencesQuery(userId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return result == null ? NotFound() : Ok();
    }

    /// <summary>
    ///     Get notification settings for user
    /// </summary>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/preferences/notifications")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Get notification settings for user")]
    [ProducesResponseType<UserNotificationPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotificationPreferences(Guid userId, CancellationToken ct)
    {
        var query = new GetUserPreferencesQuery(userId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return result?.NotificationPreferences == null ? NotFound() : Ok(result.NotificationPreferences);
    }

    /// <summary>
    ///     Replace notification preferences for user (full update)
    /// </summary>
    [HttpPut("v{version:apiVersion}/users/{userId:guid}/preferences/notifications")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Replace notification preferences for user (full update)")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceNotificationPreferences(Guid userId, [FromBody] ReplaceUserNotificationPreferencesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var command = new ReplaceUserNotificationPreferencesCommand(userId, body);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Partially update notification preferences for user
    /// </summary>
    [HttpPatch("v{version:apiVersion}/users/{userId:guid}/preferences/notifications")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Partially update notification preferences for user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateNotificationPreferences(Guid userId, [FromBody] UpdateUserNotificationPreferencesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var command = new UpdateUserNotificationPreferencesCommand(userId, body);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Reset notification preferences to defaults
    /// </summary>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/preferences/notifications:reset")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Reset notification preferences to defaults")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetNotificationPreferences(Guid userId, CancellationToken ct)
    {
        var command = new ResetUserNotificationPreferencesCommand(userId);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    // ========================================
    // ACCESSIBILITY PREFERENCES MANAGEMENT
    // ========================================

    /// <summary>
    ///     Check if accessibility preferences exist
    /// </summary>
    [HttpHead("v{version:apiVersion}/users/{userId:guid}/preferences/accessibility")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Check if accessibility preferences exist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckAccessibilityPreferences(Guid userId, CancellationToken ct)
    {
        var query = new GetUserPreferencesQuery(userId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return result == null ? NotFound() : Ok();
    }

    /// <summary>
    ///     Get accessibility settings for user
    /// </summary>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/preferences/accessibility")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Get accessibility settings for user")]
    [ProducesResponseType<UserAccessibilityPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccessibilityPreferences(Guid userId, CancellationToken ct)
    {
        var query = new GetUserPreferencesQuery(userId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return result?.AccessibilityPreferences == null ? NotFound() : Ok(result.AccessibilityPreferences);
    }

    /// <summary>
    ///     Replace accessibility preferences for user (full update)
    /// </summary>
    [HttpPut("v{version:apiVersion}/users/{userId:guid}/preferences/accessibility")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Replace accessibility preferences for user (full update)")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceAccessibilityPreferences(Guid userId, [FromBody] ReplaceUserAccessibilityPreferencesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var command = new ReplaceUserAccessibilityPreferencesCommand(userId, body);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Partially update accessibility preferences for user
    /// </summary>
    [HttpPatch("v{version:apiVersion}/users/{userId:guid}/preferences/accessibility")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Partially update accessibility preferences for user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAccessibilityPreferences(Guid userId, [FromBody] UpdateUserAccessibilityPreferencesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var command = new UpdateUserAccessibilityPreferencesCommand(userId, body);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Reset accessibility preferences to defaults
    /// </summary>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/preferences/accessibility:reset")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Reset accessibility preferences to defaults")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetAccessibilityPreferences(Guid userId, CancellationToken ct)
    {
        var command = new ResetUserAccessibilityPreferencesCommand(userId);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    // ========================================
    // PRIVACY PREFERENCES MANAGEMENT
    // ========================================

    /// <summary>
    ///     Check if privacy preferences exist
    /// </summary>
    [HttpHead("v{version:apiVersion}/users/{userId:guid}/preferences/privacy")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Check if privacy preferences exist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckPrivacyPreferences(Guid userId, CancellationToken ct)
    {
        var query = new GetUserPreferencesQuery(userId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return result == null ? NotFound() : Ok();
    }

    /// <summary>
    ///     Get privacy settings for user
    /// </summary>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/preferences/privacy")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Get privacy settings for user")]
    [ProducesResponseType<UserPrivacyPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPrivacyPreferences(Guid userId, CancellationToken ct)
    {
        var query = new GetUserPreferencesQuery(userId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return result?.PrivacyPreferences == null ? NotFound() : Ok(result.PrivacyPreferences);
    }

    /// <summary>
    ///     Replace privacy preferences for user (full update)
    /// </summary>
    [HttpPut("v{version:apiVersion}/users/{userId:guid}/preferences/privacy")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Replace privacy preferences for user (full update)")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplacePrivacyPreferences(Guid userId, [FromBody] ReplaceUserPrivacyPreferencesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var command = new ReplaceUserPrivacyPreferencesCommand(userId, body);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Partially update privacy preferences for user
    /// </summary>
    [HttpPatch("v{version:apiVersion}/users/{userId:guid}/preferences/privacy")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Partially update privacy preferences for user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePrivacyPreferences(Guid userId, [FromBody] UpdateUserPrivacyPreferencesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var command = new UpdateUserPrivacyPreferencesCommand(userId, body);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Reset privacy preferences to defaults
    /// </summary>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/preferences/privacy:reset")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Reset privacy preferences to defaults")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPrivacyPreferences(Guid userId, CancellationToken ct)
    {
        var command = new ResetUserPrivacyPreferencesCommand(userId);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    // ========================================
    // LOCALIZATION PREFERENCES MANAGEMENT
    // ========================================

    /// <summary>
    ///     Check if localization preferences exist
    /// </summary>
    [HttpHead("v{version:apiVersion}/users/{userId:guid}/preferences/localization")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Check if localization preferences exist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckLocalizationPreferences(Guid userId, CancellationToken ct)
    {
        var query = new GetUserPreferencesQuery(userId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return result == null ? NotFound() : Ok();
    }

    /// <summary>
    ///     Get localization settings for user
    /// </summary>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/preferences/localization")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Get localization settings for user")]
    [ProducesResponseType<UserLocalizationPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLocalizationPreferences(Guid userId, CancellationToken ct)
    {
        var query = new GetUserPreferencesQuery(userId);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        if (result == null)
            return NotFound();

        // Extract localization preferences from the preferences
        var localizationPrefs = result.LocalizationPreferences;

        // Build DTO from preferences dictionary
        var dto = new UserLocalizationPreferencesDto(
            JsonValueDictionary.GetString(localizationPrefs, "Language", "en-US") ?? "en-US",
            JsonValueDictionary.GetString(localizationPrefs, "Timezone", "UTC") ?? "UTC",
            JsonValueDictionary.GetString(localizationPrefs, "DateFormat", "MM/dd/yyyy") ?? "MM/dd/yyyy",
            JsonValueDictionary.GetString(localizationPrefs, "TimeFormat", "12h") ?? "12h",
            JsonValueDictionary.GetString(localizationPrefs, "Currency", "USD") ?? "USD",
            JsonValueDictionary.GetObjectMap(localizationPrefs, "NumberFormat"),
            JsonValueDictionary.GetObjectMap(localizationPrefs, "CustomSettings")
        );

        return Ok(dto);
    }

    /// <summary>
    ///     Replace localization preferences for user (full update)
    /// </summary>
    [HttpPut("v{version:apiVersion}/users/{userId:guid}/preferences/localization")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Replace localization preferences for user (full update)")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceLocalizationPreferences(Guid userId, [FromBody] ReplaceUserLocalizationPreferencesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var command = new ReplaceUserLocalizationPreferencesCommand(userId, body);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Partially update localization preferences for user
    /// </summary>
    [HttpPatch("v{version:apiVersion}/users/{userId:guid}/preferences/localization")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Partially update localization preferences for user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateLocalizationPreferences(Guid userId, [FromBody] UpdateUserLocalizationPreferencesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var command = new UpdateUserLocalizationPreferencesCommand(userId, body);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    ///     Reset localization preferences to defaults
    /// </summary>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/preferences/localization:reset")]
    [Authorize(Policy = Policies.UsersEditSelf)]
    [EndpointSummary("Reset localization preferences to defaults")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetLocalizationPreferences(Guid userId, CancellationToken ct)
    {
        var command = new ResetUserLocalizationPreferencesCommand(userId);
        await sender.Send(command, ct).ConfigureAwait(false);

        return NoContent();
    }
}
