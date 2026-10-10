using System.Text.Json;
using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authorization.Configuration;

/// <summary>
///     EF Core configuration for JitElevationRequest entity
/// </summary>
public class JitElevationRequestConfiguration : IEntityTypeConfiguration<JitElevationRequest>
{
    public void Configure(EntityTypeBuilder<JitElevationRequest> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.RequesterId);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ExpiresAt);

        // TenantId value conversion
        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(e => e.Permission).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Justification).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.ResourceType).HasMaxLength(256);
        builder.Property(e => e.ReviewerComments).HasMaxLength(2000);
        builder.Property(e => e.RevocationReason).HasMaxLength(2000);
    }
}

/// <summary>
///     EF Core configuration for PermissionDelegation entity
/// </summary>
public class PermissionDelegationConfiguration : IEntityTypeConfiguration<PermissionDelegation>
{
    public void Configure(EntityTypeBuilder<PermissionDelegation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.DelegatorUserId);
        builder.HasIndex(e => e.DelegateUserId);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => e.ExpiresAt);

        // TenantId value conversion
        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(e => e.Reason).HasMaxLength(2000);
    }
}

/// <summary>
///     EF Core configuration for SoDRule entity
/// </summary>
public class SoDRuleConfiguration : IEntityTypeConfiguration<SoDRule>
{
    public void Configure(EntityTypeBuilder<SoDRule> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.IsEnabled);

        // TenantId value conversion
        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.ConflictingPermissions).IsRequired();
        builder.Property(e => e.MitigationStrategy).HasMaxLength(2000);

        builder.HasMany(e => e.Violations)
            .WithOne(v => v.Rule)
            .HasForeignKey(v => v.RuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
///     EF Core configuration for SoDViolation entity
/// </summary>
public class SoDViolationConfiguration : IEntityTypeConfiguration<SoDViolation>
{
    public void Configure(EntityTypeBuilder<SoDViolation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.RuleId);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Status);

        // TenantId value conversion
        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(e => e.ViolationDetails).IsRequired();
        builder.Property(e => e.ConflictingItems).IsRequired();
        builder.Property(e => e.ResolutionNotes).HasMaxLength(2000);
        builder.Property(e => e.ExceptionJustification).HasMaxLength(2000);
    }
}

/// <summary>
///     EF Core configuration for AccessReviewCampaign entity
/// </summary>
public class AccessReviewCampaignConfiguration : IEntityTypeConfiguration<AccessReviewCampaign>
{
    public void Configure(EntityTypeBuilder<AccessReviewCampaign> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.StartDate, e.EndDate });

        // TenantId value conversion
        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.ScopeFilter).HasMaxLength(4000);
        builder.Property(e => e.NotificationTemplate).HasMaxLength(4000);

        builder.HasMany(e => e.Items)
            .WithOne(i => i.Campaign)
            .HasForeignKey(i => i.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
///     EF Core configuration for AccessReviewItem entity
/// </summary>
public class AccessReviewItemConfiguration : IEntityTypeConfiguration<AccessReviewItem>
{
    public void Configure(EntityTypeBuilder<AccessReviewItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.CampaignId);
        builder.HasIndex(e => e.SubjectUserId);
        builder.HasIndex(e => e.ReviewerId);
        builder.HasIndex(e => e.Decision);

        builder.Property(e => e.PermissionDetails).IsRequired();
        builder.Property(e => e.ResourceType).HasMaxLength(256);
        builder.Property(e => e.DecisionReason).HasMaxLength(2000);
        builder.Property(e => e.ReviewerNotes).HasMaxLength(2000);
    }
}

/// <summary>
///     EF Core configuration for DelegatedAdminScope entity
/// </summary>
public class DelegatedAdminScopeConfiguration : IEntityTypeConfiguration<DelegatedAdminScope>
{
    public void Configure(EntityTypeBuilder<DelegatedAdminScope> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.AdminUserId);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => new { e.StartsAt, e.ExpiresAt });

        // TenantId value conversion
        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Description).HasMaxLength(2000);
    }
}

/// <summary>
///     EF Core configuration for AbacPolicy entity
/// </summary>
public class AbacPolicyConfiguration : IEntityTypeConfiguration<AbacPolicy>
{
    public void Configure(EntityTypeBuilder<AbacPolicy> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.IsEnabled);
        builder.HasIndex(e => e.Priority);

