using GameGuild.API.Core.Integration;
using GameGuild.API.Database;
using GameGuild.API.Setup;
using GameGuild.Identity.Authentication;
using GameGuild.Learning.Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GameGuild.API.UnitTests.Core;

public sealed class CertificateEvmHostCompositionTests
{
    [Fact]
    public void DefaultDoesNotRegisterPersistenceKeysTransportOrWorkers()
    {
        var services = new ServiceCollection();
        services.AddCertificateEvmHost(new ConfigurationBuilder().Build());
        Assert.Empty(services);
    }

    [Fact]
    public void ExplicitlyDisabledConfigurationDoesNotBindUnusedSecretsOrChangeLocalProvider()
    {
        var values = ValidValues();
        values[Key("Enabled")] = "false";
        values[Key("Network:ChainId")] = "unused-invalid-value";
        values["BlockchainCertificates:Provider"] = "local";
        var services = new ServiceCollection();
        services.AddCertificateEvmHost(Configuration(values));
        Assert.Empty(services);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("secret-invalid-enable")]
    public void InvalidActivationFailsWithoutEchoingItsValueOrMutatingServices(string value)
    {
        var values = ValidValues(); values[Key("Enabled")] = value;
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() => services.AddCertificateEvmHost(Configuration(values)));
        Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException);
        Assert.Empty(services);
    }

    [Fact]
    public void EnabledHostComposesTheDurableProducerJournalTransportSignerAndBoundedWorker()
    {
        var services = new ServiceCollection();
        services.AddCertificateEvmHost(Configuration(ValidValues()));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var policy = provider.GetRequiredService<EvmCertificatePublicationPolicy>();
        Assert.Equal("sepolia-fixture", policy.Network.Profile);
        Assert.Equal(11155111, policy.Network.ChainId);
        Assert.Equal("certificate-testnet", policy.SignerReference);
        Assert.Equal(3, policy.MaximumAttempts);
        Assert.Equal(TimeSpan.FromSeconds(30), policy.CheckDelay);
        Assert.NotNull(provider.GetRequiredService<IDbContextFactory<EvmCertificateJournalDbContext>>());
        Assert.IsType<EvmCertificateEnvironmentKeySource>(provider.GetRequiredService<IEvmCertificatePrivateKeySource>());
        Assert.Contains(services, s => s.ServiceType == typeof(ICertificateDurableEvidenceProducer));
        Assert.Contains(services, s => s.ServiceType == typeof(IDatabaseMigrationPrerequisite) &&
            s.ImplementationType == typeof(CertificateEvmJournalMigrationPrerequisite));
        Assert.Contains(services, s => s.ServiceType == typeof(IHostedService) &&
            s.ImplementationType == typeof(CertificateEvmJournalReadiness));
        Assert.Contains(services, s => s.ServiceType == typeof(IHostedService) &&
            s.ImplementationType == typeof(EvmCertificatePublicationBackgroundService));
    }

    [Fact]
    public void ConfigurationReloadCannotRetargetAnExistingComposition()
    {
        var configuration = Configuration(ValidValues());
        var services = new ServiceCollection(); services.AddCertificateEvmHost(configuration);
        configuration[Key("Network:ChainId")] = "1";
        configuration[Key("Profile")] = "changed-profile";
        configuration[Key("MaximumAttempts")] = "10";
        using var provider = services.BuildServiceProvider();
        var policy = provider.GetRequiredService<EvmCertificatePublicationPolicy>();
        Assert.Equal(11155111, policy.Network.ChainId);
        Assert.Equal("sepolia-fixture", policy.Network.Profile);
        Assert.Equal(3, policy.MaximumAttempts);
    }

    [Fact]
    public void MissingEnvironmentKeyDoesNotPreventCompositionOrReadItDuringRegistration()
    {
        var values = ValidValues(); values[Key("PrivateKeyEnvironmentVariable")] = "GG_UNUSED_" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection(); services.AddCertificateEvmHost(Configuration(values));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<EvmCertificateSoftwareSigner>(scope.ServiceProvider.GetRequiredService<IEvmCertificateTransactionSigner>());
    }

    [Theory]
    [InlineData("MaximumFeePerGasWei", "0")]
    [InlineData("MaximumTransactionFeeWei", "1")]
    [InlineData("InitialFeePerGasWei", "30000000001")]
    [InlineData("InitialPriorityFeePerGasWei", "20000000001")]
    [InlineData("MaximumFeePerGasWei", "1.5")]
    [InlineData("MaximumAttempts", "11")]
    [InlineData("CheckDelay", "00:00:00")]
    [InlineData("LeaseDuration", "00:00:01")]
    [InlineData("PrivateKeyEnvironmentVariable", "not-an-environment-variable")]
    [InlineData("CanonicalIssuer", "")]
    [InlineData("Profile", "profile with spaces")]
    [InlineData("Network:RpcEndpoint", "http://private-route.invalid/credential")]
    [InlineData("Network:FinalityMode", "Confirmations")]
    [InlineData("Network:ContractAddress", "0x0000000000000000000000000000000000000000")]
    [InlineData("UnexpectedSetting", "unknown")]
    public void InvalidConfigurationFailsBeforeAnyRegistration(string setting, string value)
    {
        var values = ValidValues(); values[Key(setting)] = value;
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() => services.AddCertificateEvmHost(Configuration(values)));
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("private-route", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-database-password", error.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void EnablingLocalAndEvmAtOnceIsRejectedBeforeRegistration()
    {
        var values = ValidValues(); values["BlockchainCertificates:Provider"] = "local";
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddCertificateEvmHost(Configuration(values)));
        Assert.Empty(services);
    }

    [Fact]
    public void AnInvalidDatabaseConnectionIsRedactedAndDoesNotRegisterAnything()
    {
        var values = ValidValues(); values["ConnectionStrings:DefaultConnection"] = "secret-invalid-connection";
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() => services.AddCertificateEvmHost(Configuration(values)));
        Assert.DoesNotContain("secret-invalid-connection", error.Message, StringComparison.Ordinal);
        Assert.Null(error.InnerException); Assert.Empty(services);
    }

    [Fact]
    public void DuplicateCompositionFailsBeforeAddingAnyMoreServices()
    {
        var services = new ServiceCollection(); var configuration = Configuration(ValidValues());
        services.AddCertificateEvmHost(configuration); var count = services.Count;
        Assert.Throws<InvalidOperationException>(() => services.AddCertificateEvmHost(configuration));
        Assert.Equal(count, services.Count);
    }

    [Fact]
    public void TheActualProductCompositionCallsTheOptInEvmExtension()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(ValidValues());
        ApiProductComposition.Instance.ConfigureServices(builder);
        Assert.Contains(builder.Services, s => s.ServiceType == typeof(EvmCertificatePublicationPolicy));
        Assert.Contains(builder.Services, s => s.ServiceType == typeof(IHostedService) &&
            s.ImplementationType == typeof(CertificateEvmJournalReadiness));
    }

    internal static string Key(string name) => CertificateEvmHostOptions.SectionName + ":" + name;
    internal static IConfigurationRoot Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    internal static Dictionary<string, string?> ValidValues() => new()
    {
        [Key("Enabled")] = "true", [Key("Profile")] = "sepolia-fixture", [Key("CanonicalIssuer")] = "gameguild-testnet",
        [Key("SignerReference")] = "certificate-testnet", [Key("PrivateKeyEnvironmentVariable")] = "GG_TESTNET_ISSUER_KEY",
        [Key("Network:ChainId")] = "11155111", [Key("Network:RpcEndpoint")] = "https://fixture.invalid/private-route",
        [Key("Network:ContractAddress")] = "0x1111111111111111111111111111111111111111",
        [Key("Network:IssuerId")] = "0x" + new string('2', 64), [Key("Network:ExpectedRuntimeCodeSha256")] = new string('3', 64),
        [Key("Network:FinalityMode")] = "Finalized", [Key("GasLimit")] = "200000",
        [Key("MaximumFeePerGasWei")] = "30000000000", [Key("MaximumTransactionFeeWei")] = "6000000000000000",
        [Key("InitialFeePerGasWei")] = "20000000000", [Key("InitialPriorityFeePerGasWei")] = "1000000000",
        ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=certificate_fixture;Username=fixture;Password=fixture-database-password"
    };
}
