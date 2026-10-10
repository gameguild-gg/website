using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Entity Type Configuration for AdaptiveBehaviorBaseline.
/// </summary>
public class AdaptiveBehaviorBaselineConfiguration : IEntityTypeConfiguration<AdaptiveBehaviorBaseline>
{
    public void Configure(EntityTypeBuilder<AdaptiveBehaviorBaseline> builder)
    {
        // Configure table name (snake_case convention, same schema as authentication attempts)
        builder.ToTable("adaptivebehaviorbaseline", "gameguild.authentication");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.SubjectKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IpWeightsJson).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // One baseline per subject (tenant + user/identifier); the learned state is a singleton.
        builder.HasIndex(x => x.SubjectKey).IsUnique().HasDatabaseName("ix_adaptivebehaviorbaseline_subject_key");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_adaptivebehaviorbaseline_tenant_id");
        builder.HasIndex(x => x.LastObservedAtUtc).HasDatabaseName("ix_adaptivebehaviorbaseline_last_observed_at");
    }
}
