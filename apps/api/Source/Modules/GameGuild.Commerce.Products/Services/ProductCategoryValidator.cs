namespace GameGuild.Commerce.Products;

/// <summary>
///     Validates hierarchical-catalog invariants for product categories (issue #401):
///     self-parenting, cycles and depth beyond <see cref="ProductCategory.MaxDepth"/>
///     are rejected before any write.
/// </summary>
public interface IProductCategoryValidator
{
    /// <summary>
    ///     Validates that <paramref name="categoryId"/> may use
    /// <paramref name="newParentCategoryId"/> as its parent.
    /// </summary>
    /// <param name="categoryId">Category being moved or created (empty for creates).</param>
    /// <param name="newParentCategoryId">Candidate parent (null = root).</param>
    /// <param name="allCategoryParents">
    ///     Id → parent-id map of every (non-deleted) category in the tenant, used to
    ///     walk ancestor chains and detect cycles without recursive queries.
    /// </param>
    CategoryValidationResult ValidateParent(
        Guid categoryId,
        Guid? newParentCategoryId,
        IReadOnlyDictionary<Guid, Guid?> allCategoryParents);
}

/// <summary>Result of a category-hierarchy validation.</summary>
public sealed class CategoryValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public CategoryValidationError? Error { get; init; }

    private CategoryValidationResult() { }

    public static CategoryValidationResult Success() => new() { IsValid = true };

    public static CategoryValidationResult Failure(string message, CategoryValidationError error) =>
        new() { IsValid = false, ErrorMessage = message, Error = error };
}

/// <summary>Error codes for category-hierarchy validation failures.</summary>
public enum CategoryValidationError
{
    None,
    SelfParent,
    CycleDetected,
    DepthExceeded,
    UnknownParent
}

/// <summary>
///     Deterministic, allocation-free hierarchy checks. All rules are pure functions of
///     the id → parent map so they can be unit-tested without a database.
/// </summary>
public sealed class ProductCategoryValidator : IProductCategoryValidator
{
    /// <inheritdoc />
    public CategoryValidationResult ValidateParent(
        Guid categoryId,
        Guid? newParentCategoryId,
        IReadOnlyDictionary<Guid, Guid?> allCategoryParents)
    {
        ArgumentNullException.ThrowIfNull(allCategoryParents);

        if (newParentCategoryId is null)
            return CategoryValidationResult.Success();

        if (newParentCategoryId == categoryId)
        {
            return CategoryValidationResult.Failure(
                "A category cannot be its own parent",
                CategoryValidationError.SelfParent);
        }

        if (!allCategoryParents.ContainsKey(newParentCategoryId.Value))
        {
            return CategoryValidationResult.Failure(
                $"Parent category {newParentCategoryId} does not exist",
                CategoryValidationError.UnknownParent);
        }

        // Walk up from the candidate parent: the category itself appearing in the
        // ancestor chain means the move would close a cycle. The walk is bounded by
        // MaxDepth + 1 so a pre-existing corrupt loop cannot hang the validator.
        var steps = 0;
        var ancestorId = newParentCategoryId;
        while (ancestorId is not null && steps <= ProductCategory.MaxDepth)
        {
            if (ancestorId.Value == categoryId)
            {
                return CategoryValidationResult.Failure(
                    $"Setting category {categoryId} under {newParentCategoryId} would create a cycle",
                    CategoryValidationError.CycleDetected);
            }

            if (!allCategoryParents.TryGetValue(ancestorId.Value, out var next))
            {
                return CategoryValidationResult.Failure(
                    $"Ancestor category {ancestorId} does not exist",
                    CategoryValidationError.UnknownParent);
            }

            ancestorId = next;
            steps++;
        }

        // Depth of the subtree rooted at categoryId (longest branch) + the chain above
        // the new parent + 1 for the moved node itself must stay within MaxDepth.
        var parentDepth = GetDepth(newParentCategoryId.Value, allCategoryParents);
        if (parentDepth < 0)
        {
            return CategoryValidationResult.Failure(
                $"Ancestor chain of {newParentCategoryId} contains a cycle",
                CategoryValidationError.CycleDetected);
        }

        var subtreeHeight = GetSubtreeHeight(categoryId, allCategoryParents, allCategoryParents.ContainsKey(categoryId));
        if (parentDepth + 1 + subtreeHeight > ProductCategory.MaxDepth)
        {
            return CategoryValidationResult.Failure(
                $"Category tree cannot exceed {ProductCategory.MaxDepth} levels "
                + $"(parent depth {parentDepth} + subtree height {subtreeHeight} + 1)",
                CategoryValidationError.DepthExceeded);
        }

        return CategoryValidationResult.Success();
    }

    /// <summary>
    ///     Depth of a node in the tree (root = 0). Returns -1 when the ancestor chain
    ///     cycles or exceeds <see cref="ProductCategory.MaxDepth"/> (corrupt data guard).
    /// </summary>
    internal static int GetDepth(Guid categoryId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        var depth = 0;
        var current = categoryId;
        var visited = new HashSet<Guid>();
        while (true)
        {
            if (!visited.Add(current))
                return -1;

            if (!parents.TryGetValue(current, out var parent) || parent is null)
                return depth;

            current = parent.Value;
            depth++;

            if (depth > ProductCategory.MaxDepth)
                return -1;
        }
    }

    /// <summary>
    ///     Height of the subtree rooted at <paramref name="categoryId"/> (leaf = 0).
    /// Unknown nodes count as leaves. Bounded by MaxDepth per branch.
    /// </summary>
    internal static int GetSubtreeHeight(Guid categoryId, IReadOnlyDictionary<Guid, Guid?> parents, bool categoryExists)
    {
        if (!categoryExists)
            return 0;

        var childrenByParent = new Dictionary<Guid, List<Guid>>();
        foreach (var (id, parent) in parents)
        {
            if (parent is null)
                continue;

            if (!childrenByParent.TryGetValue(parent.Value, out var children))
            {
                children = [];
                childrenByParent[parent.Value] = children;
            }

            children.Add(id);
        }

        int Height(Guid node, ISet<Guid> seen)
        {
            if (!childrenByParent.TryGetValue(node, out var children) || children.Count == 0)
                return 0;

            var best = 0;
            foreach (var child in children)
            {
                if (!seen.Add(child))
                    continue; // corrupt cycle: ignore the branch rather than loop

                best = Math.Max(best, 1 + Height(child, seen));
                seen.Remove(child);
            }

            return best;
        }

        return Height(categoryId, new HashSet<Guid> { categoryId });
    }
}
