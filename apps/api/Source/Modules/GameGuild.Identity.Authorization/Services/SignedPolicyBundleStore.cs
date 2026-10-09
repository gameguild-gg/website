using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Fail-closed read gate for policy bundles backed by <see cref="IPolicyBundleRepository"/>
///     and <see cref="IPolicyBundleSignatureService"/>.
/// </summary>
public sealed class SignedPolicyBundleStore(
    IPolicyBundleRepository repository,
    IPolicyBundleSignatureService signatureService,
    ILogger<SignedPolicyBundleStore> logger
) : ISignedPolicyBundleStore
{
    private readonly IPolicyBundleRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    private readonly IPolicyBundleSignatureService _signatureService =
        signatureService ?? throw new ArgumentNullException(nameof(signatureService));

    private readonly ILogger<SignedPolicyBundleStore> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<PolicyBundle> GetVerifiedPublishedBundleAsync(
        string name,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var bundle = await _repository
            .GetPublishedByNameAsync(name, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (bundle is null)
            throw new PolicyBundleSignatureException(
                $"No published policy bundle named '{name}' exists for the requested scope.");

        return VerifyOrThrow(bundle, publishedRead: true);
    }

    /// <inheritdoc />
    public async Task<PolicyBundle> GetVerifiedBundleAsync(
        Guid bundleId,
        bool allowUnsignedDraft,
        CancellationToken cancellationToken = default)
    {
        var bundle = await _repository.GetByIdAsync(bundleId, cancellationToken).ConfigureAwait(false);

        if (bundle is null)
            throw new PolicyBundleSignatureException($"Policy bundle {bundleId} does not exist.");

        var requiresSignature = !allowUnsignedDraft
                                || bundle.Status is PolicyBundleStatus.Approved
                                or PolicyBundleStatus.Active;
        if (requiresSignature && string.IsNullOrWhiteSpace(bundle.DigitalSignature))
            throw new PolicyBundleSignatureException(
                $"Policy bundle {bundleId} in status {bundle.Status} carries no signature (fail closed).");

        return requiresSignature ? VerifyOrThrow(bundle, publishedRead: false) : bundle;
    }

    private PolicyBundle VerifyOrThrow(PolicyBundle bundle, bool publishedRead)
    {
        var result = _signatureService.VerifyBundle(bundle);
        if (result.IsValid)
            return bundle;

        _logger.LogError(
            "Policy bundle {BundleId} ('{BundleName}') failed signature verification ({Reason}); failing closed (publishedRead: {PublishedRead})",
            bundle.Id, bundle.Name, result.Reason, publishedRead);

        throw new PolicyBundleSignatureException(
            $"Policy bundle {bundle.Id} ('{bundle.Name}') has an invalid signature: {result.Reason}");
    }
}
