using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Resources.UnitTests.Services;

public class SlaImpactAnalysisServiceTests
{
    private readonly Mock<ISlaImpactAnalysisRepository> _analysisRepositoryMock;
    private readonly Mock<IResourceQuotaRepository> _quotaRepositoryMock;
    private readonly Mock<ISlaIncidentEscalationService> _escalationServiceMock;
    private readonly Mock<ILogger<SlaImpactAnalysisService>> _loggerMock;
    private readonly SlaImpactAnalysisService _service;

    public SlaImpactAnalysisServiceTests()
    {
        _analysisRepositoryMock = new Mock<ISlaImpactAnalysisRepository>();
        _quotaRepositoryMock = new Mock<IResourceQuotaRepository>();
        _escalationServiceMock = new Mock<ISlaIncidentEscalationService>();
        Mock<IIncidentTicketProvider> incidentTicketProviderMock = new Mock<IIncidentTicketProvider>();
        _loggerMock = new Mock<ILogger<SlaImpactAnalysisService>>();

        _service = new SlaImpactAnalysisService(
            _analysisRepositoryMock.Object,
            _quotaRepositoryMock.Object,
            _escalationServiceMock.Object,
            incidentTicketProviderMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task RecordViolationAsync_CreatesViolation_WithCorrectProperties()
    {
        // Arrange
        var quotaId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var quota = new ResourceQuota
        {
            Id = quotaId,
            Type = ResourceUsageType.Storage,
            HardLimit = 1000,
            CurrentUsage = 1200
        };
        quota.SetProperties(new Dictionary<string, object?> { ["TenantId"] = tenantId });

        _quotaRepositoryMock.Setup(r => r.GetByIdAsync(quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);

        SlaImpactAnalysis? capturedViolation = null;
        _analysisRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .Callback<SlaImpactAnalysis, CancellationToken>((v, _) => capturedViolation = v)
            .ReturnsAsync((SlaImpactAnalysis v, CancellationToken _) => v);

        // Act
        var result = await _service.RecordViolationAsync(
            quotaId,
            SlaViolationType.QuotaExceeded,
            SlaViolationSeverity.Medium,
            1000,
            1200,
            userId
        );

        // Assert
        result.Should().NotBeNull();
        capturedViolation.Should().NotBeNull();
        capturedViolation!.ResourceQuotaId.Should().Be(quotaId);
        capturedViolation.UserId.Should().Be(userId);
        capturedViolation.ViolationType.Should().Be(SlaViolationType.QuotaExceeded);
        capturedViolation.Severity.Should().Be(SlaViolationSeverity.Medium);
        capturedViolation.ExpectedValue.Should().Be(1000);
        capturedViolation.ActualValue.Should().Be(1200);
        capturedViolation.IsResolved.Should().BeFalse();
        capturedViolation.IncidentCreated.Should().BeFalse();
        capturedViolation.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task RecordViolationAsync_ThrowsException_WhenQuotaNotFound()
    {
        // Arrange
        var quotaId = Guid.NewGuid();

        _quotaRepositoryMock.Setup(r => r.GetByIdAsync(quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceQuota?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RecordViolationAsync(
                quotaId,
                SlaViolationType.QuotaExceeded,
                SlaViolationSeverity.Medium,
                1000,
                1200
            )
        );
    }

    [Fact]
    public async Task RecordViolationAsync_SetsRequiresEscalation_ForHighSeverity()
    {
        // Arrange
        var quotaId = Guid.NewGuid();
        var quota = new ResourceQuota { Id = quotaId, Type = ResourceUsageType.Storage };
        quota.SetProperties(new Dictionary<string, object?> { ["TenantId"] = Guid.NewGuid() });

        _quotaRepositoryMock.Setup(r => r.GetByIdAsync(quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);

        SlaImpactAnalysis? capturedViolation = null;
        _analysisRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .Callback<SlaImpactAnalysis, CancellationToken>((v, _) => capturedViolation = v)
            .ReturnsAsync((SlaImpactAnalysis v, CancellationToken _) => v);

        // Act
        await _service.RecordViolationAsync(
            quotaId,
            SlaViolationType.QuotaExceeded,
            SlaViolationSeverity.High,
            1000,
            1200
        );

        // Assert
        capturedViolation.Should().NotBeNull();
        capturedViolation!.RequiresEscalation.Should().BeTrue();
    }

    [Fact]
    public async Task RecordViolationAsync_AutoEscalates_ForHighSeverity()
    {
        // Arrange
        var quotaId = Guid.NewGuid();
        var quota = new ResourceQuota { Id = quotaId, Type = ResourceUsageType.Storage };
        quota.SetProperties(new Dictionary<string, object?> { ["TenantId"] = Guid.NewGuid() });

        _quotaRepositoryMock.Setup(r => r.GetByIdAsync(quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);

        var violation = new SlaImpactAnalysis { Id = Guid.NewGuid() };
        _analysisRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(violation);

        var escalationResult = SlaEscalationResult.Success("INC-12345", [Guid.NewGuid()]);

        _escalationServiceMock.Setup(s => s.EscalateViolationAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(escalationResult);

        // Act
        await _service.RecordViolationAsync(
            quotaId,
            SlaViolationType.QuotaExceeded,
            SlaViolationSeverity.High,
            1000,
            1200
        );

        // Assert
        _escalationServiceMock.Verify(
            s => s.EscalateViolationAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task RecordViolationAsync_DoesNotEscalate_ForLowSeverity()
    {
        // Arrange
        var quotaId = Guid.NewGuid();
        var quota = new ResourceQuota { Id = quotaId, Type = ResourceUsageType.Storage };
        quota.SetProperties(new Dictionary<string, object?> { ["TenantId"] = Guid.NewGuid() });

        _quotaRepositoryMock.Setup(r => r.GetByIdAsync(quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);

        _analysisRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlaImpactAnalysis { Id = Guid.NewGuid() });

        // Act
        await _service.RecordViolationAsync(
            quotaId,
            SlaViolationType.QuotaExceeded,
            SlaViolationSeverity.Low,
            1000,
            1200
        );

        // Assert
        _escalationServiceMock.Verify(
            s => s.EscalateViolationAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task RecordViolationAsync_ContinuesWhenEscalationFails()
    {
        // Arrange
        var quotaId = Guid.NewGuid();
        var quota = new ResourceQuota { Id = quotaId, Type = ResourceUsageType.Storage };
        quota.SetProperties(new Dictionary<string, object?> { ["TenantId"] = Guid.NewGuid() });

        _quotaRepositoryMock.Setup(r => r.GetByIdAsync(quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);

        var violation = new SlaImpactAnalysis { Id = Guid.NewGuid() };
        _analysisRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(violation);

        _escalationServiceMock.Setup(s => s.EscalateViolationAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Escalation service unavailable"));

        // Act & Assert (should not throw)
        var result = await _service.RecordViolationAsync(
            quotaId,
            SlaViolationType.QuotaExceeded,
            SlaViolationSeverity.Critical,
            1000,
            1200
        );

        result.Should().NotBeNull();
        result.Id.Should().Be(violation.Id);
    }

    [Fact]
    public async Task GetViolationAsync_ReturnsViolation_WhenExists()
    {
        // Arrange
        var violationId = Guid.NewGuid();
        var violation = new SlaImpactAnalysis { Id = violationId };

        _analysisRepositoryMock.Setup(r => r.GetByIdAsync(violationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(violation);

        // Act
        var result = await _service.GetViolationAsync(violationId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(violationId);
    }

    [Fact]
    public async Task GetViolationAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var violationId = Guid.NewGuid();

        _analysisRepositoryMock.Setup(r => r.GetByIdAsync(violationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SlaImpactAnalysis?)null);

        // Act
        var result = await _service.GetViolationAsync(violationId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTenantViolationsAsync_ReturnsAllViolations_WhenNoFilters()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var violations = new List<SlaImpactAnalysis>
        {
            new() { Id = Guid.NewGuid(), ViolationStartTime = DateTime.UtcNow.AddDays(-1), Severity = SlaViolationSeverity.Low },
            new() { Id = Guid.NewGuid(), ViolationStartTime = DateTime.UtcNow.AddDays(-2), Severity = SlaViolationSeverity.High }
        };

        _analysisRepositoryMock.Setup(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(violations);

        // Act
        var result = await _service.GetTenantViolationsAsync(tenantId);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTenantViolationsAsync_UsesDateRange_WhenBothDatesProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var fromDate = DateTime.UtcNow.AddDays(-7);
        var toDate = DateTime.UtcNow;

        var violations = new List<SlaImpactAnalysis>
        {
            new() { Id = Guid.NewGuid(), ViolationStartTime = DateTime.UtcNow.AddDays(-3) }
        };

        _analysisRepositoryMock.Setup(r => r.GetByDateRangeAsync(tenantId, fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(violations);

        // Act
        var result = await _service.GetTenantViolationsAsync(tenantId, fromDate, toDate);

        // Assert
        _analysisRepositoryMock.Verify(
            r => r.GetByDateRangeAsync(tenantId, fromDate, toDate, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task GetTenantViolationsAsync_FiltersBySeverity_WhenMinSeverityProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var violations = new List<SlaImpactAnalysis>
        {
            new() { Id = Guid.NewGuid(), Severity = SlaViolationSeverity.Low },
            new() { Id = Guid.NewGuid(), Severity = SlaViolationSeverity.High },
            new() { Id = Guid.NewGuid(), Severity = SlaViolationSeverity.Critical }
        };

        _analysisRepositoryMock.Setup(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(violations);

        // Act
        var result = await _service.GetTenantViolationsAsync(tenantId, minSeverity: SlaViolationSeverity.High);

        // Assert
        var list = result.ToList();
        list.Should().HaveCount(2);
        list.Should().AllSatisfy(v => v.Severity.Should().BeOneOf(SlaViolationSeverity.High, SlaViolationSeverity.Critical));
    }

    [Fact]
    public async Task GetTenantViolationsAsync_FiltersFromDate_WhenOnlyFromDateProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var fromDate = DateTime.UtcNow.AddDays(-7);

        var violations = new List<SlaImpactAnalysis>
        {
            new() { Id = Guid.NewGuid(), ViolationStartTime = DateTime.UtcNow.AddDays(-10) },
            new() { Id = Guid.NewGuid(), ViolationStartTime = DateTime.UtcNow.AddDays(-5) },
            new() { Id = Guid.NewGuid(), ViolationStartTime = DateTime.UtcNow.AddDays(-2) }
        };

        _analysisRepositoryMock.Setup(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(violations);

        // Act
        var result = await _service.GetTenantViolationsAsync(tenantId, fromDate: fromDate);

        // Assert
        var list = result.ToList();
        list.Should().HaveCount(2);
        list.Should().AllSatisfy(v => v.ViolationStartTime.Should().BeOnOrAfter(fromDate));
    }

    [Fact]
    public async Task RecordViolationAsync_CalculatesDeviation()
    {
        // Arrange
        var quotaId = Guid.NewGuid();
        var quota = new ResourceQuota { Id = quotaId, Type = ResourceUsageType.Storage };
        quota.SetProperties(new Dictionary<string, object?> { ["TenantId"] = Guid.NewGuid() });

        _quotaRepositoryMock.Setup(r => r.GetByIdAsync(quotaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);

        SlaImpactAnalysis? capturedViolation = null;
        _analysisRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .Callback<SlaImpactAnalysis, CancellationToken>((v, _) => capturedViolation = v)
            .ReturnsAsync((SlaImpactAnalysis v, CancellationToken _) => v);

        // Act
        await _service.RecordViolationAsync(
            quotaId,
            SlaViolationType.QuotaExceeded,
            SlaViolationSeverity.Medium,
            1000,
            1200,
            null
        );

        // Assert
        capturedViolation.Should().NotBeNull();
        // CalculateDeviation should set DeviationPercentage = (1200 - 1000) / 1000 = 0.20 = 20%
        capturedViolation!.DeviationPercentage.Should().BeApproximately(20.0m, 0.01m);
    }
}
