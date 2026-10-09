using FluentAssertions;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Authorization.Queries;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Handlers;

/// <summary>
///     Handler-level tests for the permission graph queries (issue #334): the tenant
///     scope always comes from the request context, never from the route.
/// </summary>
public class PermissionGraphQueryHandlerTests
{
    private readonly Mock<IPermissionGraphService> _graphService = new();
    private readonly Mock<IPermissionImpactAnalysisService> _impactService = new();
    private readonly Mock<IAuthorizationTenantContext> _tenantContext = new();

    [Fact]
    public async Task GetPermissionGraph_UsesTenantFromContext()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.Setup(c => c.TenantId).Returns(tenantId);
        var graph = new PermissionGraph { TenantId = tenantId };
        _graphService.Setup(s => s.BuildGraphAsync(tenantId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);

        var result = await new GetPermissionGraphHandler(_graphService.Object, _tenantContext.Object)
            .Handle(new GetPermissionGraphQuery(IncludeUsers: true), CancellationToken.None);

        result.Should().BeSameAs(graph);
        _graphService.Verify(s => s.BuildGraphAsync(tenantId, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeRoleDeletion_UsesTenantFromContext()
    {
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        _tenantContext.Setup(c => c.TenantId).Returns(tenantId);
        var impact = new RoleDeletionImpact { RoleFound = true, RoleId = roleId };
        _impactService.Setup(s => s.AnalyzeRoleDeletionAsync(tenantId, roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(impact);

        var result = await new AnalyzeRoleDeletionHandler(_impactService.Object, _tenantContext.Object)
            .Handle(new AnalyzeRoleDeletionQuery(roleId), CancellationToken.None);

        result.Should().BeSameAs(impact);
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_UsesTenantFromContext()
    {
        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        _tenantContext.Setup(c => c.TenantId).Returns(tenantId);
        var impact = new PermissionRemovalImpact { RoleFound = true, RoleId = roleId };
        _impactService.Setup(s => s.AnalyzePermissionRemovalAsync(tenantId, roleId, "content:read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(impact);

        var result = await new AnalyzePermissionRemovalHandler(_impactService.Object, _tenantContext.Object)
            .Handle(new AnalyzePermissionRemovalQuery(roleId, "content:read"), CancellationToken.None);

        result.Should().BeSameAs(impact);
    }
}
