namespace GameGuild.Configuration.PresentationLayer.Authentication;

/// <summary>
///     Configuration for the optional X.509 client-certificate (mTLS) authentication scheme.
/// </summary>
public sealed class ClientCertificateAuthenticationSettings
{
    public string SchemeName { get; set; } = "ClientCertificate";

    /// <summary>
    ///     PEM-encoded CA certificates allowed to issue client certificates.
    ///     Each array entry is a single PEM certificate; the collection forms the
    ///     trust allowlist used for chain validation. When the scheme is enabled,
    ///     at least one entry is required (fail closed).
    /// </summary>
    public string[] TrustedCaCertificates { get; set; } = [];

    /// <summary>
    ///     Whether certificate revocation is checked during chain validation.
    ///     Offline/internal deployments without CRL/OCSP endpoints keep this off.
    /// </summary>
    public bool CheckCertificateRevocation { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SchemeName) || SchemeName.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                "Client certificate authentication scheme name must be non-empty and contain no control characters.");
        }

        if (TrustedCaCertificates.Any(entry => string.IsNullOrWhiteSpace(entry)))
        {
            throw new InvalidOperationException(
                "Client certificate trusted CA entries must be non-empty PEM strings.");
        }
    }
}
