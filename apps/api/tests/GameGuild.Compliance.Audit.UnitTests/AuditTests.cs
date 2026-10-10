using FluentAssertions;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Compliance.Audit.UnitTests;

// The AuditAnomaly entity tests were removed together with the dead AuditAnomaly entity:
// anomaly detection now flows through the adaptive detection service and the durable
// security-event pipeline (SecurityEventTaxonomy / SecurityAlertRuleEvaluator).

#region TamperEvidentAuditLog Tests

public class TamperEvidentAuditLogTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var log = TamperEvidentAuditLog.Create(
            tenantId, userId,
            "Update", "User", entityId,
            "{\"name\":\"old\"}", "{\"name\":\"new\"}",
            "name: old -> new",
            "Medium",
            "10.0.0.1", "Chrome/120",
            "US", "CA", "SF",
            "prev_hash_123", 42);

        log.Id.Should().NotBeEmpty();
        log.TenantId.Should().Be(tenantId);
        log.UserId.Should().Be(userId);
        log.Action.Should().Be("Update");
        log.EntityType.Should().Be("User");
        log.EntityId.Should().Be(entityId);
        log.PreviousHash.Should().Be("prev_hash_123");
        log.SequenceNumber.Should().Be(42);
        log.IsVerified.Should().BeFalse();
        log.ForwardedToSiem.Should().BeFalse();
        log.IsPartOfEvidence.Should().BeFalse();
    }

    [Fact]
    public void SetCryptographicHashes_ShouldSetBothHashes()
    {
        var log = TamperEvidentAuditLog.Create(Guid.NewGuid(), null, "Read", "Doc", null, null, null, "", "Low", "", "", null, null, null, "", 1);

        log.SetCryptographicHashes("content_hash", "chain_hash");

        log.ContentHash.Should().Be("content_hash");
        log.ChainHash.Should().Be("chain_hash");
    }

    [Fact]
    public void Sign_ShouldRecordSignature()
    {
        var log = TamperEvidentAuditLog.Create(Guid.NewGuid(), null, "Read", "Doc", null, null, null, "", "Low", "", "", null, null, null, "", 1);

        log.Sign("digital_sig_xyz", "key-001");

        log.DigitalSignature.Should().Be("digital_sig_xyz");
        log.SigningKeyId.Should().Be("key-001");
    }

    [Fact]
    public void MarkAsVerified_ShouldSetFlag()
    {
        var log = TamperEvidentAuditLog.Create(Guid.NewGuid(), null, "Read", "Doc", null, null, null, "", "Low", "", "", null, null, null, "", 1);

        log.MarkAsVerified("Verified by system");

        log.IsVerified.Should().BeTrue();
        log.LastVerifiedAt.Should().NotBeNull();
        log.VerificationNotes.Should().Be("Verified by system");
    }

    [Fact]
    public void RecordCustody_ShouldBuildChain()
    {
        var log = TamperEvidentAuditLog.Create(Guid.NewGuid(), null, "Read", "Doc", null, null, null, "", "Low", "", "", null, null, null, "", 1);

        log.RecordCustody("Created by system");
        log.RecordCustody("Reviewed by analyst");

        log.CustodyChain.Should().Be("Created by system → Reviewed by analyst");
    }

    [Fact]
    public void MarkAsEvidence_ShouldSetFlag()
    {
        var log = TamperEvidentAuditLog.Create(Guid.NewGuid(), null, "Read", "Doc", null, null, null, "", "Low", "", "", null, null, null, "", 1);

        log.MarkAsEvidence("pkg-001");

        log.IsPartOfEvidence.Should().BeTrue();
        log.EvidencePackageId.Should().Be("pkg-001");
    }

    [Fact]
    public void MarkAsForwardedToSiem_ShouldSetFields()
    {
        var log = TamperEvidentAuditLog.Create(Guid.NewGuid(), null, "Read", "Doc", null, null, null, "", "Low", "", "", null, null, null, "", 1);

        log.MarkAsForwardedToSiem("corr-123");

        log.ForwardedToSiem.Should().BeTrue();
        log.ForwardedAt.Should().NotBeNull();
        log.SiemCorrelationId.Should().Be("corr-123");
    }

    [Fact]
    public void VerifyChain_ShouldReturnTrue_WhenHashMatches()
    {
        var log = TamperEvidentAuditLog.Create(Guid.NewGuid(), null, "Read", "Doc", null, null, null, "", "Low", "", "", null, null, null, "expected_hash", 1);

        log.VerifyChain("expected_hash").Should().BeTrue();
        log.VerifyChain("wrong_hash").Should().BeFalse();
    }

    [Fact]
    public void VerifyContentHash_ShouldCompareCorrectly()
    {
        var log = TamperEvidentAuditLog.Create(Guid.NewGuid(), null, "Read", "Doc", null, null, null, "", "Low", "", "", null, null, null, "", 1);
        log.SetCryptographicHashes("abc123", "chain");

        log.VerifyContentHash("abc123").Should().BeTrue();
        log.VerifyContentHash("def456").Should().BeFalse();
    }
}

