using GameGuild.Configuration.PresentationLayer.Authentication;
using Xunit;

namespace GameGuild.SharedKernel.UnitTests.Configuration;

public sealed class ClientCertificateAuthenticationSettingsTests
{
    [Fact]
    public void Validate_DefaultSettings_Succeeds()
    {
        var settings = new ClientCertificateAuthenticationSettings();

        settings.Validate();

        Assert.Equal("ClientCertificate", settings.SchemeName);
        Assert.Empty(settings.TrustedCaCertificates);
        Assert.False(settings.CheckCertificateRevocation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Legacy\r\nX-Evil: yes")]
    public void Validate_UnsafeSchemeName_Throws(string schemeName)
    {
        var settings = new ClientCertificateAuthenticationSettings { SchemeName = schemeName };

        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public void Validate_EmptyTrustedCaEntry_Throws()
    {
        var settings = new ClientCertificateAuthenticationSettings
        {
            TrustedCaCertificates = ["-----BEGIN CERTIFICATE-----\nvalid\n-----END CERTIFICATE-----\n", "   "]
        };

        var exception = Assert.Throws<InvalidOperationException>(settings.Validate);

        Assert.Contains("non-empty PEM", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticationOptions_RejectClientCertificateSchemeWhenAuthenticationIsDisabled()
    {
        var options = new AuthenticationOptions
        {
            EnableAuthentication = false,
            EnableClientCertificateAuthentication = true
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(
            "Client certificate authentication cannot be enabled when authentication is disabled",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticationOptions_FailClosedWhenEnabledWithoutCaAllowlist()
    {
        var options = CreateEnabledOptions(trustedCaCertificates: []);

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("at least one trusted CA certificate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticationOptions_FailClosedWhenEnabledWithoutSettings()
    {
        var options = new AuthenticationOptions
        {
            JwtSecretKey = new string('s', 64),
            JwtIssuer = "GameGuild",
            JwtAudience = "GameGuild.Users",
            EnableClientCertificateAuthentication = true,
            ClientCertificate = null
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void AuthenticationOptions_ValidateSucceedsWithConfiguredAllowlist()
    {
        var options = CreateEnabledOptions(
        [
            "-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----\n"
        ]);

        options.Validate();
    }

    private static AuthenticationOptions CreateEnabledOptions(string[] trustedCaCertificates) => new()
    {
        JwtSecretKey = new string('s', 64),
        JwtIssuer = "GameGuild",
        JwtAudience = "GameGuild.Users",
        EnableClientCertificateAuthentication = true,
        ClientCertificate = new ClientCertificateAuthenticationSettings
        {
            TrustedCaCertificates = trustedCaCertificates
        }
    };
}
