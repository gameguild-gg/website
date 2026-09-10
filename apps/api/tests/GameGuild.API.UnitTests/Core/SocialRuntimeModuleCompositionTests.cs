using FluentAssertions;
using GameGuild.API.Setup;

namespace GameGuild.API.UnitTests.Core;

public sealed class SocialRuntimeModuleCompositionTests
{
    [Fact]
    public void ProductComposition_EnablesEveryModuleRequiredByTheSocialFeed()
    {
        ApiProductComposition.Instance.EnabledModules.Should().Contain(
        [
            "Social.Feed",
            "Social.Follows",
            "Social.Posts",
            "Social.Profiles",
            "Social.Reactions"
        ]);
    }
}
