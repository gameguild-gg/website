using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

public sealed class TotpReplayStateConfiguration : IEntityTypeConfiguration<TotpReplayState>
{
    public void Configure(EntityTypeBuilder<TotpReplayState> builder)
    {
        builder.ToTable("totp_replay_state", "gameguild.authentication", table =>
            table.HasCheckConstraint("ck_totp_replay_state_nonnegative_step", "last_accepted_step >= 0"));
        builder.HasKey(row => new { row.ConfigurationId, row.SecretFingerprint });
        builder.Property(row => row.ConfigurationId).HasColumnName("configuration_id");
        builder.Property(row => row.SecretFingerprint).HasColumnName("secret_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(row => row.LastAcceptedStep).HasColumnName("last_accepted_step").IsRequired();
        builder.Property(row => row.LastAcceptedAt).HasColumnName("last_accepted_at").IsRequired();
        builder.HasOne<UserMfaConfiguration>().WithMany().HasForeignKey(row => row.ConfigurationId).OnDelete(DeleteBehavior.Cascade);
    }
}
