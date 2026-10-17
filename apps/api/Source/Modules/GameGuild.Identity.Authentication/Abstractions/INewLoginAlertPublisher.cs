namespace GameGuild.Identity.Authentication;

/// <summary>
///     Records the informational <see cref="NewLoginDetectedV1"/> durable event after a
///     successful sign-in. Delivery is preference-gated host-side (default off), so this
///     publisher only durably records the fact of the sign-in; it never blocks authentication.
/// </summary>
public interface INewLoginAlertPublisher
{
    /// <summary>
    ///     Records the new-login event for the created session. Defense-in-depth: failures are
    ///     logged and swallowed.
    /// </summary>
    /// <param name="userId">The signed-in user.</param>
    /// <param name="tenantId">The resolved tenant, if any.</param>
    /// <param name="sessionId">The newly created session.</param>
    /// <param name="deviceSummary">Coarse parsed device summary (never the raw agent).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(
        Guid userId,
        Guid? tenantId,
        Guid sessionId,
        string? deviceSummary,
        CancellationToken cancellationToken = default);
}
