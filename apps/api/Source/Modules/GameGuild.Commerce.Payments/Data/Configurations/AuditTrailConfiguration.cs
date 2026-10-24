using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity Type Configuration for AuditTrail
/// </summary>
public class AuditTrailConfiguration : IEntityTypeConfiguration<AuditTrail>
{
    public void Configure(EntityTypeBuilder<AuditTrail> builder)
    {
        // Configure table with constraints for EF Core 9.0
        builder.ToTable("audit_trails", tb =>
            {
                tb.HasCheckConstraint("CK_AuditTrail_EntityType_NotEmpty", "LENGTH(\"EntityType\") > 0");
                tb.HasCheckConstraint("CK_AuditTrail_EntityId_NotEmpty", "\"EntityId\" != '00000000-0000-0000-0000-000000000000'");
            }
        );

        // Primary key configuration
        builder.HasKey(x => x.Id);

        // Property configurations based on entity attributes
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();

        builder.Property(x => x.EntityId).IsRequired();

        builder.Property(x => x.Action).HasConversion<string>().IsRequired();

        builder.Property(x => x.OldValue).HasMaxLength(4000);

        builder.Property(x => x.NewValue).HasMaxLength(4000);

        builder.Property(x => x.ChangedBy).IsRequired();

        builder.Property(x => x.ChangedAt).IsRequired();

        // Configure indexes for performance
        builder.HasIndex(x => x.EntityType);
        builder.HasIndex(x => x.EntityId);
        builder.HasIndex(x => x.Action);
        builder.HasIndex(x => x.ChangedBy);
        builder.HasIndex(x => x.ChangedAt);
        // builder.Property(x => x.CreatedAt)
        //     .HasColumnName("created_at")
        //     .IsRequired();
        // 
        // builder.Property(x => x.UpdatedAt)
        //     .HasColumnName("updated_at")
        //     .IsRequired();
    }
}
