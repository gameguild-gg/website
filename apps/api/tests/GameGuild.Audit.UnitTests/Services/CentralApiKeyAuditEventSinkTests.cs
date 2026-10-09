using FluentAssertions;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class CentralApiKeyAuditEventSinkTests
{
    [Fact]
    public async Task RecordAsync_MapsLifecycleEventToCentralAuditLogKeyedByApiKeyId()
    {
        var auditService = new Mock<IAuditService>();
        CreateAuditLogRequest? captured = null;
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Callback<CreateAuditLogRequest>(request => captured = request)
            .Returns(Task.CompletedTask);
        var sink = new CentralApiKeyAuditEventSink(
            auditService.Object,
            NullLogger<CentralApiKeyAuditEventSink>.Instance);
        var keyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await sink.RecordAsync(new ApiKeyAuditEvent(
            ApiKeyAuditActions.Rotated,
            keyId,
            userId,
            tenantId,
            Description: "API key rotated",
            Metadata: new { OldKeyId = Guid.NewGuid() }),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.ActionType.Should().Be(ApiKeyAuditActions.Rotated);
        captured.ResourceType.Should().Be("ApiKey");
        captured.ResourceId.Should().Be(keyId.ToString(), "lifecycle audit entries are keyed by api_key_id");
        captured.Category.Should().Be(AuditCategory.Security);
        captured.RiskLevel.Should().Be(AuditRiskLevel.Medium);
        captured.UserId.Should().Be(userId);
        captured.TenantId.Should().Be(tenantId);
        captured.Success.Should().BeTrue();
        captured.Description.Should().Contain("rotated");
    }

    [Fact]
    public async Task RecordAsync_SwallowsAuditTransportFailures()
    {
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .ThrowsAsync(new InvalidOperationException("audit transport down"));
        var sink = new CentralApiKeyAuditEventSink(
            auditService.Object,
            NullLogger<CentralApiKeyAuditEventSink>.Instance);

        var act = async () => await sink.RecordAsync(
            new ApiKeyAuditEvent(ApiKeyAuditActions.Revoked, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().NotThrowAsync("audit transport failures must not fail lifecycle operations");
    }
}
