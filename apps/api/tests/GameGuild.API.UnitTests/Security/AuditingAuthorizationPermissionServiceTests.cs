using System.Net;
using System.Security.Claims;
using FluentAssertions;
using GameGuild.API.Core.Security;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ISiemIntegrationService = GameGuild.Identity.Authentication.ISiemIntegrationService;

namespace GameGuild.API.UnitTests.Security;

public sealed class AuditingAuthorizationPermissionServiceTests
{
    private static readonly Guid UserId = Guid.Parse("fd50f29c-c3ad-47ca-8ef0-7b9e3c4073cb");
    private static readonly Guid TenantId = Guid.Parse("254b12b2-7e43-4c3d-9cb0-b03977d50e82");

    [Fact]
    public async Task HasPermissionAsync_LogsTheIndividualPermissionDecision()
    {
        var queryService = new Mock<IPermissionQueryService>();
        queryService.Setup(service => service.HasTenantPermissionAsync(UserId, TenantId, "teams.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var auditService = new Mock<IAuditService>();
        CreateAuditLogRequest? captured = null;
        auditService.Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Callback<CreateAuditLogRequest>(request => captured = request)
            .Returns(Task.CompletedTask);
        auditService.Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>())).ReturnsAsync(0);
        var service = CreateService(queryService.Object, auditService.Object);

        var result = await service.HasPermissionAsync(UserId, TenantId, "teams.read");

        result.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.ActionType.Should().Be(AuditActionTypes.PermissionDenied);
        captured.ResourceType.Should().Be("Permission");
        captured.ResourceId.Should().Be("teams.read");
        captured.UserId.Should().Be(UserId);
        captured.TenantId.Should().Be(TenantId);
        captured.IpAddress.Should().Be("203.0.113.20");
        captured.CorrelationId.Should().Be("permission-correlation-8");
        captured.Success.Should().BeFalse();
    }

    [Fact]
    public async Task HasAllPermissionsAsync_LogsEveryGrantAndDenialIndividually()
    {
        var queryService = new Mock<IPermissionQueryService>();
        queryService.Setup(service => service.GetEffectivePermissionsAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["teams.read"]);
        var auditService = new Mock<IAuditService>();
        var logged = new List<CreateAuditLogRequest>();
        auditService.Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Callback<CreateAuditLogRequest>(logged.Add)
            .Returns(Task.CompletedTask);
        auditService.Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>())).ReturnsAsync(0);
        var service = CreateService(queryService.Object, auditService.Object);

        var result = await service.HasAllPermissionsAsync(UserId, TenantId, ["teams.read", "teams.write"]);

        result.HasAllRequired.Should().BeFalse();
        logged.Should().HaveCount(2);
        logged[0].ActionType.Should().Be(AuditActionTypes.PermissionGranted);
        logged[0].ResourceId.Should().Be("teams.read");
        logged[1].ActionType.Should().Be(AuditActionTypes.PermissionDenied);
        logged[1].ResourceId.Should().Be("teams.write");
        logged.Select(request => request.Metadata!.GetType().GetProperty("EvaluationType")!.GetValue(request.Metadata))
            .Should().OnlyContain(value => Equals(value, "HasAllPermissions"));
    }

    [Fact]
    public async Task HasAnyPermissionAsync_LogsEachCandidateEvenWhenTheOverallCheckSucceeds()
    {
        var queryService = new Mock<IPermissionQueryService>();
        queryService.Setup(service => service.GetEffectivePermissionsAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["projects.read"]);
        var auditService = new Mock<IAuditService>();
        var logged = new List<CreateAuditLogRequest>();
        auditService.Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Callback<CreateAuditLogRequest>(logged.Add)
            .Returns(Task.CompletedTask);
        auditService.Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>())).ReturnsAsync(0);
        var service = CreateService(queryService.Object, auditService.Object);

        var result = await service.HasAnyPermissionAsync(UserId, TenantId, ["projects.read", "projects.delete"]);

        result.HasAnyRequired.Should().BeTrue();
        logged.Should().HaveCount(2);
        logged.Select(request => request.ActionType)
            .Should().Equal(AuditActionTypes.PermissionGranted, AuditActionTypes.PermissionDenied);
    }

    [Fact]
    public async Task PermissionChecks_KeepTheirDecisionWhenAuditPersistenceFails()
    {
        var queryService = new Mock<IPermissionQueryService>();
        queryService.Setup(service => service.HasTenantPermissionAsync(UserId, TenantId, "teams.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var auditService = new Mock<IAuditService>();
        auditService.Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .ThrowsAsync(new InvalidOperationException("Audit storage unavailable"));
        auditService.Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>())).ReturnsAsync(0);
        var service = CreateService(queryService.Object, auditService.Object);

        var result = await service.HasPermissionAsync(UserId, TenantId, "teams.read");

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HasPermissionAsync_AlertsOnceWhenDenialsReachTheConfiguredThreshold(bool boundedCache)
    {
        var queryService = new Mock<IPermissionQueryService>();
        queryService.Setup(service => service.HasTenantPermissionAsync(UserId, TenantId, "teams.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var auditService = new Mock<IAuditService>();
        auditService.Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>())).Returns(Task.CompletedTask);
        auditService.Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>())).ReturnsAsync(5);
        var siemService = new Mock<ISiemIntegrationService>();
        siemService.Setup(service => service.SendSecurityEventAsync(It.IsAny<SiemEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var alertCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = boundedCache ? 64 : null
        });
        var service = CreateService(queryService.Object, auditService.Object, siemService.Object, alertCache);

        await service.HasPermissionAsync(UserId, TenantId, "teams.read");
        await service.HasPermissionAsync(UserId, TenantId, "teams.read");

        siemService.Verify(service => service.SendSecurityEventAsync(
            It.Is<SiemEvent>(alert =>
                alert.EventType == "PermissionDenialPatternDetected" &&
                alert.Severity == SiemSeverity.High &&
                alert.TenantId == TenantId &&
                alert.Metadata!["threshold"].ToString() == "5"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPermissionsAsync_ForwardsPermissionListingWithoutCreatingDecisionEvents()
    {
        var permissions = new List<string> { "teams.read", "teams.write" };
        var queryService = new Mock<IPermissionQueryService>();
        queryService.Setup(service => service.GetEffectivePermissionsAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);
        var auditService = new Mock<IAuditService>();
        var service = CreateService(queryService.Object, auditService.Object);

        var result = await service.GetPermissionsAsync(UserId, TenantId);

        result.Should().BeSameAs(permissions);
        auditService.Verify(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()), Times.Never);
    }

    private static AuditingAuthorizationPermissionService CreateService(
        IPermissionQueryService queryService,
        IAuditService auditService,
        ISiemIntegrationService? siemService = null)
        => CreateService(queryService, auditService, siemService, new MemoryCache(new MemoryCacheOptions()));

    private static AuditingAuthorizationPermissionService CreateService(
        IPermissionQueryService queryService,
        IAuditService auditService,
        ISiemIntegrationService? siemService,
        IMemoryCache alertCache)
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "permission-correlation-8",
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, UserId.ToString("D"))],
                "Bearer"))
        };
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.20");

        return new AuditingAuthorizationPermissionService(
            new AuthorizationPermissionServiceAdapter(queryService),
            auditService,
            new HttpContextAccessor { HttpContext = context },
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:Anomaly:MaxFailedAttemptsPerHour"] = "5"
            }).Build(),
            siemService ?? new Mock<ISiemIntegrationService>().Object,
            alertCache,
            NullLogger<AuditingAuthorizationPermissionService>.Instance);
    }
}
