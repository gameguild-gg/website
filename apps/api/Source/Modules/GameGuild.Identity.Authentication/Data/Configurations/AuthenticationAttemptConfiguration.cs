using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Entity Type Configuration for AuthenticationAttempt
/// </summary>
public class AuthenticationAttemptConfiguration : IEntityTypeConfiguration<AuthenticationAttempt>
{
    public void Configure(EntityTypeBuilder<AuthenticationAttempt> builder)
    {
        // Configure table name (snake_case convention)
        builder.ToTable("authenticationattempt", "gameguild.authentication");

        // Configure primary key
        builder.HasKey(x => x.Id);

        // Configure Id property
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();

        // Property configurations
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
        builder.Property(x => x.UserAgent).HasMaxLength(1000);
        builder.Property(x => x.IsSuccessful).IsRequired();
        builder.Property(x => x.FailureReason).HasMaxLength(50);
        builder.Property(x => x.AttemptedAt).IsRequired();
        builder.Property(x => x.ProcessingTime).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(200);
        builder.Property(x => x.Country).HasMaxLength(8);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.DeviceFingerprint).HasMaxLength(64);
        builder.Property(x => x.IsSuspicious).IsRequired();
        builder.Property(x => x.RiskScore).IsRequired();
        builder.Property(x => x.Metadata).HasMaxLength(2000);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_authenticationattempt_user_id");
        builder.HasIndex(x => x.Email).HasDatabaseName("ix_authenticationattempt_email");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_authenticationattempt_tenant_id");
        builder.HasIndex(x => x.AttemptedAt).HasDatabaseName("ix_authenticationattempt_attempted_at");
        builder.HasIndex(x => x.IpAddress).HasDatabaseName("ix_authenticationattempt_ip_address");
    }
}
