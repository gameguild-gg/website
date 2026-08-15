using FluentAssertions;
using GameGuild.API.Setup;

namespace GameGuild.API.UnitTests.Core;

public class ModuleConfigurationTests
{
    [Theory]
    [InlineData("AI")]
    [InlineData("Analytics")]
    [InlineData("Assets")]
    [InlineData("Commerce")]
    [InlineData("Commerce.Billing")]
    [InlineData("Commerce.Orders")]
    [InlineData("Commerce.Payments")]
    [InlineData("Commerce.Products")]
    [InlineData("Commerce.Subscriptions")]
    [InlineData("Compliance.Audit")]
    [InlineData("Compliance.Consent")]
    [InlineData("Compliance.KYC")]
    [InlineData("Content.Pages")]
    [InlineData("Features")]
    [InlineData("Identity.Authentication")]
    [InlineData("Identity.Authorization")]
    [InlineData("Identity.Context")]
    [InlineData("Identity.Tenants")]
    [InlineData("Identity.Users")]
    [InlineData("Localization")]
    [InlineData("Monitoring.SLA")]
    [InlineData("Notifications")]
    [InlineData("Resources")]
    [InlineData("Resources.Contents")]
    [InlineData("SharedKernel")]
    [InlineData("Tags")]
    public void CommonEnabledModules_ShouldContainEveryCommonModule(string module)
    {
        ModuleConfiguration.CommonEnabledModules.Should().Contain(module);
        ModuleConfiguration.DefaultEnabledModules.Should().Contain(module);
    }

    [Fact]
    public void DefaultEnabledModules_ShouldNotOverlapDisabledModules()
    {
        ModuleConfiguration.DefaultEnabledModules.Should()
            .NotIntersectWith(ModuleConfiguration.DefaultDisabledModules);
    }

    [Fact]
    public void HandlerTypeNames_ShouldContainExpectedTypes()
    {
        ModuleConfiguration.HandlerTypeNames.Should().BeEquivalentTo(
            "ICommandHandler",
            "IQueryHandler",
            "IRequestHandler");
    }

    [Fact]
    public void NewInstance_ShouldHaveDefaults()
    {
        var config = new ModuleConfiguration();

        config.EnabledModules.Should().BeSameAs(ModuleConfiguration.DefaultEnabledModules);
        config.AssemblyPrefix.Should().Be("GameGuild.");
        config.ExcludeTestAssemblies.Should().BeTrue();
    }

    [Theory]
    [InlineData("GameGuild.Content.Pages", true)]
    [InlineData("GameGuild.Resources.Contents", true)]
    [InlineData("GameGuild.ContentPages", true)]
    [InlineData("GameGuild.Unknown", false)]
    [InlineData(null, false)]
    public void IsEnabledAssembly_ShouldMatchDottedAndCompactModuleNames(string? assemblyName, bool expected)
    {
        var config = new ModuleConfiguration();

        config.IsEnabledAssembly(assemblyName).Should().Be(expected);
    }

    [Theory]
    [InlineData("GameGuild.Contest", false)]
    [InlineData("GameGuild.Tags", false)]
    [InlineData("GameGuild.Tags.UnitTests", true)]
    [InlineData("GameGuild.API.Tests", true)]
    [InlineData(null, false)]
    public void IsTestAssembly_ShouldOnlyMatchTestProjectNames(string? assemblyName, bool expected)
    {
        ModuleConfiguration.IsTestAssembly(assemblyName).Should().Be(expected);
    }

    [Fact]
    public void ProductComposition_ShouldOwnProductModuleSelection()
    {
        var composition = ApiProductComposition.Instance;

        composition.EnabledModules.Should().BeEquivalentTo(ModuleConfiguration.DefaultEnabledModules
            .Except(ModuleConfiguration.CommonEnabledModules));
        composition.DisabledModules.Should().BeEquivalentTo(ModuleConfiguration.DefaultDisabledModules);
    }

    [Fact]
    public void ModuleAssemblyCatalog_ShouldLoadEnabledModulesOnceInDeterministicOrder()
    {
        var configuration = new ModuleConfiguration
        {
            EnabledModules = ["AI", "AI", "Tags.UnitTests"]
        };

        var assemblies = ModuleAssemblyCatalog.Resolve(typeof(Program).Assembly, configuration);
        var names = assemblies.Select(assembly => assembly.GetName().Name!).ToArray();

        names.Should().ContainInOrder("GameGuild.API", "GameGuild.AI");
        names.Should().OnlyHaveUniqueItems();
        names.Should().OnlyContain(name => !ModuleConfiguration.IsTestAssembly(name));
    }

    [Fact]
    public void ModuleAssemblyCatalog_WhenEntryAssemblyIsConfigured_ShouldNotDuplicateIt()
    {
        var configuration = new ModuleConfiguration
        {
            EnabledModules = ["API"]
        };

        var assemblies = ModuleAssemblyCatalog.Resolve(typeof(Program).Assembly, configuration);

        assemblies.Should().ContainSingle().Which.Should().BeSameAs(typeof(Program).Assembly);
    }

    [Fact]
    public void ModuleAssemblyCatalog_ShouldDescribeEveryRequiredAssemblyInDeterministicOrder()
    {
        var configuration = new ModuleConfiguration();
        var expected = configuration.EnabledModules
            .Select(module => $"GameGuild.{module}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal);

        ModuleAssemblyCatalog.GetRequiredAssemblyNames(configuration).Should().Equal(expected);
    }

    [Fact]
    public void ModuleAssemblyCatalog_WhenTestExclusionIsDisabled_ShouldKeepExplicitTestModules()
    {
        var configuration = new ModuleConfiguration
        {
            EnabledModules = ["AI.UnitTests"],
            ExcludeTestAssemblies = false
        };

        ModuleAssemblyCatalog.GetRequiredAssemblyNames(configuration)
            .Should().Equal("GameGuild.AI.UnitTests");
    }

    [Fact]
    public void ModuleAssemblyCatalog_ShouldFailWhenAnEnabledModuleCannotBeLoaded()
    {
        var configuration = new ModuleConfiguration
        {
            EnabledModules = ["Missing.Required.Module"]
        };

        var action = () => ModuleAssemblyCatalog.Resolve(typeof(Program).Assembly, configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*GameGuild.Missing.Required.Module*");
    }
}
