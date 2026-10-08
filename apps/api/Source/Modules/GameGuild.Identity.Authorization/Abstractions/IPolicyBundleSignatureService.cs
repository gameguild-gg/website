namespace GameGuild.Identity.Authorization;

/// <summary>
///     Signs policy bundles and verifies policy bundle signatures using the trusted
///     signing-key registry from <see cref="PolicyBundleSigningOptions"/>.
/// </summary>
/// <remarks>
///     <para>Contract: ECDSA P-256 with SHA-256, a versioned P1363 (IEEE r||s) signature
///     envelope and a SHA-256 content hash of the canonical signed payload.</para>
///     <para>The canonical signed payload binds bundle identity, tenant/global scope,
///     policy data and metadata, version lineage, effective dates and creator. The signature
///     additionally binds the signer key id and the signing time.</para>
///     <para>All validation paths fail closed: invalid JSON, duplicate object properties,
///     inconsistent tenant/global scope, invalid dates or a bundle larger than the configured
///     limit are rejected for signing and make verification invalid.</para>
/// </remarks>
public interface IPolicyBundleSignatureService
{
    /// <summary>
    ///     Validates bundle inputs against the signing contract without signing.
    /// </summary>
    /// <param name="bundle">The bundle to validate.</param>
    /// <returns>The list of validation errors; empty when the bundle is valid.</returns>
    IReadOnlyList<string> ValidateBundleInputs(PolicyBundle bundle);

    /// <summary>
    ///     Signs the bundle with the configured active signing key.
    /// </summary>
    /// <param name="bundle">The bundle to sign (mutated in place with the signature envelope).</param>
    /// <param name="signerUserId">The system administrator performing the signing.</param>
    /// <returns>The signed bundle (same instance).</returns>
    /// <exception cref="PolicyBundleSignatureException">
    ///     Thrown when inputs are invalid, no active signing key is configured, or the active
    ///     private key does not match any trusted public key.
    /// </exception>
    PolicyBundle SignBundle(PolicyBundle bundle, Guid signerUserId);

    /// <summary>
    ///     Verifies the bundle's signature envelope against the trusted key registry.
    /// </summary>
    /// <param name="bundle">The bundle to verify.</param>
    /// <returns>The verification result. Never throws for signature problems — fails closed via <c>IsValid == false</c>.</returns>
    PolicyBundleVerificationResult VerifyBundle(PolicyBundle bundle);
}

/// <summary>
///     Fail-closed read/write gate for published policy bundles.
/// </summary>
/// <remarks>
///     Published (active) bundle reads fail closed with <see cref="PolicyBundleSignatureException"/>
///     when the bundle carries no signature or an invalid one. Lifecycle writers use this gate to
///     guarantee approved/active bundles always carry a valid signature.
/// </remarks>
public interface ISignedPolicyBundleStore
{
    /// <summary>
    ///     Loads a published (active) bundle by name and tenant scope and verifies its signature.
    /// </summary>
    /// <param name="name">The bundle name.</param>
    /// <param name="tenantId">The tenant scope, or null for the global scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verified bundle.</returns>
    /// <exception cref="PolicyBundleSignatureException">Thrown when no published bundle exists or its signature is missing/invalid.</exception>
    Task<PolicyBundle> GetVerifiedPublishedBundleAsync(
        string name,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads a bundle by id and verifies its signature. Draft bundles without a signature
    ///     are returned only when <paramref name="allowUnsignedDraft"/> is true.
    /// </summary>
    /// <param name="bundleId">The bundle identifier.</param>
    /// <param name="allowUnsignedDraft">Whether unsigned draft bundles may be loaded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verified bundle.</returns>
    /// <exception cref="PolicyBundleSignatureException">Thrown when the bundle is missing or unsigned when a signature is required, or its signature is invalid.</exception>
    Task<PolicyBundle> GetVerifiedBundleAsync(
        Guid bundleId,
        bool allowUnsignedDraft,
        CancellationToken cancellationToken = default);
}
