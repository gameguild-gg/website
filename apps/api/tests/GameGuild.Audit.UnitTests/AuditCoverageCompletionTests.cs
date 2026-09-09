using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using GameGuild.Tests.Audit.Unit.Services;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit;

public class AuditDomainCoverageCompletionTests
{
    [Fact]
    public void TamperEvidentAuditLog_ShouldExerciseStateTransitionsAndVerificationBranches()
    {
        var previousHash = "previous";
        var log = TamperEvidentAuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Update",
            "Lease",
            Guid.NewGuid(),
            "{}",
            "{\"status\":\"signed\"}",
            "{\"status\":\"signed\"}",
            "High",
            "198.51.100.10",
            "UnitTest",
            "US",
            "CA",
            "San Francisco",
            previousHash,
            7);

        log.SetCryptographicHashes("content", "chain");
        log.Sign("signature", "key-1");
        log.MarkAsVerified();
        log.RecordCustody("created");
        log.RecordCustody("reviewed");
        log.MarkAsEvidence("pkg-1");
        log.MarkAsForwardedToSiem("corr-1");

        log.ContentHash.Should().Be("content");
        log.ChainHash.Should().Be("chain");
        log.DigitalSignature.Should().Be("signature");
        log.SigningKeyId.Should().Be("key-1");
        log.IsVerified.Should().BeTrue();
        log.VerificationNotes.Should().BeNull();
        log.CustodyChain.Should().Contain("created").And.Contain("reviewed");
        log.EvidencePackageId.Should().Be("pkg-1");
        log.IsPartOfEvidence.Should().BeTrue();
        log.ForwardedToSiem.Should().BeTrue();
        log.SiemCorrelationId.Should().Be("corr-1");
        log.VerifyChain(previousHash).Should().BeTrue();
        log.VerifyChain("other").Should().BeFalse();
        log.VerifyContentHash("content").Should().BeTrue();
        log.VerifyContentHash("other").Should().BeFalse();
    }

    [Fact]
    public void AuditAnomaly_ShouldExerciseInvestigationLifecycle()
    {
        var anomaly = AuditAnomaly.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AnomalyType.PrivilegedOperationSpike,
            AnomalySeverity.Critical,
            "Spike",
            "Many privileged operations",
            "Rules",
            0.99,
            "203.0.113.10",
            "{}");
        var firstEvent = SystemClock.UtcNow.AddMinutes(-10);
        var lastEvent = SystemClock.UtcNow;

        anomaly.SetRelatedEvents("a,b", 2, firstEvent, lastEvent);
        anomaly.AssignTo("security@example.com");
        anomaly.MarkAsInvestigating();
        anomaly.Resolve("Resolved", "Rotated credentials");
        anomaly.MarkAsFalsePositive("Validated exception");
        anomaly.MarkNotificationSent("email");

        anomaly.RelatedAuditLogIds.Should().Be("a,b");
        anomaly.RelatedEventCount.Should().Be(2);
        anomaly.FirstRelatedEventAt.Should().Be(firstEvent);
        anomaly.LastRelatedEventAt.Should().Be(lastEvent);
        anomaly.AssignedTo.Should().Be("security@example.com");
        anomaly.InvestigatedAt.Should().NotBeNull();
        anomaly.Status.Should().Be(AnomalyStatus.FalsePositive);
        anomaly.ResolutionNotes.Should().Be("Validated exception");
        anomaly.MitigationActions.Should().Be("Rotated credentials");
        anomaly.NotificationSent.Should().BeTrue();
        anomaly.NotificationChannel.Should().Be("email");
    }

    [Fact]
    public void ComplianceEvidencePackage_ShouldExerciseReviewDeliveryAndMetadataBranches()
    {
        var package = ComplianceEvidencePackage.Create(
            Guid.NewGuid(),
            "SOC2 evidence",
            ComplianceFramework.SOC2Type2,
            "v1",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 3, 31),
            "auditor");
        var packageWithoutNotes = ComplianceEvidencePackage.Create(
            Guid.NewGuid(),
            "GDPR evidence",
            ComplianceFramework.GDPR,
            "v2",
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30),
            "privacy");

        package.SetPackageContents(10, 2, 3, 4096);
        package.Sign("hash", "signature");
        package.MarkAsReviewed("reviewer", "looks good");
        package.Approve("approver");
        package.SetStoragePath("s3://bucket/evidence.zip");
        package.MarkAsDelivered("email", "customer@example.com", "tracking-1");
        package.SetAttachmentMetadata("{\"files\":1}");
        packageWithoutNotes.MarkAsReviewed("reviewer");

        package.TotalAuditLogs.Should().Be(10);
        package.TotalAnomalies.Should().Be(2);
        package.TotalAccessLogs.Should().Be(3);
        package.PackageSizeBytes.Should().Be(4096);
        package.PackageHash.Should().Be("hash");
        package.DigitalSignature.Should().Be("signature");
        package.ReviewedBy.Should().Be("reviewer");
        package.Notes.Should().Be("looks good");
        package.ApprovedBy.Should().Be("approver");
        package.StoragePath.Should().Be("s3://bucket/evidence.zip");
        package.Status.Should().Be(CompliancePackageStatus.Delivered);
        package.DeliveryTrackingId.Should().Be("tracking-1");
        package.AttachmentMetadata.Should().Be("{\"files\":1}");
        packageWithoutNotes.Notes.Should().BeNull();
    }

    [Fact]
    public void FieldAccessAudit_ShouldExerciseValueContextComplianceAndNotificationUpdates()
    {
        var audit = FieldAccessAudit.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Customer",
            Guid.NewGuid(),
            "ssn",
            FieldAccessType.Read,
            true,
            SensitivityLevel.HighlyRestricted,
            "192.0.2.1",
            "UnitTest");

        audit.SetValues("123-45-6789", "987-65-4321", "***-**-6789", "***-**-4321");
        audit.SetAccessContext("request-1", "session-1", "/customers/1");
        audit.SetComplianceInfo("consent", "consent-1", true);
        audit.MarkNotificationSent();

        audit.OldValue.Should().Be("123-45-6789");
        audit.NewValue.Should().Be("987-65-4321");
        audit.MaskedOldValue.Should().Be("***-**-6789");
        audit.MaskedNewValue.Should().Be("***-**-4321");
        audit.RequestId.Should().Be("request-1");
        audit.SessionId.Should().Be("session-1");
        audit.ApiEndpoint.Should().Be("/customers/1");
        audit.LegalBasis.Should().Be("consent");
        audit.ConsentId.Should().Be("consent-1");
        audit.RequiresNotification.Should().BeTrue();
        audit.NotificationSent.Should().BeTrue();
        audit.NotificationSentAt.Should().NotBeNull();
    }

    [Fact]
    public void ScheduledAuditExportAndHistory_ShouldExerciseExecutionLifecycle()
    {
        var scheduled = ScheduledAuditExport.Create(
            Guid.NewGuid(),
            "nightly",
            "0 0 * * *",
            ExportDestinationType.S3,
            "s3://bucket",
            ExportFormat.Json,
            ComplianceFramework.ISO27001);
        var successAt = SystemClock.UtcNow.AddMinutes(-5);
        var failureAt = SystemClock.UtcNow;
        var nextRun = SystemClock.UtcNow.AddDays(1);

        scheduled.Disable();
        scheduled.Enable();
        scheduled.RecordSuccess(successAt);
        scheduled.RecordFailure(failureAt, "network");
        scheduled.UpdateNextRunTime(nextRun);

        var history = AuditExportHistory.Create(scheduled.Id, scheduled.TenantId!.Value);
        history.Complete(5, 2048, "/exports/audit.json", "checksum", TimeSpan.FromSeconds(2));
        var failedHistory = AuditExportHistory.Create(scheduled.Id, scheduled.TenantId!.Value);
        failedHistory.Fail("timeout", TimeSpan.FromSeconds(3));

        scheduled.IsEnabled.Should().BeTrue();
        scheduled.SuccessCount.Should().Be(1);
        scheduled.FailureCount.Should().Be(1);
        scheduled.LastSuccessAt.Should().Be(successAt);
        scheduled.LastFailureAt.Should().Be(failureAt);
        scheduled.LastErrorMessage.Should().Be("network");
        scheduled.NextRunAt.Should().Be(nextRun);
        history.Status.Should().Be(ExportStatus.Completed);
        history.RecordCount.Should().Be(5);
        history.FileSizeBytes.Should().Be(2048);
        history.ExportPath.Should().Be("/exports/audit.json");
        history.FileChecksum.Should().Be("checksum");
        failedHistory.Status.Should().Be(ExportStatus.Failed);
        failedHistory.ErrorMessage.Should().Be("timeout");
    }

    [Fact]
    public void AdvancedAuditEntities_ShouldExerciseFactoriesAndForecastRecommendationBranches()
    {
        var tenantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        var minimumPolicy = CreatePolicy(tenantId, 90, 500_000, 100, 1);
        var highGrowthPolicy = CreatePolicy(tenantId, 365, 1_500_000, 100, 1);
        var highCostPolicy = CreatePolicy(tenantId, 365, 100, 1024L * 1024 * 1024 * 1024, 2);
        var optimalPolicy = CreatePolicy(tenantId, 365, 100, 1024L * 1024 * 1024, 1);

        minimumPolicy.GenerateRecommendations();
        highGrowthPolicy.GenerateRecommendations();
        highCostPolicy.GenerateRecommendations();
        optimalPolicy.GenerateRecommendations();

        var piiRule = PiiRedactionRule.Create(
            tenantId,
            "emails",
            ["email"],
            PiiDetectionMethod.Regex,
            RedactionStrategy.PartialMasking);
        var savedQuery = SavedAuditQuery.Create(tenantId, "failed logins", "status = failed", createdBy, true);
        savedQuery.RecordExecution();
        var replay = AuditReplaySession.Create(
            tenantId,
            "incident",
            SystemClock.UtcNow.AddHours(-1),
            SystemClock.UtcNow,
            createdBy);
        replay.StartReplay();
        replay.CompleteReplay(12, 3, "https://timeline", "{\"state\":true}");
        replay.RecordFindings("No unauthorized access");

        minimumPolicy.RecommendedRetentionDays.Should().Be("90");
        minimumPolicy.RecommendedActions.Should().Contain("optimal");
        highGrowthPolicy.RecommendedRetentionDays.Should().Be("90");
        highGrowthPolicy.PotentialSavings.Should().BeGreaterThan(0);
        highCostPolicy.RecommendedRetentionDays.Should().Be("182");
        highCostPolicy.PotentialSavings.Should().BeGreaterThan(0);
        optimalPolicy.RecommendedRetentionDays.Should().Be("365");
        optimalPolicy.RecommendedActions.Should().Contain("optimal");
        piiRule.RuleName.Should().Be("emails");
        piiRule.TargetFields.Should().Contain("email");
        piiRule.IsEnabled.Should().BeTrue();
        savedQuery.IsPublic.Should().BeTrue();
        savedQuery.ExecutionCount.Should().Be(1);
        savedQuery.LastExecutedAt.Should().NotBeNull();
        replay.Status.Should().Be(ReplayStatus.Completed);
        replay.TotalEventsReplayed.Should().Be(12);
        replay.StateSnapshotsCreated.Should().Be(3);
        replay.TimelineVisualizationUrl.Should().Be("https://timeline");
        replay.FinalStateJson.Should().Be("{\"state\":true}");
        replay.Findings.Should().Be("No unauthorized access");
    }

    private static RetentionPolicySimulation CreatePolicy(
        Guid tenantId,
        int retentionDays,
        double growthRate,
        long storageBytes,
        decimal costPerGb)
    {
        var policy = RetentionPolicySimulation.Create(
            tenantId,
            $"policy-{retentionDays}-{growthRate}",
            retentionDays,
            ["Authentication"],
            costPerGb);

        policy.UpdateStorageMetrics(storageBytes, 3650, growthRate);
        policy.CalculateForecast(30);

        return policy;
    }
}

