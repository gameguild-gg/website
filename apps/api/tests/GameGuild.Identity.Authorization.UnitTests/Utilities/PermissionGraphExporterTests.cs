using System.Xml.Linq;
using FluentAssertions;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Authorization.Utilities;

namespace GameGuild.Identity.Authorization.UnitTests.Utilities;

/// <summary>
///     Tests for the DOT (Graphviz) and GraphML exports of the permission graph (issue #334).
/// </summary>
public class PermissionGraphExporterTests
{
    private static PermissionGraph BuildSampleGraph()
    {
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        return new PermissionGraph
        {
            TenantId = tenantId,
            IncludesUsers = true,
            Nodes =
            [
                new PermissionGraphNode { Id = $"role:{roleId}", Type = PermissionGraphNodeType.Role, Label = "Editor" },
                new PermissionGraphNode { Id = $"user:{userId}", Type = PermissionGraphNodeType.User, Label = userId.ToString() },
                new PermissionGraphNode { Id = "perm:content:read", Type = PermissionGraphNodeType.Permission, Label = "content:read" }
            ],
            Edges =
            [
                new PermissionGraphEdge { SourceId = $"user:{userId}", TargetId = $"role:{roleId}", Type = PermissionGraphEdgeType.UserAssignedRole },
                new PermissionGraphEdge { SourceId = $"role:{roleId}", TargetId = "perm:content:read", Type = PermissionGraphEdgeType.RoleGrantsPermission }
            ]
        };
    }

    [Fact]
    public void ToDot_RendersDigraphWithNodesAndTypedEdges()
    {
        var dot = PermissionGraphExporter.ToDot(BuildSampleGraph());

        dot.Should().StartWith("digraph PermissionGraph {");
        dot.Should().Contain("\"role:").And.Contain("Editor");
        dot.Should().Contain("shape=box").And.Contain("shape=ellipse");
        dot.Should().Contain("\"assigned\"");
        dot.Should().Contain("\"grants\"");
        dot.TrimEnd().Should().EndWith("}");
    }

    [Fact]
    public void ToDot_EscapesQuotesInLabels()
    {
        var graph = new PermissionGraph
        {
            Nodes = [new PermissionGraphNode { Id = "role:x", Type = PermissionGraphNodeType.Role, Label = "we\"ird" }],
            Edges = []
        };

        var dot = PermissionGraphExporter.ToDot(graph);

        dot.Should().Contain("we\\\"ird");
    }

    [Fact]
    public void ToGraphMl_ProducesWellFormedXmlWithNodesAndEdges()
    {
        var graphMl = PermissionGraphExporter.ToGraphMl(BuildSampleGraph());

        var document = XDocument.Parse(graphMl);
        document.Root!.Name.LocalName.Should().Be("graphml");

        var ns = (XNamespace)"http://graphml.graphdrawing.org/xmlns";
        var nodes = document.Root!.Descendants(ns + "node").ToList();
        var edges = document.Root!.Descendants(ns + "edge").ToList();

        nodes.Should().HaveCount(3);
        edges.Should().HaveCount(2);
        nodes.Should().Contain(n => n.Attribute("id")!.Value.StartsWith("user:"));
        edges.Should().Contain(e => e.Attribute("source")!.Value.StartsWith("user:"));
    }

    [Fact]
    public void Exports_ThrowOnNullGraph()
    {
        var action = () => PermissionGraphExporter.ToDot(null!);
        var actionGraphMl = () => PermissionGraphExporter.ToGraphMl(null!);

        action.Should().Throw<ArgumentNullException>();
        actionGraphMl.Should().Throw<ArgumentNullException>();
    }
}
