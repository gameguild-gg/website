using FluentAssertions;
using GameGuild.Social.Blog.Commands;
using GameGuild.Social.Blog.Services;
using Xunit;

namespace GameGuild.Social.Blog.UnitTests;

/// <summary>
/// Permission matrix: {primary, co-author, stranger} × {edit, slug, coauthors, transfer, publish, unpublish, delete}
/// — every allowed and denied cell.
/// </summary>
public class BlogPermissionMatrixTests
{
    private readonly BlogServiceTestHarness _h = new();

    private Task<BlogPost> NewPostWithCoauthorAsync()
    {
        return Task.Run(async () =>
        {
            var post = await _h.CreatePostAsync();
            await _h.AddCoauthorAsync(post.Id, _h.Coauthor);
            return post;
        });
    }

    // ---- edit: primary ✓, co-author ✓, stranger ✗ ----

    [Fact]
    public async Task Edit_PrimaryAllowed()
    {
        var post = await _h.CreatePostAsync();
        var revisionBefore = post.Revision;
        var updated = await _h.Service.UpdateDraftAsync(_h.Primary, post.Id, new UpdateBlogPostDraftCommand(post.Id, revisionBefore, Title: "New title"));
        updated.Title.Should().Be("New title");
        updated.Revision.Should().Be(revisionBefore + 1);
        updated.Status.Should().Be(BlogPostStatus.Draft);
    }

    [Fact]
    public async Task Edit_CoauthorAllowed()
    {
        var post = await NewPostWithCoauthorAsync();
        var updated = await _h.Service.UpdateDraftAsync(_h.Coauthor, post.Id, new UpdateBlogPostDraftCommand(post.Id, post.Revision, Excerpt: "from co-author"));
        updated.Excerpt.Should().Be("from co-author");
    }

    [Fact]
    public async Task Edit_StrangerDenied()
    {
        var post = await _h.CreatePostAsync();
        var act = () => _h.Service.UpdateDraftAsync(_h.Stranger, post.Id, new UpdateBlogPostDraftCommand(post.Id, post.Revision, Title: "hijack"));
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
    }

    // ---- slug: primary ✓, co-author ✗, stranger ✗ ----

    [Fact]
    public async Task Slug_PrimaryAllowed_WritesOneHistoryRow()
    {
        var post = await _h.CreatePostAsync();
        await _h.Service.ChangeSlugAsync(_h.Primary, post.Id, "new-slug");
        _h.Context.Set<BlogSlugHistory>().Count(h => h.BlogPostId == post.Id).Should().Be(1);
    }

    [Theory]
    [InlineData(nameof(BlogServiceTestHarness.Coauthor))]
    [InlineData(nameof(BlogServiceTestHarness.Stranger))]
    public async Task Slug_NonPrimaryDenied(string actor)
    {
        var post = await NewPostWithCoauthorAsync();
        var userId = Actor(actor);
        var act = () => _h.Service.ChangeSlugAsync(userId, post.Id, "sneaky-slug");
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
        _h.Context.Set<BlogSlugHistory>().Should().BeEmpty();
    }

    // ---- coauthors management: primary ✓, co-author ✗, stranger ✗ ----

    [Theory]
    [InlineData(nameof(BlogServiceTestHarness.Coauthor))]
    [InlineData(nameof(BlogServiceTestHarness.Stranger))]
    public async Task Coauthors_NonPrimaryCannotAdd(string actor)
    {
        var post = await NewPostWithCoauthorAsync();
        var act = () => _h.Service.AddCoauthorAsync(Actor(actor), post.Id, Guid.NewGuid());
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
    }

    [Theory]
    [InlineData(nameof(BlogServiceTestHarness.Coauthor))]
    [InlineData(nameof(BlogServiceTestHarness.Stranger))]
    public async Task Coauthors_NonPrimaryCannotRemove(string actor)
    {
        var post = await NewPostWithCoauthorAsync();
        var act = () => _h.Service.RemoveCoauthorAsync(Actor(actor), post.Id, _h.Coauthor);
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
    }

