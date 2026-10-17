using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Entity Type Configuration for <see cref="WebhookSecretRotation"/> (issue #408).
/// </summary>
public class WebhookSecretRotationConfiguration : IEntityTypeConfiguration<WebhookSecretRotation>
{
    public void Configure(EntityTypeBuilder<WebhookSecretRotation> builder)
    {
        builder.ToTable("WebhookSecretRotations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderKey)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EncryptedPreviousSecret)
            .IsRequired();

        builder.Property(x => x.EncryptedActiveSecret)
            .IsRequired();

        builder.Property(x => x.RotatedAtUtc)
            .IsRequired();

        builder.Property(x => x.WindowEndsAtUtc)
            .IsRequired();

        // Secret lookups always read the newest rotation of a provider first.
        builder.HasIndex(x => new { x.ProviderKey, x.RotatedAtUtc })
            .HasDatabaseName("ix_webhook_secret_rotations_provider_rotated_at");
    }
}
