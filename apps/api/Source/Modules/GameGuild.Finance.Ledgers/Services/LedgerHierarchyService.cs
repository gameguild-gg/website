using GameGuild.Finance.Ledgers.Abstractions;
using GameGuild.Finance.Ledgers.Entities;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Models;

namespace GameGuild.Finance.Ledgers.Services;

/// <summary>
/// Service for managing ledger hierarchy operations.
/// Coordinates Ledger CRUD with Closure table maintenance.
/// </summary>
public class LedgerHierarchyService : ILedgerHierarchyService
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly ILedgerClosureRepository _closureRepository;

    public LedgerHierarchyService(
        ILedgerRepository ledgerRepository,
        ILedgerClosureRepository closureRepository)
    {
        _ledgerRepository = ledgerRepository;
        _closureRepository = closureRepository;
    }

    /// <inheritdoc />
    public async Task<Ledger> CreateRootLedgerAsync(
        Guid tenantId,
        string code,
        string name,
        string currencyCode,
        Guid createdByUserId,
        string? description = null,
        CancellationToken ct = default)
    {
        // Validate code uniqueness
        if (await _ledgerRepository.ExistsAsync(tenantId, code, ct))
            throw new InvalidOperationException($"Ledger code '{code}' already exists.");

        // Create root ledger
        var ledger = Ledger.CreateRoot(tenantId, code, name, currencyCode, createdByUserId, description);
        await _ledgerRepository.AddAsync(ledger, ct);

        // Create self-referencing closure entry
        var selfClosure = LedgerClosure.CreateSelfReference(ledger);
        await _closureRepository.AddRangeAsync([selfClosure], ct);

        return ledger;
    }

    /// <inheritdoc />
    public async Task<Ledger> CreateChildLedgerAsync(
        Guid parentLedgerId,
        LedgerType type,
        string code,
        string name,
        Guid createdByUserId,
        string? description = null,
        string? currencyCode = null,
        CancellationToken ct = default)
    {
        // Get parent ledger
        var parent = await _ledgerRepository.GetByIdAsync(parentLedgerId, ct)
            ?? throw new InvalidOperationException($"Parent ledger '{parentLedgerId}' not found.");

        // Validate code uniqueness
        if (await _ledgerRepository.ExistsAsync(parent.TenantId, code, ct))
            throw new InvalidOperationException($"Ledger code '{code}' already exists.");

        // Create child ledger
        var child = Ledger.CreateChild(parent, type, code, name, createdByUserId, description, currencyCode);
        await _ledgerRepository.AddAsync(child, ct);

        // Get parent's ancestor closures (includes self-reference)
        var parentClosures = await _closureRepository.GetAncestorsAsync(parentLedgerId, ct);

        // Create closure entries for the new child
        var childClosures = LedgerClosure.CreateForNewChild(child, parentClosures);
        await _closureRepository.AddRangeAsync(childClosures, ct);

        return child;
    }

    /// <inheritdoc />
    public async Task MoveLedgerAsync(
        Guid ledgerId,
        Guid newParentLedgerId,
        Guid movedByUserId,
        CancellationToken ct = default)
    {
        // Validate move is allowed
        if (!await CanMoveToParentAsync(ledgerId, newParentLedgerId, ct))
            throw new InvalidOperationException("Cannot move ledger to the specified parent.");

        var ledger = await _ledgerRepository.GetByIdAsync(ledgerId, ct)
            ?? throw new InvalidOperationException($"Ledger '{ledgerId}' not found.");

        var newParent = await _ledgerRepository.GetByIdAsync(newParentLedgerId, ct)
            ?? throw new InvalidOperationException($"New parent ledger '{newParentLedgerId}' not found.");

        // Get the subtree (ledger + all descendants)
        var subtreeIds = await _closureRepository.GetDescendantIdsAsync(ledgerId, null, ct);

        // Delete all closure entries where any subtree node is a descendant
        // (This removes the old ancestry links)
        foreach (var subtreeId in subtreeIds)
        {
            await _closureRepository.DeleteByDescendantAsync(subtreeId, ct);
        }

        // Get new parent's ancestors (for rebuilding closures)
        var newParentClosures = await _closureRepository.GetAncestorsAsync(newParentLedgerId, ct);

        // Rebuild closures for the moved subtree
        await RebuildSubtreeClosuresAsync(ledger, newParentClosures.ToList(), ct);

        // Update ledger's parent reference and path
        // Note: This requires exposing a method on Ledger entity or using reflection
        // For now, we'll update via repository pattern
        await _ledgerRepository.UpdateAsync(ledger, ct);
    }

    /// <inheritdoc />
    public async Task<LedgerTreeNode> GetHierarchyTreeAsync(
        Guid rootLedgerId,
        int? maxDepth = null,
        CancellationToken ct = default)
    {
        var root = await _ledgerRepository.GetByIdAsync(rootLedgerId, ct)
            ?? throw new InvalidOperationException($"Ledger '{rootLedgerId}' not found.");

        return await BuildTreeNodeAsync(root, 0, maxDepth, ct);
    }

    private async Task<LedgerTreeNode> BuildTreeNodeAsync(
        Ledger ledger,
        int currentDepth,
        int? maxDepth,
        CancellationToken ct)
    {
        var node = new LedgerTreeNode
        {
            Id = ledger.Id,
            Code = ledger.Code,
            Name = ledger.Name,
            Type = ledger.Type,
            Status = ledger.Status,
            CurrencyCode = ledger.CurrencyCode,
            Depth = currentDepth,
            NetBalance = ledger.CachedNetBalance,
            EntryCount = ledger.CachedEntryCount
        };

        // If we haven't reached max depth, get children
        if (!maxDepth.HasValue || currentDepth < maxDepth.Value)
        {
            var children = await _ledgerRepository.GetChildrenAsync(ledger.Id, ct);
            foreach (var child in children)
            {
                var childNode = await BuildTreeNodeAsync(child, currentDepth + 1, maxDepth, ct);
                node.Children.Add(childNode);
            }
        }

        return node;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Ledger>> GetAncestorPathAsync(
        Guid ledgerId,
        CancellationToken ct = default)
    {
        var ancestorClosures = await _closureRepository.GetAncestorsAsync(ledgerId, ct);

        // Sort by depth descending (root first)
        return ancestorClosures
            .OrderByDescending(c => c.Depth)
            .Select(c => c.Ancestor)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> CanMoveToParentAsync(
        Guid ledgerId,
        Guid newParentLedgerId,
        CancellationToken ct = default)
    {
        // Cannot move to self
        if (ledgerId == newParentLedgerId)
            return false;

        // Cannot move to own descendant (would create cycle)
        var isDescendant = await _closureRepository.IsAncestorOfAsync(ledgerId, newParentLedgerId, ct);
        if (isDescendant)
            return false;

        var ledger = await _ledgerRepository.GetByIdAsync(ledgerId, ct);
        var newParent = await _ledgerRepository.GetByIdAsync(newParentLedgerId, ct);

        if (ledger == null || newParent == null)
            return false;

        // Must be same tenant
        if (ledger.TenantId != newParent.TenantId)
            return false;

        // Root ledgers cannot be moved
        if (ledger.Type == LedgerType.Root)
            return false;

        // Cannot move under virtual ledger
        if (newParent.Type == LedgerType.Virtual)
            return false;

        return true;
    }

    // ========================================================================
    // Private Helpers
    // ========================================================================

    private async Task RebuildSubtreeClosuresAsync(
        Ledger subtreeRoot,
        List<LedgerClosure> parentAncestorClosures,
        CancellationToken ct)
    {
        // Create closures for subtree root
        var rootClosures = LedgerClosure.CreateForNewChild(subtreeRoot, parentAncestorClosures);
        await _closureRepository.AddRangeAsync(rootClosures, ct);

        // Recursively handle children
        var children = await _ledgerRepository.GetChildrenAsync(subtreeRoot.Id, ct);
        var subtreeRootClosures = await _closureRepository.GetAncestorsAsync(subtreeRoot.Id, ct);

        foreach (var child in children)
        {
            await RebuildSubtreeClosuresAsync(child, subtreeRootClosures.ToList(), ct);
        }
    }
}
