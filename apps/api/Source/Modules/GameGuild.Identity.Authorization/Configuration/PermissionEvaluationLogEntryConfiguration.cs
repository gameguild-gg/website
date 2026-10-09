using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authorization.Configuration;

/// <summary>
///     EF Core configuration for <see cref="PermissionEvaluationLogEntry"/>.
/// </summary>
public class PermissionEvaluationLogEntryConfiguration : IEntityTypeConfiguration<PermissionEvaluationLogEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PermissionEvaluationLogEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ResourceType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ResourceId)
            .HasMaxLength(200);

        builder.Property(e => e.RequiredPermissions)
            .IsRequired();

        builder.Property(e => e.Source)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Operation)
            .HasMaxLength(200);

        builder.Property(e => e.Reason)
            .HasMaxLength(200);

        builder.HasIndex(e => new { e.TenantId, e.EvaluatedAtUtc })
            .HasDatabaseName("IX_PermissionEvaluationLogs_Tenant_Time");

        builder.HasIndex(e => e.EvaluatedAtUtc)
            .HasDatabaseName("IX_PermissionEvaluationLogs_Time");

        builder.HasIndex(e => e.Outcome)
            .HasDatabaseName("IX_PermissionEvaluationLogs_Outcome");
    }
}
