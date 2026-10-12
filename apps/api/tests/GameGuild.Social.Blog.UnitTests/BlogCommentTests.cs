using FluentAssertions;
using GameGuild.Social.Blog.Commands;
using GameGuild.Social.Blog.Services;
using Xunit;

namespace GameGuild.Social.Blog.UnitTests;

/// <summary>Comment lifecycle: create guards, depth limit, block enforcement, moderation deletes.</summary>
public class BlogCommentTests
{
    private readonly BlogServiceTestHarness _h = new();

    private async Task<BlogPost> PublishedPostAsync()
        => await _h.PublishAsync((await _h.CreatePostAsync(title: "Commentable")).Id);

    [Fact]
    public async Task Add_OnDraftPost_Rejected()
    {
        var post = await _h.CreatePostAsync();
        var act = () => _h.Service.AddCommentAsync(_h.Stranger, post.Id, "hi", null);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Add_OnPublishedPost_Succeeds_IncrementsCount_WithoutUpdatedAtBump()
    {
        var post = await PublishedPostAsync();
        var updatedAtBefore = _h.Context.Set<BlogPost>().Single(p => p.Id == post.Id).UpdatedAt;

        await _h.Service.AddCommentAsync(_h.Stranger, post.Id, "nice post", null);

        var stored = _h.Context.Set<BlogPost>().Single(p => p.Id == post.Id);
        stored.CommentsCount.Should().Be(1);
        stored.UpdatedAt.Should().Be(updatedAtBefore);
    }

    [Fact]
    public async Task Add_WhenCommentsDisabled_Rejected()
    {
        var post = await PublishedPostAsync();
        await _h.Service.UpdateDraftAsync(_h.Primary, post.Id, new UpdateBlogPostDraftCommand(post.Id, post.Revision, AllowComments: false));
        var act = () => _h.Service.AddCommentAsync(_h.Stranger, post.Id, "hi", null);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Add_ReplyToRoot_Succeeds_Depth2Rejected()
    {
        var post = await PublishedPostAsync();
        var root = await _h.Service.AddCommentAsync(_h.Stranger, post.Id, "root", null);
        var reply = await _h.Service.AddCommentAsync(_h.Primary, post.Id, "reply", root.Id);
        reply.ParentCommentId.Should().Be(root.Id);

        var act = () => _h.Service.AddCommentAsync(_h.Stranger, post.Id, "nested", reply.Id);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Add_ParentFromOtherPost_Rejected()
    {
        var postA = await PublishedPostAsync();
        var postB = await PublishedPostAsync();
        var rootB = await _h.Service.AddCommentAsync(_h.Stranger, postB.Id, "on B", null);
        var act = () => _h.Service.AddCommentAsync(_h.Stranger, postA.Id, "cross-parent", rootB.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Add_BlockedEitherDirection_Rejected()
    {
        var post = await PublishedPostAsync();
        _h.Moderation.BlockedPairs.Add((_h.Stranger, _h.Primary));
        var act = () => _h.Service.AddCommentAsync(_h.Stranger, post.Id, "blocked comment", null);
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
    }

    [Fact]
    public async Task Add_ReverseBlockAlsoRejected()
    {
        var post = await PublishedPostAsync();
        _h.Moderation.BlockedPairs.Add((_h.Primary, _h.Stranger)); // primary blocked the commenter
        var act = () => _h.Service.AddCommentAsync(_h.Stranger, post.Id, "blocked comment", null);
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
    }

    [Fact]
    public async Task Add_NotBlocked_Succeeds()
    {
        var post = await PublishedPostAsync();
        var comment = await _h.Service.AddCommentAsync(_h.Stranger, post.Id, "friendly", null);
        comment.Content.Should().Be("friendly");
    }

    [Fact]
    public async Task Delete_ByCommentAuthor_SoftDeletesAndDecrements()
    {
        var post = await PublishedPostAsync();
        var comment = await _h.Service.AddCommentAsync(_h.Stranger, post.Id, "bye", null);

        await _h.Service.DeleteCommentAsync(_h.Stranger, comment.Id);

        var stored = _h.Context.Set<BlogComment>().Single(c => c.Id == comment.Id);
        stored.DeletedAt.Should().NotBeNull();
        stored.DeletedByUserId.Should().Be(_h.Stranger);
        _h.Context.Set<BlogPost>().Single(p => p.Id == post.Id).CommentsCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_ByPostPrimary_Moderates()
    {
        var post = await PublishedPostAsync();
        var comment = await _h.Service.AddCommentAsync(_h.Stranger, post.Id, "rude", null);
        await _h.Service.DeleteCommentAsync(_h.Primary, comment.Id);
        _h.Context.Set<BlogComment>().Single(c => c.Id == comment.Id).DeletedByUserId.Should().Be(_h.Primary);
    }

    [Fact]
    public async Task Delete_ByCoauthor_Moderates()
    {
        var post = await PublishedPostAsync();
        await _h.AddCoauthorAsync(post.Id, _h.Coauthor);
        var comment = await _h.Service.AddCommentAsync(_h.Stranger, post.Id, "rude", null);
        await _h.Service.DeleteCommentAsync(_h.Coauthor, comment.Id);
        _h.Context.Set<BlogComment>().Single(c => c.Id == comment.Id).DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_ByStranger_Denied()
    {
        var post = await PublishedPostAsync();
        var outsider = Guid.NewGuid();
        var comment = await _h.Service.AddCommentAsync(_h.Stranger, post.Id, "mine", null);
        var act = () => _h.Service.DeleteCommentAsync(outsider, comment.Id);
        await act.Should().ThrowAsync<BlogAccessDeniedException>();
        _h.Context.Set<BlogComment>().Single(c => c.Id == comment.Id).DeletedAt.Should().BeNull();
    }
}

/// <summary>Handler-level actor-identity tests: identity always from IActorContextAccessor.</summary>
public class BlogCommandHandlerActorTests
{
    [Fact]
    public async Task Handlers_WithoutAuthenticatedActor_Throw()
    {
        using var h = new BlogServiceTestHarness();
        var accessor = new StubAccessor(GameGuild.Identity.Context.Actors.ActorContext.Anonymous);
        var handler = new GameGuild.Social.Blog.Commands.CreateBlogPostCommandHandler(h.Service, accessor);
        var act = () => handler.Handle(new GameGuild.Social.Blog.Commands.CreateBlogPostCommand("T", BlogContentFormat.Markdown), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handlers_UseActorIdentity_NotBodyFields()
    {
        using var h = new BlogServiceTestHarness();
        var actorId = Guid.NewGuid();
        var accessor = new StubAccessor(Actor(actorId));
        var handler = new GameGuild.Social.Blog.Commands.CreateBlogPostCommandHandler(h.Service, accessor);

        var post = await handler.Handle(new GameGuild.Social.Blog.Commands.CreateBlogPostCommand("Actor Owned", BlogContentFormat.Markdown), CancellationToken.None);

        post.PrimaryAuthorId.Should().Be(actorId);
        post.Slug.Should().Be("actor-owned");
        post.Revision.Should().Be(1);
    }

    private static GameGuild.Identity.Context.Actors.ActorContext Actor(Guid subjectId, bool authenticated = true)
        => new()
        {
            ActorKind = GameGuild.Identity.Context.Actors.ActorKind.User,
            SubjectId = subjectId.ToString(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = authenticated,
        };

    private sealed class StubAccessor(GameGuild.Identity.Context.Actors.ActorContext context)
        : GameGuild.Identity.Context.Actors.IActorContextAccessor
    {
        public GameGuild.Identity.Context.Actors.ActorContext ActorContext => context;
        public void SetActorContext(GameGuild.Identity.Context.Actors.ActorContext value) { }
        public void ClearActorContext() { }

        // The blog tests never dispatch nested actors; the scope matches this stub's
        // no-op SetActorContext semantics (install nothing, restore nothing).
        public GameGuild.Identity.Context.Actors.IActorContextScope BeginActorContextScope(
            GameGuild.Identity.Context.Actors.ActorContext value) => new NoopScope();

        private sealed class NoopScope : GameGuild.Identity.Context.Actors.IActorContextScope
        {
            public void Dispose() { }
        }
    }
}
