using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentAssertions;
using GameGuild.API.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API.UnitTests.Core;

public sealed class ApiHostLifecycleTests
{
    [Fact]
    public void ConfigureServices_ShouldRegisterDataProtectionProvider()
    {
        var services = new ServiceCollection();
        var errors = new List<string>();

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=dataprotection-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certificateBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(certificate.ExportCertificatePem()));
        var keyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem()));
        DataProtectionStartupConfiguration.ConfigureServices(services, "TestProduct", errors.Add,
            name => name == "DATAPROTECTION_CERTIFICATE_BASE64" ? certificateBase64 : keyBase64);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDataProtectionProvider>().Should().NotBeNull();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void LoadCertificate_ShouldFailClosedWhenEnvironmentVariablesAreAbsent()
    {
        var errors = new List<string>();

        var load = () => DataProtectionStartupConfiguration.LoadCertificate(_ => null, errors.Add);
        load.Should().Throw<InvalidOperationException>().WithMessage("*required for durable key storage*");
        errors.Should().ContainSingle().Which.Should().Contain("required");
    }

    [Fact]
    public void LoadCertificate_ShouldFailClosedWhenOnlyOneVariableIsSet()
    {
        var errors = new List<string>();

        var load = () => DataProtectionStartupConfiguration.LoadCertificate(
            name => name == "DATAPROTECTION_CERTIFICATE_BASE64" ? "Y2VydA==" : null,
            errors.Add);

        load.Should().Throw<InvalidOperationException>().WithMessage("*required for durable key storage*");
    }

    [Fact]
    public void LoadCertificate_ShouldReturnCertificateForValidBase64Pem()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=dataprotection-test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var selfSigned = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        var certificateBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(selfSigned.ExportCertificatePem()));
        var keyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem()));

        using var certificate = DataProtectionStartupConfiguration.LoadCertificate(
            name => name == "DATAPROTECTION_CERTIFICATE_BASE64" ? certificateBase64 : keyBase64,
            _ => { });

        certificate.Should().NotBeNull();
        certificate!.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void LoadCertificate_ShouldFailClosedOnMalformedBase64()
    {
        var errors = new List<string>();

        var load = () => DataProtectionStartupConfiguration.LoadCertificate(_ => "!!!not-base64!!!", errors.Add);
        load.Should().Throw<InvalidOperationException>().WithMessage("*invalid or unavailable*");
        errors.Should().ContainSingle().Which.Should().Contain("invalid or unavailable");
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task RunAsync_ShouldHonorProductInitializationDecision(bool continueStartup, int expectedCalls)
    {
        await using var app = WebApplication.CreateBuilder().Build();
        var composition = new TestProductComposition(continueStartup);
        var pipelineCalls = 0;
        var runCalls = 0;

        await ApiHostLifecycle.RunAsync(
            app,
            composition,
            true,
            ["--test"],
            _ =>
            {
                pipelineCalls++;
                return Task.CompletedTask;
            },
            _ =>
            {
                runCalls++;
                return Task.CompletedTask;
            });

        pipelineCalls.Should().Be(expectedCalls);
        runCalls.Should().Be(expectedCalls);
        composition.InitializeCalls.Should().Be(1);
        composition.Arguments.Should().Equal("--test");
    }

    private sealed class TestProductComposition(bool continueStartup) : IApiProductComposition
    {
        public int InitializeCalls { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public string ApplicationName => "TestProduct";

        public IReadOnlyList<string> EnabledModules => [];

        public IReadOnlyList<string> DisabledModules => [];

        public void ConfigureServices(WebApplicationBuilder builder) { }

        public void ConfigureOpenApi(SwaggerGenOptions options) { }

        public Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> InitializeAsync(
            WebApplication app,
            bool databaseInitialized,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            databaseInitialized.Should().BeTrue();
            InitializeCalls++;
            Arguments = arguments;
            return Task.FromResult(continueStartup);
        }
    }
}
