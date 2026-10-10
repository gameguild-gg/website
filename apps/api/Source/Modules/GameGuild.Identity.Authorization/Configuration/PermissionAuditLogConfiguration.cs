using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authorization.Configuration;

/// <summary>
///     EF Core configuration for <see cref="PermissionAuditLog"/>.
/// </summary>
/// <remarks>
///     The audit trail is a security control (issue #327: every permission mutation
///     must be audited), so the entity is mapped explicitly with the same
///     conventions as the rest of the authorization module: <c>TenantId</c> value
///     conversion, enum persisted as <c>int</c>, computed properties ignored.
/// </remarks>
public class PermissionAuditLogConfiguration : IEntityTypeConfiguration<PermissionAuditLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PermissionAuditLog> builder)
    {
        builder.ToTable("PermissionAuditLogs");

        builder.HasKey(log => log.Id);

        // TenantId value conversion (matches JitElevationRequestConfiguration).
        builder.Property(log => log.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(log => log.OperationType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(log => log.ResourceType)
            .HasMaxLength(200);

        builder.Property(log => log.PermissionType)
            .HasMaxLength(256);

        builder.Property(log => log.PerformedBy)
            .IsRequired();

        builder.Property(log => log.IpAddress)
            .HasMaxLength(64);

        builder.Property(log => log.UserAgent)
            .HasMaxLength(512);

        builder.Property(log => log.Reason)
            .HasMaxLength(2000);

        builder.Property(log => log.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(log => log.Timestamp)
            .IsRequired();

        // Computed alias over PermissionType — not persisted.
        builder.Ignore(log => log.Permission);

        // Query shapes used by PermissionAuditLogRepository.
        builder.HasIndex(log => new { log.TenantId, log.Timestamp })
            .HasDatabaseName("IX_PermissionAuditLogs_Tenant_Time");

        builder.HasIndex(log => log.UserId)
            .HasDatabaseName("IX_PermissionAuditLogs_UserId");

        builder.HasIndex(log => log.Timestamp)
            .HasDatabaseName("IX_PermissionAuditLogs_Time");

        builder.HasIndex(log => log.OperationType)
            .HasDatabaseName("IX_PermissionAuditLogs_OperationType");
    }
}
