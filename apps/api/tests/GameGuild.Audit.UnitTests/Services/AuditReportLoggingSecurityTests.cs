using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditReportLoggingSecurityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Dashboard_RedactsTenantInLogsAndPreservesReportScope(bool hasTenant)
    {
        Guid? tenantId = hasTenant ? Guid.NewGuid() : null;
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AuditReportPrivacy_{Guid.NewGuid()}").Options;
        await using var context = new TestApplicationDbContext(options);
        var permissions = new Mock<IPermissionAuditLogRepository>();
        permissions.Setup(value => value.GetByDateRangeAsync(start, end, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var logger = new Mock<ILogger<AuditReportService>>();
        var service = new AuditReportService(context, permissions.Object, Mock.Of<IAuditLogQueryService>(), logger.Object);

        var dashboard = await service.GetSecurityDashboardAsync(start, end, tenantId);

        dashboard.TenantId.Should().Be(tenantId);
        dashboard.StartDate.Should().Be(start);
        dashboard.EndDate.Should().Be(end);
        permissions.Verify(value => value.GetByDateRangeAsync(start, end, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        var invocation = logger.Invocations.Single(value => value.Method.Name == "Log");
        invocation.Arguments[3].Should().BeNull();
        var properties = ((IEnumerable<KeyValuePair<string, object?>>)invocation.Arguments[2]).ToDictionary(value => value.Key, value => value.Value);
        properties["TenantId"].Should().Be(LogRedaction.RedactId(tenantId, "tid"));
        var rendered = ((Delegate)invocation.Arguments[4]).DynamicInvoke(invocation.Arguments[2], invocation.Arguments[3])?.ToString();
        rendered.Should().NotBeNullOrEmpty().And.Contain(LogRedaction.RedactId(tenantId, "tid"));
        if (tenantId.HasValue)
        {
            rendered.Should().NotContain(tenantId.Value.ToString());
        }
    }
}