public class AuditServiceCoverageCompletionTests : IDisposable
{
    private readonly TestApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly AuditService _service;
    private readonly ServiceProvider _serviceProvider;

    public AuditServiceCoverageCompletionTests()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AuditCoverage_{Guid.NewGuid()}", databaseRoot)
            .Options;

        _context = new TestApplicationDbContext(options);
        _serviceProvider = new ServiceCollection()
            .AddScoped<IApplicationDbContext>(_ => new TestApplicationDbContext(options))
            .BuildServiceProvider();
        _service = new AuditService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _httpContextAccessor.Object,
            NullLogger<AuditService>.Instance);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _context.Dispose();
    }

    [Fact]
    public async Task LogAsync_ShouldPreferForwardedHeadersAndPersistSessionAndCorrelationIds()
    {
        var sessionId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-For"] = "203.0.113.8, 10.0.0.1";
        httpContext.Request.Headers["X-Correlation-ID"] = "corr-1";
        httpContext.Request.Headers.UserAgent = "ForwardedAgent";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", sessionId.ToString())]));
        _httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);

        await _service.LogAsync(new CreateAuditLogRequest
        {
            ActionType = "Forwarded",
            ResourceType = "Audit",
            Success = true,
            RiskLevel = AuditRiskLevel.Low
        });

        var log = await _context.Set<AuditLog>().SingleAsync();
        log.IpAddress.Should().Be("203.0.113.8");
        log.UserAgent.Should().Be("ForwardedAgent");
        log.SessionId.Should().Be(sessionId);
        log.CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    public async Task LogAsync_ShouldUseRealIpHeader_WhenForwardedHeaderIsAbsent()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Real-IP"] = "198.51.100.4";
        _httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);

        await _service.LogAsync(new CreateAuditLogRequest
        {
            ActionType = "RealIp",
            ResourceType = "Audit",
            Success = false,
            RiskLevel = AuditRiskLevel.High
        });

        var log = await _context.Set<AuditLog>().SingleAsync();
        log.IpAddress.Should().Be("198.51.100.4");
        log.Success.Should().BeFalse();
    }

    [Fact]
    public async Task LogAsync_ShouldHandleRemoteIpInvalidSessionAndUnknownRiskLevel()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.55");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", "not-a-guid")]));
        _httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);

        await _service.LogAsync(new CreateAuditLogRequest
        {
            ActionType = "UnknownRisk",
            ResourceType = "Audit",
            Success = true,
            RiskLevel = (AuditRiskLevel)999
        });

        var log = await _context.Set<AuditLog>().SingleAsync();
        log.IpAddress.Should().Be("192.0.2.55");
        log.SessionId.Should().BeNull();
        log.RiskLevel.Should().Be((AuditRiskLevel)999);
    }

    [Fact]
    public async Task LogAsync_ShouldHandleHttpContextWithoutRemoteIpOrUser()
    {
        var httpContext = new DefaultHttpContext
        {
            User = null!
        };
        _httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);

        await _service.LogAsync(new CreateAuditLogRequest
        {
            ActionType = "NoConnectionContext",
            ResourceType = "Audit",
            Success = true,
            RiskLevel = AuditRiskLevel.Low
        });

        var log = await _context.Set<AuditLog>().SingleAsync();
        log.IpAddress.Should().BeNull();
        log.SessionId.Should().BeNull();
    }

    [Fact]
    public async Task AuditOperationHelpers_ShouldPersistSpecificDescriptions()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns((HttpContext?)null);

        await _service.LogTenantIsolationBypassAsync(userId, "support escalation", new { ticket = "T-1" });
        await _service.LogPrivacyOperationAsync("PrivacyUpdated", userId, "email", "old", "new", tenantId, new { source = "unit" });
        await _service.LogPrivacyOperationAsync("PrivacyViewed", userId);
        await _service.LogPrivacyViolationAsync(userId, Guid.NewGuid(), "ssn", "not allowed", tenantId);
        await _service.LogUsernameOperationAsync("UsernameChanged", userId, "old-user", "new-user", "requested");
        await _service.LogUsernameOperationAsync("UsernameViewed", userId, reason: "audit");
        await _service.LogTenantOperationAsync("TenantSync", tenantId, success: false);

        var logs = await _context.Set<AuditLog>().OrderBy(log => log.CreatedAt).ToListAsync();

        logs.Should().HaveCount(7);
        logs.Should().Contain(log => log.ActionType == AuditActionTypes.TenantIsolationBypassed && log.Category == AuditCategory.Security);
        logs.Should().Contain(log => log.Description!.Contains("changed from 'old' to 'new'"));
        logs.Should().Contain(log => log.Description == "Privacy operation: PrivacyViewed");
        logs.Should().Contain(log => log.ActionType == AuditActionTypes.PrivacyViolationAttempt && !log.Success);
        logs.Should().Contain(log => log.Description!.Contains("Username changed from 'old-user' to 'new-user'"));
        logs.Should().Contain(log => log.Description == "Username operation: UsernameViewed - Reason: audit");
        logs.Should().Contain(log => log.Description == "Tenant operation: TenantSync" && !log.Success);
    }
}

