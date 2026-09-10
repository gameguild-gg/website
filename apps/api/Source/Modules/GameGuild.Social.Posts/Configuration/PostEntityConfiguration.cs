using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Social.Posts.Configuration;

/// <summary>
/// EF Core configurations for Post and related entities
/// </summary>
public class PostEntityConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Content)
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(p => p.MediaUrl)
            .HasMaxLength(2000);

        builder.HasIndex(p => p.AuthorId);
        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => p.Visibility);
        builder.HasIndex(p => p.IsPinned);
        builder.HasIndex(p => p.CreatedAt);
        builder.HasIndex(p => new { p.AuthorId, p.RepostOfPostId })
            .IsUnique()
            .HasFilter("\"RepostOfPostId\" IS NOT NULL AND \"DeletedAt\" IS NULL");
    }
}

public class PostCommentEntityConfiguration : IEntityTypeConfiguration<PostComment>
{
    public void Configure(EntityTypeBuilder<PostComment> builder)
    {
        builder.ToTable("post_comments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.HasIndex(c => c.PostId);
        builder.HasIndex(c => c.AuthorId);
        builder.HasIndex(c => c.ParentCommentId);
    }
}

public class PostStatisticsEntityConfiguration : IEntityTypeConfiguration<PostStatistics>
{
    public void Configure(EntityTypeBuilder<PostStatistics> builder)
    {
        builder.ToTable("post_statistics");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.PostId)
            .IsUnique();

        builder.HasIndex(s => s.TrendingScore);
        builder.HasIndex(s => s.EngagementScore);
    }
}

public class PostContentReferenceEntityConfiguration : IEntityTypeConfiguration<PostContentReference>
{
    public void Configure(EntityTypeBuilder<PostContentReference> builder)
    {
        builder.ToTable("post_content_references");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReferenceType)
            .HasMaxLength(50);

        builder.Property(r => r.ResourceType)
            .HasMaxLength(100);

        builder.Property(r => r.Context)
            .HasMaxLength(500);

        builder.HasIndex(r => r.PostId);
        builder.HasIndex(r => r.ReferencedResourceId);
        builder.HasIndex(r => r.ReferenceType);
    }
}

public class PostFollowerEntityConfiguration : IEntityTypeConfiguration<PostFollower>
{
    public void Configure(EntityTypeBuilder<PostFollower> builder)
    {
        builder.ToTable("post_followers");

        builder.HasKey(f => f.Id);

        builder.HasIndex(f => f.PostId);
        builder.HasIndex(f => f.UserId);
        builder.HasIndex(f => new { f.PostId, f.UserId })
            .IsUnique();
    }
}

public class PostTagEntityConfiguration : IEntityTypeConfiguration<PostTag>
{
    public void Configure(EntityTypeBuilder<PostTag> builder)
    {
        builder.ToTable("post_tags");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.DisplayName)
            .HasMaxLength(100);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.Category)
            .HasMaxLength(50);

        builder.Property(t => t.Color)
            .HasMaxLength(7);

        builder.HasIndex(t => t.Name)
            .IsUnique();

        builder.HasIndex(t => t.Category);
        builder.HasIndex(t => t.UsageCount);
    }
}

public class PostTagAssignmentEntityConfiguration : IEntityTypeConfiguration<PostTagAssignment>
{
    public void Configure(EntityTypeBuilder<PostTagAssignment> builder)
    {
        builder.ToTable("post_tag_assignments");

        builder.HasKey(a => a.Id);

        builder.HasIndex(a => a.PostId);
        builder.HasIndex(a => a.TagId);
        builder.HasIndex(a => new { a.PostId, a.TagId })
            .IsUnique();
    }
}

public class PostViewEntityConfiguration : IEntityTypeConfiguration<PostView>
{
    public void Configure(EntityTypeBuilder<PostView> builder)
    {
        builder.ToTable("post_views");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.IpAddress)
            .HasMaxLength(45);

        builder.Property(v => v.UserAgent)
            .HasMaxLength(500);

        builder.Property(v => v.Referrer)
            .HasMaxLength(500);

        builder.HasIndex(v => v.PostId);
        builder.HasIndex(v => v.UserId);
        builder.HasIndex(v => v.ViewedAt);
        builder.HasIndex(v => v.IpAddress);
    }
}

public class PostLikeEntityConfiguration : IEntityTypeConfiguration<PostLike>
{
    public void Configure(EntityTypeBuilder<PostLike> builder)
    {
        builder.ToTable("post_likes");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.ReactionType)
            .HasMaxLength(20);

        builder.HasIndex(l => l.PostId);
        builder.HasIndex(l => l.UserId);
        builder.HasIndex(l => new { l.PostId, l.UserId })
            .IsUnique();
    }
}
