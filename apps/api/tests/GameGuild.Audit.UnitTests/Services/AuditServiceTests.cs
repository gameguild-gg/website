using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
/// Unit tests for the AuditService
/// Tests audit logging functionality and various audit scenarios
/// </summary>
public class AuditServiceTests : IDisposable
{
    private readonly TestApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<ILogger<AuditService>> _mockLogger;
    private readonly AuditService _auditService;
    private readonly ServiceProvider _serviceProvider;
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;

    public AuditServiceTests()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        _contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}", databaseRoot)
            .Options;

        _context = new TestApplicationDbContext(_contextOptions);
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<AuditService>>();
        _serviceProvider = new ServiceCollection()
            .AddScoped<IApplicationDbContext>(_ => new TestApplicationDbContext(_contextOptions))
            .BuildServiceProvider();

        _auditService = new AuditService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _mockHttpContextAccessor.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _context.Dispose();
    }

    [Fact]
    public async Task LogAsync_ShouldCreateAuditLogEntry_WhenValidRequestProvided()
    {
        // Arrange
        var request = new CreateAuditLogRequest
        {
            ActionType = "TestAction",
            ResourceType = "TestResource",
            ResourceId = Guid.NewGuid().ToString(),
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Description = "Test description",
            Success = true,
            RiskLevel = AuditRiskLevel.Medium,
            Category = AuditCategory.General
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "Test User Agent";
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        await _auditService.LogAsync(request);

        // Assert
        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.ActionType.Should().Be(request.ActionType);
        auditLog.ResourceType.Should().Be(request.ResourceType);
        auditLog.ResourceId.Should().Be(request.ResourceId);
        auditLog.UserId.Should().Be(request.UserId);
        auditLog.TenantId.Should().Be(request.TenantId);
        auditLog.Description.Should().Be(request.Description);
        auditLog.Success.Should().Be(request.Success);
        auditLog.RiskLevel.Should().Be(request.RiskLevel);
        auditLog.Category.Should().Be(request.Category);
        auditLog.IpAddress.Should().Be("192.168.1.1");
        auditLog.UserAgent.Should().Be("Test User Agent");
    }

    [Fact]
    public async Task LogAsync_ShouldUseProvidedIpAndUserAgent_WhenSpecifiedInRequest()
    {
        // Arrange
        var request = new CreateAuditLogRequest
        {
            ActionType = "TestAction",
            ResourceType = "TestResource",
            IpAddress = "10.0.0.1",
            UserAgent = "Custom User Agent",
            Success = true
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "Default User Agent";
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        await _auditService.LogAsync(request);

        // Assert
        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.IpAddress.Should().Be("10.0.0.1");
        auditLog.UserAgent.Should().Be("Custom User Agent");
    }

    [Fact]
    public async Task LogAsync_ShouldSerializeMetadata_WhenMetadataProvided()
    {
        // Arrange
        var metadata = new { Key1 = "Value1", Key2 = 42 };
        var request = new CreateAuditLogRequest
        {
            ActionType = "TestAction",
            ResourceType = "TestResource",
            Metadata = metadata,
            Success = true
        };

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        await _auditService.LogAsync(request);

        // Assert
        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.Metadata.Should().NotBeNull();

        var deserializedMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.Metadata!);
        deserializedMetadata.Should().ContainKey("Key1");
        deserializedMetadata.Should().ContainKey("Key2");
    }

    [Fact]
    public async Task LogAsync_ShouldHandleException_WithoutThrowingUp()
    {
        // Arrange
        var request = new CreateAuditLogRequest
        {
            ActionType = "TestAction",
            ResourceType = "TestResource",
            Success = true
        };

        // Dispose the context to simulate database error
        _context.Dispose();

        // Act & Assert
        var act = async () => await _auditService.LogAsync(request);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogPermissionGrantAsync_ShouldCreateCorrectAuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var permissionName = "TestPermission";
        var resourceType = "TestResource";
        var resourceId = "resource-123";

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        await _auditService.LogPermissionGrantAsync(userId, permissionName, resourceType, resourceId, tenantId);

        // Assert
        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.ActionType.Should().Be(AuditActionTypes.PermissionGranted);
        auditLog.ResourceType.Should().Be(resourceType);
        auditLog.ResourceId.Should().Be(resourceId);
        auditLog.UserId.Should().Be(userId);
        auditLog.TenantId.Should().Be(tenantId);
        auditLog.Success.Should().BeTrue();
        auditLog.RiskLevel.Should().Be(AuditRiskLevel.Medium);
        auditLog.Category.Should().Be(AuditCategory.Permission);
        auditLog.Description.Should().Contain(permissionName);
    }

    [Fact]
    public async Task LogPermissionDenyAsync_ShouldCreateCorrectAuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var permissionName = "TestPermission";
        var resourceType = "TestResource";
        var resourceId = "resource-123";
        var reason = "Insufficient privileges";

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        await _auditService.LogPermissionDenyAsync(userId, permissionName, resourceType, resourceId, reason);

        // Assert
        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.ActionType.Should().Be(AuditActionTypes.PermissionDenied);
        auditLog.ResourceType.Should().Be(resourceType);
        auditLog.ResourceId.Should().Be(resourceId);
        auditLog.UserId.Should().Be(userId);
        auditLog.Success.Should().BeFalse();
        auditLog.RiskLevel.Should().Be(AuditRiskLevel.High);
        auditLog.Category.Should().Be(AuditCategory.Permission);
        auditLog.Description.Should().Contain(permissionName);
        auditLog.Description.Should().Contain(reason);
    }

    [Fact]
    public async Task LogAuthenticationAsync_ShouldCreateCorrectAuditLog_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var actionType = "Login";

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        await _auditService.LogAuthenticationAsync(actionType, userId, success: true);

        // Assert
        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.ActionType.Should().Be(actionType);
        auditLog.ResourceType.Should().Be("User");
        auditLog.ResourceId.Should().Be(userId.ToString());
        auditLog.UserId.Should().Be(userId);
        auditLog.Success.Should().BeTrue();
        auditLog.RiskLevel.Should().Be(AuditRiskLevel.Low);
        auditLog.Category.Should().Be(AuditCategory.Authentication);
        auditLog.Description.Should().Contain("Success");
    }

    [Fact]
    public async Task LogAuthenticationAsync_ShouldCreateCorrectAuditLog_WhenFailed()
    {
        // Arrange
        var actionType = "Login";
        var errorMessage = "Invalid credentials";

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        await _auditService.LogAuthenticationAsync(actionType, null, success: false, errorMessage);

        // Assert
        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.ActionType.Should().Be(actionType);
        auditLog.ResourceType.Should().Be("User");
        auditLog.Success.Should().BeFalse();
        auditLog.RiskLevel.Should().Be(AuditRiskLevel.High);
        auditLog.Category.Should().Be(AuditCategory.Authentication);
        auditLog.ErrorMessage.Should().Be(errorMessage);
        auditLog.Description.Should().Contain("Failed");
    }

    [Fact]
    public async Task LogAdminActionAsync_ShouldCreateCorrectAuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var actionType = "SystemConfiguration";
        var description = "Updated system settings";
        var metadata = new { Setting = "MaxUsers", OldValue = 100, NewValue = 200 };

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        await _auditService.LogAdminActionAsync(userId, actionType, description, metadata);

        // Assert
        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.ActionType.Should().Be(actionType);
        auditLog.ResourceType.Should().Be("System");
        auditLog.UserId.Should().Be(userId);
        auditLog.Description.Should().Be(description);
        auditLog.Success.Should().BeTrue();
        auditLog.RiskLevel.Should().Be(AuditRiskLevel.High);
        auditLog.Category.Should().Be(AuditCategory.Admin);
        auditLog.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task LogSecurityViolationAsync_ShouldCreateCorrectAuditLog()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var violationType = "BruteForce";
        var description = "Multiple failed login attempts";
        var metadata = new { AttemptCount = 5, TimeWindow = "5 minutes" };

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        await _auditService.LogSecurityViolationAsync(violationType, description, userId, metadata);

        // Assert
        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.ActionType.Should().Be(AuditActionTypes.SecurityViolation);
        auditLog.ResourceType.Should().Be("Security");
        auditLog.UserId.Should().Be(userId);
        auditLog.Success.Should().BeFalse();
        auditLog.RiskLevel.Should().Be(AuditRiskLevel.Critical);
        auditLog.Category.Should().Be(AuditCategory.Security);
        auditLog.Description.Should().Contain(violationType);
        auditLog.Description.Should().Contain(description);
        auditLog.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task LogTenantOperationAsync_ShouldCreateCorrectAuditLog()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var actionType = "TenantCreated";
        var description = "New tenant created";
        var metadata = new { TenantName = "TestTenant" };

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        await _auditService.LogTenantOperationAsync(actionType, tenantId, userId, description, metadata);

        // Assert
        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.ActionType.Should().Be(actionType);
        auditLog.ResourceType.Should().Be("Tenant");
        auditLog.ResourceId.Should().Be(tenantId.ToString());
        auditLog.UserId.Should().Be(userId);
        auditLog.TenantId.Should().Be(tenantId);
        auditLog.Description.Should().Be(description);
        auditLog.Success.Should().BeTrue();
        auditLog.RiskLevel.Should().Be(AuditRiskLevel.Medium);
        auditLog.Category.Should().Be(AuditCategory.Tenant);
        auditLog.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task StreamAuditLogsAsync_ShouldApplyFiltersAndPaginationWithoutMaterializingAllRows()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expected = new AuditLog
        {
            UserId = userId,
            ActionType = "Exported",
            ResourceType = "Report",
            CreatedAt = start.AddHours(2)
        };
        var newest = new AuditLog
        {
            UserId = userId,
            ActionType = "Exported",
            ResourceType = "Report",
            CreatedAt = start.AddHours(3)
        };
        var outsideDateRange = new AuditLog
        {
            UserId = userId,
            ActionType = "Exported",
            ResourceType = "Report",
            CreatedAt = start.AddDays(1)
        };
        var otherUser = new AuditLog
        {
            UserId = otherUserId,
            ActionType = "Exported",
            ResourceType = "Report",
            CreatedAt = start.AddHours(2)
        };

        _context.Set<AuditLog>().AddRange(expected, newest, outsideDateRange, otherUser);
        await _context.SaveChangesAsync();

        var streamed = new List<AuditLog>();
        await foreach (var auditLog in _auditService.StreamAuditLogsAsync(
                           new AuditLogQuery
                           {
                               UserId = userId,
                               ActionType = "Exported",
                               StartDate = start,
                               EndDate = start.AddHours(5),
                               Skip = 1,
                               Take = 1
                           },
                           CancellationToken.None))
        {
            streamed.Add(auditLog);
        }

        streamed.Should().ContainSingle().Which.Id.Should().Be(expected.Id);
    }

    [Fact]
    public async Task GetAuditActivityAsync_ShouldAggregateMatchingRecordsIntoUtcBuckets()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var start = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var matchingFirst = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            ActionType = "Updated",
            ResourceType = "Project",
            CreatedAt = start.AddMinutes(5)
        };
        var matchingSecond = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            ActionType = "Updated",
            ResourceType = "Project",
            CreatedAt = start.AddMinutes(45)
        };
        var nextHour = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            ActionType = "Updated",
            ResourceType = "Project",
            CreatedAt = start.AddHours(1).AddMinutes(10)
        };
        var otherTenant = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = userId,
            ActionType = "Updated",
            ResourceType = "Project",
            CreatedAt = start.AddMinutes(15)
        };

        _context.Set<AuditLog>().AddRange(matchingFirst, matchingSecond, nextHour, otherTenant);
        await _context.SaveChangesAsync();

        var query = new AuditLogQuery
        {
            TenantId = tenantId,
            UserId = userId,
            ActionType = "Updated",
            StartDate = start,
            EndDate = start.AddHours(2)
        };

        var hourly = await _auditService.GetAuditActivityAsync(query, AuditActivityBucketSize.Hourly);
        var daily = await _auditService.GetAuditActivityAsync(query, AuditActivityBucketSize.Daily);

        hourly.Should().BeEquivalentTo(
        [
            new AuditActivityBucket(start, 2),
            new AuditActivityBucket(start.AddHours(1), 1)
        ], options => options.WithStrictOrdering());
        daily.Should().ContainSingle().Which.Should().Be(new AuditActivityBucket(start.Date, 3));
    }
}