public class AdvancedAuditFeatureServiceCoverageTests
{
    [Fact]
    public void AdvancedAuditResultModels_ShouldExposeMutableState()
    {
        var anomaly = AuditAnomaly.Create(
            Guid.NewGuid(),
            null,
            AnomalyType.Other,
            AnomalySeverity.Low,
            "Detected",
            "Detected by unit test",
            "Rule",
            0.5,
            "127.0.0.1",
            "{}");

        var verification = new ChainVerificationResult(true, 3, 3, 0, []);
        var detection = new AnomalyDetectionResult
        {
            IsAnomaly = true,
            ConfidenceScore = 0.75,
            DetectedAnomalies = [anomaly]
        };

        verification.IsValid.Should().BeTrue();
        verification.TotalLogs.Should().Be(3);
        detection.IsAnomaly.Should().BeTrue();
        detection.ConfidenceScore.Should().Be(0.75);
        detection.DetectedAnomalies.Should().ContainSingle().Which.Should().BeSameAs(anomaly);
    }

    [Fact]
    public async Task AdvancedAuditFeatureServices_ShouldDelegateAllOperations()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var export = ScheduledAuditExport.Create(tenantId, "export", "* * * * *", ExportDestinationType.Https, "https://example.test", ExportFormat.Csv);
        var exportHistory = AuditExportHistory.Create(export.Id, tenantId);
        var policy = RetentionPolicySimulation.Create(tenantId, "retention", 365, ["Security"], 1);
        policy.UpdateStorageMetrics(1024, 100, 10);
        var rule = PiiRedactionRule.Create(tenantId, "pii", ["email"], PiiDetectionMethod.Regex, RedactionStrategy.FullRedaction);
        var query = SavedAuditQuery.Create(tenantId, "saved", "{}", userId);
        var replay = AuditReplaySession.Create(tenantId, "replay", SystemClock.UtcNow.AddHours(-1), SystemClock.UtcNow, userId);
        var queryResult = new { Total = 1 };

