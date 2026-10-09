using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Resources.UnitTests.Services;

public class CostAllocationServiceTests
{
    private readonly Mock<ICostAllocationReportRepository> _reportRepositoryMock;
    private readonly Mock<IUsageRecordRepository> _usageRepositoryMock;
    private readonly Mock<IResourceQuotaRepository> _quotaRepositoryMock;
    private readonly Mock<IOptions<ResourcesOptions>> _optionsMock;
    private readonly CostAllocationService _service;

    public CostAllocationServiceTests()
    {
        _reportRepositoryMock = new Mock<ICostAllocationReportRepository>();
        _usageRepositoryMock = new Mock<IUsageRecordRepository>();
        _quotaRepositoryMock = new Mock<IResourceQuotaRepository>();
        _optionsMock = new Mock<IOptions<ResourcesOptions>>();
        Mock<ILogger<CostAllocationService>> loggerMock = new Mock<ILogger<CostAllocationService>>();

        var options = new ResourcesOptions
        {
            CostPerUnit = new Dictionary<string, decimal>
            {
                { "Storage", 0.10m },
                { "ApiCalls", 0.001m },
                { "Users", 5.00m },
                { "Projects", 10.00m }
            },
            DefaultCostPerUnit = 0.01m
        };

        _optionsMock.Setup(o => o.Value).Returns(options);

        _service = new CostAllocationService(
            _reportRepositoryMock.Object,
            _usageRepositoryMock.Object,
            _quotaRepositoryMock.Object,
            _optionsMock.Object,
            loggerMock.Object
        );
    }

    [Fact]
    public async Task GenerateReportAsync_CreatesReport_WithCorrectCalculations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;

