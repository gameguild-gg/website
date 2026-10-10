using System.Security.Claims;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
    public void SecurityAlert_ShouldExerciseInvestigationLifecycle()
    {
        var alert = SecurityAlert.Raise(
            Guid.NewGuid(),
            SecurityAlertRules.CriticalEvent,
            SecurityEventKind.ThreatDetection,
            AuditRiskLevel.Critical,
            "Spike",
            "Many privileged operations",
            AuditActionTypes.SecurityViolation,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "203.0.113.10",
            SystemClock.UtcNow);
        var firstSeen = alert.FirstSeenAtUtc;
        var admin = Guid.NewGuid();

        alert.RecordOccurrence(firstSeen.AddMinutes(1), Guid.NewGuid());
        alert.Acknowledge(admin, "investigating", firstSeen.AddMinutes(2));
        alert.Resolve(admin, "Rotated credentials", firstSeen.AddMinutes(3));

        alert.OccurrenceCount.Should().Be(2);
        alert.LastSeenAtUtc.Should().BeAfter(firstSeen);
        alert.Status.Should().Be(SecurityAlertStatus.Resolved);
        alert.AcknowledgedByUserId.Should().Be(admin);
        alert.ResolvedByUserId.Should().Be(admin);
        alert.ResolutionNotes.Should().Be("Rotated credentials");

        // Terminal state: neither acknowledgement nor resolution may follow a resolution.
        var act = () => alert.Acknowledge(admin, "late", firstSeen.AddMinutes(4));
        act.Should().Throw<SecurityAlertTransitionException>();
        var resolveAgain = () => alert.Resolve(admin, "again", firstSeen.AddMinutes(5));
        resolveAgain.Should().Throw<SecurityAlertTransitionException>();
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
    public void AdvancedAuditResultModels_ShouldExposeState()
    {
        var verification = new ChainVerificationResult(true, 3, 3, 0, []);

        verification.IsValid.Should().BeTrue();
        verification.TotalLogs.Should().Be(3);
        verification.VerifiedLogs.Should().Be(3);
        verification.FailedLogs.Should().Be(0);
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
    public async Task SearchAuditLogsByDateRange_ShouldNormalizeDatesAndCombineFilters()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var auditService = new Mock<IAuditService>();
        AuditLogQuery? capturedQuery = null;
        var bucketStart = new DateTime(2025, 12, 31, 22, 0, 0, DateTimeKind.Utc);
        auditService
            .Setup(service => service.LogAdminActionAsync(
                userId,
                "SearchAuditLogsByDateRange",
                "Admin searched audit logs by date range",
                It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        auditService
            .Setup(service => service.GetAuditLogsAsync(It.IsAny<AuditLogQuery>()))
            .Callback<AuditLogQuery>(query => capturedQuery = query)
            .ReturnsAsync([]);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .ReturnsAsync(3);
        auditService
            .Setup(service => service.GetAuditActivityAsync(
                It.IsAny<AuditLogQuery>(),
                AuditActivityBucketSize.Hourly,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AuditActivityBucket(bucketStart, 3)]);
        var controller = CreateAuditController(CreateActor(userId), auditService.Object);

        var result = await controller.SearchAuditLogsByDateRange(new AuditDateRangeSearchRequest
        {
            Start = "2026-01-01T00:00:00+02:00",
            End = "2026-01-01T02:00:00+02:00",
            TimeZoneId = "America/Sao_Paulo",
            BucketSize = AuditActivityBucketSize.Hourly,
            TenantId = tenantId,
            UserId = userId,
            ActionType = "Update",
            Category = AuditCategory.Admin,
            RiskLevel = AuditRiskLevel.High,
            Success = false,
            IpAddress = "192.0.2.10",
            Skip = 5,
            Take = 25
        }, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AuditDateRangeSearchResponse>().Subject;
        capturedQuery.Should().NotBeNull();
        capturedQuery!.StartDate.Should().Be(bucketStart);
        capturedQuery.EndDate.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        capturedQuery.TenantId.Should().Be(tenantId);
        capturedQuery.UserId.Should().Be(userId);
        capturedQuery.ActionType.Should().Be("Update");
        capturedQuery.Category.Should().Be(AuditCategory.Admin);
        capturedQuery.RiskLevel.Should().Be(AuditRiskLevel.High);
        capturedQuery.Success.Should().BeFalse();
        capturedQuery.IpAddress.Should().Be("192.0.2.10");
        capturedQuery.Skip.Should().Be(5);
        capturedQuery.Take.Should().Be(25);
        response.StartDateUtc.Should().Be(new DateTimeOffset(bucketStart, TimeSpan.Zero));
        response.TimeZoneId.Should().Be("America/Sao_Paulo");
        response.Results.TotalCount.Should().Be(3);
        response.Results.Skip.Should().Be(5);
        response.Activity.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            StartUtc = new DateTimeOffset(bucketStart, TimeSpan.Zero),
            StartLocal = new DateTimeOffset(2025, 12, 31, 19, 0, 0, TimeSpan.FromHours(-3)),
            EventCount = 3
        });
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
    public async Task ExportAuditLogs_ShouldStreamEscapedCsvAndHonorSelectedColumnOrder()
    {
        var userId = Guid.NewGuid();
        var auditService = new Mock<IAuditService>();
        var auditLog = CreateAuditLog(userId, Guid.NewGuid());
        auditLog.Description = "changed, \"quoted\"\r\nline";
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        auditService
            .Setup(service => service.LogAdminActionAsync(userId, "ExportAuditLogs", "Admin exported audit logs", It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.Is<AuditLogQuery>(query =>
                query.ActionType == "Update" && query.StartDate == startDate && query.EndDate == endDate)))
            .ReturnsAsync(1);
        auditService
            .Setup(service => service.StreamAuditLogsAsync(
                It.Is<AuditLogQuery>(query =>
                    query.ActionType == "Update" && query.StartDate == startDate && query.EndDate == endDate && query.Take == 0),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([auditLog]));
        var controller = CreateAuditController(CreateActor(userId), auditService.Object);
        var responseBody = new MemoryStream();
        controller.HttpContext.Response.Body = responseBody;

        var result = await controller.ExportAuditLogs(new AuditExportRequest
        {
            ActionType = "Update",
            StartDate = startDate,
            EndDate = endDate,
            Columns = ["Description", "ActionType"]
        });

        result.Should().BeOfType<EmptyResult>();
        controller.HttpContext.Response.ContentType.Should().Be("text/csv; charset=utf-8");
        controller.HttpContext.Response.Headers["Content-Disposition"].ToString().Should().Contain("audit-logs-").And.Contain(".csv");
        controller.HttpContext.Response.Headers["X-Audit-Total-Records"].ToString().Should().Be("1");

        responseBody.Position = 0;
        var csv = Encoding.UTF8.GetString(responseBody.ToArray());
        csv.Should().Be("Description,ActionType\r\n\"changed, \"\"quoted\"\"\r\nline\",Update\r\n");
    }

    [Fact]
    public async Task ExportAuditLogs_ShouldNotifyCompletionWebhook()
    {
        var userId = Guid.NewGuid();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAdminActionAsync(userId, "ExportAuditLogs", "Admin exported audit logs", It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .ReturnsAsync(0);
        auditService
            .Setup(service => service.StreamAuditLogsAsync(It.IsAny<AuditLogQuery>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(Array.Empty<AuditLog>()));

        var webhookNotifier = new Mock<IAuditExportWebhookNotifier>();
        webhookNotifier.Setup(service => service.ValidateWebhookUrl(It.IsAny<string?>())).Returns((string?)null);
        webhookNotifier.Setup(service => service.NotifyAsync(
                It.IsAny<string?>(),
                It.IsAny<AuditExportWebhookNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = CreateAuditController(CreateActor(userId), auditService.Object, webhookNotifier.Object);
        controller.HttpContext.Response.Body = new MemoryStream();

        await controller.ExportAuditLogs(new AuditExportRequest
        {
            WebhookUrl = "https://hooks.example.com/audit"
        });

        webhookNotifier.Verify(service => service.NotifyAsync(
                "https://hooks.example.com/audit",
                It.Is<AuditExportWebhookNotification>(notification =>
                    notification.Type == "audit.export.completed" &&
                    notification.Format == "csv" &&
                    notification.Status == "completed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportAuditLogs_ShouldNotifyFailureWebhookWhenPreparationFails()
    {
        var userId = Guid.NewGuid();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAdminActionAsync(userId, "ExportAuditLogs", "Admin exported audit logs", It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .ThrowsAsync(new InvalidOperationException("simulated query failure"));

        var webhookNotifier = new Mock<IAuditExportWebhookNotifier>();
        webhookNotifier.Setup(service => service.ValidateWebhookUrl(It.IsAny<string?>())).Returns((string?)null);
        webhookNotifier.Setup(service => service.NotifyAsync(
                It.IsAny<string?>(),
                It.IsAny<AuditExportWebhookNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = CreateAuditController(CreateActor(userId), auditService.Object, webhookNotifier.Object);

        await controller
            .Invoking(c => c.ExportAuditLogs(new AuditExportRequest { WebhookUrl = "https://hooks.example.com/audit" }))
            .Should()
            .ThrowAsync<InvalidOperationException>();

        webhookNotifier.Verify(service => service.NotifyAsync(
                "https://hooks.example.com/audit",
                It.Is<AuditExportWebhookNotification>(notification =>
                    notification.Type == "audit.export.failed" &&
                    notification.Format == "csv" &&
                    notification.ErrorCode == "audit_export_failed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("csv", 7, 99, 7, 7)]
    [InlineData("json", 7, 99, 7, 7)]
    [InlineData("csv", 200, 5, 200, 5)]
    [InlineData("json", 200, 5, 200, 5)]
    [InlineData("csv", -3, 12, 0, 0)]
    [InlineData("json", -3, 12, 0, 0)]
    [InlineData("csv", 10, -4, 10, 0)]
    [InlineData("json", 10, -4, 10, 0)]
    public async Task ExportCompletionWebhook_UsesTrackedProgressAndClampsOnlyTheNotification(
        string format, int totalRecords, int recordsWritten, int expectedTotal, int expectedWritten)
    {
        var userId = Guid.NewGuid();
        var service = CreateWebhookExportService();
        var progress = new Mock<IAuditExportProgressTracker>();
        var tracked = new AuditExportProgressResponse(Guid.NewGuid(), "Completed", totalRecords, recordsWritten, 100,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        progress.Setup(value => value.GetAsync(It.IsAny<Guid>(), userId, CancellationToken.None)).ReturnsAsync(tracked);
        var notifier = new Mock<IAuditExportWebhookNotifier>();
        notifier.Setup(value => value.ValidateWebhookUrl(It.IsAny<string?>())).Returns((string?)null);
        notifier.Setup(value => value.NotifyAsync(It.IsAny<string?>(), It.IsAny<AuditExportWebhookNotification>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
        var controller = CreateAuditController(CreateActor(userId), service.Object, notifier.Object, progress.Object);
        controller.Response.Body = new MemoryStream();
        var request = new AuditExportRequest { WebhookUrl = "https://hooks.example.com/audit" };

        var result = format == "csv"
            ? await controller.ExportAuditLogs(request)
            : await controller.ExportAuditLogsJson(request);

        Assert.IsType<EmptyResult>(result);
        notifier.Verify(value => value.NotifyAsync(request.WebhookUrl,
            It.Is<AuditExportWebhookNotification>(notification => notification.Status == "completed"
                && notification.Format == format && notification.TotalRecords == expectedTotal
                && notification.RecordsWritten == expectedWritten && notification.ErrorCode == null),
            CancellationToken.None), Times.Once);
        progress.Verify(value => value.GetAsync(It.IsAny<Guid>(), userId, CancellationToken.None), Times.Once);
        tracked.TotalRecords.Should().Be(totalRecords);
        tracked.RecordsWritten.Should().Be(recordsWritten);
    }

    [Theory]
    [InlineData("csv", false)]
    [InlineData("json", false)]
    [InlineData("csv", true)]
    [InlineData("json", true)]
    public async Task ExportCompletionWebhook_PreservesPreparationCountsWhenProgressIsMissingOrUnavailable(string format, bool unavailable)
    {
        var userId = Guid.NewGuid();
        var service = CreateWebhookExportService();
        var progress = new Mock<IAuditExportProgressTracker>();
        var lookup = progress.Setup(value => value.GetAsync(It.IsAny<Guid>(), userId, CancellationToken.None));
        if (unavailable)
        {
            lookup.ThrowsAsync(new InvalidOperationException("Progress unavailable"));
        }
        else
        {
            lookup.ReturnsAsync((AuditExportProgressResponse?)null);
        }
        var notifier = new Mock<IAuditExportWebhookNotifier>();
        notifier.Setup(value => value.ValidateWebhookUrl(It.IsAny<string?>())).Returns((string?)null);
        notifier.Setup(value => value.NotifyAsync(It.IsAny<string?>(), It.IsAny<AuditExportWebhookNotification>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
        var controller = CreateAuditController(CreateActor(userId), service.Object, notifier.Object, progress.Object);
        controller.Response.Body = new MemoryStream();
        var request = new AuditExportRequest { WebhookUrl = "https://hooks.example.com/audit" };

        var result = format == "csv"
            ? await controller.ExportAuditLogs(request)
            : await controller.ExportAuditLogsJson(request);

        Assert.IsType<EmptyResult>(result);
        notifier.Verify(value => value.NotifyAsync(request.WebhookUrl,
            It.Is<AuditExportWebhookNotification>(notification => notification.Status == "completed"
                && notification.Format == format && notification.TotalRecords == 11
                && notification.RecordsWritten == 11 && notification.ErrorCode == null),
            CancellationToken.None), Times.Once);
        progress.Verify(value => value.GetAsync(It.IsAny<Guid>(), userId, CancellationToken.None), Times.Once);
    }

    private static Mock<IAuditService> CreateWebhookExportService()
    {
        var service = new Mock<IAuditService>();
        service.Setup(value => value.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>())).ReturnsAsync(11);
        service.Setup(value => value.StreamAuditLogsAsync(It.IsAny<AuditLogQuery>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new[] { new AuditLog { ActionType = "Update", ResourceType = "Test" } }));
        return service;
    }

    [Fact]
    public async Task ExportAuditLogsJson_ShouldStreamNestedSchemaWithPaginationAndFilters()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var auditLog = CreateAuditLog(userId, tenantId);
        auditLog.ActionType = "SignIn";
        auditLog.Metadata = "{\"requestId\":\"req-42\"}";
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAdminActionAsync(userId, "ExportAuditLogs", "Admin exported audit logs", It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.Is<AuditLogQuery>(query => query.Skip == 5 && query.Take == 5)))
            .ReturnsAsync(11);
        auditService
            .Setup(service => service.StreamAuditLogsAsync(
                It.Is<AuditLogQuery>(query => query.UserId == userId
                    && query.ActionType == "SignIn"
                    && query.RiskLevel == AuditRiskLevel.High
                    && query.Skip == 5
                    && query.Take == 5),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([auditLog]));
        var webhookNotifier = new Mock<IAuditExportWebhookNotifier>();
        webhookNotifier.Setup(service => service.ValidateWebhookUrl(It.IsAny<string?>())).Returns((string?)null);
        webhookNotifier.Setup(service => service.NotifyAsync(
                It.IsAny<string?>(),
                It.IsAny<AuditExportWebhookNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = CreateAuditController(CreateActor(userId), auditService.Object, webhookNotifier.Object);
        var responseBody = new MemoryStream();
        controller.HttpContext.Response.Body = responseBody;

        var result = await controller.ExportAuditLogsJson(new AuditExportRequest
        {
            UserId = userId,
            ActionType = "SignIn",
            RiskLevel = AuditRiskLevel.High,
            PageNumber = 2,
            PageSize = 5,
            WebhookUrl = "https://hooks.example.com/audit"
        });

        result.Should().BeOfType<EmptyResult>();
        controller.HttpContext.Response.ContentType.Should().Be("application/json; charset=utf-8");

        using var json = JsonDocument.Parse(responseBody.ToArray());
        json.RootElement.GetProperty("schemaVersion").GetString().Should().Be("1.0");
        json.RootElement.GetProperty("pagination").GetProperty("pageNumber").GetInt32().Should().Be(2);
        json.RootElement.GetProperty("pagination").GetProperty("pageSize").GetInt32().Should().Be(5);
        json.RootElement.GetProperty("pagination").GetProperty("totalRecords").GetInt32().Should().Be(11);
        json.RootElement.GetProperty("pagination").GetProperty("totalPages").GetInt32().Should().Be(3);
        var firstRecord = json.RootElement.GetProperty("records")[0];
        firstRecord.GetProperty("event").GetProperty("actionType").GetString().Should().Be("SignIn");
        firstRecord.GetProperty("actor").GetProperty("userId").GetGuid().Should().Be(userId);
        firstRecord.GetProperty("actor").GetProperty("tenantId").GetGuid().Should().Be(tenantId);
        firstRecord.GetProperty("resource").GetProperty("resourceType").GetString().Should().Be("Lease");
        firstRecord.GetProperty("metadata").GetProperty("requestId").GetString().Should().Be("req-42");
        webhookNotifier.Verify(service => service.NotifyAsync(
                "https://hooks.example.com/audit",
                It.Is<AuditExportWebhookNotification>(notification =>
                    notification.Type == "audit.export.completed" &&
                    notification.Format == "json" &&
                    notification.Status == "completed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("42", "value", "42")]
    [InlineData("legacy metadata", "raw", "legacy metadata")]
    public void AuditJsonExportMapper_ShouldKeepMetadataCompatibleWithObjectSchema(
        string metadata,
        string propertyName,
        string expectedValue)
    {
        var auditLog = CreateAuditLog(Guid.NewGuid(), Guid.NewGuid());
        auditLog.Metadata = metadata;
        var record = AuditJsonExportMapper.Map(auditLog);

        record.Metadata.Should().NotBeNull();
        record.Metadata!.Value.ValueKind.Should().Be(JsonValueKind.Object);
        record.Metadata.Value.GetProperty(propertyName).ToString().Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("json")]
    public async Task Exports_ShouldWriteBeforeEnumerationCompletesWithoutBufferingTheDataset(string format)
    {
        var recordCount = format == "csv" ? 100_000 : 1000;
        var userId = Guid.NewGuid();
        var sink = new ExportRecordingStream();
        var enumerated = 0;
        var service = new Mock<IAuditService>();
        service.Setup(value => value.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>())).ReturnsAsync(recordCount);
        service.Setup(value => value.StreamAuditLogsAsync(It.IsAny<AuditLogQuery>(), It.IsAny<CancellationToken>()))
            .Returns((AuditLogQuery _, CancellationToken token) => Records(token));
        var controller = CreateAuditController(CreateActor(userId), service.Object);
        controller.Response.Body = sink;

        var result = format == "csv"
            ? await controller.ExportAuditLogs(new AuditExportRequest { Columns = ["Description"] })
            : await controller.ExportAuditLogsJson(new AuditExportRequest { PageNumber = 1, PageSize = 1000 });

        Assert.IsType<EmptyResult>(result);
        Assert.Equal(recordCount, enumerated);
        Assert.True(sink.BytesWritten > recordCount * 128L);
        service.Verify(value => value.GetAuditLogsAsync(It.IsAny<AuditLogQuery>()), Times.Never);

        async IAsyncEnumerable<AuditLog> Records([EnumeratorCancellation] CancellationToken token)
        {
            for (var index = 0; index < recordCount; index++)
            {
                token.ThrowIfCancellationRequested();
                if (index == 512) { Assert.True(sink.BytesWritten > 0, "The export buffered records before writing."); }
                enumerated++;
                yield return new AuditLog { ActionType = "Update", ResourceType = "Test", Description = new string('é', 128) };
                if (index % 256 == 0) { await Task.Yield(); }
            }
        }
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("json")]
    public async Task Exports_ShouldStopEnumerationAndNotifyCancellationWhenTheClientDisconnects(string format)
    {
        using var cancellation = new CancellationTokenSource();
        var userId = Guid.NewGuid();
        var enumerated = 0;
        var sink = new ExportRecordingStream(cancellation.Cancel);
        var service = new Mock<IAuditService>();
        service.Setup(value => value.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>())).ReturnsAsync(100_000);
        service.Setup(value => value.StreamAuditLogsAsync(It.IsAny<AuditLogQuery>(), It.IsAny<CancellationToken>()))
            .Returns((AuditLogQuery _, CancellationToken token) => Records(token));
        var notifier = new Mock<IAuditExportWebhookNotifier>();
        notifier.Setup(value => value.ValidateWebhookUrl(It.IsAny<string?>())).Returns((string?)null);
        var controller = CreateAuditController(CreateActor(userId), service.Object, notifier.Object);
        controller.Response.Body = sink;
        controller.HttpContext.RequestAborted = cancellation.Token;
        var request = new AuditExportRequest { Columns = ["Description"], WebhookUrl = "https://hooks.example.com/audit" };

        var result = format == "csv"
            ? await controller.ExportAuditLogs(request)
            : await controller.ExportAuditLogsJson(request);

        Assert.IsType<EmptyResult>(result);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.InRange(enumerated, 1, 1000);
        notifier.Verify(value => value.NotifyAsync(
            request.WebhookUrl,
            It.Is<AuditExportWebhookNotification>(notification => notification.Status == "cancelled" && notification.Format == format),
            It.IsAny<CancellationToken>()), Times.Once);

        async IAsyncEnumerable<AuditLog> Records([EnumeratorCancellation] CancellationToken token)
        {
            for (var index = 0; index < 100_000; index++)
            {
                token.ThrowIfCancellationRequested();
                enumerated++;
                yield return new AuditLog { ActionType = "Update", ResourceType = "Test", Description = new string('x', 900) };
                await Task.Yield();
            }
        }
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("json")]
    public async Task Exports_ShouldAbortStartedStreamsAndRecordSafeFailureStatus(string format)
    {
        var userId = Guid.NewGuid();
        var responseFeature = new ExportResponseFeature();
        var lifetime = new ExportRequestLifetimeFeature();
        var service = new Mock<IAuditService>();
        service.Setup(value => value.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>())).ReturnsAsync(1000);
        service.Setup(value => value.StreamAuditLogsAsync(It.IsAny<AuditLogQuery>(), It.IsAny<CancellationToken>()))
            .Returns(FailingRecords());
        var progress = new Mock<IAuditExportProgressTracker>();
        progress.Setup(value => value.GetAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditExportProgressResponse(Guid.NewGuid(), "InProgress", 1000, 256, 25.6,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));
        var notifier = new Mock<IAuditExportWebhookNotifier>();
        notifier.Setup(value => value.ValidateWebhookUrl(It.IsAny<string?>())).Returns((string?)null);
        var controller = CreateAuditController(CreateActor(userId), service.Object, notifier.Object, progress.Object);
        controller.HttpContext.Features.Set<IHttpResponseFeature>(responseFeature);
        controller.HttpContext.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        controller.Response.Body = new ExportRecordingStream(() => responseFeature.Started = true);
        var request = new AuditExportRequest { Columns = ["Description"], WebhookUrl = "https://hooks.example.com/audit" };

        var result = format == "csv"
            ? await controller.ExportAuditLogs(request)
            : await controller.ExportAuditLogsJson(request);

        Assert.IsType<EmptyResult>(result);
        Assert.True(responseFeature.HasStarted);
        Assert.True(lifetime.Aborted);
        progress.Verify(value => value.ReportAsync(
            It.IsAny<Guid>(), userId, 256, AuditExportProgressStatus.Failed, "The export could not be completed.",
            It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(value => value.NotifyAsync(request.WebhookUrl,
            It.Is<AuditExportWebhookNotification>(notification => notification.Status == "failed"
                && notification.Format == format && notification.ErrorCode == "audit_export_failed"),
            It.IsAny<CancellationToken>()), Times.Once);

        static async IAsyncEnumerable<AuditLog> FailingRecords()
        {
            for (var index = 0; index < 600; index++)
            {
                yield return new AuditLog { ActionType = "Update", ResourceType = "Test", Description = new string('x', 900) };
            }
            await Task.Yield();
            throw new InvalidOperationException("private database connection details");
        }
    }

    private sealed class ExportResponseFeature : HttpResponseFeature
    {
        public bool Started { get; set; }
        public override bool HasStarted => Started;
    }

    private sealed class ExportRequestLifetimeFeature : IHttpRequestLifetimeFeature
    {
        public CancellationToken RequestAborted { get; set; }
        public bool Aborted { get; private set; }
        public void Abort() => Aborted = true;
    }

    private sealed class ExportRecordingStream(Action? onWrite = null) : Stream
    {
        public long BytesWritten { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;
        public override long Position { get => BytesWritten; set => throw new NotSupportedException(); }
        public override void Flush()
        {
            // Writes are counted immediately; the stream has no buffered data to flush.
        }
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            _ = buffer.AsSpan(offset, count);
            throw new NotSupportedException("The recording stream only accepts writes.");
        }
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException($"Seeking to {offset} relative to {origin} is not supported.");
        public override void SetLength(long value) =>
            throw new NotSupportedException($"Setting the recording stream length to {value} is not supported.");
        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            _ = buffer.AsSpan(offset, count);
            BytesWritten += count;
            onWrite?.Invoke();
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BytesWritten += buffer.Length;
            onWrite?.Invoke();
            return ValueTask.CompletedTask;
        }
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            yield return value;
            await Task.Yield();
        }
    }

    private static AuditController CreateAuditController(
        ActorContext actorContext,
        IAuditService? auditService = null,
        IAuditExportWebhookNotifier? exportWebhookNotifier = null,
        IAuditExportProgressTracker? exportProgressTracker = null)
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(accessor => accessor.ActorContext).Returns(actorContext);

        return new AuditController(
            auditService ?? Mock.Of<IAuditService>(),
            actorAccessor.Object,
            NullLogger<AuditController>.Instance,
            new CommandHandlerSender(auditService ?? Mock.Of<IAuditService>(), Mock.Of<ISecurityAuditAggregator>(), actorAccessor.Object),
            exportProgressTracker ?? CreateProgressTracker(),
            exportWebhookNotifier ?? CreateWebhookNotifier(),
            Mock.Of<IScheduledAuditExportService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static IAuditExportWebhookNotifier CreateWebhookNotifier()
    {
        var notifier = new Mock<IAuditExportWebhookNotifier>();
        notifier.Setup(service => service.ValidateWebhookUrl(It.IsAny<string?>())).Returns((string?)null);
        notifier.Setup(service => service.NotifyAsync(
                It.IsAny<string?>(),
                It.IsAny<AuditExportWebhookNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return notifier.Object;
    }

    private static IAuditExportProgressTracker CreateProgressTracker()
    {
        var tracker = new Mock<IAuditExportProgressTracker>();
        tracker.Setup(service => service.BeginAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        tracker.Setup(service => service.ReportAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<AuditExportProgressStatus>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        tracker.Setup(service => service.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditExportProgressResponse?)null);
        return tracker.Object;
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
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ITamperEvidentAuditService) && descriptor.ImplementationType == typeof(TamperEvidentAuditService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ICryptographicSigningService) && descriptor.ImplementationType == typeof(EcdsaCryptographicSigningService));
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
