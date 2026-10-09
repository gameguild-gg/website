using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization.Controllers;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Authorization.Queries;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Controller-level tests for the permission graph endpoints (issue #334): JSON
///     default, DOT/GraphML export, and impact-analysis pass-through.
/// </summary>
public class PermissionGraphControllerTests
{
    private readonly Mock<ISender> _sender = new();

    private PermissionGraphController CreateController() => new(_sender.Object);

    [Fact]
    public async Task GetGraph_DefaultFormat_ReturnsJsonGraph()
    {
        var graph = new PermissionGraph { TenantId = Guid.NewGuid() };
        _sender.Setup(s => s.Send(It.Is<GetPermissionGraphQuery>(q => q.IncludeUsers), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);

        var result = await CreateController().GetGraph(includeUsers: true, format: GraphExportFormat.JSON);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(graph);
        _sender.Verify(s => s.Send(It.Is<GetPermissionGraphQuery>(q => q.IncludeUsers), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGraph_DotFormat_ReturnsGraphvizContent()
    {
        _sender.Setup(s => s.Send(It.IsAny<GetPermissionGraphQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionGraph());

        var result = await CreateController().GetGraph(includeUsers: false, format: GraphExportFormat.DOT);

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.ContentType.Should().StartWith("text/vnd.graphviz");
        content.Content.Should().StartWith("digraph PermissionGraph {");
    }

    [Fact]
    public async Task GetGraph_GraphMlFormat_ReturnsXmlContent()
    {
        _sender.Setup(s => s.Send(It.IsAny<GetPermissionGraphQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionGraph());

        var result = await CreateController().GetGraph(includeUsers: true, format: GraphExportFormat.GraphML);

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.ContentType.Should().StartWith("application/xml");
        content.Content.Should().Contain("<graphml");
    }

    [Fact]
    public async Task AnalyzeRoleDeletion_ReturnsImpact()
    {
        var roleId = Guid.NewGuid();
        var impact = new RoleDeletionImpact { RoleFound = true, RoleId = roleId };
        _sender.Setup(s => s.Send(It.Is<AnalyzeRoleDeletionQuery>(q => q.RoleId == roleId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(impact);

        var result = await CreateController().AnalyzeRoleDeletion(roleId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(impact);
    }

    [Fact]
    public async Task AnalyzePermissionRemoval_PassesKeyWithColonsThroughRouteValue()
    {
        var roleId = Guid.NewGuid();
        var impact = new PermissionRemovalImpact { RoleFound = true, RoleId = roleId, PermissionKey = "content:read" };
        _sender.Setup(s => s.Send(
                It.Is<AnalyzePermissionRemovalQuery>(q => q.RoleId == roleId && q.PermissionKey == "content:read"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(impact);

        var result = await CreateController().AnalyzePermissionRemoval(roleId, "content:read");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(impact);
    }
}
