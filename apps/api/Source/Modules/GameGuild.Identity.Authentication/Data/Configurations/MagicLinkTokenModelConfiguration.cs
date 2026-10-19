using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

public sealed class MagicLinkTokenModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MagicLinkToken>();
        entity.ToTable("magic_link_tokens", "gameguild.authentication");
        entity.HasKey(token => token.TokenHash);
        entity.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsFixedLength();
        entity.Property(token => token.UserId).HasColumnName("user_id").IsRequired();
        entity.Property(token => token.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        entity.Property(token => token.ExpiresAt).HasColumnName("expires_at").IsRequired();
        entity.Property(token => token.ConsumedAt).HasColumnName("consumed_at");
        entity.HasIndex(token => token.ExpiresAt).HasDatabaseName("ix_magic_link_tokens_expires_at");
    }
}