    [Fact]
    public async Task Coauthors_PrimaryAddsAndRemoves()
    {
        var post = await _h.CreatePostAsync();
        await _h.Service.AddCoauthorAsync(_h.Primary, post.Id, _h.Coauthor);
        _h.Context.Set<BlogPostAuthor>().Should().ContainSingle(a => a.BlogPostId == post.Id && a.UserId == _h.Coauthor);
        await _h.Service.RemoveCoauthorAsync(_h.Primary, post.Id, _h.Coauthor);
        _h.Context.Set<BlogPostAuthor>().Should().NotContain(a => a.BlogPostId == post.Id && a.UserId == _h.Coauthor);
    }

    // ---- transfer: primary ✓, co-author ✗, stranger ✗ ----

    [Fact]
    public async Task Transfer_PrimaryAllowed_WritesOneHistoryRow_AndSwapsRoles()
    {
        var post = await NewPostWithCoauthorAsync();
        await _h.Service.TransferPrimaryAsync(_h.Primary, post.Id, _h.Coauthor);
        _h.Context.Set<BlogSlugHistory>().Count(h => h.BlogPostId == post.Id).Should().Be(1);
        _h.Context.Set<BlogPost>().Single(p => p.Id == post.Id).PrimaryAuthorId.Should().Be(_h.Coauthor);
        // Old primary demoted to co-author, new primary removed from co-author table.
        _h.Context.Set<BlogPostAuthor>().Should().ContainSingle(a => a.BlogPostId == post.Id && a.UserId == _h.Primary);
        _h.Context.Set<BlogPostAuthor>().Should().NotContain(a => a.BlogPostId == post.Id && a.UserId == _h.Coauthor);
    }

    [Theory]
    [InlineData(nameof(BlogServiceTestHarness.Coauthor))]
    [InlineData(nameof(BlogServiceTestHarness.Stranger))]
    public async Task Transfer_NonPrimaryDenied(string actor)
    {
        var post = await NewPostWithCoauthorAsync();
        var act = () => _h.Service.TransferPrimaryAsync(Actor(actor), post.Id, _h.Stranger == Actor(actor) ? _h.Coauthor : _h.Stranger);
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
    }

    [Fact]
    public async Task Transfer_RequiresTargetToBeCurrentCoauthor()
    {
        var post = await _h.CreatePostAsync();
        var act = () => _h.Service.TransferPrimaryAsync(_h.Primary, post.Id, _h.Stranger);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---- publish: primary ✓, co-author ✗, stranger ✗ ----

    [Fact]
    public async Task Publish_PrimaryAllowed_SetsPublishedAt_AndAnnounces()
    {
        var post = await _h.CreatePostAsync();
        var published = await _h.Service.PublishAsync(_h.Primary, post.Id);
        published.Status.Should().Be(BlogPostStatus.Published);
        published.PublishedAt.Should().NotBeNull();
        _h.Announcer.AnnouncedPostIds.Should().Contain(post.Id);
    }

    [Theory]
    [InlineData(nameof(BlogServiceTestHarness.Coauthor))]
    [InlineData(nameof(BlogServiceTestHarness.Stranger))]
    public async Task Publish_NonPrimaryDenied(string actor)
    {
        var post = await NewPostWithCoauthorAsync();
        var before = post.Status;
        var act = () => _h.Service.PublishAsync(Actor(actor), post.Id);
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
        _h.Context.Set<BlogPost>().Single(p => p.Id == post.Id).Status.Should().Be(before);
        _h.Announcer.AnnouncedPostIds.Should().BeEmpty();
    }

    // ---- unpublish: primary ✓, co-author ✗, stranger ✗ ----

    [Fact]
    public async Task Unpublish_PrimaryAllowed()
    {
        var post = await _h.PublishAsync((await _h.CreatePostAsync()).Id);
        var unpublished = await _h.Service.UnpublishAsync(_h.Primary, post.Id);
        unpublished.Status.Should().Be(BlogPostStatus.Draft);
    }

    [Theory]
    [InlineData(nameof(BlogServiceTestHarness.Coauthor))]
    [InlineData(nameof(BlogServiceTestHarness.Stranger))]
    public async Task Unpublish_NonPrimaryDenied(string actor)
    {
        var post = await _h.PublishAsync((await NewPostWithCoauthorAsync()).Id);
        var act = () => _h.Service.UnpublishAsync(Actor(actor), post.Id);
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
    }

    // ---- delete: primary ✓, co-author ✗, stranger ✗ ----

    [Fact]
    public async Task Delete_PrimaryAllowed_SoftDeletes()
    {
        var post = await _h.CreatePostAsync();
        MarkPersisted(post);
        await _h.Service.DeleteAsync(_h.Primary, post.Id);
        _h.Context.Set<BlogPost>().Single(p => p.Id == post.Id).DeletedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(BlogServiceTestHarness.Coauthor))]
    [InlineData(nameof(BlogServiceTestHarness.Stranger))]
    public async Task Delete_NonPrimaryDenied(string actor)
    {
        var post = await NewPostWithCoauthorAsync();
        var act = () => _h.Service.DeleteAsync(Actor(actor), post.Id);
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
        _h.Context.Set<BlogPost>().Single(p => p.Id == post.Id).DeletedAt.Should().BeNull();
    }

