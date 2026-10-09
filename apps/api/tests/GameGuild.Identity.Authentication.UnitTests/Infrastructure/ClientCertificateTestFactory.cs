using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GameGuild.Identity.Authentication.UnitTests.Infrastructure;

/// <summary>
///     Generates self-signed test certificate authorities and client certificates
///     for X.509 client-certificate authentication tests.
/// </summary>
public static class ClientCertificateTestFactory
{
    public const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
    public const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    private static readonly DateTimeOffset NotBefore = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateTimeOffset NotAfter = DateTimeOffset.UtcNow.AddDays(30);

    /// <summary>
    ///     Creates a persistable certificate authority with a CA basic constraint.
    /// </summary>
    public static X509Certificate2 CreateCertificateAuthority(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));

        using var ephemeral = request.CreateSelfSigned(NotBefore, NotAfter);
        return ImportPersistable(ephemeral);
    }

    /// <summary>
    ///     Issues a client certificate (client-authentication EKU) signed by the given authority.
    /// </summary>
    public static X509Certificate2 CreateClientCertificate(string commonName, X509Certificate2 certificateAuthority)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new(ClientAuthenticationOid) }, true));

        // Derive validity from the issuer so the issued leaf can never outlive it.
        using var issued = request.Create(
            certificateAuthority,
            certificateAuthority.NotBefore,
            certificateAuthority.NotAfter,
            RandomNumberGenerator.GetBytes(16));
        return ImportPersistable(issued);
    }

    /// <summary>
    ///     Creates a self-signed TLS server certificate (server-authentication EKU).
    /// </summary>
    public static X509Certificate2 CreateServerCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new(ServerAuthenticationOid) }, true));

        using var ephemeral = request.CreateSelfSigned(NotBefore, NotAfter);
        return ImportPersistable(ephemeral);
    }

    /// <summary>
    ///     Round-trips a certificate through PFX so its private key stays usable across
    ///     TLS handshakes and chain builds.
    /// </summary>
    private static X509Certificate2 ImportPersistable(X509Certificate2 certificate)
    {
        var pfx = certificate.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }
}
