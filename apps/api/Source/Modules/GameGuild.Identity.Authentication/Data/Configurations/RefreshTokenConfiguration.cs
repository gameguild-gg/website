using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Entity Type Configuration for RefreshToken
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // Configure table name (must match existing migration)
        builder.ToTable("refreshtoken", "gameguild.authentication");

        // Configure primary key
        builder.HasKey(x => x.Id);

        // Configure Id property
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();

        // Property configurations
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.ParentTokenId);
        builder.Property(x => x.SessionId);
        builder.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ParentTokenId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserSession>().WithMany().HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Token).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.IsRevoked).IsRequired();
        builder.Property(x => x.RevokedByIp).HasMaxLength(45);
        builder.Property(x => x.ReplacedByToken).HasMaxLength(500);
        builder.Property(x => x.CreatedByIp).HasMaxLength(45).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Ignore(x => x.IsExpired);
        builder.Ignore(x => x.IsActive);

        // Indexes
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_refreshtoken_user_id");
        builder.HasIndex(x => x.ParentTokenId).HasDatabaseName("ix_refreshtoken_parent_token_id");
        builder.HasIndex(x => x.SessionId).HasDatabaseName("ix_refreshtoken_session_id");
        builder.HasIndex(x => x.Token).IsUnique().HasDatabaseName("ix_refreshtoken_token");
        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("ix_refreshtoken_expires_at");
    }
}
