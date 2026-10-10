namespace GameGuild.Identity.Authentication;

/// <summary>
///     X.509 client-certificate credential data for machine-to-machine authentication.
/// </summary>
/// <remarks>
///     The thumbprint is the SHA-1 hex fingerprint of the certificate. The optional
///     SPKI hash is the SHA-256 hex hash of the certificate's Subject Public Key Info
///     and pins the key instead of the full certificate (survives renewal).
/// </remarks>
public class CertificateCredentialData : ICredentialData
{
    /// <summary>
    ///     SHA-1 hex thumbprint of the client certificate.
    /// </summary>
    public string Thumbprint { get; set; } = string.Empty;

    /// <summary>
    ///     Optional SHA-256 hex hash of the certificate's Subject Public Key Info (key pin).
    /// </summary>
    public string? SpkiSha256 { get; set; }

    public string Type { get => "certificate"; }
}
