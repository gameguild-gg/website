using Asp.Versioning;
using GameGuild.Identity.Authorization;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Tenants;

/// <summary>
///     Controller for managing user's tenant memberships.
///     Provides user-centric view of tenant memberships (similar to Discord's "My Servers").
/// </summary>
/// <remarks>
///     This controller is in the Tenants module but serves user-centric endpoints
///     to maintain proper module boundaries while providing intuitive API paths.
/// </remarks>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("users/memberships")]
[Authorize]
public sealed class UserMembershipsController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     Add a user to a tenant membership.
    ///     Useful for assigning a user to a workspace they can actively switch into.
    /// </summary>
    /// <param name="userId">The user receiving the tenant membership</param>
    /// <param name="body">Membership details including tenant and role</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Membership creation result</returns>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/memberships")]
    [Authorize(Policy = Policies.TenantAdmin)]
    [EndpointSummary("Add a tenant membership for a user")]
    [EndpointDescription("Adds the specified user to a tenant with the requested role so the user can access that workspace.")]
    [ProducesResponseType<AddTenantMemberResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<AddTenantMemberResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AddTenantMemberResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddUserMembership(Guid userId, [FromBody] AddUserMembershipRequest body, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!CanManageTenant(body.TenantId) || !CanAssignRole(body.Role))
        {
            return Forbid();
        }

        var result = await sender.Send(
                new AddTenantMemberCommand(body.TenantId, userId, body.Role, body.InvitedByEmail, body.RequiresAcceptance, body.InviteeEmail, body.InviteeName),
                ct
            )
            .ConfigureAwait(false);

        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, result);
        }

        if (result.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
        {
            return NotFound(result);
        }

        return Conflict(result);
    }

    /// <summary>
    ///     Update a user's tenant role.
    ///     This is an operator/admin action used to promote or demote console access.
    /// </summary>
    /// <param name="userId">The member user ID.</param>
    /// <param name="tenantId">The tenant/workspace where the role applies.</param>
    /// <param name="body">The new role payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Role update result.</returns>
    [HttpPatch("v{version:apiVersion}/users/{userId:guid}/memberships/{tenantId:guid}/role")]
    [Authorize(Policy = Policies.TenantAdmin)]
    [EndpointSummary("Update tenant membership role")]
    [EndpointDescription("Updates the user's role in the specified tenant/workspace. Use this for console promotion/demotion flows.")]
    [ProducesResponseType<UpdateTenantMemberRoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<UpdateTenantMemberRoleResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserMembershipRole(
        Guid userId,
        Guid tenantId,
        [FromBody] UpdateUserMembershipRoleRequest body,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!CanManageTenant(tenantId) || !CanAssignRole(body.Role))
        {
            return Forbid();
        }

        var result = await sender.Send(
                new UpdateTenantMemberRoleCommand(tenantId, userId, body.Role),
                ct)
            .ConfigureAwait(false);

        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("v{version:apiVersion}/users/{userId:guid}/memberships/{tenantId:guid}:deactivate")]
    [Authorize(Policy = Policies.TenantAdmin)]
    [EndpointSummary("Deactivate a tenant membership")]
    [EndpointDescription("Suspends access to the specified tenant without deleting membership history.")]
    [ProducesResponseType<SetTenantMembershipStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SetTenantMembershipStatusResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<SetTenantMembershipStatusResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateUserMembership(
        Guid userId,
        Guid tenantId,
        [FromBody] SetTenantMembershipStatusRequest? body,
        CancellationToken ct = default)
    {
        if (!CanManageTenant(tenantId))
        {
            return Forbid();
        }

        var result = await sender.Send(
                new SetTenantMembershipStatusCommand(tenantId, userId, false, body?.Reason),
                ct)
            .ConfigureAwait(false);

        return ToMembershipStatusResult(result);
    }

    [HttpPost("v{version:apiVersion}/users/{userId:guid}/memberships/{tenantId:guid}:activate")]
    [Authorize(Policy = Policies.TenantAdmin)]
    [EndpointSummary("Activate a tenant membership")]
    [EndpointDescription("Restores access to the specified tenant membership.")]
    [ProducesResponseType<SetTenantMembershipStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SetTenantMembershipStatusResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateUserMembership(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (!CanManageTenant(tenantId))
        {
            return Forbid();
        }

        var result = await sender.Send(
                new SetTenantMembershipStatusCommand(tenantId, userId, true),
                ct)
            .ConfigureAwait(false);

        return ToMembershipStatusResult(result);
    }

    /// <summary>
    ///     Resend a pending membership invite.
    /// </summary>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/memberships/{tenantId:guid}/invite:resend")]
    [Authorize(Policy = Policies.TenantAdmin)]
    [EndpointSummary("Resend tenant membership invite")]
    [ProducesResponseType<UpdateTenantMemberInviteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<UpdateTenantMemberInviteResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<UpdateTenantMemberInviteResponse>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> ResendUserMembershipInvite(Guid userId, Guid tenantId, [FromBody] UpdateUserMembershipInviteRequest? body = null, CancellationToken ct = default)
    {
        return UpdateInvite(userId, tenantId, TenantMemberInviteAction.Resend, body, ct);
    }

    /// <summary>
    ///     Cancel a pending membership invite without deleting the audit trail.
    /// </summary>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/memberships/{tenantId:guid}/invite:cancel")]
    [Authorize(Policy = Policies.TenantAdmin)]
    [EndpointSummary("Cancel tenant membership invite")]
    [ProducesResponseType<UpdateTenantMemberInviteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<UpdateTenantMemberInviteResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<UpdateTenantMemberInviteResponse>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> CancelUserMembershipInvite(Guid userId, Guid tenantId, [FromBody] UpdateUserMembershipInviteRequest? body = null, CancellationToken ct = default)
    {
        return UpdateInvite(userId, tenantId, TenantMemberInviteAction.Cancel, body, ct);
    }

    /// <summary>
    ///     Accept a pending membership invite and activate the membership.
    /// </summary>
    [HttpPost("v{version:apiVersion}/users/{userId:guid}/memberships/{tenantId:guid}/invite:accept")]
    [Authorize]
    [EndpointSummary("Accept tenant membership invite")]
    [ProducesResponseType<UpdateTenantMemberInviteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<UpdateTenantMemberInviteResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<UpdateTenantMemberInviteResponse>(StatusCodes.Status409Conflict)]
    public Task<IActionResult> AcceptUserMembershipInvite(Guid userId, Guid tenantId, [FromBody] UpdateUserMembershipInviteRequest? body = null, CancellationToken ct = default)
    {
        return UpdateInvite(userId, tenantId, TenantMemberInviteAction.Accept, body, ct);
    }

    private async Task<IActionResult> UpdateInvite(
        Guid userId,
        Guid tenantId,
        TenantMemberInviteAction action,
        UpdateUserMembershipInviteRequest? body,
        CancellationToken ct)
    {
        if (!CanUpdateInvite(userId, tenantId, action))
        {
            return Forbid();
        }

        var result = await sender.Send(
                new UpdateTenantMemberInviteCommand(tenantId, userId, action, body?.ActorEmail),
                ct)
            .ConfigureAwait(false);

        if (result.Success)
        {
            return Ok(result);
        }

        if (result.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
        {
            return NotFound(result);
        }

        return Conflict(result);
    }

    /// <summary>
    ///     Get all tenant memberships for a user.
    ///     Returns a list of all tenants the user belongs to with their role and status.
    ///     Similar to Discord's server list showing which servers you're a member of.
    /// </summary>
    /// <param name="userId">The user ID to get memberships for</param>
    /// <param name="includeInactive">Include inactive/left memberships (default: false)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of tenant memberships for the user</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/memberships")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Get all tenant memberships for a user")]
    [EndpointDescription("Returns all tenants the user belongs to, with role and membership status. Similar to Discord's 'My Servers' view.")]
    [ProducesResponseType<GetUserMembershipsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserMemberships(
        Guid userId,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        if (!CanReadMemberships(userId))
        {
            return Forbid();
        }

        var query = new GetUserMembershipsQuery(userId, includeInactive);
        var result = await sender.Send(query, ct).ConfigureAwait(false);

        return Ok(ScopeMemberships(result, userId));
    }

    /// <summary>
    ///     Check if user has any memberships
    /// </summary>
    /// <param name="userId">The user ID to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>200 OK if user has memberships, 404 if none</returns>
    [HttpHead("v{version:apiVersion}/users/{userId:guid}/memberships")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Check if user has any tenant memberships")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckUserHasMemberships(Guid userId, CancellationToken ct = default)
    {
        if (!CanReadMemberships(userId))
        {
            return Forbid();
        }

        var query = new GetUserMembershipsQuery(userId, IncludeInactive: false);
        var result = await sender.Send(query, ct).ConfigureAwait(false);
        var scoped = ScopeMemberships(result, userId);

        return scoped.TotalCount > 0 ? Ok() : NotFound();
    }

    /// <summary>
    ///     Get count of user's active memberships
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Count of active memberships</returns>
    [HttpGet("v{version:apiVersion}/users/{userId:guid}/memberships:count")]
    [Authorize(Policy = Policies.UsersReadSelf)]
    [EndpointSummary("Get count of user's active tenant memberships")]
    [ProducesResponseType<MembershipCountResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembershipCount(Guid userId, CancellationToken ct = default)
    {
        if (!CanReadMemberships(userId))
        {
            return Forbid();
        }

        var query = new GetUserMembershipsQuery(userId, IncludeInactive: false);
        var result = await sender.Send(query, ct).ConfigureAwait(false);
        var scoped = ScopeMemberships(result, userId);

        return Ok(new MembershipCountResponse { Count = scoped.TotalCount });
    }

    private bool CanManageTenant(Guid tenantId)
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsSystemAdmin ||
               actor.IsTenantAdmin && actor.TenantId.HasValue && actor.TenantId.Value == tenantId;
    }

    private bool CanUpdateInvite(Guid userId, Guid tenantId, TenantMemberInviteAction action)
    {
        var actor = actorContextAccessor.ActorContext;
        return CanManageTenant(tenantId) ||
               action == TenantMemberInviteAction.Accept && actor.SubjectIdAsGuid == userId;
    }

    private bool CanAssignRole(string role)
    {
        var actor = actorContextAccessor.ActorContext;
        return !string.Equals(role, "SystemAdmin", StringComparison.OrdinalIgnoreCase) || actor.IsSystemAdmin;
    }

    private bool CanReadMemberships(Guid userId)
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsSystemAdmin ||
               actor.SubjectIdAsGuid == userId ||
               actor.IsTenantAdmin && actor.TenantId.HasValue;
    }

    private GetUserMembershipsResponse ScopeMemberships(GetUserMembershipsResponse result, Guid userId)
    {
        var actor = actorContextAccessor.ActorContext;
        if (actor.IsSystemAdmin || actor.SubjectIdAsGuid == userId)
        {
            return result;
        }

        var memberships = result.Memberships
            .Where(membership => membership.TenantId == actor.TenantId)
            .ToList();
        return new GetUserMembershipsResponse
        {
            Memberships = memberships,
            TotalCount = memberships.Count
        };
    }

    private IActionResult ToMembershipStatusResult(SetTenantMembershipStatusResponse result)
    {
        if (result.Success)
        {
            return Ok(result);
        }

        return result.NotFound ? NotFound(result) : Conflict(result);
    }
}

/// <summary>
///     Response containing membership count
/// </summary>
public sealed record MembershipCountResponse
{
    /// <summary>
    ///     Number of active memberships
    /// </summary>
    public int Count { get; init; }
}

/// <summary>
///     Request body for adding a user membership.
/// </summary>
public sealed record AddUserMembershipRequest
{
    /// <summary>
    ///     Tenant to join.
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    ///     Role assigned in the tenant.
    /// </summary>
    public string Role { get; init; } = "Member";

    /// <summary>
    ///     Optional inviter identifier for audit trail purposes.
    /// </summary>
    public string? InvitedByEmail { get; init; }

    /// <summary>
    ///     Whether the invited user must accept the membership before gaining active access.
    /// </summary>
    public bool RequiresAcceptance { get; init; }

    /// <summary>
    ///     Email of the user receiving an invite, used for delivery and audit metadata.
    /// </summary>
    public string? InviteeEmail { get; init; }

    /// <summary>
    ///     Display name of the user receiving an invite.
    /// </summary>
    public string? InviteeName { get; init; }
}

/// <summary>
///     Request body for membership invite actions.
/// </summary>
public sealed record UpdateUserMembershipInviteRequest
{
    /// <summary>
    ///     Optional operator email for audit trail purposes.
    /// </summary>
    public string? ActorEmail { get; init; }
}

/// <summary>
///     Request body for changing a user's tenant role.
/// </summary>
public sealed record UpdateUserMembershipRoleRequest
{
    /// <summary>
    ///     New role assigned in the tenant.
    /// </summary>
    public string Role { get; init; } = "Member";
}

public sealed record SetTenantMembershipStatusRequest
{
    public string? Reason { get; init; }
}