        var repository = new Mock<IAdvancedAuditRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.AddScheduledExportAsync(export)).Returns(Task.CompletedTask);
        repository.Setup(repo => repo.GetScheduledExportsAsync(tenantId)).ReturnsAsync([export]);
        repository.Setup(repo => repo.AddRetentionPolicyAsync(policy)).Returns(Task.CompletedTask);
        repository.Setup(repo => repo.GetRetentionPolicyAsync(policy.Id)).ReturnsAsync(policy);
        repository.Setup(repo => repo.GetRetentionPolicyAsync(Guid.Empty)).ReturnsAsync((RetentionPolicySimulation?)null);
        repository.Setup(repo => repo.UpdateRetentionPolicyAsync(policy)).Returns(Task.CompletedTask);
        repository.Setup(repo => repo.GetRetentionPoliciesAsync(tenantId)).ReturnsAsync([policy]);
        repository.Setup(repo => repo.AddRedactionRuleAsync(rule)).Returns(Task.CompletedTask);
        repository.Setup(repo => repo.GetRedactionRulesAsync(tenantId)).ReturnsAsync([rule]);
        repository.Setup(repo => repo.AddSavedQueryAsync(query)).Returns(Task.CompletedTask);
        repository.Setup(repo => repo.GetSavedQueriesAsync(tenantId, userId)).ReturnsAsync([query]);
        repository.Setup(repo => repo.AddReplaySessionAsync(replay)).Returns(Task.CompletedTask);
        repository.Setup(repo => repo.GetReplaySessionsAsync(tenantId)).ReturnsAsync([replay]);

        var exportService = new Mock<IScheduledExportService>();
        exportService.Setup(service => service.ExecuteExportAsync(export.Id)).ReturnsAsync(exportHistory);
        var redactionService = new Mock<IPiiRedactionService>();
        redactionService.Setup(service => service.RedactAsync("email@example.com", tenantId)).ReturnsAsync("[redacted]");
        var queryService = new Mock<IAuditQueryService>();
        queryService.Setup(service => service.ExecuteQueryAsync(query.Id, userId)).ReturnsAsync(queryResult);
        var replayService = new Mock<IAuditReplayService>();
        replayService.Setup(service => service.ExecuteReplayAsync(replay.Id)).ReturnsAsync(replay);

        var service = new AdvancedAuditFeatureServices(
            repository.Object,
            exportService.Object,
            redactionService.Object,
            queryService.Object,
            replayService.Object);

        (await service.CreateScheduledExportAsync(export)).Should().BeSameAs(export);
        (await service.ExecuteScheduledExportAsync(export.Id)).Should().BeSameAs(exportHistory);
        (await service.GetScheduledExportsAsync(tenantId)).Should().ContainSingle().Which.Should().BeSameAs(export);
        (await service.CreateRetentionPolicyAsync(policy)).Should().BeSameAs(policy);
        (await service.SimulateForecastAsync(policy.Id, 30)).Should().BeSameAs(policy);
        await service.Invoking(s => s.SimulateForecastAsync(Guid.Empty, 30)).Should().ThrowAsync<InvalidOperationException>();
        (await service.GetRetentionPoliciesAsync(tenantId)).Should().ContainSingle().Which.Should().BeSameAs(policy);
        (await service.RedactPiiAsync("email@example.com", tenantId)).Should().Be("[redacted]");
        (await service.CreateRedactionRuleAsync(rule)).Should().BeSameAs(rule);
        (await service.GetRedactionRulesAsync(tenantId)).Should().ContainSingle().Which.Should().BeSameAs(rule);
        (await service.CreateSavedQueryAsync(query)).Should().BeSameAs(query);
        (await service.ExecuteSavedQueryAsync(query.Id, userId)).Should().BeSameAs(queryResult);
        (await service.GetSavedQueriesAsync(tenantId, userId)).Should().ContainSingle().Which.Should().BeSameAs(query);
        (await service.CreateReplaySessionAsync(replay)).Should().BeSameAs(replay);
        (await service.ExecuteReplayAsync(replay.Id)).Should().BeSameAs(replay);
        (await service.GetReplaySessionsAsync(tenantId)).Should().ContainSingle().Which.Should().BeSameAs(replay);
        repository.VerifyAll();
    }
}

