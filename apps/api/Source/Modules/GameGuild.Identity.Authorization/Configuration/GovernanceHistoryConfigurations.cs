using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Identity.Authorization.Configuration;

/// <summary>
///     EF Core configuration for <see cref="PolicyVersionHistory"/> (append-only policy
///     version snapshots, issue #188).
/// </summary>
public class PolicyVersionHistoryConfiguration : IEntityTypeConfiguration<PolicyVersionHistory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PolicyVersionHistory> builder)
    {
        builder.ToTable("PolicyVersionHistory");

        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.Kind)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(snapshot => snapshot.ChangeType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(snapshot => snapshot.Version)
            .IsRequired();

        builder.Property(snapshot => snapshot.SnapshotJson)
            .IsRequired();

        builder.Property(snapshot => snapshot.PolicyName)
            .HasMaxLength(200);

        builder.Property(snapshot => snapshot.Reason)
            .HasMaxLength(2000);

        builder.Property(snapshot => snapshot.RecordedAt)
            .IsRequired();

        // One row per policy version; ordered reads are covered by the clustered index
        // plus the (PolicyId, Version) unique index declared on the entity.
        builder.HasIndex(snapshot => new { snapshot.PolicyId, snapshot.Version })
            .IsUnique()
            .HasDatabaseName("IX_PolicyVersionHistory_Policy_Version");

        builder.HasIndex(snapshot => snapshot.RecordedAt)
            .HasDatabaseName("IX_PolicyVersionHistory_RecordedAt");
    }
}

/// <summary>
///     EF Core configuration for the multi-stage permission approval workflow entities
///     (issue #188).
/// </summary>
public class PermissionApprovalWorkflowConfiguration : IEntityTypeConfiguration<PermissionApprovalWorkflow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PermissionApprovalWorkflow> builder)
    {
        builder.ToTable("PermissionApprovalWorkflows");

        builder.HasKey(workflow => workflow.Id);

        builder.Property(workflow => workflow.Permissions)
            .IsRequired();

        builder.Property(workflow => workflow.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(workflow => workflow.Reason)
            .HasMaxLength(2000);

        builder.Property(workflow => workflow.CreatedAt)
            .IsRequired();

        builder.HasMany(workflow => workflow.Stages)
            .WithOne()
            .HasForeignKey(stage => stage.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(workflow => workflow.SubjectUserId)
            .HasDatabaseName("IX_PermissionApprovalWorkflows_Subject");

        builder.HasIndex(workflow => new { workflow.TenantId, workflow.Status })
            .HasDatabaseName("IX_PermissionApprovalWorkflows_Tenant_Status");
    }
}

/// <summary>
///     EF Core configuration for <see cref="PermissionApprovalStage"/>.
/// </summary>
public class PermissionApprovalStageConfiguration : IEntityTypeConfiguration<PermissionApprovalStage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PermissionApprovalStage> builder)
    {
        builder.ToTable("PermissionApprovalStages");

        builder.HasKey(stage => stage.Id);

        builder.Property(stage => stage.StageName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(stage => stage.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(stage => stage.DecisionNotes)
            .HasMaxLength(2000);

        builder.HasIndex(stage => new { stage.WorkflowId, stage.StageIndex })
            .IsUnique()
            .HasDatabaseName("IX_PermissionApprovalStages_Workflow_Index");
    }
}
