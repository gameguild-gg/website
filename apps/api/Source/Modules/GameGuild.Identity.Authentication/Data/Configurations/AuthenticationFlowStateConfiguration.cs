using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Entity Type Configuration for AuthenticationFlowStateRecord
/// </summary>
public class AuthenticationFlowStateConfiguration : IEntityTypeConfiguration<AuthenticationFlowStateRecord>
{
    public void Configure(EntityTypeBuilder<AuthenticationFlowStateRecord> builder)
    {
        // Configure table name (snake_case convention)
        builder.ToTable("authentication_flow_states", "gameguild.authentication");

        // Configure primary key
        builder.HasKey(x => x.FlowId);

        builder.Property(x => x.FlowId).HasColumnName("flow_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.CurrentStep).HasColumnName("current_step").IsRequired();
        builder.Property(x => x.RequiredStepsJson).HasColumnName("required_steps").IsRequired();
        builder.Property(x => x.CompletedStepsJson).HasColumnName("completed_steps").IsRequired();
        builder.Property(x => x.IsComplete).HasColumnName("is_complete").IsRequired();
        builder.Property(x => x.RiskScore).HasColumnName("risk_score");
        builder.Property(x => x.InitiatedAt).HasColumnName("initiated_at").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(x => x.DeviceFingerprint).HasColumnName("device_fingerprint").HasMaxLength(128);
        builder.Property(x => x.StepDataJson).HasColumnName("step_data");
        builder.Property(x => x.AbandonedAt).HasColumnName("abandoned_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // Indexes
        builder.HasIndex(x => x.ExpiresAt).HasDatabaseName("ix_authentication_flow_states_expires_at");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_authentication_flow_states_user_id");
    }
}