public class AuditControllerCoverageCompletionTests
{
    [Fact]
    public async Task AuditController_ShouldRejectAnonymousUsersForAllEndpoints()
    {
        var controller = CreateAuditController(ActorContext.Anonymous);

        await controller.Invoking(c => c.GetAuditLogs(new AuditLogQueryRequest())).Should().ThrowAsync<UnauthorizedAccessException>();
        await controller.Invoking(c => c.GetAuditStatistics(new AuditStatisticsRequest())).Should().ThrowAsync<UnauthorizedAccessException>();
        await controller.Invoking(c => c.ExportAuditLogs(new AuditExportRequest())).Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetAuditLogs_ShouldMapLogsAndCapQueryTake()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var auditService = new Mock<IAuditService>();
        AuditLogQuery? capturedQuery = null;
        var auditLog = CreateAuditLog(userId, tenantId);
        auditService
            .Setup(service => service.LogAdminActionAsync(userId, "ViewAuditLogs", "Admin accessed audit logs", It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        auditService
            .Setup(service => service.GetAuditLogsAsync(It.IsAny<AuditLogQuery>()))
            .Callback<AuditLogQuery>(query => capturedQuery = query)
            .ReturnsAsync([auditLog]);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .ReturnsAsync(9);
        var controller = CreateAuditController(CreateActor(userId), auditService.Object);
        var request = new AuditLogQueryRequest
        {
            TenantId = tenantId,
            UserId = userId,
            ActionType = "Update",
            ResourceType = "Lease",
            Category = AuditCategory.Admin,
            RiskLevel = AuditRiskLevel.High,
            Success = true,
            StartDate = SystemClock.UtcNow.AddDays(-1),
            EndDate = SystemClock.UtcNow,
            IpAddress = "192.0.2.1",
            Skip = 2,
            Take = 5000
        };

        var result = await controller.GetAuditLogs(request);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<AuditLogResponse>().Subject;
        response.TotalCount.Should().Be(9);
        response.Skip.Should().Be(2);
        response.Take.Should().Be(5000);
        response.Logs.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            auditLog.Id,
            auditLog.ActionType,
            auditLog.ResourceType,
            auditLog.ResourceId,
            auditLog.UserId,
            auditLog.TenantId,
            auditLog.IpAddress,
            auditLog.UserAgent,
            auditLog.SessionId,
            auditLog.Description,
            auditLog.Success,
            auditLog.ErrorMessage,
            auditLog.RiskLevel,
            auditLog.Category,
            auditLog.CorrelationId,
            auditLog.CreatedAt
        });
        capturedQuery.Should().NotBeNull();
        capturedQuery!.Take.Should().Be(1000);
    }

