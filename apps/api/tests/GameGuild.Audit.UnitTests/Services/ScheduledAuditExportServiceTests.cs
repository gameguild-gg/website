using System.ComponentModel.DataAnnotations;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class ScheduledAuditExportServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsActionFilterAndOrderedCsvColumns()
    {
        ScheduledAuditExport? persisted = null;
        var repository = new Mock<IScheduledAuditExportRepository>();
        repository
            .Setup(item => item.AddAsync(It.IsAny<ScheduledAuditExport>(), It.IsAny<CancellationToken>()))
            .Callback<ScheduledAuditExport, CancellationToken>((export, _) => persisted = export)
            .Returns(Task.CompletedTask);
        var auditService = new Mock<IAuditService>();
        var service = new ScheduledAuditExportService(
            repository.Object,
            auditService.Object,
            new AuditExportCronSchedule(),
            Mock.Of<IAuditScheduledExportStorage>(),
            Options.Create(new AuditScheduledExportOptions()),
            Mock.Of<ILogger<ScheduledAuditExportService>>());
        var request = new CreateScheduledAuditExportRequest
        {
            TenantId = Guid.NewGuid(),
            JobName = "Daily sign-ins",
            CronExpression = "0 8 * * *",
            Timezone = "UTC",
            ExportFormat = ExportFormat.Csv,
            ActionType = "UserLogin",
            Columns = [nameof(AuditLog.CreatedAt), nameof(AuditLog.ActionType)]
        };

        var result = await service.CreateAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal("UserLogin", Assert.Single(persisted.IncludeEventTypes));
        Assert.Equal(new[] { nameof(AuditLog.CreatedAt), nameof(AuditLog.ActionType) }, persisted.CsvColumns);
        Assert.Equal("UTC", persisted.Timezone);
        Assert.Equal(request.TenantId, result.TenantId);
        repository.Verify(item => item.AddAsync(It.IsAny<ScheduledAuditExport>(), It.IsAny<CancellationToken>()), Times.Once);
        auditService.Verify(item => item.LogTenantOperationAsync(
            "CreateScheduledAuditExport",
            request.TenantId,
            It.IsAny<Guid?>(),
            It.IsAny<string>(),
            It.IsAny<object>(),
            true), Times.Once);
    }

    [Fact]
    public void CreateRequest_RejectsUnsupportedCsvColumns()
    {
        var request = ValidRequest();
        request.Columns = ["SecretColumn"];

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(request.Columns)));
    }

    [Fact]
    public void CreateRequest_RejectsColumnsForJsonExports()
    {
        var request = ValidRequest();
        request.ExportFormat = ExportFormat.Json;
        request.Columns = [nameof(AuditLog.ActionType)];

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(request.Columns)));
    }

    [Fact]
    public async Task ExpireExpiredFilesAsync_DeletesStoredFileBeforeMarkingHistoryUnavailable()
    {
        var tenantId = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        var repository = new Mock<IScheduledAuditExportRepository>();
        repository
            .Setup(item => item.GetExpiredFilesAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExpiredAuditExportFile(historyId, tenantId, "storage-reference")]);
        var storage = new Mock<IAuditScheduledExportStorage>();
        storage
            .Setup(item => item.DeleteAsync(tenantId, "storage-reference", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new ScheduledAuditExportService(
            repository.Object,
            Mock.Of<IAuditService>(),
            new AuditExportCronSchedule(),
            storage.Object,
            Options.Create(new AuditScheduledExportOptions()),
            Mock.Of<ILogger<ScheduledAuditExportService>>());

        var expired = await service.ExpireExpiredFilesAsync(CancellationToken.None);

        Assert.Equal(1, expired);
        storage.Verify(item => item.DeleteAsync(tenantId, "storage-reference", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(item => item.MarkFileExpiredAsync(historyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireExpiredFilesAsync_KeepsHistoryDownloadableWhenStorageDeletionFails()
    {
        var tenantId = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        var repository = new Mock<IScheduledAuditExportRepository>();
        repository
            .Setup(item => item.GetExpiredFilesAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExpiredAuditExportFile(historyId, tenantId, "storage-reference")]);
        var storage = new Mock<IAuditScheduledExportStorage>();
        storage
            .Setup(item => item.DeleteAsync(tenantId, "storage-reference", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Storage unavailable"));
        var service = new ScheduledAuditExportService(
            repository.Object,
            Mock.Of<IAuditService>(),
            new AuditExportCronSchedule(),
            storage.Object,
            Options.Create(new AuditScheduledExportOptions()),
            Mock.Of<ILogger<ScheduledAuditExportService>>());

        var expired = await service.ExpireExpiredFilesAsync(CancellationToken.None);

        Assert.Equal(0, expired);
        repository.Verify(item => item.MarkFileExpiredAsync(historyId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDueAsync_RecoversStaleClaimsWithConfiguredThresholdBeforeClaiming()
    {
        var options = new AuditScheduledExportOptions { StaleClaimThreshold = TimeSpan.FromMinutes(45) };
        var repository = new Mock<IScheduledAuditExportRepository>();
        repository
            .Setup(item => item.GetDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScheduledAuditExport>());
        repository
            .Setup(item => item.RecoverStaleClaimsAsync(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var service = new ScheduledAuditExportService(
            repository.Object,
            Mock.Of<IAuditService>(),
            new AuditExportCronSchedule(),
            Mock.Of<IAuditScheduledExportStorage>(),
            Options.Create(options),
            Mock.Of<ILogger<ScheduledAuditExportService>>());

        var processed = await service.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(0, processed);
        repository.Verify(item => item.RecoverStaleClaimsAsync(
            It.IsAny<DateTime>(),
            TimeSpan.FromMinutes(45),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CreateScheduledAuditExportRequest ValidRequest() => new()
    {
        TenantId = Guid.NewGuid(),
        JobName = "Daily audit",
        CronExpression = "0 8 * * *",
        Timezone = "UTC",
        ExportFormat = ExportFormat.Csv
    };

    private static List<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
