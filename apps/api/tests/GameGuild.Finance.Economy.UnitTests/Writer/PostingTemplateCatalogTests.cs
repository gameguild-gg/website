using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Writer;

namespace GameGuild.Finance.Economy.UnitTests.Writer;

public sealed class PostingTemplateCatalogTests
{
    [Fact]
    public void CatalogRegistersEveryTemplateExactlyOnceAtCurrentVersion()
    {
        PostingTemplateCatalog.All.Should().HaveCount(Enum.GetValues<PostingTemplateKind>().Length);
        PostingTemplateCatalog.All.Select(item => item.Kind)
            .Should().BeEquivalentTo(Enum.GetValues<PostingTemplateKind>());
        PostingTemplateCatalog.All.Should().OnlyContain(item => item.Version == PostingTemplate.CurrentVersion);
        PostingTemplateCatalog.All.Should().OnlyContain(item => item.RequiresReserveAuthorization);
        PostingTemplateCatalog.All.GroupBy(item => (item.Kind, item.Version))
            .Should().OnlyContain(group => group.Count() == 1);
    }

    [Theory]
    [InlineData(PostingTemplateKind.ConfirmedTopUpMint, PostingAuthority.ProviderConfirmation, 2, SourceConfirmationState.Confirmed)]
    [InlineData(PostingTemplateKind.HardToSoftConversion, PostingAuthority.WalletOwner, 4, null)]
    [InlineData(PostingTemplateKind.SystemBackedGrant, PostingAuthority.PlatformSystem, 4, null)]
    [InlineData(PostingTemplateKind.PayoutSuccess, PostingAuthority.PayoutCoordinator, 2, null)]
    [InlineData(PostingTemplateKind.AdminWithdrawalFailure, PostingAuthority.Administrator, 2, null)]
    public void CatalogCarriesImmutableProcedureShape(
        PostingTemplateKind kind,
        PostingAuthority authority,
        int lineCount,
        SourceConfirmationState? requiredSourceState)
    {
        var registration = PostingTemplateCatalog.Find(kind, PostingTemplate.CurrentVersion);

        registration.Should().NotBeNull();
        registration!.Authority.Should().Be(authority);
        registration.LineCount.Should().Be(lineCount);
        registration.RequiredSourceState.Should().Be(requiredSourceState);
    }

    [Fact]
    public void CatalogFailsClosedForUnknownVersionOrKind()
    {
        PostingTemplateCatalog.Find(PostingTemplateKind.Spend, PostingTemplate.CurrentVersion + 1).Should().BeNull();
        PostingTemplateCatalog.Find((PostingTemplateKind)int.MaxValue, PostingTemplate.CurrentVersion).Should().BeNull();
    }

    [Fact]
    public void VariableTemplateLineCountsRespectBothLowerAndOptionalUpperBounds()
    {
        var unbounded = PostingTemplateCatalog.Find(PostingTemplateKind.BountyEscrow, PostingTemplate.CurrentVersion)!;
        var bounded = new PostingTemplateRegistration(
            PostingTemplateKind.Spend,
            PostingTemplate.CurrentVersion,
            PostingAuthority.WalletOwner,
            2,
            null,
            true,
            4);

        unbounded.AllowsLineCount(1).Should().BeFalse();
        unbounded.AllowsLineCount(2).Should().BeTrue();
        bounded.AllowsLineCount(1).Should().BeFalse();
        bounded.AllowsLineCount(2).Should().BeTrue();
        bounded.AllowsLineCount(4).Should().BeTrue();
        bounded.AllowsLineCount(5).Should().BeFalse();
    }
}
