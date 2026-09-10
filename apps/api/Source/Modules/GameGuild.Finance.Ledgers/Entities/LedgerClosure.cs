namespace GameGuild.Finance.Ledgers.Entities;

/// <summary>
/// Closure table entry for efficient hierarchical queries.
/// Each row represents an ancestor-descendant relationship at any depth.
/// Enables O(1) subtree queries and O(log n) insertions.
/// </summary>
/// <remarks>
/// For a tree: Root -> Dept -> Project
/// Closure entries would be:
/// - (Root, Root, 0)     - self-reference
/// - (Dept, Dept, 0)     - self-reference
/// - (Root, Dept, 1)     - Root is ancestor of Dept at depth 1
/// - (Project, Project, 0) - self-reference
/// - (Dept, Project, 1)  - Dept is ancestor of Project at depth 1
/// - (Root, Project, 2)  - Root is ancestor of Project at depth 2
/// </remarks>
public class LedgerClosure
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>The ancestor ledger in this relationship.</summary>
    public Guid AncestorId { get; private set; }

    /// <summary>Navigation to ancestor ledger.</summary>
    public Ledger Ancestor { get; private set; } = null!;

    /// <summary>The descendant ledger in this relationship.</summary>
    public Guid DescendantId { get; private set; }

    /// <summary>Navigation to descendant ledger.</summary>
    public Ledger Descendant { get; private set; } = null!;

    /// <summary>
    /// Distance between ancestor and descendant.
    /// 0 = self-reference, 1 = direct parent-child, 2 = grandparent, etc.
    /// </summary>
    public int Depth { get; private set; }

    /// <summary>
    /// Tenant discriminator (denormalized for efficient filtering).
    /// </summary>
    public Guid TenantId { get; private set; }

    // ========================================================================
    // Factory Methods
    // ========================================================================

    private LedgerClosure() { } // EF Core

    /// <summary>
    /// Creates a self-reference closure entry (every node has one).
    /// </summary>
    public static LedgerClosure CreateSelfReference(Ledger ledger)
    {
        return new LedgerClosure
        {
            Id = Guid.NewGuid(),
            AncestorId = ledger.Id,
            Ancestor = ledger,
            DescendantId = ledger.Id,
            Descendant = ledger,
            Depth = 0,
            TenantId = ledger.TenantId
        };
    }

    /// <summary>
    /// Creates closure entries for a new child node.
    /// Must be called with all ancestor closures of the parent.
    /// </summary>
    public static IEnumerable<LedgerClosure> CreateForNewChild(
        Ledger child,
        IEnumerable<LedgerClosure> parentAncestorClosures)
    {
        // Self-reference for the new child
        yield return CreateSelfReference(child);

        // Copy all parent's ancestor relationships, incrementing depth
        foreach (var parentClosure in parentAncestorClosures)
        {
            yield return new LedgerClosure
            {
                Id = Guid.NewGuid(),
                AncestorId = parentClosure.AncestorId,
                Ancestor = parentClosure.Ancestor,
                DescendantId = child.Id,
                Descendant = child,
                Depth = parentClosure.Depth + 1,
                TenantId = child.TenantId
            };
        }
    }

    /// <summary>
    /// Creates a direct ancestor-descendant closure entry.
    /// </summary>
    public static LedgerClosure Create(
        Ledger ancestor,
        Ledger descendant,
        int depth)
    {
        if (ancestor.TenantId != descendant.TenantId)
            throw new InvalidOperationException("Ancestor and descendant must belong to same tenant.");

        return new LedgerClosure
        {
            Id = Guid.NewGuid(),
            AncestorId = ancestor.Id,
            Ancestor = ancestor,
            DescendantId = descendant.Id,
            Descendant = descendant,
            Depth = depth,
            TenantId = ancestor.TenantId
        };
    }
}