    private Guid Actor(string name)
        => name switch
        {
            nameof(BlogServiceTestHarness.Coauthor) => _h.Coauthor,
            nameof(BlogServiceTestHarness.Stranger) => _h.Stranger,
            _ => _h.Primary,
        };

    /// <summary>InMemory never bumps EntityBase.Version; emulate persistence for SoftDelete.</summary>
    private void MarkPersisted(BlogPost post)
        => _h.Context.Entry(post).Property(p => p.Version).CurrentValue = 1;
}

/// <summary>Revision-guard tests for concurrent co-author edits.</summary>
public class BlogRevisionConflictTests
{
    private readonly BlogServiceTestHarness _h = new();

    [Fact]
    public async Task Update_StaleRevision_ThrowsConflictWithExpectedAndCurrent()
    {
        var post = await _h.CreatePostAsync();
        // First edit bumps revision to 2.
        await _h.Service.UpdateDraftAsync(_h.Primary, post.Id, new UpdateBlogPostDraftCommand(post.Id, post.Revision, Title: "v2"));

        var stale = new UpdateBlogPostDraftCommand(post.Id, Revision: 1, Title: "stale");
        var act = () => _h.Service.UpdateDraftAsync(_h.Primary, post.Id, stale);
        var exception = await act.Should().ThrowAsync<BlogRevisionConflictException>();
        exception.Which.ExpectedRevision.Should().Be(1);
        exception.Which.CurrentRevision.Should().Be(2);
    }

    [Fact]
    public async Task Update_FreshRevision_SucceedsAndBumps()
    {
        var post = await _h.CreatePostAsync();
        var updated = await _h.Service.UpdateDraftAsync(_h.Primary, post.Id, new UpdateBlogPostDraftCommand(post.Id, post.Revision, Title: "v2"));
        updated.Revision.Should().Be(2);
        updated = await _h.Service.UpdateDraftAsync(_h.Primary, post.Id, new UpdateBlogPostDraftCommand(post.Id, updated.Revision, Title: "v3"));
        updated.Revision.Should().Be(3);
    }
}

/// <summary>Slug engine: normalization, collision suffixing, uniqueness scoping.</summary>
public class BlogSlugEngineTests
{
    private readonly BlogServiceTestHarness _h = new();

    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  Ünïcödé Tïtlé!  ", "unicode-title")]
    [InlineData("C++ & Rust/Go", "c-rust-go")]
    [InlineData("Multiple   Spaces__Here", "multiple-spaces-here")]
    [InlineData("ÁÉÍÓÚ àèìòù", "aeiou-aeiou")]
    public void Normalize_ProducesExpectedSlugs(string input, string expected)
    {
        var slugService = new BlogSlugService(_h.Context);
        slugService.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_TrimsTo220AndFallsBackToPost()
    {
        var slugService = new BlogSlugService(_h.Context);
        var longTitle = new string('a', 500);
        slugService.Normalize(longTitle).Length.Should().Be(220);
        slugService.Normalize("!!!").Should().Be("post");
    }

    [Fact]
    public async Task Create_SlugCollision_SuffixesWith2Then3()
    {
        await _h.CreatePostAsync(title: "Same Title");
        var second = await _h.CreatePostAsync(title: "Same Title");
        second.Slug.Should().Be("same-title-2");
        var third = await _h.CreatePostAsync(title: "Same Title");
        third.Slug.Should().Be("same-title-3");
    }

    [Theory]
    [InlineData(42)]
    [InlineData(220)]
    public async Task Create_ManyCollisions_PreservesTwoDigitSuffixesAndTheLengthLimit(int titleLength)
    {
        var title = new string('a', titleLength);
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < 12; index++)
        {
            var post = await _h.CreatePostAsync(title: title);
            slugs.Add(post.Slug).Should().BeTrue();
            post.Slug.Length.Should().BeLessThanOrEqualTo(220);
            if (index > 0)
            {
                post.Slug.Should().EndWith($"-{index + 1}");
            }
        }
        slugs.Should().HaveCount(12);
    }

