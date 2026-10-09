using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace GameGuild.API.UnitTests.Core;

internal static class TestDataProtectionEnvironment
{
    private const string CertificateVariable = "DATAPROTECTION_CERTIFICATE_BASE64";
    private const string KeyVariable = "DATAPROTECTION_CERTIFICATE_KEY_BASE64";
    private static readonly Lock Sync = new();

    public static void EnsureConfigured()
    {
        if (HasAnyConfiguration()) return;

        lock (Sync)
        {
            if (HasAnyConfiguration()) return;

            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=gameguild-api-unit-tests",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddDays(1));

            Environment.SetEnvironmentVariable(
                CertificateVariable,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(certificate.ExportCertificatePem())));
            Environment.SetEnvironmentVariable(
                KeyVariable,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem())));
        }
    }

    private static bool HasAnyConfiguration() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CertificateVariable))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(KeyVariable));
}
