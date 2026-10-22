using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command issuing an anonymous guest session (issue #213). Acting identity is nobody:
///     issuance creates the guest principal itself, and the request context (IP, user agent)
///     is captured server-side, never from the body.
/// </summary>
public sealed class IssueGuestSessionCommand : ICommand<GuestSessionResponse>
{
    /// <summary>Optional client device fingerprint, stored as operational metadata.</summary>
    public string? DeviceFingerprint { get; init; }
}
