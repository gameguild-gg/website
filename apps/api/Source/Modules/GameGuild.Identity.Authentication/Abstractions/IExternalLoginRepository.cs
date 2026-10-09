namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository for managing ExternalLogin links between GameGuild users and external identity providers.
/// </summary>
public interface IExternalLoginRepository
{
    /// <summary>
    ///     Looks up an external login by its provider and provider-scoped key.
    /// </summary>
    Task<ExternalLogin?> GetByProviderKeyAsync(string provider, string providerKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns all external logins linked to the given user.
    /// </summary>
    Task<List<ExternalLogin>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Inserts a new external login, or — if a row already exists for the same
    ///     (Provider, ProviderKey) — updates the linked UserId on the existing row.
    ///     Idempotent under repeated calls with the same (Provider, ProviderKey).
    ///     Sign-in flows only: authenticated linking must use <see cref="AddAsync" /> so a
    ///     concurrent winner's row can never be silently reassigned.
    /// </summary>
    Task<ExternalLogin> UpsertAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Insert-only: adds the row without reading first. Throws <c>DbUpdateException</c> when the
    ///     (Provider, ProviderKey) unique index rejects a concurrent duplicate — callers handle the
    ///     race by refetching. Never updates an existing row.
    /// </summary>
    Task<ExternalLogin> AddAsync(ExternalLogin externalLogin, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Hard-deletes the external login row for the given (provider, user).
    /// </summary>
    /// <returns>True when a row was removed; false when no such link exists.</returns>
    Task<bool> DeleteAsync(string provider, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Replaces the persisted granted-scope list for the given (provider, user) without
    ///     touching the consent stamp — used by per-scope revocation. Keeps the original
    ///     <see cref="ExternalLogin.ConsentedAt" /> so the first-consent record survives
    ///     partial revocations.
    /// </summary>
    /// <returns>The updated row, or null when no such link exists.</returns>
    Task<ExternalLogin?> UpdateGrantedScopesAsync(string provider, Guid userId, IReadOnlyList<string> grantedScopes, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records a fresh consent: replaces the granted-scope list AND stamps
    ///     <see cref="ExternalLogin.ConsentedAt" /> / <see cref="ExternalLogin.ConsentVersion" />
    ///     with the current terms. Used when a link is (re-)authorized with a scope set that
    ///     differs from the recorded one, or to backfill consent on a legacy row.
    /// </summary>
    /// <returns>The updated row, or null when no such link exists.</returns>
    Task<ExternalLogin?> RecordConsentAsync(string provider, Guid userId, IReadOnlyList<string> grantedScopes, CancellationToken cancellationToken = default);
}