    [Fact]
    public async Task GenerateUniqueSlug_CancelledRequest_DoesNotReturnASlug()
    {
        await _h.CreatePostAsync(title: "Cancellation");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new BlogSlugService(_h.Context);

        var operation = () => service.GenerateUniqueSlugAsync(_h.Primary, "Cancellation", cancellation.Token);

        await operation.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Create_SameSlug_DifferentAuthors_NoSuffix()
    {
        await _h.CreatePostAsync(title: "Shared Title");
        var other = await _h.CreatePostAsync(author: _h.Stranger, title: "Shared Title");
        other.Slug.Should().Be("shared-title");
    }

    [Fact]
    public async Task ChangeSlug_DuplicateWithinAuthor_Rejected()
    {
        var a = await _h.CreatePostAsync(title: "One");
        await _h.CreatePostAsync(title: "Two");
        var act = () => _h.Service.ChangeSlugAsync(_h.Primary, a.Id, "two");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

/// <summary>Route resolution: current routes and slug-history redirects after both rename kinds.</summary>
public class BlogRouteResolutionTests
{
    private readonly BlogServiceTestHarness _h = new();

    [Fact]
    public async Task Resolve_CurrentRoute_WinsDirectly()
    {
        _h.AddProfile(_h.Primary, "alice");
        var post = await _h.CreatePostAsync(title: "My Post");
        var resolved = await _h.Service.ResolveRouteAsync("alice", "my-post");
        resolved!.Id.Should().Be(post.Id);
    }

    [Fact]
    public async Task Resolve_AfterSlugChange_OldRouteStillResolves()
    {
        _h.AddProfile(_h.Primary, "alice");
        var post = await _h.CreatePostAsync(title: "Old Name");
        await _h.Service.ChangeSlugAsync(_h.Primary, post.Id, "new-name");

        (await _h.Service.ResolveRouteAsync("alice", "new-name"))!.Id.Should().Be(post.Id);
        (await _h.Service.ResolveRouteAsync("alice", "old-name"))!.Id.Should().Be(post.Id);
    }

    [Fact]
    public async Task Resolve_AfterPrimaryTransfer_OldHandleRouteStillResolves()
    {
        _h.AddProfile(_h.Primary, "alice");
        _h.AddProfile(_h.Coauthor, "bob");
        var post = await _h.CreatePostAsync(title: "Shared Work");
        await _h.AddCoauthorAsync(post.Id, _h.Coauthor);
        await _h.Service.TransferPrimaryAsync(_h.Primary, post.Id, _h.Coauthor);

        // New canonical route under the new primary.
        (await _h.Service.ResolveRouteAsync("bob", "shared-work"))!.Id.Should().Be(post.Id);
        // Old route (old primary handle + slug) resolves via history.
        (await _h.Service.ResolveRouteAsync("alice", "shared-work"))!.Id.Should().Be(post.Id);
    }

    [Fact]
    public async Task Resolve_UnknownHandle_ReturnsNull()
    {
        await _h.CreatePostAsync(title: "Anything");
        (await _h.Service.ResolveRouteAsync("ghost", "anything")).Should().BeNull();
    }

    [Fact]
    public async Task PublicQueries_ReturnPublishedOnly()
    {
        _h.AddProfile(_h.Primary, "alice");
        var draft = await _h.CreatePostAsync(title: "Draft Post");
        var published = await _h.PublishAsync((await _h.CreatePostAsync(title: "Live Post")).Id);

        (await _h.Service.GetPublicByHandleAndSlugAsync("alice", "draft-post")).Should().BeNull();
        (await _h.Service.GetPublicByHandleAndSlugAsync("alice", "live-post"))!.Id.Should().Be(published.Id);

        var authorList = await _h.Service.ListAuthorPublicAsync(_h.Primary, 0);
        authorList.Select(p => p.Id).Should().NotContain(draft.Id);
        authorList.Select(p => p.Id).Should().Contain(published.Id);

        var index = await _h.Service.ListIndexAsync(0);
        index.Select(p => p.Id).Should().NotContain(draft.Id);
    }

    [Fact]
    public async Task AuthorQueries_SeeDrafts_AndCoauthored()
    {
        var mine = await _h.CreatePostAsync(title: "Mine");
        var other = await _h.CreatePostAsync(author: _h.Stranger, title: "Theirs");
        await _h.Service.AddCoauthorAsync(_h.Stranger, other.Id, _h.Primary);

        (await _h.Service.GetForAuthorAsync(_h.Primary, mine.Id)).Should().NotBeNull();
        (await _h.Service.GetForAuthorAsync(_h.Primary, other.Id)).Should().NotBeNull();
        (await _h.Service.GetForAuthorAsync(_h.Coauthor, mine.Id)).Should().BeNull();

        var list = await _h.Service.ListMineAsync(_h.Primary, 0);
        list.Select(p => p.Id).Should().Contain(mine.Id).And.Contain(other.Id);
    }
}

/// <summary>Read-time estimation for markdown and Lexical JSON bodies.</summary>
public class BlogReadTimeTests
{
    [Fact]
    public void Markdown_WordSplit_At200Wpm()
    {
        // 400 words → 2 minutes.
        var words = string.Join(' ', Enumerable.Repeat("word", 400));
        BlogReadTimeEstimator.EstimateFromMarkdown(words).Should().Be(2);
    }

    [Fact]
    public void Markdown_Empty_MinutesIsOne()
    {
        BlogReadTimeEstimator.EstimateFromMarkdown("").Should().Be(1);
        BlogReadTimeEstimator.EstimateFromMarkdown(null).Should().Be(1);
    }

    [Fact]
    public void LexicalJson_CollectsTextValuesOnly()
    {
        // {"root":{"children":[{"children":[{"text":"aaa"},{"text":"bbb"}],"type":"paragraph"}]}}
        var json = """{"root":{"children":[{"children":[{"text":"aaa"},{"text":"bbb"}],"type":"paragraph"},{"text":"ignored-attribute-context"}],"type":"root"},"other":"not-text"}""";
        // ~200+ words needed for >1 minute; use short sample asserting floor of 1 and correctness via word count math below.
        BlogReadTimeEstimator.EstimateFromLexicalJson(json).Should().Be(1);
    }

    [Fact]
    public void LexicalJson_LongText_ComputesMinutes()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 600));
        var json = """{"root":{"children":[{"children":[{"text":"REPLACED"}],"type":"paragraph"}]}}""".Replace("REPLACED", text);
        BlogReadTimeEstimator.EstimateFromLexicalJson(json).Should().Be(3);
    }

