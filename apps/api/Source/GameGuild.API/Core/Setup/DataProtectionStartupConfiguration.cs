using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Setup;

internal static class DataProtectionStartupConfiguration
{
    public static void Configure(WebApplicationBuilder builder, IApiProductComposition productComposition)
    {
        ConfigureServices(
            builder.Services,
            productComposition.ApplicationName,
            Console.Error.WriteLine);
    }

    internal static void ConfigureServices(
        IServiceCollection services,
        string applicationName,
        Action<string> writeError)
        => ConfigureServices(services, applicationName, writeError, Environment.GetEnvironmentVariable);

    internal static void ConfigureServices(
        IServiceCollection services,
        string applicationName,
        Action<string> writeError,
        Func<string, string?> environmentReader)
    {
        // Validate before registering a durable repository. Development and CI also
        // require an explicit certificate; an unavailable key must never select plaintext.
        var certificate = LoadCertificate(environmentReader, writeError);
        var builder = services.AddDataProtection()
            .SetApplicationName(applicationName)
            .ProtectKeysWithCertificate(certificate);
        services.AddSingleton(provider => new CertificateProtectedKeyRepository(provider, certificate));
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(provider =>
            new ConfigureOptions<KeyManagementOptions>(options =>
        {
            options.XmlRepository = provider.GetRequiredService<CertificateProtectedKeyRepository>();
        }));
    }

    internal static X509Certificate2 LoadCertificate(
        Func<string, string?> environmentReader,
        Action<string> writeError)
    {
        var certificateBase64 = environmentReader("DATAPROTECTION_CERTIFICATE_BASE64");
        var keyBase64 = environmentReader("DATAPROTECTION_CERTIFICATE_KEY_BASE64");

        if (string.IsNullOrWhiteSpace(certificateBase64) || string.IsNullOrWhiteSpace(keyBase64))
        {
            const string message = "[DataProtection] Both key-protection certificate variables are required for durable key storage.";
            writeError(message);
            throw new InvalidOperationException(message);
        }

        try
        {
            var certificatePem = Encoding.UTF8.GetString(Convert.FromBase64String(certificateBase64));
            var keyPem = Encoding.UTF8.GetString(Convert.FromBase64String(keyBase64));
            var certificate = X509Certificate2.CreateFromPem(certificatePem, keyPem);
            using var rsa = certificate.GetRSAPrivateKey();
            if (rsa is null || rsa.KeySize < 2048
                || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow
                || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
            {
                certificate.Dispose();
                throw new CryptographicException("The key-protection certificate is unsuitable.");
            }
            return certificate;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or ArgumentException)
        {
            const string message = "[DataProtection] The key-protection certificate is invalid or unavailable.";
            writeError(message);
            throw new InvalidOperationException(message);
        }
    }
}