#endregion

#region ComplianceEvidencePackage Tests

public class ComplianceEvidencePackageTests
{
    private static ComplianceEvidencePackage CreatePackage() =>
        ComplianceEvidencePackage.Create(
            Guid.NewGuid(),
            "SOC2 Q1 2025",
            ComplianceFramework.SOC2Type1,
            "1.0",
            new DateTime(2025, 1, 1),
            new DateTime(2025, 3, 31),
            "compliance-team");

    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var pkg = CreatePackage();

        pkg.Id.Should().NotBeEmpty();
        pkg.PackageName.Should().Be("SOC2 Q1 2025");
        pkg.Framework.Should().Be(ComplianceFramework.SOC2Type1);
        pkg.PackageVersion.Should().Be("1.0");
        pkg.Status.Should().Be(CompliancePackageStatus.Draft);
        pkg.PreparedBy.Should().Be("compliance-team");
    }

    [Fact]
    public void SetPackageContents_ShouldSetMetrics()
    {
        var pkg = CreatePackage();

        pkg.SetPackageContents(1000, 5, 500, 1024 * 1024);

        pkg.TotalAuditLogs.Should().Be(1000);
        pkg.TotalAnomalies.Should().Be(5);
        pkg.TotalAccessLogs.Should().Be(500);
        pkg.PackageSizeBytes.Should().Be(1024 * 1024);
    }

    [Fact]
    public void Sign_ShouldTransitionToSigned()
    {
        var pkg = CreatePackage();

        pkg.Sign("hash_abc", "sig_xyz");

        pkg.Status.Should().Be(CompliancePackageStatus.Signed);
        pkg.PackageHash.Should().Be("hash_abc");
        pkg.DigitalSignature.Should().Be("sig_xyz");
    }

    [Fact]
    public void MarkAsReviewed_ShouldTransition()
    {
        var pkg = CreatePackage();

        pkg.MarkAsReviewed("reviewer@co.com", "Looks good");

        pkg.Status.Should().Be(CompliancePackageStatus.Reviewed);
        pkg.ReviewedBy.Should().Be("reviewer@co.com");
        pkg.ReviewedAt.Should().NotBeNull();
        pkg.Notes.Should().Be("Looks good");
    }

    [Fact]
    public void Approve_ShouldTransition()
    {
        var pkg = CreatePackage();

        pkg.Approve("ciso@co.com");

        pkg.Status.Should().Be(CompliancePackageStatus.Approved);
        pkg.ApprovedBy.Should().Be("ciso@co.com");
        pkg.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsDelivered_ShouldTransition()
    {
        var pkg = CreatePackage();

        pkg.MarkAsDelivered("SFTP", "auditor@firm.com", "track-001");

        pkg.Status.Should().Be(CompliancePackageStatus.Delivered);
        pkg.DeliveryMethod.Should().Be("SFTP");
        pkg.DeliveredTo.Should().Be("auditor@firm.com");
        pkg.DeliveryTrackingId.Should().Be("track-001");
        pkg.DeliveredAt.Should().NotBeNull();
    }
}

#endregion

#region RetentionPolicySimulation Tests

public class RetentionPolicySimulationTests
{
    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var sim = RetentionPolicySimulation.Create(
            Guid.NewGuid(),
            "Standard Retention",
            365,
            new[] { "Login", "DataAccess" },
            0.10m);

