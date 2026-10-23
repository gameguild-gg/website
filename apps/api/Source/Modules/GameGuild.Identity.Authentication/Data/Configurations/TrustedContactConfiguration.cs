using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

public sealed class TrustedContactConfiguration : IEntityTypeConfiguration<TrustedContact>
{
    public void Configure(EntityTypeBuilder<TrustedContact> builder)
    {
        builder.ToTable("trusted_contacts", "gameguild.authentication", table =>
        {
            table.HasCheckConstraint("ck_trusted_contacts_status", "status IN ('Invited', 'Confirmed', 'Revoked')");
            table.HasCheckConstraint("ck_trusted_contacts_invite_token", "(invite_token_hash IS NULL OR invite_token_hash ~ '^[a-f0-9]{64}$')");
        });
        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Id).HasColumnName("id");
        builder.Property(contact => contact.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(contact => contact.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(contact => contact.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(contact => contact.Relation).HasColumnName("relation").HasMaxLength(100);
        builder.Property(contact => contact.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(contact => contact.InviteTokenHash).HasColumnName("invite_token_hash").HasMaxLength(64);
        builder.Property(contact => contact.InviteTokenExpiresAt).HasColumnName("invite_token_expires_at");
        builder.Property(contact => contact.InvitedAt).HasColumnName("invited_at").IsRequired();
        builder.Property(contact => contact.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(contact => contact.RevokedAt).HasColumnName("revoked_at");
        builder.HasIndex(contact => new { contact.OwnerUserId, contact.Email })
            .IsUnique()
            .HasFilter("status IN ('Invited', 'Confirmed')")
            .HasDatabaseName("ux_trusted_contacts_active_owner_email");
        builder.HasIndex(contact => new { contact.OwnerUserId, contact.Status }).HasDatabaseName("ix_trusted_contacts_owner_status");
        builder.HasIndex(contact => contact.TenantId).HasDatabaseName("ix_trusted_contacts_tenant");
    }
}
