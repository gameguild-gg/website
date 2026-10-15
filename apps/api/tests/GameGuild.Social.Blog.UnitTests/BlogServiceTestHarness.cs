using FluentAssertions;
using GameGuild;
using GameGuild.Assets;
using GameGuild.Social.Blog.Configuration;
using GameGuild.Social.Blog.Services;
using GameGuild.Social.Follows;
using GameGuild.Social.Follows.Services;
using GameGuild.Social.Profiles;
using GameGuild.Social.Reactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Social.Blog.UnitTests;

/// <summary>
/// Service-level test fixture: InMemory context with the blog model + profiles + reactions, a mocked
/// moderation service, and helpers for actors/posts.
/// </summary>
public sealed class BlogServiceTestHarness : IDisposable
{
    public Guid Primary { get; } = Guid.NewGuid();
    public Guid Coauthor { get; } = Guid.NewGuid();
    public Guid Stranger { get; } = Guid.NewGuid();

    public MockModerationService Moderation { get; } = new();
    public RecordingAnnouncer Announcer { get; } = new();

    public BlogTestDbContext Context { get; }
    public IBlogPostService Service { get; }
    public IBlogViewCounterService ViewCounter { get; }

    public BlogServiceTestHarness()
    {
        var options = new DbContextOptionsBuilder<BlogTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Context = new BlogTestDbContext(options);
        var slugService = new BlogSlugService(Context);
        var manifest = new BlogAssetManifestService(Context);
        Service = new BlogPostService(Context, slugService, Moderation, Announcer, manifest);
        ViewCounter = new BlogViewCounterService(Context);
    }

    public async Task<BlogPost> CreatePostAsync(Guid? author = null, string title = "Hello World", BlogContentFormat format = BlogContentFormat.Markdown)
        => await Service.CreateAsync(author ?? Primary, title, format, null);

    public async Task<BlogPost> AddCoauthorAsync(Guid postId, Guid userId)
    {
        await Service.AddCoauthorAsync(Primary, postId, userId);
        return await Context.Set<BlogPost>().SingleAsync(p => p.Id == postId);
    }

    public async Task<BlogPost> PublishAsync(Guid postId)
        => await Service.PublishAsync(Primary, postId);

    public Task<BlogPost> PublishAsync(Guid postId, Guid? ogImageAssetReferenceId)
        => Service.PublishAsync(Primary, postId, ogImageAssetReferenceId);

    /// <summary>Soft-deletes via the service after faking the concurrency Version InMemory never bumps.</summary>
    public async Task DeletePostAsync(Guid postId)
    {
        Context.Entry(Context.Set<BlogPost>().Single(p => p.Id == postId)).Property(p => p.Version).CurrentValue = 1;
        await Service.DeleteAsync(Primary, postId);
    }

    public SocialProfile AddProfile(Guid userId, string handle)
    {
        var profile = new SocialProfile { Id = Guid.NewGuid(), UserId = userId, Handle = SocialProfile.NormalizeHandle(handle), DisplayName = handle };
        Context.Set<SocialProfile>().Add(profile);
        Context.SaveChanges();
        return profile;
    }

    public Reaction AddReaction(Guid userId, Guid targetId, ReactionType type = ReactionType.Like)
    {
        var reaction = Reaction.Create(userId, targetId, ReactionTargetType.BlogPost, type);
        Context.Set<Reaction>().Add(reaction);
        Context.SaveChanges();
        return reaction;
    }

    public void Dispose() => Context.Dispose();
}

/// <summary>InMemory context applying the blog model plus the SocialProfile, Reaction, and Asset shapes used by tests.</summary>
public sealed class BlogTestDbContext(DbContextOptions<BlogTestDbContext> options) : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        new BlogModelConfiguration().Configure(modelBuilder);
        modelBuilder.Entity<SocialProfile>(builder =>
        {
            builder.HasKey(profile => profile.Id);
            builder.Ignore(profile => profile.Skills);
            builder.Ignore(profile => profile.PortfolioItems);
        });
        modelBuilder.Entity<Reaction>(builder => builder.HasKey(reaction => reaction.Id));
        modelBuilder.Entity<AssetContent>(builder =>
        {
            builder.HasKey(content => content.Id);
            builder.Ignore(content => content.References);
            builder.Ignore(content => content.TransformedVersions);
        });
        modelBuilder.Entity<AssetReference>(builder =>
        {
            builder.HasKey(reference => reference.Id);
            builder.Ignore(reference => reference.Reports);
            builder.Ignore(reference => reference.Localizations);
            builder.Ignore(reference => reference.Revisions);
            builder
                .HasOne(reference => reference.Content)
                .WithMany()
                .HasForeignKey(reference => reference.AssetContentId);
        });
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

/// <summary>Scripted moderation service for block/mute-enforcement tests.</summary>
public sealed class MockModerationService : IUserModerationService
{
    public HashSet<(Guid, Guid)> BlockedPairs { get; } = [];

    public HashSet<(Guid MuterId, Guid MutedId)> MutedPairs { get; } = [];

    public Task<Result<bool>> AreUsersBlockedAsync(Guid userId1, Guid userId2, CancellationToken ct = default)
        => Task.FromResult(Result.Success(BlockedPairs.Contains((userId1, userId2)) || BlockedPairs.Contains((userId2, userId1))));

    public Task<Result<bool>> IsUserBlockedAsync(Guid blockingUserId, Guid blockedUserId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Result<List<Block>>> GetBlockedUsersAsync(Guid userId, int skip = 0, int take = 50, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Result<Block>> BlockUserAsync(Guid blockingUserId, Guid blockedUserId, string? reason = null, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Result> UnblockUserAsync(Guid blockingUserId, Guid blockedUserId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Result<bool>> IsUserMutedAsync(Guid mutingUserId, Guid mutedUserId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Result<List<Mute>>> GetMutedUsersAsync(Guid userId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var mutes = MutedPairs
            .Where(pair => pair.MuterId == userId)
            .Select(pair => Mute.Create(pair.MuterId, pair.MutedId))
            .ToList();
        return Task.FromResult(Result.Success(mutes));
    }

    public Task<Result<Mute>> MuteUserAsync(Guid mutingUserId, Guid mutedUserId, string? reason = null, DateTime? expiresAt = null, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Result> UnmuteUserAsync(Guid mutingUserId, Guid mutedUserId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Result<int>> CleanupExpiredMutesAsync(CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Result<FollowPrivacySettings>> GetPrivacySettingsAsync(Guid userId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<Result<FollowPrivacySettings>> UpdatePrivacySettingsAsync(Guid userId, bool isFollowerListPublic, bool isFollowingListPublic, bool allowFollowers, bool notifyOnNewFollower, bool showFollowerCount, bool showFollowingCount, CancellationToken ct = default)
        => throw new NotSupportedException();
}

/// <summary>Records publish announcements so tests can assert dispatch happened (or not).</summary>
public sealed class RecordingAnnouncer : IPublicationAnnouncer
{
    public List<Guid> AnnouncedPostIds { get; } = [];

    public Task AnnounceBlogPublishedAsync(BlogPost post, CancellationToken ct = default)
    {
        AnnouncedPostIds.Add(post.Id);
        return Task.CompletedTask;
    }
}
