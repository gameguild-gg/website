using System.Text;
using GameGuild.Identity.Authorization.Models;

namespace GameGuild.Identity.Authorization.Utilities;

/// <summary>
///     Renders a <see cref="PermissionGraph"/> to interchange formats consumable by
///     visualization tooling: DOT (Graphviz) and GraphML (yEd, Gephi).
/// </summary>
public static class PermissionGraphExporter
{
    /// <summary>
    ///     Renders the graph in the DOT language (Graphviz).
    /// </summary>
    /// <param name="graph">The graph to render.</param>
    /// <returns>The DOT document.</returns>
    public static string ToDot(PermissionGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var builder = new StringBuilder();
        builder.AppendLine("digraph PermissionGraph {");
        builder.AppendLine("  rankdir=LR;");

        foreach (var node in graph.Nodes)
        {
            var shape = node.Type switch
            {
                PermissionGraphNodeType.User => "box",
                PermissionGraphNodeType.Role => "ellipse",
                _ => "note"
            };
            builder.AppendLine($"  \"{node.Id}\" [label=\"{Escape(node.Label)}\", shape={shape}];");
        }

        foreach (var edge in graph.Edges)
        {
            builder.AppendLine($"  \"{edge.SourceId}\" -> \"{edge.TargetId}\" [label=\"{EdgeLabel(edge.Type)}\"];");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>
    ///     Renders the graph as GraphML (XML).
    /// </summary>
    /// <param name="graph">The graph to render.</param>
    /// <returns>The GraphML document.</returns>
    public static string ToGraphMl(PermissionGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.AppendLine("<graphml xmlns=\"http://graphml.graphdrawing.org/xmlns\">");
        builder.AppendLine("  <key id=\"nType\" for=\"node\" attr.name=\"type\" attr.type=\"string\"/>");
        builder.AppendLine("  <key id=\"eType\" for=\"edge\" attr.name=\"type\" attr.type=\"string\"/>");
        builder.AppendLine($"  <graph id=\"permission-graph-{graph.TenantId:N}\" edgedefault=\"directed\">");

        foreach (var node in graph.Nodes)
        {
            builder.AppendLine($"    <node id=\"{Escape(node.Id)}\">");
            builder.AppendLine($"      <data key=\"nType\">{Escape(node.Type.ToString())}</data>");
            builder.AppendLine("    </node>");
        }

        var edgeIndex = 0;
        foreach (var edge in graph.Edges)
        {
            builder.AppendLine($"    <edge id=\"e{edgeIndex++}\" source=\"{Escape(edge.SourceId)}\" target=\"{Escape(edge.TargetId)}\">");
            builder.AppendLine($"      <data key=\"eType\">{Escape(edge.Type.ToString())}</data>");
            builder.AppendLine("    </edge>");
        }

        builder.AppendLine("  </graph>");
        builder.AppendLine("</graphml>");
        return builder.ToString();
    }

    private static string EdgeLabel(PermissionGraphEdgeType type) => type switch
    {
        PermissionGraphEdgeType.RoleInheritsFrom => "inherits",
        PermissionGraphEdgeType.RoleGrantsPermission => "grants",
        PermissionGraphEdgeType.RoleDeniesPermission => "denies",
        PermissionGraphEdgeType.UserAssignedRole => "assigned",
        PermissionGraphEdgeType.UserDirectGrant => "direct grant",
        PermissionGraphEdgeType.UserDirectDeny => "direct deny",
        _ => type.ToString()
    };

    private static string Escape(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "\\\"");
}