        sim.Id.Should().NotBeEmpty();
        sim.PolicyName.Should().Be("Standard Retention");
        sim.RetentionDays.Should().Be(365);
        sim.ApplicableEventTypes.Should().HaveCount(2);
        sim.CostPerGbPerMonth.Should().Be(0.10m);
        sim.IsActive.Should().BeTrue();
    }

    [Fact]
    public void UpdateStorageMetrics_ShouldSetValues()
    {
        var sim = RetentionPolicySimulation.Create(Guid.NewGuid(), "Test", 90, Array.Empty<string>(), 0.10m);

        sim.UpdateStorageMetrics(1024 * 1024 * 1024L, 50000, 1000);

        sim.CurrentStorageSizeBytes.Should().Be(1024 * 1024 * 1024L);
        sim.RecordCount.Should().Be(50000);
        sim.AverageGrowthRatePerDay.Should().Be(1000);
    }

    [Fact]
    public void CalculateForecast_ShouldComputeProjections()
    {
        var sim = RetentionPolicySimulation.Create(Guid.NewGuid(), "Test", 90, Array.Empty<string>(), 0.10m);
        sim.UpdateStorageMetrics(1024L * 1024 * 1024, 90000, 1000);

        sim.CalculateForecast(30);

        sim.ForecastDays.Should().Be(30);
        sim.EstimatedStorageSizeBytes.Should().BeGreaterThan(sim.CurrentStorageSizeBytes);
        sim.EstimatedRecordCount.Should().BeGreaterThan(sim.RecordCount);
        sim.CurrentMonthlyCost.Should().BeGreaterThan(0);
        sim.ProjectedAnnualCost.Should().Be(sim.EstimatedMonthlyCost * 12);
    }
}

#endregion

#region ScheduledAuditExport Tests

public class ScheduledAuditExportTests
{
    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var export = ScheduledAuditExport.Create(
            Guid.NewGuid(), "NightlyExport", "0 2 * * *",
            ExportDestinationType.S3, "s3://bucket/audit",
            ExportFormat.Json, ComplianceFramework.SOC2Type1);

        export.Id.Should().NotBeEmpty();
        export.JobName.Should().Be("NightlyExport");
        export.CronExpression.Should().Be("0 2 * * *");
        export.DestinationType.Should().Be(ExportDestinationType.S3);
        export.ExportFormat.Should().Be(ExportFormat.Json);
        export.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void EnableDisable_ShouldToggle()
    {
        var export = ScheduledAuditExport.Create(Guid.NewGuid(), "Test", "* * * * *",
            ExportDestinationType.LocalFileSystem, "/tmp", ExportFormat.Csv);

        export.Disable();
        export.IsEnabled.Should().BeFalse();

        export.Enable();
        export.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void RecordSuccess_ShouldIncrementCount()
    {
        var export = ScheduledAuditExport.Create(Guid.NewGuid(), "Test", "* * * * *",
            ExportDestinationType.Sftp, "sftp://host", ExportFormat.Csv);
        var now = DateTime.UtcNow;

        export.RecordSuccess(now);
        export.RecordSuccess(now);

        export.SuccessCount.Should().Be(2);
        export.LastSuccessAt.Should().Be(now);
        export.LastRunAt.Should().Be(now);
    }

    [Fact]
    public void RecordFailure_ShouldTrackError()
    {
        var export = ScheduledAuditExport.Create(Guid.NewGuid(), "Test", "* * * * *",
            ExportDestinationType.AzureBlobStorage, "https://blob", ExportFormat.Parquet);
        var now = DateTime.UtcNow;

        export.RecordFailure(now, "Connection timeout");

        export.FailureCount.Should().Be(1);
        export.LastFailureAt.Should().Be(now);
        export.LastErrorMessage.Should().Be("Connection timeout");
    }
}

#endregion

#region AuditExportHistory Tests

public class AuditExportHistoryTests
{
    [Fact]
    public void Create_ShouldSetStatusInProgress()
    {
        var exportId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var history = AuditExportHistory.Create(exportId, tenantId);

        history.Id.Should().NotBeEmpty();
        history.ScheduledExportId.Should().Be(exportId);
        history.Status.Should().Be(ExportStatus.InProgress);
    }

