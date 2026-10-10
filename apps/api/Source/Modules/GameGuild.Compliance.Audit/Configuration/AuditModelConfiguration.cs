using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.Audit;

public sealed class AuditModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComplianceEvidenceDocument>(entity =>
        {
            entity.ToTable("ComplianceEvidenceDocuments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).IsRequired();
            entity.Property(item => item.TemplateId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Type).HasMaxLength(80).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Content).IsRequired();
            entity.Property(item => item.ContentSha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourceUri).HasMaxLength(2048).IsRequired();
            entity.Property(item => item.ControlIdsJson).HasColumnType("text").IsRequired();
            entity.Property(item => item.ValidationFieldsJson).HasColumnType("text").IsRequired();
            entity.Property(item => item.ReviewNotes).HasMaxLength(2000);
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.CreatedAt });
        });
        modelBuilder.Entity<ComplianceSealedPackage>(entity =>
        {
            entity.ToTable("ComplianceSealedPackages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.TemplateId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ArtifactSha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SigningKeyId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.ArtifactContent).IsRequired();
            entity.Property(item => item.ManifestJson).HasColumnType("text").IsRequired();
            entity.Property(item => item.SealJson).HasColumnType("text").IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.CreatedAt });
        });
        modelBuilder.Entity<AuditRetentionConfiguration>(entity =>
        {
            entity.ToTable("AuditRetentionConfigurations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).IsRequired();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.Property(item => item.ConfigurationJson).HasColumnType("text").IsRequired();
            entity.HasIndex(item => item.TenantId).IsUnique();
        });
        modelBuilder.Entity<AuditRetentionSimulationRun>(entity =>
        {
            entity.ToTable("AuditRetentionSimulationRuns");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).IsRequired();
            entity.Property(item => item.ConfigurationJson).HasColumnType("text").IsRequired();
            entity.Property(item => item.RequestJson).HasColumnType("text").IsRequired();
            entity.Property(item => item.ReportJson).HasColumnType("text").IsRequired();
            entity.Property(item => item.ModelVersion).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            entity.Property(item => item.BaselineTotalCost).HasPrecision(38, 8);
            entity.Property(item => item.RecommendedTotalCost).HasPrecision(38, 8);
            entity.HasIndex(item => new { item.TenantId, item.CreatedAt });
        });
        modelBuilder.Entity<AuditDataAccessObservation>(entity =>
        {
            entity.ToTable("AuditDataAccessObservations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.CreatedAt });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(log => log.Id);

            entity.Property(log => log.ActionType).HasMaxLength(100).IsRequired();
            entity.Property(log => log.ResourceType).HasMaxLength(100).IsRequired();
            entity.Property(log => log.ResourceId).HasMaxLength(100);
            entity.Property(log => log.IpAddress).HasMaxLength(45);
            entity.Property(log => log.UserAgent).HasMaxLength(500);
            entity.Property(log => log.Description).HasMaxLength(1000);
            entity.Property(log => log.ErrorMessage).HasMaxLength(500);
            entity.Property(log => log.CorrelationId).HasMaxLength(100);
            entity.Property(log => log.RiskLevel).HasConversion<int>();
            entity.Property(log => log.Category).HasConversion<int>();

            entity.HasIndex(log => log.ActionType);
            entity.HasIndex(log => log.CorrelationId);
            entity.HasIndex(log => log.ResourceType);
            entity.HasIndex(log => log.ResourceId);
            entity.HasIndex(log => log.UserId);
            entity.HasIndex(log => log.TenantId);
            entity.HasIndex(log => log.CreatedAt);
            entity.HasIndex(log => new { log.TenantId, log.CreatedAt });
        });

        modelBuilder.Entity<TamperEvidentAuditLog>(entity =>
        {
            entity.ToTable("TamperEvidentAuditLogs");
            entity.HasKey(log => log.Id);

            entity.Property(log => log.TenantId).IsRequired();
            entity.Property(log => log.Action).HasMaxLength(100).IsRequired();
            entity.Property(log => log.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(log => log.RiskLevel).HasMaxLength(50).IsRequired();
            entity.Property(log => log.IpAddress).HasMaxLength(45).IsRequired();
            entity.Property(log => log.UserAgent).HasMaxLength(500).IsRequired();
            entity.Property(log => log.CorrelationId).HasMaxLength(100);
            entity.Property(log => log.Country).HasMaxLength(100);
            entity.Property(log => log.Region).HasMaxLength(100);
            entity.Property(log => log.City).HasMaxLength(100);
            entity.Property(log => log.ContentHash).HasMaxLength(64).IsRequired();
            entity.Property(log => log.PreviousHash).HasMaxLength(64).IsRequired();
            entity.Property(log => log.ChainHash).HasMaxLength(64).IsRequired();
            entity.Property(log => log.DigitalSignature).HasMaxLength(256).IsRequired();
            entity.Property(log => log.SigningKeyId).HasMaxLength(100).IsRequired();
            entity.Property(log => log.CustodyChain).HasColumnType("text");
            entity.Property(log => log.BeforeSnapshot).HasColumnType("text");
            entity.Property(log => log.AfterSnapshot).HasColumnType("text");
            entity.Property(log => log.Changes).HasColumnType("text").IsRequired();
            entity.Property(log => log.VerificationNotes).HasColumnType("text");

            entity.HasIndex(log => new { log.TenantId, log.SequenceNumber }).IsUnique();
            entity.HasIndex(log => new { log.TenantId, log.Timestamp });
            entity.HasIndex(log => new { log.TenantId, log.Action, log.Timestamp });
            entity.HasIndex(log => new { log.TenantId, log.SessionId, log.Timestamp });
        });

        modelBuilder.Entity<ScheduledAuditExport>(entity =>
        {
            entity.ToTable("ScheduledAuditExports");
            entity.HasKey(export => export.Id);
            entity.Property(export => export.JobName).HasMaxLength(100).IsRequired();
            entity.Property(export => export.Description).HasMaxLength(1000);
            entity.Property(export => export.CronExpression).HasMaxLength(100).IsRequired();
            entity.Property(export => export.Timezone).HasMaxLength(80).IsRequired();
            entity.Property(export => export.DestinationType).HasConversion<int>();
            entity.Property(export => export.DestinationUrl).HasMaxLength(2048).IsRequired();
            entity.Property(export => export.DestinationPath).HasMaxLength(2048);
            entity.Property(export => export.CredentialKeyName).HasMaxLength(200);
            entity.Property(export => export.ExportFormat).HasConversion<int>();
            entity.Property(export => export.ExportTemplate).HasMaxLength(200);
            entity.Property(export => export.IncludeEventTypes).HasColumnType("text[]");
            entity.Property(export => export.ExcludeEventTypes).HasColumnType("text[]");
            entity.Property(export => export.CsvColumns).HasColumnType("text[]");
            entity.Property(export => export.RiskLevelFilter).HasMaxLength(50);
            entity.Property(export => export.UserIdFilter).HasMaxLength(36);
            entity.Property(export => export.EncryptionKeyId).HasMaxLength(200);
            entity.Property(export => export.LastErrorMessage).HasMaxLength(1000);
            entity.Property(export => export.NotificationEmails).HasColumnType("text[]");
            entity.Property(export => export.Version).IsConcurrencyToken();
            entity.HasIndex(export => new { export.IsEnabled, export.NextRunAt });
            entity.HasIndex(export => new { export.TenantId, export.JobName });
        });

        modelBuilder.Entity<AuditExportHistory>(entity =>
        {
            entity.ToTable("AuditExportHistories");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Status).HasConversion<int>();
            entity.Property(history => history.ExportPath).HasColumnType("text");
            entity.Property(history => history.FileName).HasMaxLength(255);
            entity.Property(history => history.FileChecksum).HasMaxLength(64);
            entity.Property(history => history.ErrorMessage).HasMaxLength(1000);
            entity.HasOne<ScheduledAuditExport>()
                .WithMany()
                .HasForeignKey(history => history.ScheduledExportId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(history => new { history.TenantId, history.ExecutedAt });
            entity.HasIndex(history => new { history.ScheduledExportId, history.ExecutedAt });
        });

        modelBuilder.Entity<SecurityAlert>(entity =>
        {
            entity.ToTable("SecurityAlerts");
            entity.HasKey(alert => alert.Id);
            entity.Property(alert => alert.TenantId);
            entity.Property(alert => alert.RuleId).HasMaxLength(100).IsRequired();
            entity.Property(alert => alert.Kind).HasConversion<int>();
            entity.Property(alert => alert.Severity).HasConversion<int>();
            entity.Property(alert => alert.Title).HasMaxLength(200).IsRequired();
            entity.Property(alert => alert.Description).HasMaxLength(1000).IsRequired();
            entity.Property(alert => alert.SourceActionType).HasMaxLength(100).IsRequired();
            entity.Property(alert => alert.IpAddress).HasMaxLength(45);
            entity.Property(alert => alert.DeduplicationKey).HasMaxLength(400).IsRequired();
            entity.Property(alert => alert.Status).HasConversion<int>();
            entity.Property(alert => alert.AcknowledgementNotes).HasMaxLength(1000);
            entity.Property(alert => alert.ResolutionNotes).HasMaxLength(1000);
            entity.HasIndex(alert => alert.DeduplicationKey);
            entity.HasIndex(alert => new { alert.Status, alert.Severity, alert.LastSeenAtUtc });
            entity.HasIndex(alert => new { alert.TenantId, alert.Status, alert.LastSeenAtUtc });
        });

        modelBuilder.Entity<SecurityLogRetentionPolicy>(entity =>
        {
            entity.ToTable("SecurityLogRetentionPolicies");
            entity.HasKey(policy => policy.Id);
            entity.Property(policy => policy.TenantId).IsRequired();
            entity.Property(policy => policy.Revision).IsConcurrencyToken();
            entity.Property(policy => policy.CategoryOverridesJson).HasColumnType("text");
            entity.HasIndex(policy => policy.TenantId).IsUnique();
        });

        modelBuilder.Entity<SecurityLogRetentionExecution>(entity =>
        {
            entity.ToTable("SecurityLogRetentionExecutions");
            entity.HasKey(execution => execution.Id);
            entity.Property(execution => execution.TenantId).IsRequired();
            entity.HasIndex(execution => new { execution.TenantId, execution.ExecutedAtUtc });
        });
    }
}