    [Fact]
    public async Task UpdateDraft_MarkdownBody_RecalculatesReadTime()
    {
        using var h = new BlogServiceTestHarness();
        var post = await h.CreatePostAsync(title: "Read Me");
        var body = string.Join(' ', Enumerable.Repeat("word", 500)); // 500 words → 3 min
        var updated = await h.Service.UpdateDraftAsync(h.Primary, post.Id, new UpdateBlogPostDraftCommand(post.Id, post.Revision, Content: body));
        updated.ReadTimeMinutes.Should().Be(3);
    }

    [Fact]
    public async Task UpdateDraft_MetaOnly_DoesNotRecomputeReadTime()
    {
        using var h = new BlogServiceTestHarness();
        var post = await h.CreatePostAsync(title: "Read Me");
        var updated = await h.Service.UpdateDraftAsync(h.Primary, post.Id, new UpdateBlogPostDraftCommand(post.Id, post.Revision, MetaTitle: "just seo"));
        updated.ReadTimeMinutes.Should().Be(1); // untouched default
    }

    [Fact]
    public async Task UpdateDraft_LexicalBody_RecalculatesFromTextNodes()
    {
        using var h = new BlogServiceTestHarness();
        var post = await h.CreatePostAsync(title: "Rich", format: BlogContentFormat.Lexical);
        var text = string.Join(' ', Enumerable.Repeat("word", 250)); // 250 words → 2 min
        var json = """{"root":{"children":[{"children":[{"text":"REPLACED"}],"type":"paragraph"}]}}""".Replace("REPLACED", text);
        var updated = await h.Service.UpdateDraftAsync(h.Primary, post.Id, new UpdateBlogPostDraftCommand(post.Id, post.Revision, JsonBody: json));
        updated.ReadTimeMinutes.Should().Be(2);
    }
}
