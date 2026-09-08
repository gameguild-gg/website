using FluentAssertions;
using GameGuild.API.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GameGuild.API.UnitTests.Core;

public sealed class OperationalStartupConfigurationTests
{
    [Fact]
    public void Validate_RequiresSesRegionAndSenderWhenSesDeliveryIsEnabled()
    {
        var missingRegion = CompleteValues();
        missingRegion["EmailDelivery:Provider"] = "Ses";
        missingRegion.Remove("EmailDelivery:Ses:Region");

        var missingFromEmail = CompleteValues();
        missingFromEmail["EmailDelivery:Provider"] = "Ses";
        missingFromEmail["EmailDelivery:Ses:Region"] = "us-east-1";
        missingFromEmail.Remove("EmailDelivery:FromEmail");

        OperationalStartupConfiguration.Validate(CreateConfiguration(missingRegion), Environments.Production)
            .Should().ContainSingle(message => message.Contains("AWS region", StringComparison.Ordinal));
        OperationalStartupConfiguration.Validate(CreateConfiguration(missingFromEmail), Environments.Production)
            .Should().ContainSingle(message => message.Contains("sender address", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SES")]
    [InlineData(" ses ")]
    [InlineData(null)]
    public void Validate_AcceptsSesIncludingExistingRegionOnlyConfiguration(string? provider)
    {
        var values = CompleteValues();
        values["EmailDelivery:Provider"] = provider;
        values["EmailDelivery:Ses:Region"] = "us-east-1";
        values.Remove("EmailDelivery:SmtpHost");
        values.Remove("EmailDelivery:SendGridApiKey");
        OperationalStartupConfiguration.Validate(CreateConfiguration(values), Environments.Production).Should().BeEmpty();
    }

    [Fact]
    public void Validate_RequiresSesRegionWhenSesIsExplicitlySelected()
    {
        var values = CompleteValues();
        values["EmailDelivery:Provider"] = "Ses";
        OperationalStartupConfiguration.Validate(CreateConfiguration(values), Environments.Production)
            .Should().ContainSingle(message => message.Contains("SES", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    [InlineData("Testing")]
    public void Validate_AllowsReducedLocalConfiguration(string environmentName)
    {
        OperationalStartupConfiguration.Validate(CreateConfiguration(), environmentName).Should().BeEmpty();
    }

    [Fact]
    public void Validate_RejectsMissingProductionDependencies()
    {
        var failures = OperationalStartupConfiguration.Validate(CreateConfiguration(), Environments.Production);

        failures.Should().Contain(message => message.Contains("JWT secret", StringComparison.Ordinal));
        failures.Should().Contain(message => message.Contains("JWT issuer", StringComparison.Ordinal));
        failures.Should().Contain(message => message.Contains("JWT audience", StringComparison.Ordinal));
        failures.Should().Contain(message => message.Contains("encryption", StringComparison.OrdinalIgnoreCase));
        failures.Should().Contain(message => message.Contains("runtime database", StringComparison.OrdinalIgnoreCase));
        failures.Should().Contain(message => message.Contains("migration database", StringComparison.OrdinalIgnoreCase));
        failures.Should().Contain(message => message.Contains("Redis", StringComparison.Ordinal));
        failures.Should().Contain(message => message.Contains("Email delivery", StringComparison.Ordinal));
        failures.Should().Contain(message => message.Contains("storage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_AcceptsCompleteProductionConfiguration()
    {
        OperationalStartupConfiguration.Validate(CreateCompleteConfiguration(), Environments.Production).Should().BeEmpty();
    }

    [Fact]
    public void Validate_RequiresProviderSpecificEmailConfiguration()
    {
        var smtp = CompleteValues();
        smtp.Remove("EmailDelivery:SmtpHost");

        var sendGrid = CompleteValues();
        sendGrid["EmailDelivery:Provider"] = "SendGrid";
        sendGrid.Remove("EmailDelivery:SendGridApiKey");

        var unsupported = CompleteValues();
        unsupported["EmailDelivery:Provider"] = "Unknown";

        OperationalStartupConfiguration.Validate(CreateConfiguration(smtp), Environments.Production)
            .Should().ContainSingle(message => message.Contains("SMTP host", StringComparison.Ordinal));
        OperationalStartupConfiguration.Validate(CreateConfiguration(sendGrid), Environments.Production)
            .Should().ContainSingle(message => message.Contains("SendGrid API key", StringComparison.Ordinal));
        OperationalStartupConfiguration.Validate(CreateConfiguration(unsupported), Environments.Production)
            .Should().ContainSingle(message => message.Contains("supported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsDisabledRedisAndEmailDelivery()
    {
        var values = CompleteValues();
        values["Redis:Enabled"] = "false";
        values["EmailDelivery:Enabled"] = "false";

        var failures = OperationalStartupConfiguration.Validate(CreateConfiguration(values), Environments.Staging);

        failures.Should().Contain(message => message.Contains("Redis must be enabled", StringComparison.Ordinal));
        failures.Should().Contain(message => message.Contains("Email delivery must be enabled", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsNonPositiveSmtpPort()
    {
        var values = CompleteValues();
        values["EmailDelivery:SmtpPort"] = "0";

        OperationalStartupConfiguration.Validate(CreateConfiguration(values), Environments.Production)
            .Should().ContainSingle(message => message.Contains("positive SMTP port", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsMissingSmtpPort()
    {
        var values = CompleteValues();
        values.Remove("EmailDelivery:SmtpPort");

        OperationalStartupConfiguration.Validate(CreateConfiguration(values), Environments.Production)
            .Should().ContainSingle(message => message.Contains("positive SMTP port", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsPlaceholderAndShortSecrets()
    {
        var values = CompleteValues();
        values["Jwt:SecretKey"] = "CHANGE_THIS_TO_A_SECURE_SECRET";
        values["Encryption:EncryptionKey"] = "short";

        var failures = OperationalStartupConfiguration.Validate(CreateConfiguration(values), Environments.Production);

        failures.Should().Contain(message => message.Contains("JWT secret", StringComparison.Ordinal));
        failures.Should().Contain(message => message.Contains("encryption key", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("c2hvcnQ=")]
    public void Validate_RejectsInvalidAssetTokenSigningKeys(string secret)
    {
        var values = CompleteValues();
        values["Assets:Token:SecretKey"] = secret;

        var failures = OperationalStartupConfiguration.Validate(CreateConfiguration(values), Environments.Production);

        failures.Should().ContainSingle(message => message.Contains("asset token signing key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ThrowIfInvalid_ThrowsOneActionableException()
    {
        var act = () => OperationalStartupConfiguration.ThrowIfInvalid(CreateConfiguration(), Environments.Production);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Unsafe operational startup configuration*");
    }

    [Fact]
    public void ThrowIfInvalid_AcceptsCompleteConfiguration()
    {
        var act = () => OperationalStartupConfiguration.ThrowIfInvalid(CreateCompleteConfiguration(), Environments.Production);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsNullConfiguration()
    {
        var act = () => OperationalStartupConfiguration.Validate(null!, Environments.Production);

        act.Should().Throw<ArgumentNullException>();
    }

    private static IConfiguration CreateCompleteConfiguration() => CreateConfiguration(CompleteValues());

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build();

    private static Dictionary<string, string?> CompleteValues() => new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=app;Username=app_runtime;Password=runtime",
        ["ConnectionStrings:MigrationConnection"] = "Host=localhost;Database=app;Username=app_migrator;Password=migrator",
        ["Jwt:SecretKey"] = "a-production-secret-with-at-least-32-characters",
        ["Jwt:Issuer"] = "Product",
        ["Jwt:Audience"] = "Product.Users",
        ["Encryption:EncryptionKey"] = "a-production-encryption-key-with-32-characters",
        ["Redis:Enabled"] = "true",
        ["Redis:ConnectionString"] = "redis:6379",
        ["EmailDelivery:Enabled"] = "true",
        ["EmailDelivery:Provider"] = "Smtp",
        ["EmailDelivery:FromEmail"] = "no-reply@example.com",
        ["EmailDelivery:SmtpHost"] = "mailhog",
        ["EmailDelivery:SmtpPort"] = "1025",
        ["EmailDelivery:SendGridApiKey"] = "sendgrid-key",
        ["Assets:Storage:ServiceUrl"] = "http://garage:3900",
        ["Assets:Storage:AccessKey"] = "access-key",
        ["Assets:Storage:SecretKey"] = "secret-key",
        ["Assets:Token:SecretKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
        ["Assets:Storage:BucketName"] = "assets"
    };
}