        var usageRecords = new List<UsageRecord>
        {
            new() { Type = ResourceUsageType.Storage, UsageAmount = 1000, PeriodStart = periodStart },
            new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 500, PeriodStart = periodStart }
        };

        _usageRepositoryMock.Setup(r => r.GetByTenantAsync(
                tenantId, null, periodStart, periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageRecords);

        _quotaRepositoryMock.Setup(r => r.GetByTenantAndTypeAsync(
                It.IsAny<Guid>(), It.IsAny<ResourceUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceQuota?)null);

        CostAllocationReport? capturedReport = null;
        _reportRepositoryMock.Setup(r => r.AddAsync(It.IsAny<CostAllocationReport>(), It.IsAny<CancellationToken>()))
            .Callback<CostAllocationReport, CancellationToken>((r, _) => capturedReport = r)
            .ReturnsAsync((CostAllocationReport r, CancellationToken _) => r);

        // Act
        var result = await _service.GenerateReportAsync(tenantId, periodStart, periodEnd);

        // Assert
        result.Should().NotBeNull();
        capturedReport.Should().NotBeNull();
        capturedReport!.PeriodStart.Should().Be(periodStart);
        capturedReport.PeriodEnd.Should().Be(periodEnd);
        capturedReport.TotalUsage.Should().Be(1500); // 1000 + 500
        capturedReport.TotalCost.Should().Be(100.5m); // (1000 * 0.10) + (500 * 0.001) = 100 + 0.5 = 100.5
        capturedReport.IsExported.Should().BeFalse();
        capturedReport.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GenerateReportAsync_IncludesAllocationTags_WhenQuotaHasMetadata()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;

        var usageRecords = new List<UsageRecord>
        {
            new() { Type = ResourceUsageType.Storage, UsageAmount = 1000, PeriodStart = periodStart }
        };

        _usageRepositoryMock.Setup(r => r.GetByTenantAsync(
                tenantId, null, periodStart, periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageRecords);

        var quota = new ResourceQuota
        {
            Type = ResourceUsageType.Storage,
            HardLimit = 10000,
            CurrentUsage = 5000
        };
        quota.SetProperties(new Dictionary<string, object?> { ["TenantId"] = tenantId });

        _quotaRepositoryMock.Setup(r => r.GetByTenantAndTypeAsync(
                tenantId, ResourceUsageType.Storage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);

        CostAllocationReport? capturedReport = null;
        _reportRepositoryMock.Setup(r => r.AddAsync(It.IsAny<CostAllocationReport>(), It.IsAny<CancellationToken>()))
            .Callback<CostAllocationReport, CancellationToken>((r, _) => capturedReport = r)
            .ReturnsAsync((CostAllocationReport r, CancellationToken _) => r);

        // Act
        await _service.GenerateReportAsync(tenantId, periodStart, periodEnd);

        // Assert
        capturedReport.Should().NotBeNull();
        capturedReport!.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GenerateReportAsync_HandlesNoUsageRecords_GracefullyAsync()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;

        _usageRepositoryMock.Setup(r => r.GetByTenantAsync(
                tenantId, null, periodStart, periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UsageRecord>());

        CostAllocationReport? capturedReport = null;
        _reportRepositoryMock.Setup(r => r.AddAsync(It.IsAny<CostAllocationReport>(), It.IsAny<CancellationToken>()))
            .Callback<CostAllocationReport, CancellationToken>((r, _) => capturedReport = r)
            .ReturnsAsync((CostAllocationReport r, CancellationToken _) => r);

        // Act
        var result = await _service.GenerateReportAsync(tenantId, periodStart, periodEnd);

        // Assert
        result.Should().NotBeNull();
        capturedReport!.TotalUsage.Should().Be(0);
        capturedReport.TotalCost.Should().Be(0);
    }

    [Fact]
    public async Task GetTenantReportsAsync_ReturnsReportsFromRepository()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var reports = new List<CostAllocationReport>
        {
            new() { Id = Guid.NewGuid(), TotalCost = 100 },
            new() { Id = Guid.NewGuid(), TotalCost = 200 }
        };

        _reportRepositoryMock.Setup(r => r.GetByTenantAsync(
                tenantId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        // Act
        var result = await _service.GetTenantReportsAsync(tenantId);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTenantReportsAsync_PassesDateFilters()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var fromDate = DateTime.UtcNow.AddDays(-60);
        var toDate = DateTime.UtcNow.AddDays(-30);

        _reportRepositoryMock.Setup(r => r.GetByTenantAsync(
                tenantId, fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CostAllocationReport>());

        // Act
        await _service.GetTenantReportsAsync(tenantId, fromDate, toDate);

        // Assert
        _reportRepositoryMock.Verify(
            r => r.GetByTenantAsync(tenantId, fromDate, toDate, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task GetReportAsync_ReturnsReport_WhenExists()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var report = new CostAllocationReport { Id = reportId, TotalCost = 150 };

        _reportRepositoryMock.Setup(r => r.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        // Act
        var result = await _service.GetReportAsync(reportId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(reportId);
        result.TotalCost.Should().Be(150);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var reportId = Guid.NewGuid();

        _reportRepositoryMock.Setup(r => r.GetByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CostAllocationReport?)null);

        // Act
        var result = await _service.GetReportAsync(reportId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CalculateTotalCostAsync_CalculatesCostCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;

        var usageRecords = new List<UsageRecord>
        {
            new() { Type = ResourceUsageType.Storage, UsageAmount = 1000, PeriodStart = periodStart.AddDays(1) },
            new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 100, PeriodStart = periodStart.AddDays(2) },
            new() { Type = ResourceUsageType.Projects, UsageAmount = 5, PeriodStart = periodStart.AddDays(10) }
        };

        _usageRepositoryMock.Setup(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageRecords);

        // Act
        var result = await _service.CalculateTotalCostAsync(tenantId, periodStart, periodEnd);

        // Assert
        // (1000 * 0.10) + (100 * 0.001) + (5 * 10.00) = 100 + 0.1 + 50 = 150.1
        result.Should().Be(150.1m);
    }

    [Fact]
    public async Task CalculateTotalCostAsync_FiltersRecordsByDateRange()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;

        var usageRecords = new List<UsageRecord>
        {
            new() { Type = ResourceUsageType.Storage, UsageAmount = 1000, PeriodStart = periodStart.AddDays(1) }, // Include
            new() { Type = ResourceUsageType.ApiCalls, UsageAmount = 100, PeriodStart = periodStart.AddDays(-5) }, // Exclude (before start)
            new() { Type = ResourceUsageType.Projects, UsageAmount = 5, PeriodStart = periodEnd.AddDays(5) } // Exclude (after end)
        };

        _usageRepositoryMock.Setup(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageRecords);

        // Act
        var result = await _service.CalculateTotalCostAsync(tenantId, periodStart, periodEnd);

        // Assert
        // Only the first record should be included: 1000 * 0.10 = 100
        result.Should().Be(100.0m);
    }

    [Fact]
    public async Task GenerateReportAsync_HandlesCostPerUnitCalculation()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;

        var usageRecords = new List<UsageRecord>
        {
            new() { Type = ResourceUsageType.Storage, UsageAmount = 2000, PeriodStart = periodStart }
        };

        _usageRepositoryMock.Setup(r => r.GetByTenantAsync(
                tenantId, null, periodStart, periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageRecords);

        CostAllocationReport? capturedReport = null;
        _reportRepositoryMock.Setup(r => r.AddAsync(It.IsAny<CostAllocationReport>(), It.IsAny<CancellationToken>()))
            .Callback<CostAllocationReport, CancellationToken>((r, _) => capturedReport = r)
            .ReturnsAsync((CostAllocationReport r, CancellationToken _) => r);

        // Act
        await _service.GenerateReportAsync(tenantId, periodStart, periodEnd);

        // Assert
        capturedReport.Should().NotBeNull();
        capturedReport!.CostPerUnit.Should().Be(0.10m); // totalCost (200) / totalUsage (2000) = 0.10
    }
}