        // TenantId value conversion
        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Description).HasMaxLength(2000);
    }
}

/// <summary>
///     EF Core configuration for ConditionalPolicy entity
/// </summary>
public class ConditionalPolicyConfiguration : IEntityTypeConfiguration<ConditionalPolicy>
{
    public void Configure(EntityTypeBuilder<ConditionalPolicy> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.IsEnabled);
        builder.HasIndex(e => e.Priority);

        // TenantId value conversion
        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Description).HasMaxLength(2000);
    }
}

/// <summary>
///     EF Core configuration for DataMaskingRule entity
/// </summary>
public class DataMaskingRuleConfiguration : IEntityTypeConfiguration<DataMaskingRule>
{
    public void Configure(EntityTypeBuilder<DataMaskingRule> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.IsEnabled);
        builder.HasIndex(e => e.ResourceType);

        // TenantId value conversion
        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.FieldName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.ResourceType).IsRequired().HasMaxLength(256);
    }
}

/// <summary>
///     EF Core configuration for TenantPermission entity
/// </summary>
public class TenantPermissionConfiguration : IEntityTypeConfiguration<TenantPermission>
{
    public void Configure(EntityTypeBuilder<TenantPermission> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasQueryFilter(permission => permission.DeletedAt == null);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => new { e.UserId, e.TenantId }, "IX_TenantPermissions_User_Tenant")
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");
        builder.HasIndex(e => e.ExpiresAt);

        builder.Property(e => e.Permissions).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);

        // Explicitly map Metadata as a JSON-serialized property (not a navigation)
        // so both Npgsql and InMemory providers handle Dictionary<string, object> correctly
        builder.Property(e => e.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null)
            );

        builder.Property(e => e.Metadata)
            .Metadata.SetValueComparer(JsonDictionaryValueComparers.ObjectDictionaryComparer);
    }
}

/// <summary>
///     EF Core configuration for PermissionTemplate entity
/// </summary>
public class PermissionTemplateConfiguration : IEntityTypeConfiguration<PermissionTemplate>
{
    public void Configure(EntityTypeBuilder<PermissionTemplate> builder)
    {
        builder.HasKey(e => e.Id);

        // Explicitly map Metadata as a JSON-serialized property (not a navigation)
        // so both Npgsql and InMemory providers handle Dictionary<string, object> correctly
        builder.Property(e => e.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null)
            );

        builder.Property(e => e.Metadata)
            .Metadata.SetValueComparer(JsonDictionaryValueComparers.ObjectDictionaryComparer);
    }
}

/// <summary>
///     EF Core configuration for the PermissionAuditLog entity (issues #331, #346).
///     The audit trail was written through <c>PermissionAuditLogRepository</c> but the
///     entity was never part of the EF model, so every write failed with "type is not
///     included in the model" and was swallowed by the callers' best-effort catches —
///     permission decision audits (including <see cref="PermissionOperationType.Check"/>
///     endpoint decisions) silently never persisted.
/// </summary>
public class PermissionAuditLogConfiguration : IEntityTypeConfiguration<PermissionAuditLog>
{
    public void Configure(EntityTypeBuilder<PermissionAuditLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => new { e.UserId, e.PermissionType, e.Timestamp }, "IX_PermissionAuditLogs_User_Permission_Timestamp");

        // TenantId value conversion (same pattern as AccessReviewCampaignConfiguration)
        builder.Property(e => e.TenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(e => e.OperationType).HasConversion<int>();

        // Free-form audit payloads (reasons, errors, old/new values, user agents) are
        // deliberately uncapped text columns: an audit write must never fail because a
        // caller-supplied string exceeded a length limit.
        builder.Property(e => e.ResourceType);
        builder.Property(e => e.PermissionType);
        builder.Property(e => e.PermissionDetails);
        builder.Property(e => e.Reason);
        builder.Property(e => e.ErrorMessage);
        builder.Property(e => e.IpAddress);
        builder.Property(e => e.UserAgent);
        builder.Property(e => e.OldValue);
        builder.Property(e => e.NewValue);
    }
}

internal static class JsonDictionaryValueComparers
{
    public static ValueComparer<Dictionary<string, object>?> ObjectDictionaryComparer { get; } =
        new(
            (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
            value => value == null ? 0 : JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
            value => value == null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(
                    JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                    (JsonSerializerOptions?)null));
}
