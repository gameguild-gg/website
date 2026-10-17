using FluentAssertions;
using GameGuild.Assets;
using GameGuild.Social.Blog.Commands;
using GameGuild.Social.Blog.Services;
using Xunit;

namespace GameGuild.Social.Blog.UnitTests;

/// <summary>
/// asset:// manifest support: usage guard over post bodies (draft + published),
/// safe-to-serve publish gate naming offending asset ids, OG image derivation.
/// </summary>
public sealed class BlogAssetManifestTests : IDisposable
{
    private readonly BlogServiceTestHarness _h = new();

    private async Task<BlogPost> CreateDraftWithBodyAsync(
        string content,
        BlogContentFormat format = BlogContentFormat.Markdown,
        string? jsonBody = null)
    {
        var post = await _h.CreatePostAsync(format: format);
        return await _h.Service.UpdateDraftAsync(
            _h.Primary, post.Id, new UpdateBlogPostDraftCommand(post.Id, post.Revision, Content: content, JsonBody: jsonBody));
    }

    private AssetReference AddAsset(
        Guid ownerId,
        ModerationStatus moderation = ModerationStatus.Approved,
        VirusScanStatus virus = VirusScanStatus.Clean)
    {
        var content = new AssetContent("bucket", $"key-{Guid.NewGuid():N}", new string('a', 64), "image/png", 4, null, null)
        {
            VirusScanStatus = virus,
            ModerationStatus = moderation,
        };
        var reference = new AssetReference(content.Id, ownerId, "cover.png", AssetAccessPolicy.Private, null, null)
        {
            Content = content,
        };
        _h.Context.AddRange(content, reference);
        _h.Context.SaveChanges();
        return reference;
    }

    // ---- usage guard ----

    [Fact]
    public async Task Guard_InUse_WhenDraftBodyReferencesAsset()
    {
        var asset = AddAsset(_h.Primary);
        await CreateDraftWithBodyAsync($"![cover](asset://{asset.Id})");

        var guard = new BlogAssetUsageGuard(_h.Context);
        (await guard.IsInUseAsync(asset.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Guard_InUse_WhenPublishedJsonBodyReferencesAsset()
    {
        var asset = AddAsset(_h.Primary);
        var post = await CreateDraftWithBodyAsync(
            "lexical", BlogContentFormat.Lexical, "{\"root\":{\"url\":\"asset://" + asset.Id + "\"}}");
        await _h.PublishAsync(post.Id);

        var guard = new BlogAssetUsageGuard(_h.Context);
        (await guard.IsInUseAsync(asset.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Guard_NotInUse_AfterIdRemovedFromBody()
    {
        var asset = AddAsset(_h.Primary);
        var post = await CreateDraftWithBodyAsync($"![cover](asset://{asset.Id})");
        await _h.Service.UpdateDraftAsync(
            _h.Primary, post.Id, new UpdateBlogPostDraftCommand(post.Id, post.Revision, Content: "plain text now"));

        var guard = new BlogAssetUsageGuard(_h.Context);
        (await guard.IsInUseAsync(asset.Id)).Should().BeFalse();
    }

    // ---- publish gate ----

    [Fact]
    public async Task Publish_Blocked_WhenBodyAssetRejected_ErrorNamesAssetId()
    {
        var asset = AddAsset(_h.Primary, ModerationStatus.Rejected);
        var post = await CreateDraftWithBodyAsync($"![cover](asset://{asset.Id})");

        var act = () => _h.PublishAsync(post.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{asset.Id}*");
        Reload(post.Id).Status.Should().Be(BlogPostStatus.Draft);
        _h.Announcer.AnnouncedPostIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_Blocked_WhenBodyAssetVirusNotClean_ErrorNamesAssetId()
    {
        var asset = AddAsset(_h.Primary, virus: VirusScanStatus.Infected);
        var post = await CreateDraftWithBodyAsync($"![cover](asset://{asset.Id})");

        var act = () => _h.PublishAsync(post.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{asset.Id}*");
    }

    [Fact]
    public async Task Publish_Blocked_WhenBodyAssetMissing_FailClosed()
    {
        var missing = Guid.NewGuid();
        var post = await CreateDraftWithBodyAsync($"asset://{missing}");

        var act = () => _h.PublishAsync(post.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{missing}*");
    }

    [Fact]
    public async Task Publish_Succeeds_WhenAllBodyAssetsApprovedAndClean()
    {
        var approved = AddAsset(_h.Primary);
        var warned = AddAsset(_h.Primary, ModerationStatus.ApprovedWithWarning);
        var post = await CreateDraftWithBodyAsync($"![a](asset://{approved.Id}) ![b](asset://{warned.Id})");

        var published = await _h.PublishAsync(post.Id);

        published.Status.Should().Be(BlogPostStatus.Published);
        _h.Announcer.AnnouncedPostIds.Should().Contain(post.Id);
    }

    [Fact]
    public async Task Publish_Succeeds_WithExternalHttpUrlsOnly()
    {
        var post = await CreateDraftWithBodyAsync("![x](https://cdn.example.com/pic.png) [link](http://example.com)");

        var published = await _h.PublishAsync(post.Id);

        published.Status.Should().Be(BlogPostStatus.Published);
    }

    [Fact]
    public async Task Publish_Blocked_WhenJsonBodyAssetPending()
    {
        var asset = AddAsset(_h.Primary, ModerationStatus.Pending);
        var post = await CreateDraftWithBodyAsync(
            "lexical", BlogContentFormat.Lexical, "{\"root\":{\"url\":\"asset://" + asset.Id + "\"}}");

        var act = () => _h.PublishAsync(post.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{asset.Id}*");
    }

    // ---- OG image derivation ----

    [Fact]
    public async Task Publish_DerivesOgImageUrl_WhenOgAssetParamSet()
    {
        var og = AddAsset(_h.Primary);
        var post = await CreateDraftWithBodyAsync("text only");

        var published = await _h.PublishAsync(post.Id, og.Id);

        published.OgImageUrl.Should().Be($"/api/assets/{og.Id}/content");
        published.Status.Should().Be(BlogPostStatus.Published);
    }

    [Fact]
    public async Task Publish_Blocked_WhenOgAssetRejected()
    {
        var og = AddAsset(_h.Primary, ModerationStatus.Blocked);
        var post = await CreateDraftWithBodyAsync("text only");

        var act = () => _h.PublishAsync(post.Id, og.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{og.Id}*");
    }

    [Fact]
    public async Task Publish_Blocked_WhenOgAssetNotOwnedByPublisher()
    {
        var og = AddAsset(Guid.NewGuid());
        var post = await CreateDraftWithBodyAsync("text only");

        var act = () => _h.PublishAsync(post.Id, og.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{og.Id}*");
    }

    // ---- extraction ----

    [Fact]
    public void ExtractAssetIds_UnionsAndDedupsAcrossContentAndJsonBody()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var ids = BlogAssetManifestService.ExtractAssetIds(
            $"asset://{a} plain",
            $$"""{"img":"asset://{{b}}","again":"asset://{{a.ToString().ToUpperInvariant()}}"}""");

        ids.Should().BeEquivalentTo([a, b]);
    }

    [Fact]
    public void ExtractAssetIds_IgnoresHttpUrlsAndMalformedUris()
    {
        var ids = BlogAssetManifestService.ExtractAssetIds("https://x/a.png asset://not-a-uuid", null);

        ids.Should().BeEmpty();
    }

    private BlogPost Reload(Guid postId) =>
        _h.Context.Set<BlogPost>().Single(p => p.Id == postId);

    public void Dispose() => _h.Dispose();
}
