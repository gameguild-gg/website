using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.Tests.Audit.Integration;

/// <summary>
/// Integration tests for audit features
/// Tests audit logging, compliance reporting, and query capabilities
/// </summary>
public class AuditIntegrationTests : IClassFixture<WebApplicationFactory<GameGuild.API.Program>>, IDisposable
{
    private readonly WebApplicationFactory<GameGuild.API.Program> _factory;
    private readonly IServiceScope _scope;
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public AuditIntegrationTests(WebApplicationFactory<GameGuild.API.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                var databaseName = $"TestDb_{Guid.NewGuid()}";

                // Remove all EF Core and Npgsql service registrations
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                                d.ServiceType == typeof(ApplicationDbContext) ||
                                d.ServiceType.FullName?.Contains("EntityFramework") == true ||
                                d.ImplementationType?.FullName?.Contains("Npgsql") == true)
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(databaseName);
                });
                services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
            });
        });

        _scope = _factory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _auditService = _scope.ServiceProvider.GetRequiredService<IAuditService>();
    }

    [Fact]
    public async Task AuditService_ShouldPersistAuditLog_WhenLoggingAction() {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateAuditLogRequest {
            ActionType = "Integration.Test",
            ResourceType = "TestResource",
            ResourceId = Guid.NewGuid().ToString(),
            UserId = userId,
            Description = "Integration test audit log",
            Success = true,
            RiskLevel = AuditRiskLevel.Low,
            Category = AuditCategory.General
        };

        // Act
        await _auditService.LogAsync(request);

        // Assert
        var auditLogs = await _context.Set<AuditLog>().ToListAsync();
        auditLogs.Should().HaveCount(1);

        var auditLog = auditLogs.First();
        auditLog.ActionType.Should().Be(request.ActionType);
        auditLog.ResourceType.Should().Be(request.ResourceType);
        auditLog.ResourceId.Should().Be(request.ResourceId);
        auditLog.UserId.Should().Be(request.UserId);
        auditLog.Description.Should().Be(request.Description);
        auditLog.Success.Should().Be(request.Success);
        auditLog.RiskLevel.Should().Be(request.RiskLevel);
        auditLog.Category.Should().Be(request.Category);
    }

    [Fact]
    public async Task ActionTypeSearch_ShouldBeRegisteredAndReachableThroughCqrs()
    {
        var sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        var request = new AuditActionTypeSearchRequest
        {
            ActionTypes = ["NoSuchAuditAction"],
            IncludeTrends = false
        };

        var result = await sender.Send(new SearchAuditActionTypesQuery(request), CancellationToken.None);

        result.TotalCount.Should().Be(0);
        result.Logs.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditService_ShouldUseIndependentContext_WithoutSavingCallerChanges() {
        var pendingCallerLog = new AuditLog {
            ActionType = "Caller.Pending",
            ResourceType = "TestResource",
            Success = true,
            Category = AuditCategory.General
        };
        _context.Set<AuditLog>().Add(pendingCallerLog);

        await _auditService.LogAsync(new CreateAuditLogRequest {
            ActionType = "Audit.Independent",
            ResourceType = "TestResource",
            Success = true,
            Category = AuditCategory.General
        });

        _context.Entry(pendingCallerLog).State.Should().Be(EntityState.Added);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedActions = await verificationContext.Set<AuditLog>()
            .Select(log => log.ActionType)
            .ToListAsync();

        persistedActions.Should().ContainSingle(action => action == "Audit.Independent");
        persistedActions.Should().NotContain("Caller.Pending");
    }

    [Fact]
    public async Task AuditService_ShouldQueryAuditLogs_WithFiltering() {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Create multiple audit logs
        await _auditService.LogAsync(new CreateAuditLogRequest {
            ActionType = "Login",
            ResourceType = "User",
            UserId = userId1,
            TenantId = tenantId,
            Success = true,
            Category = AuditCategory.Authentication
        });

        await _auditService.LogAsync(new CreateAuditLogRequest {
            ActionType = "Logout",
            ResourceType = "User",
            UserId = userId1,
            TenantId = tenantId,
            Success = true,
            Category = AuditCategory.Authentication
        });

        await _auditService.LogAsync(new CreateAuditLogRequest {
            ActionType = "Login",
            ResourceType = "User",
            UserId = userId2,
            Success = false,
            Category = AuditCategory.Authentication
        });

        // Act - Query for specific user
        var query = new AuditLogQuery {
            UserId = userId1,
            TenantId = tenantId
        };

        var auditLogs = await _auditService.GetAuditLogsAsync(query);
        var count = await _auditService.GetAuditLogCountAsync(query);

        // Assert
        auditLogs.Should().HaveCount(2);
        count.Should().Be(2);
        auditLogs.Should().OnlyContain(log => log.UserId == userId1);
        auditLogs.Should().OnlyContain(log => log.TenantId == tenantId);
    }

    [Fact]
    public async Task AuditService_ShouldLogPermissionEvents_Correctly() {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var permissionName = "TestPermission";
        var resourceType = "TestResource";
        var resourceId = "resource-123";

        // Act - Log permission grant and deny
        await _auditService.LogPermissionGrantAsync(userId, permissionName, resourceType, resourceId, tenantId);
        await _auditService.LogPermissionDenyAsync(userId, permissionName, resourceType, resourceId, "Insufficient privileges", tenantId);

        // Assert
        var auditLogs = await _context.Set<AuditLog>()
            .Where(log => log.UserId == userId && log.Category == AuditCategory.Permission)
            .OrderBy(log => log.CreatedAt)
            .ToListAsync();

        auditLogs.Should().HaveCount(2);

        var grantLog = auditLogs[0];
        grantLog.ActionType.Should().Be(AuditActionTypes.PermissionGranted);
        grantLog.Success.Should().BeTrue();
        grantLog.RiskLevel.Should().Be(AuditRiskLevel.Medium);

        var denyLog = auditLogs[1];
        denyLog.ActionType.Should().Be(AuditActionTypes.PermissionDenied);
        denyLog.Success.Should().BeFalse();
        denyLog.RiskLevel.Should().Be(AuditRiskLevel.High);
    }

    [Fact]
    public async Task AuditService_ShouldLogAuthenticationEvents_Correctly() {
        // Arrange
        var userId = Guid.NewGuid();

        // Act - Log successful and failed authentication
        await _auditService.LogAuthenticationAsync("Login", userId, success: true);
        await _auditService.LogAuthenticationAsync("Login", null, success: false, "Invalid credentials");

        // Assert
        var auditLogs = await _context.Set<AuditLog>()
            .Where(log => log.Category == AuditCategory.Authentication)
            .OrderBy(log => log.CreatedAt)
            .ToListAsync();

        auditLogs.Should().HaveCount(2);

        var successLog = auditLogs[0];
        successLog.Success.Should().BeTrue();
        successLog.RiskLevel.Should().Be(AuditRiskLevel.Low);
        successLog.UserId.Should().Be(userId);

        var failureLog = auditLogs[1];
        failureLog.Success.Should().BeFalse();
        failureLog.RiskLevel.Should().Be(AuditRiskLevel.High);
        failureLog.ErrorMessage.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task AuditService_ShouldLogSecurityViolations_WithCriticalRiskLevel() {
        // Arrange
        var userId = Guid.NewGuid();
        var violationType = "BruteForce";
        var description = "Multiple failed login attempts";

        // Act
        await _auditService.LogSecurityViolationAsync(violationType, description, userId);

        // Assert
        var auditLog = await _context.Set<AuditLog>()
            .FirstOrDefaultAsync(log => log.Category == AuditCategory.Security);

        auditLog.Should().NotBeNull();
        auditLog!.ActionType.Should().Be(AuditActionTypes.SecurityViolation);
        auditLog.ResourceType.Should().Be("Security");
        auditLog.UserId.Should().Be(userId);
        auditLog.Success.Should().BeFalse();
        auditLog.RiskLevel.Should().Be(AuditRiskLevel.Critical);
        auditLog.Category.Should().Be(AuditCategory.Security);
        auditLog.Description.Should().Contain(violationType);
        auditLog.Description.Should().Contain(description);
    }

    [Fact]
    public async Task AuditService_ShouldLogTenantOperations_WithProperContext() {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var actionType = "TenantCreated";
        var description = "New tenant created via integration test";

        // Act
        await _auditService.LogTenantOperationAsync(actionType, tenantId, userId, description);

        // Assert
        var auditLog = await _context.Set<AuditLog>()
            .FirstOrDefaultAsync(log => log.Category == AuditCategory.Tenant);

        auditLog.Should().NotBeNull();
        auditLog!.ActionType.Should().Be(actionType);
        auditLog.ResourceType.Should().Be("Tenant");
        auditLog.ResourceId.Should().Be(tenantId.ToString());
        auditLog.UserId.Should().Be(userId);
        auditLog.TenantId.Should().Be(tenantId);
        auditLog.Description.Should().Be(description);
        auditLog.Success.Should().BeTrue();
        auditLog.RiskLevel.Should().Be(AuditRiskLevel.Medium);
    }

    [Fact]
    public async Task AuditService_ShouldHandleConcurrentAuditLogging() {
        // Arrange
        var userId = Guid.NewGuid();
        var tasks = new List<Task>();

        // Act - Create multiple concurrent audit log tasks
        for (int i = 0; i < 10; i++) {
            var index = i;
            tasks.Add(_auditService.LogAsync(new CreateAuditLogRequest {
                ActionType = $"ConcurrentTest_{index}",
                ResourceType = "TestResource",
                ResourceId = index.ToString(),
                UserId = userId,
                Description = $"Concurrent audit log {index}",
                Success = true
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var auditLogs = await _context.Set<AuditLog>()
            .Where(log => log.UserId == userId)
            .ToListAsync();

        auditLogs.Should().HaveCount(10);
        auditLogs.Should().OnlyContain(log => log.ActionType.StartsWith("ConcurrentTest_"));
    }

    public void Dispose() {
        _scope.Dispose();
    }
}