    [Fact]
    public async Task GetAuditStatistics_ShouldSupportDefaultAndExplicitDateRanges()
    {
        var userId = Guid.NewGuid();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAdminActionAsync(userId, "ViewAuditStatistics", "Admin accessed audit statistics", null))
            .Returns(Task.CompletedTask);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .ReturnsAsync((AuditLogQuery query) => query.Category switch
            {
                AuditCategory.Authentication => 1,
                AuditCategory.Permission => 2,
                AuditCategory.Security => 3,
                _ when query.Success == false => 4,
                _ when query.RiskLevel == AuditRiskLevel.High => 5,
                _ => 6
            });
        var controller = CreateAuditController(CreateActor(userId), auditService.Object);
        var explicitStart = new DateTime(2026, 1, 1);
        var explicitEnd = new DateTime(2026, 1, 31);

        var explicitResult = await controller.GetAuditStatistics(new AuditStatisticsRequest
        {
            StartDate = explicitStart,
            EndDate = explicitEnd
        });
        var defaultResult = await controller.GetAuditStatistics(new AuditStatisticsRequest());

        var explicitResponse = explicitResult.Result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<AuditStatisticsResponse>().Subject;
        explicitResponse.StartDate.Should().Be(explicitStart);
        explicitResponse.EndDate.Should().Be(explicitEnd);
        explicitResponse.TotalEvents.Should().Be(6);
        explicitResponse.AuthenticationEvents.Should().Be(1);
        explicitResponse.PermissionEvents.Should().Be(2);
        explicitResponse.SecurityEvents.Should().Be(3);
        explicitResponse.FailedEvents.Should().Be(4);
        explicitResponse.HighRiskEvents.Should().Be(5);
        defaultResult.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExportAuditLogs_ShouldReturnCsvFile()
    {
        var userId = Guid.NewGuid();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAdminActionAsync(userId, "ExportAuditLogs", "Admin exported audit logs", It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        auditService
            .Setup(service => service.GetAuditLogsAsync(It.Is<AuditLogQuery>(query => query.Take == 0)))
            .ReturnsAsync([CreateAuditLog(userId, Guid.NewGuid())]);
        var controller = CreateAuditController(CreateActor(userId), auditService.Object);

        var result = await controller.ExportAuditLogs(new AuditExportRequest { ActionType = "Update" });

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        file.FileDownloadName.Should().StartWith("audit-logs-");
        Encoding.UTF8.GetString(file.FileContents).Should().Contain("Id,ActionType").And.Contain("Update");
    }

    private static AuditController CreateAuditController(ActorContext actorContext, IAuditService? auditService = null)
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(accessor => accessor.ActorContext).Returns(actorContext);

        return new AuditController(
            auditService ?? Mock.Of<IAuditService>(),
            actorAccessor.Object,
            NullLogger<AuditController>.Instance,
            new CommandHandlerSender(auditService ?? Mock.Of<IAuditService>(), Mock.Of<ISecurityAuditAggregator>(), actorAccessor.Object))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static AuditLog CreateAuditLog(Guid userId, Guid tenantId)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            ActionType = "Update",
            ResourceType = "Lease",
            ResourceId = "lease-1",
            UserId = userId,
            TenantId = tenantId,
            IpAddress = "192.0.2.1",
            UserAgent = "UnitTest",
            SessionId = Guid.NewGuid(),
            Description = "Updated lease",
            Success = true,
            ErrorMessage = null,
            RiskLevel = AuditRiskLevel.High,
            Category = AuditCategory.Admin,
            CorrelationId = "corr-1",
            CreatedAt = new DateTime(2026, 1, 1)
        };
    }

    private static ActorContext CreateActor(Guid userId)
    {
        return new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            Roles = new HashSet<string> { "Admin" },
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        };
    }
}

