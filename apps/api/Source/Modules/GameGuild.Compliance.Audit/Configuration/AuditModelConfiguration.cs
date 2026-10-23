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

        // Archive storage tier for the security audit log (issue #193): copies of aged rows moved
        // out of the hot AuditLogs table by retention enforcement.
        modelBuilder.Entity<ArchivedSecurityAuditLog>(entity =>
        {
            entity.ToTable("ArchivedAuditLogs");
            entity.HasKey(archived => archived.Id);
            entity.Property(archived => archived.TenantId);
            entity.Property(archived => archived.ActionType).HasMaxLength(100).IsRequired();
            entity.Property(archived => archived.ResourceType).HasMaxLength(100).IsRequired();
            entity.Property(archived => archived.ResourceId).HasMaxLength(100);
            entity.Property(archived => archived.IpAddress).HasMaxLength(45);
            entity.Property(archived => archived.UserAgent).HasMaxLength(500);
            entity.Property(archived => archived.Description).HasMaxLength(1000);
            entity.Property(archived => archived.ErrorMessage).HasMaxLength(500);
            entity.Property(archived => archived.CorrelationId).HasMaxLength(100);
            entity.Property(archived => archived.RiskLevel).HasConversion<int>();
            entity.Property(archived => archived.Category).HasConversion<int>();
            // Idempotency for re-archival of the same source row within a tenant.
            entity.HasIndex(archived => new { archived.TenantId, archived.SourceAuditLogId }).IsUnique();
            entity.HasIndex(archived => new { archived.TenantId, archived.OriginalCreatedAt });
        });

        modelBuilder.Entity<PiiRedactionRule>(entity =>
        {
            entity.ToTable("PiiRedactionRules");
            entity.HasKey(rule => rule.Id);
            entity.Property(rule => rule.TenantId);
            entity.Property(rule => rule.RuleName).HasMaxLength(200).IsRequired();
            entity.Property(rule => rule.Description).HasMaxLength(1000);
            entity.Property(rule => rule.PatternType).HasConversion<int>();
            entity.Property(rule => rule.Preset).HasConversion<int>();
            entity.Property(rule => rule.Action).HasConversion<int>();
            entity.Property(rule => rule.Scope).HasConversion<int>();
            entity.Property(rule => rule.RegexPattern).HasMaxLength(1000);
            entity.Property(rule => rule.Replacement).HasMaxLength(100).IsRequired();
            entity.Property(rule => rule.TargetFields).HasColumnType("text[]");
            entity.Property(rule => rule.Revision).IsConcurrencyToken();
            entity.HasIndex(rule => new { rule.TenantId, rule.IsEnabled });
        });

        modelBuilder.Entity<PiiTokenVaultEntry>(entity =>
        {
            entity.ToTable("PiiTokenVaultEntries");
            entity.HasKey(vault => vault.Id);
            entity.Property(vault => vault.TenantId);
            entity.Property(vault => vault.ScopeKey).HasMaxLength(40).IsRequired();
            entity.Property(vault => vault.Token).HasMaxLength(64).IsRequired();
            entity.Property(vault => vault.EncryptedValue).IsRequired();
            entity.HasIndex(vault => new { vault.ScopeKey, vault.Token }).IsUnique();
            entity.HasIndex(vault => vault.TenantId);
        });

        modelBuilder.Entity<PiiRedactionOperation>(entity =>
        {
            entity.ToTable("PiiRedactionOperations");
            entity.HasKey(operation => operation.Id);
            entity.Property(operation => operation.TenantId);
            entity.Property(operation => operation.Source).HasConversion<int>();
            entity.Property(operation => operation.AppliedRuleIds).HasColumnType("uuid[]");
            entity.Property(operation => operation.FailureReason).HasMaxLength(1000);
            entity.HasIndex(operation => new { operation.TenantId, operation.AppliedAt });
            entity.HasIndex(operation => new { operation.TenantId, operation.FailClosed });
        });

        modelBuilder.Entity<SavedAuditQuery>(entity =>
        {
            entity.ToTable("SavedAuditQueries");
            entity.HasKey(query => query.Id);
            entity.Property(query => query.TenantId);
            entity.Property(query => query.QueryName).HasMaxLength(200).IsRequired();
            entity.Property(query => query.Description).HasMaxLength(1000);
            entity.Property(query => query.QueryDsl).HasColumnType("text").IsRequired();
            entity.Property(query => query.AllowedRoles).HasColumnType("text[]");
            entity.Property(query => query.AllowedUserIds).HasColumnType("uuid[]");
            entity.Property(query => query.Version).IsConcurrencyToken();
            entity.HasIndex(query => new { query.TenantId, query.QueryName });
            entity.HasIndex(query => new { query.TenantId, query.CreatedByUserId });
            entity.HasIndex(query => query.IsPublic);
        });

        // Deliberately no foreign key to SavedAuditQueries: revision history is retained as an
        // audit record even after the saved view itself is deleted.
        modelBuilder.Entity<SavedAuditQueryRevision>(entity =>
        {
            entity.ToTable("SavedAuditQueryRevisions");
            entity.HasKey(revision => revision.Id);
            entity.Property(revision => revision.TenantId);
            entity.Property(revision => revision.QueryName).HasMaxLength(200).IsRequired();
            entity.Property(revision => revision.Description).HasMaxLength(1000);
            entity.Property(revision => revision.QueryDsl).HasColumnType("text").IsRequired();
            entity.HasIndex(revision => revision.SavedAuditQueryId);
            entity.HasIndex(revision => new { revision.SavedAuditQueryId, revision.Revision }).IsUnique();
        });

        // Mutation value snapshots (issue #176): insert-only forensic value store staged in the
        // same SaveChanges call as each mutation. No FKs by design: snapshots outlive the rows
        // they describe and retention may strip payloads while checksums stay verifiable.
        modelBuilder.Entity<MutationValueSnapshot>(entity =>
        {
            entity.ToTable("MutationValueSnapshots");
            entity.HasKey(snapshot => snapshot.Id);
            entity.Property(snapshot => snapshot.TenantId);
            entity.Property(snapshot => snapshot.EntityName).HasMaxLength(300).IsRequired();
            entity.Property(snapshot => snapshot.KeyId).HasMaxLength(300).IsRequired();
            entity.Property(snapshot => snapshot.ChangeKind).HasMaxLength(20).IsRequired();
            entity.Property(snapshot => snapshot.BeforeIntegritySha256).HasMaxLength(64);
            entity.Property(snapshot => snapshot.AfterIntegritySha256).HasMaxLength(64);
            entity.Property(snapshot => snapshot.BeforeStoredSha256).HasMaxLength(64);
            entity.Property(snapshot => snapshot.AfterStoredSha256).HasMaxLength(64);
            entity.Property(snapshot => snapshot.BeforePayload).HasColumnType("text");
            entity.Property(snapshot => snapshot.AfterPayload).HasColumnType("text");
            entity.Property(snapshot => snapshot.StructuredDiffJson).HasColumnType("text").IsRequired();
            entity.Property(snapshot => snapshot.StorageMode).HasMaxLength(10).IsRequired();
            entity.Property(snapshot => snapshot.CorrelationId).HasMaxLength(100);
            entity.HasIndex(snapshot => new { snapshot.TenantId, snapshot.EntityName, snapshot.KeyId, snapshot.OccurredAtUtc });
            entity.HasIndex(snapshot => new { snapshot.TenantId, snapshot.OccurredAtUtc });
            entity.HasIndex(snapshot => snapshot.CorrelationId);
            entity.HasIndex(snapshot => snapshot.BeforeStoredSha256);
            entity.HasIndex(snapshot => snapshot.AfterStoredSha256);
        });

        modelBuilder.Entity<MutationValuePayloadChunk>(entity =>
        {
            entity.ToTable("MutationValuePayloadChunks");
            entity.HasKey(chunk => chunk.Id);
            entity.Property(chunk => chunk.TenantId);
            entity.Property(chunk => chunk.ContentSha256).HasMaxLength(64).IsRequired();
            entity.Property(chunk => chunk.Content).HasColumnType("text").IsRequired();
            entity.HasIndex(chunk => new { chunk.TenantId, chunk.ContentSha256 }).IsUnique();
        });

        modelBuilder.Entity<ValueSnapshotRetentionPolicy>(entity =>
        {
            entity.ToTable("ValueSnapshotRetentionPolicies");
            entity.HasKey(policy => policy.Id);
            entity.Property(policy => policy.TenantId).IsRequired();
            entity.Property(policy => policy.EntityOverridesJson).HasColumnType("text").IsRequired();
            entity.Property(policy => policy.Revision).IsConcurrencyToken();
            entity.HasIndex(policy => policy.TenantId).IsUnique();
        });

        modelBuilder.Entity<ValueSnapshotRetentionExecution>(entity =>
        {
            entity.ToTable("ValueSnapshotRetentionExecutions");
            entity.HasKey(execution => execution.Id);
            entity.Property(execution => execution.TenantId).IsRequired();
            entity.HasIndex(execution => new { execution.TenantId, execution.ExecutedAtUtc });
        });

        modelBuilder.Entity<ValueSnapshotRollbackPlan>(entity =>
        {
            entity.ToTable("ValueSnapshotRollbackPlans");
            entity.HasKey(plan => plan.Id);
            entity.Property(plan => plan.TenantId).IsRequired();
            entity.Property(plan => plan.TargetEntityName).HasMaxLength(300).IsRequired();
            entity.Property(plan => plan.TargetKeyId).HasMaxLength(300).IsRequired();
            entity.Property(plan => plan.BasisStateJson).HasColumnType("text").IsRequired();
            entity.Property(plan => plan.BasisStateSha256).HasMaxLength(64).IsRequired();
            entity.Property(plan => plan.PlanJson).HasColumnType("text").IsRequired();
            entity.Property(plan => plan.Status).HasMaxLength(20).IsRequired();
            entity.Property(plan => plan.FailureReason).HasMaxLength(1000);
            entity.Property(plan => plan.ResultJson).HasColumnType("text");
            entity.HasIndex(plan => new { plan.TenantId, plan.TargetEntityName, plan.TargetKeyId });
            entity.HasIndex(plan => new { plan.TenantId, plan.Status });
        });

        // Chain-of-custody store (issue #177): append-only custody chains, custodian authorization,
        // transfer proofs, legal holds, notifications, and isolation-enforced access logs. No FKs
        // by design: custody records must survive independent lifecycle of their neighbors.
        modelBuilder.Entity<CustodyEvidenceItem>(entity =>
        {
            entity.ToTable("CustodyEvidenceItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId).IsRequired();
            entity.Property(item => item.SourceKind).HasMaxLength(60).IsRequired();
            entity.Property(item => item.SourceId).HasMaxLength(300).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Content).IsRequired();
            entity.Property(item => item.ContentSha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.CustodyState).HasMaxLength(30).IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.CollectedAtUtc });
            entity.HasIndex(item => new { item.TenantId, item.SourceKind, item.SourceId });
            entity.HasIndex(item => new { item.TenantId, item.LegalHoldId });
        });

        modelBuilder.Entity<CustodyEvent>(entity =>
        {
            entity.ToTable("CustodyEvents");
            entity.HasKey(custodyEvent => custodyEvent.Id);
            entity.Property(custodyEvent => custodyEvent.TenantId).IsRequired();
            entity.Property(custodyEvent => custodyEvent.EventType).HasMaxLength(40).IsRequired();
            entity.Property(custodyEvent => custodyEvent.Purpose).HasMaxLength(1000);
            entity.Property(custodyEvent => custodyEvent.ProofJson).HasColumnType("text");
            entity.HasIndex(custodyEvent => new { custodyEvent.TenantId, custodyEvent.EvidenceId, custodyEvent.SequenceNumber }).IsUnique();
            entity.HasIndex(custodyEvent => custodyEvent.TransferId);
        });

        modelBuilder.Entity<EvidenceCustodian>(entity =>
        {
            entity.ToTable("EvidenceCustodians");
            entity.HasKey(custodian => custodian.Id);
            entity.Property(custodian => custodian.TenantId).IsRequired();
            entity.Property(custodian => custodian.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(custodian => custodian.ScopesJson).HasColumnType("text").IsRequired();
            entity.Property(custodian => custodian.RevocationReason).HasMaxLength(1000);
            entity.HasIndex(custodian => new { custodian.TenantId, custodian.UserId }).IsUnique();
        });

        modelBuilder.Entity<EvidenceTransfer>(entity =>
        {
            entity.ToTable("EvidenceTransfers");
            entity.HasKey(transfer => transfer.Id);
            entity.Property(transfer => transfer.TenantId).IsRequired();
            entity.Property(transfer => transfer.ChallengeNonce).HasMaxLength(64).IsRequired();
            entity.Property(transfer => transfer.EvidenceHash).HasMaxLength(64).IsRequired();
            entity.Property(transfer => transfer.ProofJson).HasColumnType("text").IsRequired();
            entity.Property(transfer => transfer.Signature).HasMaxLength(512).IsRequired();
            entity.Property(transfer => transfer.SigningKeyId).HasMaxLength(128).IsRequired();
            entity.Property(transfer => transfer.AcknowledgmentSignature).HasMaxLength(512);
            entity.Property(transfer => transfer.State).HasMaxLength(40).IsRequired();
            entity.Property(transfer => transfer.Purpose).HasMaxLength(1000);
            entity.HasIndex(transfer => new { transfer.TenantId, transfer.EvidenceId, transfer.State });
        });

        modelBuilder.Entity<LegalHold>(entity =>
        {
            entity.ToTable("LegalHolds");
            entity.HasKey(hold => hold.Id);
            entity.Property(hold => hold.TenantId).IsRequired();
            entity.Property(hold => hold.CaseNumber).HasMaxLength(200).IsRequired();
            entity.Property(hold => hold.Description).HasMaxLength(2000);
            entity.Property(hold => hold.PreservationTargetsJson).HasColumnType("text").IsRequired();
            entity.Property(hold => hold.NotificationTargetsJson).HasColumnType("text").IsRequired();
            entity.Property(hold => hold.Status).HasMaxLength(20).IsRequired();
            entity.Property(hold => hold.ReleaseReason).HasMaxLength(1000);
            entity.HasIndex(hold => new { hold.TenantId, hold.CaseNumber }).IsUnique();
            entity.HasIndex(hold => new { hold.TenantId, hold.Status });
        });

        modelBuilder.Entity<LegalHoldNotification>(entity =>
        {
            entity.ToTable("LegalHoldNotifications");
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.TenantId).IsRequired();
            entity.Property(notification => notification.TargetKind).HasMaxLength(20).IsRequired();
            entity.Property(notification => notification.Target).HasMaxLength(300).IsRequired();
            entity.Property(notification => notification.Status).HasMaxLength(20).IsRequired();
            entity.Property(notification => notification.FailureReason).HasMaxLength(1000);
            entity.HasIndex(notification => new { notification.TenantId, notification.LegalHoldId, notification.Status });
        });

        modelBuilder.Entity<CustodyAccessLog>(entity =>
        {
            entity.ToTable("CustodyAccessLogs");
            entity.HasKey(accessLog => accessLog.Id);
            entity.Property(accessLog => accessLog.TenantId).IsRequired();
            entity.Property(accessLog => accessLog.Decision).HasMaxLength(10).IsRequired();
            entity.Property(accessLog => accessLog.Reason).HasMaxLength(500).IsRequired();
            entity.Property(accessLog => accessLog.Purpose).HasMaxLength(1000);
            entity.HasIndex(accessLog => new { accessLog.TenantId, accessLog.EvidenceId, accessLog.OccurredAtUtc });
        });
    }
}