    [Fact]
    public void Complete_ShouldSetAllFields()
    {
        var history = AuditExportHistory.Create(Guid.NewGuid(), Guid.NewGuid());

        history.Complete(100, 5000, "/exports/file.json", "sha256:abc", TimeSpan.FromSeconds(30));

        history.Status.Should().Be(ExportStatus.Completed);
        history.RecordCount.Should().Be(100);
        history.FileSizeBytes.Should().Be(5000);
        history.ExportPath.Should().Be("/exports/file.json");
        history.FileChecksum.Should().Be("sha256:abc");
        history.ExecutionDuration.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Fail_ShouldSetErrorAndDuration()
    {
        var history = AuditExportHistory.Create(Guid.NewGuid(), Guid.NewGuid());

        history.Fail("Disk full", TimeSpan.FromMinutes(2));

        history.Status.Should().Be(ExportStatus.Failed);
        history.ErrorMessage.Should().Be("Disk full");
        history.ExecutionDuration.Should().Be(TimeSpan.FromMinutes(2));
    }
}

#endregion

#region FieldAccessAudit Tests

public class FieldAccessAuditTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var audit = FieldAccessAudit.Create(
            tenantId, userId, "User", entityId, "Email",
            FieldAccessType.Read, true, SensitivityLevel.Confidential,
            "10.0.0.1", "Firefox/120");

        audit.Id.Should().NotBeEmpty();
        audit.UserId.Should().Be(userId);
        audit.EntityType.Should().Be("User");
        audit.FieldName.Should().Be("Email");
        audit.AccessType.Should().Be(FieldAccessType.Read);
        audit.IsSensitiveField.Should().BeTrue();
        audit.SensitivityLevel.Should().Be(SensitivityLevel.Confidential);
    }

    [Fact]
    public void SetValues_ShouldSetRawAndMasked()
    {
        var audit = FieldAccessAudit.Create(Guid.NewGuid(), Guid.NewGuid(), "User", Guid.NewGuid(), "SSN",
            FieldAccessType.Write, true, SensitivityLevel.Restricted, "1.1.1.1", "Agent");

        audit.SetValues("123-45-6789", "987-65-4321", "***-**-6789", "***-**-4321");

        audit.OldValue.Should().Be("123-45-6789");
        audit.NewValue.Should().Be("987-65-4321");
        audit.MaskedOldValue.Should().Be("***-**-6789");
        audit.MaskedNewValue.Should().Be("***-**-4321");
    }

    [Fact]
    public void SetComplianceInfo_ShouldSetFields()
    {
        var audit = FieldAccessAudit.Create(Guid.NewGuid(), Guid.NewGuid(), "User", Guid.NewGuid(), "Phone",
            FieldAccessType.Export, true, SensitivityLevel.Internal, "1.1.1.1", "Agent");

        audit.SetComplianceInfo("GDPR Art. 6(1)(b)", "consent-123", true);

        audit.LegalBasis.Should().Be("GDPR Art. 6(1)(b)");
        audit.ConsentId.Should().Be("consent-123");
        audit.RequiresNotification.Should().BeTrue();
    }

    [Fact]
    public void MarkNotificationSent_ShouldSetFlag()
    {
        var audit = FieldAccessAudit.Create(Guid.NewGuid(), Guid.NewGuid(), "User", Guid.NewGuid(), "Phone",
            FieldAccessType.Read, false, SensitivityLevel.Public, "1.1.1.1", "Agent");

        audit.MarkNotificationSent();

        audit.NotificationSent.Should().BeTrue();
        audit.NotificationSentAt.Should().NotBeNull();
    }
}

#endregion

#region PiiRedactionRule Tests

public class PiiRedactionRuleTests
{
    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var rule = PiiRedactionRule.Create(
            Guid.NewGuid(),
            "Email Redaction",
            new[] { "email", "contact_email" },
            PiiDetectionMethod.Regex,
            RedactionStrategy.PartialMasking);

        rule.Id.Should().NotBeEmpty();
        rule.RuleName.Should().Be("Email Redaction");
        rule.TargetFields.Should().HaveCount(2);
        rule.DetectionMethod.Should().Be(PiiDetectionMethod.Regex);
        rule.RedactionStrategy.Should().Be(RedactionStrategy.PartialMasking);
        rule.IsEnabled.Should().BeTrue();
        rule.Priority.Should().Be(100);
    }
}

#endregion

#region SavedAuditQuery Tests

