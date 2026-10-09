using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GameGuild.Configuration.PresentationLayer.Authentication;
using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Xunit;

namespace GameGuild.API.UnitTests.Core.Extensions;

public sealed class ClientCertificateAuthenticationSchemeRegistrationTests : IDisposable
{
    private readonly X509Certificate2 _certificateAuthority;

    public ClientCertificateAuthenticationSchemeRegistrationTests()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Registration Test Root", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        _certificateAuthority = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), null);
    }

    public void Dispose()
    {
        _certificateAuthority.Dispose();
    }

    [Fact]
    public async Task SetupAuthentication_RegistersClientCertificateScheme_WhenEnabledWithCaAllowlist()
    {
        var services = new ServiceCollection();
        var options = CreateValidOptions(caPem =>
            new ClientCertificateAuthenticationSettings { TrustedCaCertificates = [caPem] });

        services.AddLogging();
        services.SetupAuthentication(CreateConfiguration(), options);
        using var serviceProvider = services.BuildServiceProvider();

        var scheme = await serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(ClientCertificateAuthenticationOptions.SchemeName);
        Assert.NotNull(scheme);

        var certificateOptions = serviceProvider.GetRequiredService<IOptionsMonitor<ClientCertificateAuthenticationOptions>>()
            .Get(ClientCertificateAuthenticationOptions.SchemeName);
        Assert.Single(certificateOptions.TrustedCertificateAuthorities);
    }

    [Fact]
    public async Task SetupAuthentication_DoesNotRegisterClientCertificateScheme_WhenDisabled()
    {
        var services = new ServiceCollection();
        var options = new AuthenticationOptions
        {
            JwtSecretKey = new string('s', 64),
            JwtIssuer = "GameGuild",
            JwtAudience = "GameGuild.Users"
        };

        services.AddLogging();
        services.SetupAuthentication(CreateConfiguration(), options);
        using var serviceProvider = services.BuildServiceProvider();

        var scheme = await serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(ClientCertificateAuthenticationOptions.SchemeName);
        Assert.Null(scheme);
    }

    [Fact]
    public void SetupAuthentication_FailsClosed_WhenEnabledWithoutCaAllowlist()
    {
        var services = new ServiceCollection();
        var options = new AuthenticationOptions
        {
            JwtSecretKey = new string('s', 64),
            JwtIssuer = "GameGuild",
            JwtAudience = "GameGuild.Users",
            EnableClientCertificateAuthentication = true,
            ClientCertificate = new ClientCertificateAuthenticationSettings()
        };

        Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddLogging();
            services.SetupAuthentication(CreateConfiguration(), options);
        });
    }

    [Fact]
    public void SetupAuthentication_FailsClosed_WhenCaEntryIsNotValidPem()
    {
        var services = new ServiceCollection();
        var options = CreateValidOptions(_ => new ClientCertificateAuthenticationSettings
        {
            TrustedCaCertificates = ["this-is-not-a-pem-certificate"]
        });

        Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddLogging();
            services.SetupAuthentication(CreateConfiguration(), options);
        });
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder().Build();

    private AuthenticationOptions CreateValidOptions(Func<string, ClientCertificateAuthenticationSettings> settingsFactory) => new()
    {
        JwtSecretKey = new string('s', 64),
        JwtIssuer = "GameGuild",
        JwtAudience = "GameGuild.Users",
        EnableClientCertificateAuthentication = true,
        ClientCertificate = settingsFactory(ExportCertificatePem(_certificateAuthority))
    };

    private static string ExportCertificatePem(X509Certificate2 certificate)
    {
        var der = certificate.Export(X509ContentType.Cert);
        return new string(PemEncoding.Write("CERTIFICATE", der));
    }
}
