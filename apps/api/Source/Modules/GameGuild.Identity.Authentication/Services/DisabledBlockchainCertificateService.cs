using GameGuild.Configuration.ApplicationLayer;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Safe-default implementation of <see cref="IBlockchainCertificateService" /> used when
///     <c>BlockchainCertificates:Provider</c> is <c>"none"</c> (the default). Nothing is persisted,
///     hashed, or published anywhere.
/// </summary>
/// <remarks>
///     <para>
///         Behavior contract:
///         <list type="bullet">
///             <item><see cref="AnchorCertificateAsync" /> returns a failed result (never throws) so callers can treat anchoring as auxiliary evidence.</item>
///             <item><see cref="VerifyCertificateAsync" /> fails closed (returns <c>false</c>): with no anchors there is nothing to verify.</item>
///             <item><see cref="GetUserCertificatesAsync" /> returns an empty list.</item>
///             <item><see cref="RevokeCertificateAsync" /> throws because nothing was ever anchored.</item>
///         </list>
///     </para>
/// </remarks>
public sealed class DisabledBlockchainCertificateService : IBlockchainCertificateService
{
    /// <summary>
    ///     Message reported when anchoring is attempted while the provider is disabled.
    /// </summary>
    public const string DisabledMessage =
        $"Blockchain certificate anchoring is disabled ({BlockchainCertificateOptions.SectionName}:{nameof(BlockchainCertificateOptions.Provider)}={BlockchainCertificateOptions.ProviderNone}).";

    /// <inheritdoc />
    public Task<BlockchainAnchorResult> AnchorCertificateAsync(Guid userId, string certificateData, string certificateType)
    {
        return Task.FromResult<BlockchainAnchorResult>(
            BlockchainCertificateAnchorResult.Failure(DisabledMessage));
    }

    /// <inheritdoc />
    public Task<bool> VerifyCertificateAsync(string certificateHash, string transactionHash)
    {
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<List<BlockchainCertificateAnchor>> GetUserCertificatesAsync(Guid userId)
    {
        return Task.FromResult(new List<BlockchainCertificateAnchor>());
    }

    /// <inheritdoc />
    public Task<string> RevokeCertificateAsync(string certificateHash, string reason)
    {
        throw new InvalidOperationException(DisabledMessage);
    }

    /// <inheritdoc />
    public Task<VerifiableCredential> GenerateVerifiableCredentialAsync(Guid userId, string credentialType, Dictionary<string, object> claims)
    {
        throw new NotSupportedException(DisabledMessage);
    }
}
