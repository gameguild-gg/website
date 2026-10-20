using FluentAssertions;
using Xunit;

namespace GameGuild.Resources.UnitTests;

// Registry resets temporarily remove the built-in entries observed by other tests.
[CollectionDefinition(nameof(ResourceUsageTypeRegistry), DisableParallelization = true)]
public sealed class ResourceUsageTypeRegistryCollection;

[Collection(nameof(ResourceUsageTypeRegistry))]
public sealed class ResourceUsageTypeRegistrySchedulingTests
{
    [Fact]
    public void RegistryMutations_RunInAnExclusiveCollection()
    {
        var definition = typeof(ResourceUsageTypeRegistryCollection).GetCustomAttributesData()
            .Single(attribute => attribute.AttributeType == typeof(CollectionDefinitionAttribute));

        definition.ConstructorArguments.Should().ContainSingle().Which.Value.Should().Be(nameof(ResourceUsageTypeRegistry));
        definition.NamedArguments.Should()
            .ContainSingle(argument => argument.MemberName == nameof(CollectionDefinitionAttribute.DisableParallelization))
            .Which.TypedValue.Value.Should().Be(true);

        var mutatingTests = new[]
        {
            typeof(DependencyInjectionTests),
            typeof(ResourceUsageTypeRegistryTests),
            typeof(ResourceInfrastructureCoverageCompletionTests),
            typeof(ResourceRegistryCoverageCompletionTests)
        };

        foreach (var testType in mutatingTests)
        {
            var collection = testType.GetCustomAttributesData()
                .Single(attribute => attribute.AttributeType == typeof(CollectionAttribute));

            collection.ConstructorArguments.Should().ContainSingle().Which.Value.Should().Be(nameof(ResourceUsageTypeRegistry));
        }
    }
}
