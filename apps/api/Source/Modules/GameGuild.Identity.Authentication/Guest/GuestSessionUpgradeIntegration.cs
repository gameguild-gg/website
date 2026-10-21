namespace GameGuild.Identity.Authentication;

/// <summary>
///     Session-continuity helper for the guest upgrade path (issue #213). Sign-in and
///     sign-up handlers call this after a successful authenticated issuance: a presented
///     guest credential is upgraded into the fresh session and the response records the
///     continuation. An invalid, expired or already-upgraded guest token never blocks the
///     sign-in itself — the user still gets the authenticated session; only the continuity
///     link is dropped (and the rejection is audited inside the upgrade service).
/// </summary>
public static class GuestSessionUpgradeIntegration
{
    public static async Task<SignInResponse> ApplyGuestUpgradeAsync(
        SignInResponse response,
        IGuestSessionService guestSessionService,
        string? guestAccessToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(guestSessionService);

        if (!response.Success ||
            response.UserId == Guid.Empty ||
            response.SessionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(guestAccessToken))
        {
            return response;
        }

        var upgrade = await guestSessionService
            .UpgradeAsync(guestAccessToken, response.UserId, response.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (upgrade.Success)
        {
            response.UpgradedFromGuestSessionId = upgrade.GuestSessionId;
        }

        return response;
    }
}
