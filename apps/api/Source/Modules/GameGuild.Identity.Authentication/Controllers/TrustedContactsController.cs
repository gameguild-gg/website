using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Trusted-contact management for delegated account recovery (issue #209). Listing,
///     adding and removing require the owner's session; the invite confirmation is
///     anonymous and rests on the single-use emailed token (email-verification style).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth/trusted-contacts")]
[Tags("auth/trusted-contacts")]
[Authorize]
public sealed class TrustedContactsController(
    ITrustedContactStore contactStore,
    ISender sender) : AuthControllerBase
{
    /// <summary>
    ///     Lists the current user's trusted contacts (active invites and confirmed contacts).
    /// </summary>
    [HttpGet]
    [EndpointSummary("List trusted contacts")]
    [EndpointDescription("Returns the acting user's trusted contacts for account recovery. Never includes invite tokens.")]
    [ProducesResponseType<List<TrustedContactDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListTrustedContacts(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var contacts = await contactStore.GetActiveByOwnerAsync(userId, ct).ConfigureAwait(false);
        return Ok(contacts.Select(ToDto).ToList());
    }

    /// <summary>
    ///     Designates a trusted contact and emails them a single-use invite.
    /// </summary>
    /// <param name="body">Contact email and optional relation</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost]
    [EndpointSummary("Add a trusted contact")]
    [EndpointDescription("Designates a trusted recovery contact for the acting user and sends a single-use confirmation invite. The contact must confirm before receiving recovery codes.")]
    [ProducesResponseType<TrustedContactActionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddTrustedContact([FromBody] AddTrustedContactRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var userId = GetCurrentUserId();
        var result = await sender.Send(new AddTrustedContactCommand
        {
            OwnerUserId = userId,
            Email = body.Email,
            Relation = body.Relation,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct).ConfigureAwait(false);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    ///     Redeems a trusted-contact invite token; the contact confirms participation.
    /// </summary>
    /// <param name="body">Invite token from the email</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generic outcome; does not reveal token state</returns>
    [AllowAnonymous]
    [HttpPost("confirm")]
    [EndpointSummary("Confirm a trusted-contact invite")]
    [EndpointDescription("Consumes the single-use, expiring invite token emailed to the contact. Denials are generic and never reveal token state.")]
    [ProducesResponseType<TrustedContactConfirmResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmTrustedContact([FromBody] ConfirmTrustedContactRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new ConfirmTrustedContactCommand
        {
            InviteToken = body.InviteToken,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Removes (revokes) one of the current user's trusted contacts.
    /// </summary>
    /// <param name="contactId">Contact identifier</param>
    /// <param name="ct">Cancellation token</param>
    [HttpDelete("{contactId:guid}")]
    [EndpointSummary("Remove a trusted contact")]
    [EndpointDescription("Revokes one of the acting user's trusted contacts. The contact immediately stops counting toward recovery thresholds.")]
    [ProducesResponseType<TrustedContactActionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveTrustedContact(Guid contactId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new RemoveTrustedContactCommand
        {
            OwnerUserId = userId,
            ContactId = contactId,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        }, ct).ConfigureAwait(false);

        return result.Success ? Ok(result) : NotFound(result);
    }

    private static TrustedContactDto ToDto(TrustedContact contact) => new()
    {
        Id = contact.Id,
        Email = contact.Email,
        Relation = contact.Relation,
        Status = contact.Status,
        InvitedAt = contact.InvitedAt,
        ConfirmedAt = contact.ConfirmedAt
    };
}

public sealed record AddTrustedContactRequest
{
    /// <summary>Email of the contact to designate.</summary>
    public required string Email { get; init; }

    /// <summary>Optional free-form relation to the owner.</summary>
    public string? Relation { get; init; }
}

public sealed record ConfirmTrustedContactRequest
{
    /// <summary>Invite token received by email.</summary>
    public required string InviteToken { get; init; }
}