public class SecurityAuditControllerCoverageCompletionTests
{
    [Fact]
    public async Task SecurityAuditController_ShouldRejectAnonymousUsersForAllEndpoints()
    {
        var controller = CreateController(ActorContext.Anonymous);

        (await controller.GetSecurityAuditLogs(new UnifiedSecurityAuditRequest())).Result.Should().BeOfType<UnauthorizedObjectResult>();
        (await controller.GetAuthenticationLogs(new AuthenticationAuditRequest())).Result.Should().BeOfType<UnauthorizedObjectResult>();
        (await controller.GetPermissionLogs(new PermissionAuditRequest())).Result.Should().BeOfType<UnauthorizedObjectResult>();
        (await controller.GetSecurityDashboard()).Result.Should().BeOfType<UnauthorizedObjectResult>();
        (await controller.ExportSecurityAuditLogs(new UnifiedSecurityAuditRequest())).Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task SecurityAuditController_ShouldDelegateAuthorizedRequestsAndReturnResponses()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var aggregator = new Mock<ISecurityAuditAggregator>();
        var auditService = new Mock<IAuditService>();
        var unifiedRequest = new UnifiedSecurityAuditRequest();
        var authenticationRequest = new AuthenticationAuditRequest();
        var permissionRequest = new PermissionAuditRequest();
        var unifiedResponse = new UnifiedSecurityAuditResponse { TotalCount = 1 };
        var authenticationResponse = new AuthenticationAuditResponse { TotalCount = 2 };
        var permissionResponse = new PermissionAuditResponse { TotalCount = 3 };
        var dashboard = new SecurityAuditDashboard { TenantId = tenantId };
        var exportBytes = Encoding.UTF8.GetBytes("csv");

        auditService
            .Setup(service => service.LogAdminActionAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        aggregator.Setup(service => service.GetUnifiedAuditLogsAsync(unifiedRequest, It.IsAny<CancellationToken>())).ReturnsAsync(unifiedResponse);
        aggregator.Setup(service => service.GetAuthenticationLogsAsync(authenticationRequest, It.IsAny<CancellationToken>())).ReturnsAsync(authenticationResponse);
        aggregator.Setup(service => service.GetPermissionLogsAsync(permissionRequest, It.IsAny<CancellationToken>())).ReturnsAsync(permissionResponse);
        aggregator
            .Setup(service => service.GetSecurityDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);
        aggregator.Setup(service => service.ExportAuditLogsAsync(unifiedRequest, It.IsAny<CancellationToken>())).ReturnsAsync(exportBytes);
        var controller = CreateController(CreateActor(userId), aggregator.Object, auditService.Object);

        var unifiedResult = await controller.GetSecurityAuditLogs(unifiedRequest);
        var authenticationResult = await controller.GetAuthenticationLogs(authenticationRequest);
        var permissionResult = await controller.GetPermissionLogs(permissionRequest);
        var dashboardResult = await controller.GetSecurityDashboard(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), tenantId);
        var exportResult = await controller.ExportSecurityAuditLogs(unifiedRequest);

        unifiedResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(unifiedResponse);
        authenticationResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(authenticationResponse);
        permissionResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(permissionResponse);
        dashboardResult.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dashboard);
        var file = exportResult.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        file.FileContents.Should().Equal(exportBytes);
    }

    [Fact]
    public async Task SecurityAuditController_ShouldUseDefaultDashboardDates()
    {
        var userId = Guid.NewGuid();
        var aggregator = new Mock<ISecurityAuditAggregator>();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAdminActionAsync(userId, "ViewSecurityDashboard", "Admin accessed security audit dashboard", null))
            .Returns(Task.CompletedTask);
        aggregator
            .Setup(service => service.GetSecurityDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityAuditDashboard());
        var controller = CreateController(CreateActor(userId), aggregator.Object, auditService.Object);

        var result = await controller.GetSecurityDashboard();

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    private static SecurityAuditController CreateController(
        ActorContext actorContext,
        ISecurityAuditAggregator? aggregator = null,
        IAuditService? auditService = null)
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(accessor => accessor.ActorContext).Returns(actorContext);

        return new SecurityAuditController(
            aggregator ?? Mock.Of<ISecurityAuditAggregator>(),
            auditService ?? Mock.Of<IAuditService>(),
            actorAccessor.Object,
            NullLogger<SecurityAuditController>.Instance,
            new CommandHandlerSender(aggregator ?? Mock.Of<ISecurityAuditAggregator>(), auditService ?? Mock.Of<IAuditService>(), actorAccessor.Object));
    }

    private static ActorContext CreateActor(Guid userId)
    {
        return new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            Roles = new HashSet<string> { "Admin" },
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        };
    }
}

