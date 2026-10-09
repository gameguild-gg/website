namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>
///     Configuration options for blockchain anchoring of certificates.
///     Safe default: anchoring is DISABLED (<c>Provider = "none"</c>) so existing
///     deployments keep their current behavior. Anchoring is opt-in via configuration,
///     following the same opt-in semantics as <c>TenantAuthConfiguration.AllowWeb3Auth</c>.
/// </summary>
/// <remarks>
///     <para>
///         Provider values:
///         <list type="bullet">
///             <item><c>none</c> (default) — anchoring disabled; a no-op implementation is registered and no data leaves the application database.</item>
///             <item><c>local</c> — deterministic local/dev provider. It persists anchors in the existing
///                 <c>BlockchainCertificateAnchor</c> table and publishes ONLY a SHA-256 hash of a
///                 canonicalized, PII-free certificate payload. No external network is contacted.</item>
///         </list>
///     </para>
///     <para>
///         DEFERRED OPERATIONS DECISIONS (deliberately out of scope): targeting a real chain/network,
///         issuer key custody, transaction funding, and confirmation/finality policy. When those are
///         decided, a new provider value can be introduced; the canonicalization contract documented in
///         <c>BlockchainCertificateCanonicalizer</c> must be preserved so existing anchors stay verifiable.
///         See docs/api/blockchain-certificate-anchoring.md.
///     </para>
/// </remarks>
public sealed class BlockchainCertificateOptions : BaseOptions
{
    /// <summary>
    ///     The configuration section name.
    /// </summary>
    public const string SectionName = "BlockchainCertificates";

    /// <summary>
    ///     Provider value that disables anchoring entirely (safe default).
    /// </summary>
    public const string ProviderNone = "none";

    /// <summary>
    ///     Provider value selecting the deterministic local/dev implementation.
    /// </summary>
    public const string ProviderLocal = "local";

    /// <summary>
    ///     Anchoring provider selector. Safe default: <c>"none"</c> (disabled).
    /// </summary>
    public string Provider { get; set; } = ProviderNone;

    /// <summary>
    ///     Issuer identity embedded in the canonical anchored payload. Must not contain personal data.
    /// </summary>
    public string Issuer { get; set; } = "gameguild";

    /// <summary>
    ///     Network label recorded on anchors produced by the local provider
    ///     (never a real chain identifier; the local provider contacts no external network).
    /// </summary>
    public string LocalNetworkName { get; set; } = "local";

    /// <inheritdoc />
    public new (bool IsValid, string[] Errors) Validate()
    {
        var errors = new List<string>();

        Provider = (Provider ?? string.Empty).Trim().ToLowerInvariant();

        if (Provider is not (ProviderNone or ProviderLocal))
        {
            errors.Add($"Provider must be '{ProviderNone}' or '{ProviderLocal}' (received '{Provider}')");
        }

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            errors.Add("Issuer is required");
        }

        if (string.IsNullOrWhiteSpace(LocalNetworkName))
        {
            errors.Add("LocalNetworkName is required");
        }

        return (errors.Count == 0, errors.ToArray());
    }

    /// <summary>
    ///     Creates default options (anchoring disabled).
    /// </summary>
    public static BlockchainCertificateOptions CreateDefault() => new();
}
