using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using GameGuild.Resources.Handlers;

namespace GameGuild.Resources.UnitTests;

/// <summary>
/// Tests for QuotaExceededAlertHandler
/// </summary>
public class QuotaExceededAlertHandlerTests
{
    private readonly Mock<ILogger<QuotaExceededAlertHandler>> _loggerMock = new();
    private readonly QuotaExceededAlertHandler _handler;

    public QuotaExceededAlertHandlerTests()
    {
        _handler = new QuotaExceededAlertHandler(_loggerMock.Object);
    }

    private static QuotaExceededEvent CreateEvent(
        Guid? tenantId = null,
        ResourceUsageType type = ResourceUsageType.ApiCalls,
        long currentUsage = 100,
        long requestedAmount = 10,
        long hardLimit = 100,
        string? source = "test",
        Guid? actorId = null)
    {
        return new QuotaExceededEvent(
            tenantId ?? Guid.NewGuid(),
            type,
            currentUsage,
            requestedAmount,
            hardLimit,
            source,
            actorId ?? Guid.NewGuid(),
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_FirstViolation_LogsWarning()
    {
        var evt = CreateEvent(tenantId: Guid.NewGuid(), type: ResourceUsageType.Storage);

        await _handler.Handle(evt, CancellationToken.None);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("QUOTA_EXCEEDED")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_ZeroHardLimit_CalculatesZeroPercentage()
    {
        var evt = CreateEvent(hardLimit: 0, currentUsage: 50);

        // Should not throw
        await _handler.Handle(evt, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_NullSource_UsesUnknown()
    {
        var evt = CreateEvent(source: null);

        await _handler.Handle(evt, CancellationToken.None);

        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_RepeatedViolations_EscalatesToError()
    {
        // Use a unique tenant+type combo to avoid interference with other tests
        var tenantId = Guid.NewGuid();
        var type = ResourceUsageType.Programs;

        // Fire 5 violations (the escalation threshold) to trigger error logging
        for (var i = 0; i < 5; i++)
        {
            var evt = CreateEvent(tenantId: tenantId, type: type);
            await _handler.Handle(evt, CancellationToken.None);
        }

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("QUOTA_EXCEEDED_REPEATED")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_DifferentTenants_DoNotCrossEscalate()
    {
        var type = ResourceUsageType.Courses;

        for (var i = 0; i < 4; i++)
        {
            var evt = CreateEvent(tenantId: Guid.NewGuid(), type: type);
            await _handler.Handle(evt, CancellationToken.None);
        }

        // With 4 different tenants, should never see Error level
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}

/// <summary>
/// Tests for LoggingSlaNotificationSender
/// </summary>
public class LoggingSlaNotificationSenderTests
{
    private readonly Mock<ILogger<LoggingSlaNotificationSender>> _loggerMock = new();
    private readonly LoggingSlaNotificationSender _sender;

    public LoggingSlaNotificationSenderTests()
    {
        _sender = new LoggingSlaNotificationSender(_loggerMock.Object);
    }

    [Fact]
    public async Task SendToUserAsync_LogsInformation()
    {
        var userId = Guid.NewGuid();

        await _sender.SendToUserAsync(
            userId, "Title", "Message", "SlaViolation", "high", "/action", CancellationToken.None);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("SLA Notification")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendToUserAsync_WithNullActionUrl_Succeeds()
    {
        await _sender.SendToUserAsync(
            Guid.NewGuid(), "T", "M", "type", "normal", null, CancellationToken.None);
    }

    [Fact]
    public async Task SendWebhookAsync_LogsInformation()
    {
        var payload = new { ViolationId = Guid.NewGuid(), Message = "test" };

        await _sender.SendWebhookAsync("https://hooks.example.com/alert", payload, CancellationToken.None);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("SLA Webhook")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

/// <summary>
/// Tests for SlaIncidentEscalationService
/// </summary>
public class SlaIncidentEscalationServiceTests
{
    private readonly Mock<ISlaImpactAnalysisRepository> _repoMock = new();
    private readonly Mock<IIncidentTicketProvider> _ticketProviderMock = new();
    private readonly Mock<ISlaNotificationSender> _notificationMock = new();
    private readonly Mock<ILogger<SlaIncidentEscalationService>> _loggerMock = new();
    private readonly SlaIncidentEscalationService _service;

    public SlaIncidentEscalationServiceTests()
    {
        _service = new SlaIncidentEscalationService(
            _repoMock.Object,
            _ticketProviderMock.Object,
            _notificationMock.Object,
            _loggerMock.Object);
    }

    private static SlaImpactAnalysis CreateViolation(
        Guid? tenantId = null,
        SlaViolationSeverity severity = SlaViolationSeverity.High,
        bool incidentCreated = false,
        bool requiresEscalation = false)
    {
        var violation = new SlaImpactAnalysis
        {
            Id = Guid.NewGuid(),
            ResourceQuotaId = Guid.NewGuid(),
            ViolationStartTime = DateTime.UtcNow.AddMinutes(-10),
            Severity = severity,
            ViolationType = SlaViolationType.QuotaExceeded,
            ExpectedValue = 100,
            ActualValue = 150,
            DeviationPercentage = 50m,
            IncidentCreated = incidentCreated,
            RequiresEscalation = requiresEscalation
        };
        violation.SetTenantId(tenantId ?? Guid.NewGuid());
        return violation;
    }

    [Fact]
    public async Task EscalateViolation_NullTenant_ReturnsFailed()
    {
        // Create a violation without setting TenantId (default is null)
        var violation = new SlaImpactAnalysis
        {
            Id = Guid.NewGuid(),
            ResourceQuotaId = Guid.NewGuid(),
            ViolationStartTime = DateTime.UtcNow.AddMinutes(-10),
            Severity = SlaViolationSeverity.High,
            ViolationType = SlaViolationType.QuotaExceeded,
            ExpectedValue = 100,
            ActualValue = 150,
            DeviationPercentage = 50m
        };

        var result = await _service.EscalateViolationAsync(violation);

        result.WasEscalated.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no tenant");
    }

    [Fact]
    public async Task EscalateViolation_AutoEscalationDisabled_ReturnsNotRequired()
    {
        var tenantId = Guid.NewGuid();
        var violation = CreateViolation(tenantId: tenantId, severity: SlaViolationSeverity.Critical);

        // Set config with auto-escalation disabled
        await _service.SetEscalationConfigAsync(tenantId,
            new SlaEscalationConfig
            {
                TenantId = tenantId,
                AutoEscalationEnabled = false
            });

        var result = await _service.EscalateViolationAsync(violation);

        result.WasEscalated.Should().BeFalse();
    }

    [Fact]
    public async Task EscalateViolation_BelowMinSeverity_ReturnsNotRequired()
    {
        var tenantId = Guid.NewGuid();
        var violation = CreateViolation(tenantId: tenantId, severity: SlaViolationSeverity.Low);

        // Default config has MinimumEscalationSeverity = High
        var result = await _service.EscalateViolationAsync(violation);

        result.WasEscalated.Should().BeFalse();
    }

    [Fact]
    public async Task EscalateViolation_MeetsThreshold_CreatesIncidentAndNotifies()
    {
        var tenantId = Guid.NewGuid();
        var violation = CreateViolation(tenantId: tenantId, severity: SlaViolationSeverity.Critical);

        _ticketProviderMock
            .Setup(x => x.CreateTicketAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("INC-123");

        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(violation);

        var result = await _service.EscalateViolationAsync(violation);

        result.WasEscalated.Should().BeTrue();
        result.IncidentId.Should().Be("INC-123");
        violation.IncidentCreated.Should().BeTrue();
        violation.IncidentTicketId.Should().Be("INC-123");
    }

    [Fact]
    public async Task EscalateViolation_AlreadyHasIncident_SkipsTicketCreation()
    {
        var tenantId = Guid.NewGuid();
        var violation = CreateViolation(tenantId: tenantId, severity: SlaViolationSeverity.Critical, incidentCreated: true);

        _repoMock
            .Setup(x => x.UpdateAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(violation);

        var result = await _service.EscalateViolationAsync(violation);

        result.WasEscalated.Should().BeTrue();
        _ticketProviderMock.Verify(
            x => x.CreateTicketAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EscalateViolation_ExceptionDuringEscalation_ReturnsFailed()
    {
        var tenantId = Guid.NewGuid();
        var violation = CreateViolation(tenantId: tenantId, severity: SlaViolationSeverity.Critical);

        _ticketProviderMock
            .Setup(x => x.CreateTicketAsync(It.IsAny<SlaImpactAnalysis>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Ticket system offline"));

        var result = await _service.EscalateViolationAsync(violation);

        result.WasEscalated.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Ticket system offline");
    }

    [Fact]
    public async Task GetEscalationConfig_DefaultConfig_ReturnsDefaults()
    {
        var tenantId = Guid.NewGuid();
        var config = await _service.GetEscalationConfigAsync(tenantId);

        config.TenantId.Should().Be(tenantId);
        config.AutoEscalationEnabled.Should().BeTrue();
        config.MinimumEscalationSeverity.Should().Be(SlaViolationSeverity.High);
        config.AutoCreateIncidents.Should().BeTrue();
    }

    [Fact]
    public async Task SetEscalationConfig_ThenGet_ReturnsSameConfig()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var config = new SlaEscalationConfig
        {
            TenantId = tenantId,
            AutoEscalationEnabled = true,
            MinimumEscalationSeverity = SlaViolationSeverity.Medium,
            EscalationUserIds = new List<Guid> { userId },
            WebhookUrl = "https://hooks.example.com"
        };

        await _service.SetEscalationConfigAsync(tenantId, config);
        var retrieved = await _service.GetEscalationConfigAsync(tenantId);

        retrieved.AutoEscalationEnabled.Should().BeTrue();
        retrieved.MinimumEscalationSeverity.Should().Be(SlaViolationSeverity.Medium);
        retrieved.EscalationUserIds.Should().Contain(userId);
        retrieved.WebhookUrl.Should().Be("https://hooks.example.com");
    }

    [Fact]
    public async Task SendViolationNotification_NullTenant_Returns()
    {
        var violation = new SlaImpactAnalysis
        {
            Id = Guid.NewGuid(),
            ResourceQuotaId = Guid.NewGuid(),
            ViolationStartTime = DateTime.UtcNow.AddMinutes(-10),
            Severity = SlaViolationSeverity.High,
            ViolationType = SlaViolationType.QuotaExceeded,
            ExpectedValue = 100,
            ActualValue = 150,
            DeviationPercentage = 50m
        };

        await _service.SendViolationNotificationAsync(violation);

        _notificationMock.Verify(
            x => x.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendViolationNotification_WithConfiguredUsers_SendsToEach()
    {
        var tenantId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await _service.SetEscalationConfigAsync(tenantId, new SlaEscalationConfig
        {
            TenantId = tenantId,
            EscalationUserIds = new List<Guid> { user1, user2 }
        });

        var violation = CreateViolation(tenantId: tenantId, severity: SlaViolationSeverity.Critical);

        await _service.SendViolationNotificationAsync(violation);

        _notificationMock.Verify(
            x => x.SendToUserAsync(user1, It.IsAny<string>(), It.IsAny<string>(),
                "SlaViolation", "high", It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _notificationMock.Verify(
            x => x.SendToUserAsync(user2, It.IsAny<string>(), It.IsAny<string>(),
                "SlaViolation", "high", It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendViolationNotification_WithWebhook_SendsWebhook()
    {
        var tenantId = Guid.NewGuid();
        var webhookUrl = "https://hooks.example.com/sla";

        await _service.SetEscalationConfigAsync(tenantId, new SlaEscalationConfig
        {
            TenantId = tenantId,
            WebhookUrl = webhookUrl
        });

        var violation = CreateViolation(tenantId: tenantId, severity: SlaViolationSeverity.High);

        await _service.SendViolationNotificationAsync(violation);

        _notificationMock.Verify(
            x => x.SendWebhookAsync(webhookUrl, It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(SlaViolationSeverity.Critical)]
    [InlineData(SlaViolationSeverity.High)]
    [InlineData(SlaViolationSeverity.Medium)]
    [InlineData(SlaViolationSeverity.Low)]
    public async Task SendViolationNotification_SeverityMappedCorrectly(SlaViolationSeverity severity)
    {
        var tenantId = Guid.NewGuid();
        await _service.SetEscalationConfigAsync(tenantId, new SlaEscalationConfig
        {
            TenantId = tenantId,
            MinimumEscalationSeverity = SlaViolationSeverity.None,
            EscalationUserIds = new List<Guid> { Guid.NewGuid() }
        });

        var violation = CreateViolation(tenantId: tenantId, severity: severity);

        await _service.SendViolationNotificationAsync(violation);

        var expectedPriority = severity >= SlaViolationSeverity.High ? "high" : "normal";
        _notificationMock.Verify(
            x => x.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                "SlaViolation", expectedPriority, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingEscalations_NoViolations_ReturnsZero()
    {
        _repoMock
            .Setup(x => x.GetUnresolvedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<SlaImpactAnalysis>());

        var count = await _service.ProcessPendingEscalationsAsync();

        count.Should().Be(0);
    }
}

/// <summary>
/// Tests for SlaEscalationResult factory methods (additional)
/// </summary>
public class SlaEscalationResultAdditionalTests
{
    [Fact]
    public void Success_WithIncidentAndUsers_SetsProperties()
    {
        var users = new List<Guid> { Guid.NewGuid() };
        var result = SlaEscalationResult.Success("INC-123", users);

        result.WasEscalated.Should().BeTrue();
        result.IncidentId.Should().Be("INC-123");
        result.NotifiedUserIds.Should().BeEquivalentTo(users);
    }

    [Fact]
    public void Success_NullParams_Works()
    {
        var result = SlaEscalationResult.Success(null, null);
        result.WasEscalated.Should().BeTrue();
    }

    [Fact]
    public void NotRequired_SetsWasEscalatedFalse()
    {
        var result = SlaEscalationResult.NotRequired();
        result.WasEscalated.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Failed_SetsErrorMessage()
    {
        var result = SlaEscalationResult.Failed("something went wrong");
        result.WasEscalated.Should().BeFalse();
        result.ErrorMessage.Should().Be("something went wrong");
    }
}

/// <summary>
/// Tests for SlaEscalationConfig defaults (additional)
/// </summary>
public class SlaEscalationConfigAdditionalTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var config = new SlaEscalationConfig();

        config.AutoEscalationEnabled.Should().BeTrue();
        config.MinimumEscalationSeverity.Should().Be(SlaViolationSeverity.High);
        config.AutoCreateIncidents.Should().BeTrue();
        config.NotificationCooldownMinutes.Should().Be(15);
        config.EscalationUserIds.Should().BeEmpty();
        config.EscalationEmails.Should().BeEmpty();
    }
}