public class SavedAuditQueryTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var userId = Guid.NewGuid();
        var query = SavedAuditQuery.Create(Guid.NewGuid(), "Failed Logins", "action:login AND success:false", userId);

        query.Id.Should().NotBeEmpty();
        query.QueryName.Should().Be("Failed Logins");
        query.QueryDsl.Should().Contain("login");
        query.CreatedByUserId.Should().Be(userId);
        query.IsPublic.Should().BeFalse();
        query.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public void RecordExecution_ShouldIncrementCount()
    {
        var query = SavedAuditQuery.Create(Guid.NewGuid(), "Test", "dsl", Guid.NewGuid());

        query.RecordExecution();
        query.RecordExecution();

        query.ExecutionCount.Should().Be(2);
        query.LastExecutedAt.Should().NotBeNull();
    }
}

#endregion

#region AuditReplaySession Tests

public class AuditReplaySessionTests
{
    [Fact]
    public void Create_ShouldSetStatusToCreated()
    {
        var session = AuditReplaySession.Create(
            Guid.NewGuid(), "Incident #42",
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow,
            Guid.NewGuid());

        session.Id.Should().NotBeEmpty();
        session.SessionName.Should().Be("Incident #42");
        session.Status.Should().Be(ReplayStatus.Created);
    }

    [Fact]
    public void StartReplay_ShouldTransition()
    {
        var session = AuditReplaySession.Create(Guid.NewGuid(), "Test", DateTime.UtcNow, DateTime.UtcNow, Guid.NewGuid());

        session.StartReplay();

        session.Status.Should().Be(ReplayStatus.InProgress);
    }

    [Fact]
    public void CompleteReplay_ShouldSetResults()
    {
        var session = AuditReplaySession.Create(Guid.NewGuid(), "Test", DateTime.UtcNow, DateTime.UtcNow, Guid.NewGuid());
        session.StartReplay();

        session.CompleteReplay(150, 10, "https://timeline.url", "{\"state\":\"final\"}");

        session.Status.Should().Be(ReplayStatus.Completed);
        session.TotalEventsReplayed.Should().Be(150);
        session.StateSnapshotsCreated.Should().Be(10);
        session.TimelineVisualizationUrl.Should().Be("https://timeline.url");
        session.FinalStateJson.Should().Contain("final");
    }
}

#endregion

#region AuditLog Tests

public class AuditLogTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var log = new AuditLog();

        log.ActionType.Should().BeEmpty();
        log.ResourceType.Should().BeEmpty();
        log.Success.Should().BeFalse();
        log.RiskLevel.Should().Be(AuditRiskLevel.Low);
        log.Category.Should().Be(AuditCategory.General);
    }

    [Fact]
    public void CanSetAllProperties()
    {
        var log = new AuditLog
        {
            ActionType = "UserLogin",
            ResourceType = "User",
            ResourceId = "user-123",
            UserId = Guid.NewGuid(),
            IpAddress = "192.168.1.1",
            UserAgent = "Chrome/120",
            Success = true,
            RiskLevel = AuditRiskLevel.High,
            Category = AuditCategory.Security,
            Description = "User logged in",
            CorrelationId = "corr-456"
        };

        log.ActionType.Should().Be("UserLogin");
        log.Success.Should().BeTrue();
        log.RiskLevel.Should().Be(AuditRiskLevel.High);
    }
}

#endregion

#region Enum Tests

public class AuditEnumTests
{
    [Fact]
    public void CompliancePackageStatus_ShouldHave8Values()
    {
        Enum.GetValues<CompliancePackageStatus>().Should().HaveCount(8);
    }

    [Fact]
    public void ExportDestinationType_ShouldIncludeTenantStorage()
    {
        Enum.GetValues<ExportDestinationType>()
            .Should()
            .Contain(ExportDestinationType.TenantStorage)
            .And.HaveCount(7);
    }

    [Fact]
    public void ExportFormat_ShouldHave4Values()
    {
        Enum.GetValues<ExportFormat>().Should().HaveCount(4);
    }

    [Fact]
    public void FieldAccessType_ShouldHave7Values()
    {
        Enum.GetValues<FieldAccessType>().Should().HaveCount(7);
    }

    [Fact]
    public void ReplayStatus_ShouldHave5Values()
    {
        Enum.GetValues<ReplayStatus>().Should().HaveCount(5);
    }
}

#endregion