public class AuditFacadeAndModuleCoverageTests
{
    [Fact]
    public async Task SecurityAuditAggregator_ShouldDelegateToQueryAndReportServices()
    {
        var startDate = new DateTime(2026, 1, 1);
        var endDate = new DateTime(2026, 1, 31);
        var tenantId = Guid.NewGuid();
        var unifiedRequest = new UnifiedSecurityAuditRequest();
        var authRequest = new AuthenticationAuditRequest();
        var permissionRequest = new PermissionAuditRequest();
        var unified = new UnifiedSecurityAuditResponse();
        var auth = new AuthenticationAuditResponse();
        var permission = new PermissionAuditResponse();
        var dashboard = new SecurityAuditDashboard();
        var export = Encoding.UTF8.GetBytes("csv");
        var queryService = new Mock<IAuditLogQueryService>();
        var reportService = new Mock<IAuditReportService>();
        queryService.Setup(service => service.GetUnifiedAuditLogsAsync(unifiedRequest, It.IsAny<CancellationToken>())).ReturnsAsync(unified);
        queryService.Setup(service => service.GetAuthenticationLogsAsync(authRequest, It.IsAny<CancellationToken>())).ReturnsAsync(auth);
        queryService.Setup(service => service.GetPermissionLogsAsync(permissionRequest, It.IsAny<CancellationToken>())).ReturnsAsync(permission);
        reportService.Setup(service => service.GetSecurityDashboardAsync(startDate, endDate, tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(dashboard);
        reportService.Setup(service => service.ExportAuditLogsAsync(unifiedRequest, It.IsAny<CancellationToken>())).ReturnsAsync(export);
        var aggregator = new SecurityAuditAggregator(queryService.Object, reportService.Object);

        (await aggregator.GetUnifiedAuditLogsAsync(unifiedRequest)).Should().BeSameAs(unified);
        (await aggregator.GetAuthenticationLogsAsync(authRequest)).Should().BeSameAs(auth);
        (await aggregator.GetPermissionLogsAsync(permissionRequest)).Should().BeSameAs(permission);
        (await aggregator.GetSecurityDashboardAsync(startDate, endDate, tenantId)).Should().BeSameAs(dashboard);
        (await aggregator.ExportAuditLogsAsync(unifiedRequest)).Should().BeSameAs(export);
    }

    [Fact]
    public void AddAuditServices_ShouldRegisterAuditServicesAndReturnServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddAuditServices();

        returned.Should().BeSameAs(services);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAuditService) && descriptor.ImplementationType == typeof(AuditService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAuditLogQueryService) && descriptor.ImplementationType == typeof(AuditLogQueryService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAuditReportService) && descriptor.ImplementationType == typeof(AuditReportService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ISecurityAuditAggregator) && descriptor.ImplementationType == typeof(SecurityAuditAggregator));
    }

    [Fact]
    public async Task AuditReportExport_ShouldHandleNullAndEmptyCsvValues()
    {
        var request = new UnifiedSecurityAuditRequest();
        var queryService = new Mock<IAuditLogQueryService>();
        queryService
            .Setup(service => service.GetUnifiedAuditLogsAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnifiedSecurityAuditResponse
            {
                Entries =
                [
                    new UnifiedSecurityAuditEntry
                    {
                        Timestamp = new DateTime(2026, 1, 1, 1, 2, 3),
                        SourceType = SecurityAuditSourceType.General,
                        ActionType = string.Empty,
                        ResourceType = null,
                        ResourceId = null,
                        UserId = null,
                        IpAddress = null,
                        Success = false,
                        Description = null
                    }
                ]
            });
        var service = new AuditReportService(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<GameGuild.Identity.Authorization.IPermissionAuditLogRepository>(),
            queryService.Object,
            NullLogger<AuditReportService>.Instance);

        var csv = Encoding.UTF8.GetString(await service.ExportAuditLogsAsync(request));

        csv.Should().Contain("\"General\"");
        csv.Should().Contain("\"False\"");
    }
}
