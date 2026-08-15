using System.Reflection;
using FluentAssertions;
using GameGuild.API.Database;
using Moq;

namespace GameGuild.API.UnitTests.Database;

public sealed class ApplicationDbContextAssemblyLoadingTests
{
    [Fact]
    public void LoadTypes_WhenAssemblyPartiallyLoads_ReturnsAvailableTypes()
    {
        var exception = new ReflectionTypeLoadException(
            [typeof(ApplicationDbContextAssemblyLoadingTests), null],
            [new TypeLoadException("Unavailable dependency"), null]);
        var assembly = new Mock<Assembly>();
        assembly.Setup(value => value.GetTypes()).Throws(exception);

        InvokePrivate<IEnumerable<Type>>("LoadTypes", assembly.Object)
            .Should().ContainSingle().Which.Should().Be(typeof(ApplicationDbContextAssemblyLoadingTests));
    }

    [Fact]
    public void TryLoadReferencedAssembly_LoadsQueuesSkipsDuplicatesAndContainsFailures()
    {
        var reference = new AssemblyName("Common.Module");
        var loadedAssembly = new Mock<Assembly>().Object;
        var assemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>();
        var loadCount = 0;
        Func<AssemblyName, Assembly> loader = _ =>
        {
            loadCount++;
            return loadedAssembly;
        };

        InvokePrivate<object?>("TryLoadReferencedAssembly", reference, assemblies, pending, loader);
        InvokePrivate<object?>("TryLoadReferencedAssembly", reference, assemblies, pending, loader);

        loadCount.Should().Be(1);
        assemblies.Should().ContainKey("Common.Module").WhoseValue.Should().BeSameAs(loadedAssembly);
        pending.Should().ContainSingle().Which.Should().BeSameAs(loadedAssembly);

        var failedAssemblies = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var failedPending = new Queue<Assembly>();
        Func<AssemblyName, Assembly> failingLoader = _ => throw new FileNotFoundException("Missing module");

        var act = () => InvokePrivate<object?>(
            "TryLoadReferencedAssembly",
            new AssemblyName("Missing.Module"),
            failedAssemblies,
            failedPending,
            failingLoader);

        act.Should().NotThrow();
        failedAssemblies.Should().BeEmpty();
        failedPending.Should().BeEmpty();
    }

    [Theory]
    [InlineData("GameGuild.SharedKernel", true)]
    [InlineData("External.Library", false)]
    [InlineData(null, false)]
    public void IsGameGuildAssemblyName_ShouldOnlyMatchProductAssemblies(string? name, bool expected)
    {
        InvokePrivate<bool>("IsGameGuildAssemblyName", name).Should().Be(expected);
    }

    [Fact]
    public void ForceLoadGameGuildAssembliesFromOutput_IgnoresInvalidOptionalModule()
    {
        var probePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            $"GameGuild.InvalidModule.{Guid.NewGuid():N}.dll");

        try
        {
            File.WriteAllText(probePath, "not a managed assembly");

            var act = () => InvokePrivate<object?>("ForceLoadGameGuildAssembliesFromOutput");

            act.Should().NotThrow();
        }
        finally
        {
            File.Delete(probePath);
        }
    }

    private static T InvokePrivate<T>(string name, params object?[] arguments)
    {
        var method = typeof(ApplicationDbContext).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        var value = method!.Invoke(null, arguments);
        return value is null ? default! : value.Should().BeAssignableTo<T>().Subject;
    }
}
