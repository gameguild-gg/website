using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Entity Type Configuration for ExternalLogin
/// </summary>
public class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        // snake_case table name (module convention)
        builder.ToTable("externallogin", "gameguild.authentication");

        // Primary key
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").IsRequired();

        // Property configurations
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProviderKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.GrantedScopes).HasMaxLength(1024);
        builder.Property(x => x.ConsentedAt);
        builder.Property(x => x.ConsentVersion).IsRequired().HasDefaultValue(0);

        // Indexes — unique on (Provider, ProviderKey), non-unique on UserId
        builder.HasIndex(x => new { x.Provider, x.ProviderKey })
            .IsUnique()
            .HasDatabaseName("ix_externallogin_provider_provider_key");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_externallogin_user_id");
    }
}
