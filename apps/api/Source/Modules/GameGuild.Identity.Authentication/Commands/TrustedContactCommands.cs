using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Designates a trusted contact for the acting account owner (issue #209). The
///     acting user identity is supplied by the controller from the authenticated
///     principal, never from the request body.
/// </summary>
public sealed class AddTrustedContactCommand : ICommand<TrustedContactActionResult>
{
    public Guid OwnerUserId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string? Relation { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>
///     Removes (revokes) one of the acting owner's trusted contacts; the contact can no
///     longer receive or redeem recovery codes.
/// </summary>
public sealed class RemoveTrustedContactCommand : ICommand<TrustedContactActionResult>
{
    public Guid OwnerUserId { get; init; }

    public Guid ContactId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>
///     Redeems a trusted-contact invite token (anonymous, email-verification style):
///     the contact proves control of the invited mailbox and becomes Confirmed.
/// </summary>
public sealed class ConfirmTrustedContactCommand : ICommand<TrustedContactConfirmResult>
{
    public string InviteToken { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>Outcome of a trusted-contact mutation by the owner.</summary>
public sealed class TrustedContactActionResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public TrustedContactDto? Contact { get; init; }
}

/// <summary>Outcome of an invite redemption; a denial never distinguishes token states.</summary>
public sealed class TrustedContactConfirmResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}

/// <summary>Owner-facing view of a trusted contact; never carries token material.</summary>
public sealed class TrustedContactDto
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string? Relation { get; init; }

    public TrustedContactStatus Status { get; init; }

    public DateTimeOffset InvitedAt { get; init; }

    public DateTimeOffset? ConfirmedAt { get; init; }
}
