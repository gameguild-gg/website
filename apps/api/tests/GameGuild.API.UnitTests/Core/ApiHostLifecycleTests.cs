using FluentAssertions;
using GameGuild.API.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API.UnitTests.Core;

public sealed class ApiHostLifecycleTests
{
    [Fact]
    public void ResolveKeysPath_ShouldPreferConfigurationThenEnvironmentThenDefault()
    {
        var configured = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataProtection:KeysPath"] = "configured-path"
        }).Build();
        var empty = new ConfigurationBuilder().Build();

        DataProtectionStartupConfiguration.ResolveKeysPath(configured, "default-path", _ => "environment-path")
            .Should().Be("configured-path");
        DataProtectionStartupConfiguration.ResolveKeysPath(empty, "default-path", _ => "environment-path")
            .Should().Be("environment-path");
        DataProtectionStartupConfiguration.ResolveKeysPath(empty, "default-path", _ => null)
            .Should().Be("default-path");
    }

    [Fact]
    public void ConfigureServices_ShouldPersistKeysWhenDirectoryIsWritable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"keys-{Guid.NewGuid():N}");
        var services = new ServiceCollection();
        var errors = new List<string>();

        try
        {
            DataProtectionStartupConfiguration.ConfigureServices(services, path, "TestProduct", errors.Add);
            using var provider = services.BuildServiceProvider();

            Directory.Exists(path).Should().BeTrue();
            provider.GetRequiredService<IDataProtectionProvider>().Should().NotBeNull();
            errors.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void ConfigureServices_WhenDirectoryCannotBeCreated_ShouldUseFallbackProvider()
    {
        var path = Path.GetTempFileName();
        var services = new ServiceCollection();
        var errors = new List<string>();

        try
        {
            DataProtectionStartupConfiguration.ConfigureServices(services, path, "TestProduct", errors.Add);
            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<IDataProtectionProvider>().Should().NotBeNull();
            errors.Should().ContainSingle().Which.Should().Contain("Falling back to defaults");
        }
        finally
        {
            File.Delete(path);
        }
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

        public string DefaultDataProtectionKeysPath => "test-keys";

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
