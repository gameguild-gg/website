using System.Diagnostics;
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
using Xunit.Abstractions;

namespace GameGuild.Tests.Audit.Performance;

/// <summary>
/// Performance tests for Audit module
/// Tests audit logging operations under load and measures performance
/// </summary>
public class AuditPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TestApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<ILogger<AuditService>> _mockLogger;
    private readonly AuditService _auditService;
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;
    private readonly ServiceProvider _serviceProvider;

    public AuditPerformanceTests(ITestOutputHelper output)
    {
        _output = output;

        var databaseRoot = new InMemoryDatabaseRoot();
        _contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}", databaseRoot)
            .Options;

        _context = new TestApplicationDbContext(_contextOptions);
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<AuditService>>();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "Performance Test User Agent";
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
        _serviceProvider = new ServiceCollection()
            .AddScoped<IApplicationDbContext>(_ => new TestApplicationDbContext(_contextOptions))
            .BuildServiceProvider();

        _auditService = new AuditService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _mockHttpContextAccessor.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Single_Audit_Log_Should_Execute_Within_Performance_Threshold()
    {
        // Arrange
        var request = new CreateAuditLogRequest
        {
            ActionType = "PerformanceTest",
            ResourceType = "TestResource",
            ResourceId = Guid.NewGuid().ToString(),
            UserId = Guid.NewGuid(),
            Description = "Performance test audit log",
            Success = true,
            RiskLevel = AuditRiskLevel.Low,
            Category = AuditCategory.General
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        await _auditService.LogAsync(request);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Single audit log execution time: {stopwatch.ElapsedMilliseconds}ms");

        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50); // Should complete within 50ms
    }

    [Fact]
    public async Task Concurrent_Audit_Logging_Should_Handle_Load()
    {
        // Arrange
        const int concurrentRequests = 100;
        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            var request = new CreateAuditLogRequest
            {
                ActionType = $"ConcurrentTest_{i}",
                ResourceType = "TestResource",
                ResourceId = i.ToString(),
                UserId = Guid.NewGuid(),
                Description = $"Concurrent performance test {i}",
                Success = true,
                RiskLevel = AuditRiskLevel.Low,
                Category = AuditCategory.General
            };

            tasks.Add(_auditService.LogAsync(request));
        }

        await Task.WhenAll(tasks);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Concurrent audit logging ({concurrentRequests} requests) execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average per request: {stopwatch.ElapsedMilliseconds / (double)concurrentRequests:F2}ms");

        var auditLogs = await _context.Set<AuditLog>().ToListAsync();
        auditLogs.Should().HaveCount(concurrentRequests);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000); // Should complete within 15 seconds
    }

    [Fact]
    public async Task Bulk_Audit_Operations_Should_Be_Efficient()
    {
        // Arrange
        const int bulkSize = 1000;
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < bulkSize; i++)
        {
            await _auditService.LogAsync(new CreateAuditLogRequest
            {
                ActionType = $"BulkTest_{i}",
                ResourceType = "TestResource",
                ResourceId = i.ToString(),
                UserId = Guid.NewGuid(),
                Description = $"Bulk performance test {i}",
                Success = i % 2 == 0, // Mix of success/failure
                RiskLevel = (AuditRiskLevel)(i % 4), // Cycle through risk levels
                Category = (AuditCategory)(i % 5) // Cycle through categories
            });
        }

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Bulk audit logging ({bulkSize} requests) execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average per request: {stopwatch.ElapsedMilliseconds / (double)bulkSize:F2}ms");

        var auditLogs = await _context.Set<AuditLog>().CountAsync();
        auditLogs.Should().Be(bulkSize);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // Should complete within 10 seconds
    }

    [Fact]
    public async Task Audit_Query_Performance_Should_Be_Acceptable()
    {
        // Arrange - Create test data
        const int testDataSize = 500;
        var testUserId = Guid.NewGuid();
        var testTenantId = Guid.NewGuid();

        for (int i = 0; i < testDataSize; i++)
        {
            await _auditService.LogAsync(new CreateAuditLogRequest
            {
                ActionType = $"QueryTest_{i}",
                ResourceType = "TestResource",
                UserId = i < testDataSize / 2 ? testUserId : Guid.NewGuid(),
                TenantId = i < testDataSize / 4 ? testTenantId : Guid.NewGuid(),
                Description = $"Query performance test {i}",
                Success = true,
                Category = AuditCategory.General
            });
        }

        var query = new AuditLogQuery
        {
            UserId = testUserId,
            TenantId = testTenantId,
            Category = AuditCategory.General,
            Skip = 0,
            Take = 50
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        var auditLogs = await _auditService.GetAuditLogsAsync(query);
        var count = await _auditService.GetAuditLogCountAsync(query);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Audit query execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Query returned {auditLogs.Count} records out of {count} total matching");

        auditLogs.Should().NotBeEmpty();
        auditLogs.Should().OnlyContain(log => log.UserId == testUserId);
        auditLogs.Should().OnlyContain(log => log.TenantId == testTenantId);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500); // Should complete within 500ms
    }

    [Fact]
    public async Task Large_Metadata_Serialization_Should_Be_Efficient()
    {
        // Arrange - Create large metadata object
        var largeMetadata = new Dictionary<string, object>();
        for (int i = 0; i < 100; i++)
        {
            largeMetadata[$"key_{i}"] = new
            {
                Id = Guid.NewGuid(),
                Name = $"Test Object {i}",
                Description = new string('A', 500), // 500 character string
                Values = Enumerable.Range(0, 50).ToArray()
            };
        }

        var request = new CreateAuditLogRequest
        {
            ActionType = "LargeMetadataTest",
            ResourceType = "TestResource",
            ResourceId = Guid.NewGuid().ToString(),
            UserId = Guid.NewGuid(),
            Description = "Large metadata performance test",
            Metadata = largeMetadata,
            Success = true,
            RiskLevel = AuditRiskLevel.Medium,
            Category = AuditCategory.General
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        await _auditService.LogAsync(request);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Large metadata audit log execution time: {stopwatch.ElapsedMilliseconds}ms");

        var auditLog = await _context.Set<AuditLog>().FirstOrDefaultAsync();
        auditLog.Should().NotBeNull();
        auditLog!.Metadata.Should().NotBeNull();
        auditLog.Metadata!.Length.Should().BeGreaterThan(1000); // Should contain serialized large object
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200); // Should complete within 200ms
    }

    [Fact]
    public async Task Authentication_Audit_Methods_Should_Be_Performant()
    {
        // Arrange
        const int iterations = 100;
        var userIds = Enumerable.Range(0, iterations).Select(_ => Guid.NewGuid()).ToList();

        await _auditService.LogAuthenticationAsync("Warmup", Guid.NewGuid(), success: true);
        var warmupLogs = await _context.Set<AuditLog>().ToListAsync();
        _context.RemoveRange(warmupLogs);
        await _context.SaveChangesAsync();

        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < iterations; i++)
        {
            var userId = userIds[i];
            var success = i % 2 == 0;
            var errorMessage = success ? null : "Invalid credentials";

            tasks.Add(_auditService.LogAuthenticationAsync("Login", userId, success, errorMessage));
        }

        await Task.WhenAll(tasks);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Authentication audit methods ({iterations} calls) execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average per call: {stopwatch.ElapsedMilliseconds / (double)iterations:F2}ms");

        var auditLogs = await _context.Set<AuditLog>()
            .Where(log => log.Category == AuditCategory.Authentication)
            .CountAsync();

        auditLogs.Should().Be(iterations);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000); // Should complete within 2 seconds
    }

    [Fact]
    public async Task Permission_Audit_Methods_Should_Scale_Well()
    {
        // Arrange
        const int iterations = 200;
        var userIds = Enumerable.Range(0, iterations / 2).Select(_ => Guid.NewGuid()).ToList();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < iterations; i++)
        {
            var userId = userIds[i % userIds.Count];
            var permissionName = $"Permission_{i % 10}";
            var resourceType = $"Resource_{i % 5}";
            var resourceId = $"resource_{i}";

            if (i % 2 == 0)
            {
                tasks.Add(_auditService.LogPermissionGrantAsync(userId, permissionName, resourceType, resourceId));
            }
            else
            {
                tasks.Add(_auditService.LogPermissionDenyAsync(userId, permissionName, resourceType, resourceId, "Access denied"));
            }
        }

        await Task.WhenAll(tasks);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Permission audit methods ({iterations} calls) execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average per call: {stopwatch.ElapsedMilliseconds / (double)iterations:F2}ms");

        var auditLogs = await _context.Set<AuditLog>()
            .Where(log => log.Category == AuditCategory.Permission)
            .CountAsync();

        auditLogs.Should().Be(iterations);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(15000); // Should complete within 15 seconds
    }

    [Fact]
    public async Task Memory_Usage_Should_Be_Reasonable_Under_Heavy_Load()
    {
        // Arrange
        const int iterations = 2000;
        var initialMemory = GC.GetTotalMemory(true);

        // Act
        for (int i = 0; i < iterations; i++)
        {
            await _auditService.LogAsync(new CreateAuditLogRequest
            {
                ActionType = $"MemoryTest_{i}",
                ResourceType = "TestResource",
                ResourceId = i.ToString(),
                UserId = Guid.NewGuid(),
                Description = $"Memory performance test {i}",
                Metadata = new { Iteration = i, Timestamp = DateTime.UtcNow },
                Success = true,
                RiskLevel = AuditRiskLevel.Low,
                Category = AuditCategory.General
            });

            // Force garbage collection every 200 iterations
            if (i % 200 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        // Force final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Memory usage after {iterations} audit log operations:");
        _output.WriteLine($"Initial: {initialMemory / 1024 / 1024:F2} MB");
        _output.WriteLine($"Final: {finalMemory / 1024 / 1024:F2} MB");
        _output.WriteLine($"Increase: {memoryIncrease / 1024 / 1024:F2} MB");

        var auditLogs = await _context.Set<AuditLog>().CountAsync();
        auditLogs.Should().Be(iterations);

        // Memory increase should be reasonable (less than 20MB for 2000 operations with metadata)
        memoryIncrease.Should().BeLessThan(20 * 1024 * 1024);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _context.Dispose();
    }
}
