using GameGuild.CQRS;
using GameGuild.Finance.Ledgers.Abstractions;
using GameGuild.Finance.Ledgers.Commands;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Models;
using GameGuild.Finance.Ledgers.Queries;

namespace GameGuild.Finance.Ledgers.Handlers;

/// <summary>
/// Maps Ledger entity to LedgerDto with correct parameter order.
/// </summary>
internal static class LedgerMapper
{
    public static LedgerDto MapToDto(Entities.Ledger ledger, string? parentLedgerCode = null, int childCount = 0) => new(
        Id: ledger.Id,
        TenantId: ledger.TenantId,
        Code: ledger.Code,
        Name: ledger.Name,
        Description: ledger.Description,
        Slug: ledger.Slug,
        Type: ledger.Type,
        Status: ledger.Status,
        CurrencyCode: ledger.CurrencyCode,
        FiscalYearStartMonth: ledger.FiscalYearStartMonth,
        ParentLedgerId: ledger.ParentLedgerId,
        ParentLedgerCode: parentLedgerCode,
        HierarchyPath: ledger.HierarchyPath,
        HierarchyDepth: ledger.HierarchyDepth,
        IsShared: ledger.IsShared,
        AllowDirectEntries: ledger.AllowDirectEntries,
        BudgetLimit: ledger.BudgetLimit,
        ProjectStartDate: ledger.ProjectStartDate,
        ProjectEndDate: ledger.ProjectEndDate,
        CachedNetBalance: ledger.CachedNetBalance,
        CachedEntryCount: ledger.CachedEntryCount,
        StatsCalculatedAt: ledger.StatsCalculatedAt,
        Tags: ledger.Tags.AsReadOnly(),
        ChildCount: childCount,
        CreatedByUserId: ledger.CreatedByUserId,
        CreatedAt: ledger.CreatedAt,
        UpdatedAt: ledger.UpdatedAt);
}

// ============================================================================
// Ledger Command Handlers
// ============================================================================

public class CreateRootLedgerHandler(ILedgerHierarchyService hierarchyService)
    : IRequestHandler<CreateRootLedgerCommand, LedgerDto>
{
    public async Task<LedgerDto> Handle(CreateRootLedgerCommand request, CancellationToken ct)
    {
        var ledger = await hierarchyService.CreateRootLedgerAsync(
            request.TenantId,
            request.Code,
            request.Name,
            request.CurrencyCode,
            request.CreatedByUserId,
            request.Description,
            ct);

        return LedgerMapper.MapToDto(ledger);
    }
}

public class CreateChildLedgerHandler(ILedgerHierarchyService hierarchyService)
    : IRequestHandler<CreateChildLedgerCommand, LedgerDto>
{
    public async Task<LedgerDto> Handle(CreateChildLedgerCommand request, CancellationToken ct)
    {
        var ledger = await hierarchyService.CreateChildLedgerAsync(
            request.ParentLedgerId,
            request.Type,
            request.Code,
            request.Name,
            request.CreatedByUserId,
            request.Description,
            request.CurrencyCode,
            ct);

        return LedgerMapper.MapToDto(ledger);
    }
}

// ============================================================================
// Ledger Query Handlers
// ============================================================================

public class GetLedgerByIdHandler(ILedgerRepository repository)
    : IRequestHandler<GetLedgerByIdQuery, LedgerDto?>
{
    public async Task<LedgerDto?> Handle(GetLedgerByIdQuery request, CancellationToken ct)
    {
        var ledger = await repository.GetByIdAsync(request.LedgerId, ct);
        return ledger is null ? null : LedgerMapper.MapToDto(ledger);
    }
}

public class GetLedgerByCodeHandler(ILedgerRepository repository)
    : IRequestHandler<GetLedgerByCodeQuery, LedgerDto?>
{
    public async Task<LedgerDto?> Handle(GetLedgerByCodeQuery request, CancellationToken ct)
    {
        var ledger = await repository.GetByCodeAsync(request.TenantId, request.Code, ct);
        return ledger is null ? null : LedgerMapper.MapToDto(ledger);
    }
}

public class GetLedgersByTenantHandler(ILedgerRepository repository)
    : IRequestHandler<GetLedgersByTenantQuery, IReadOnlyList<LedgerDto>>
{
    public async Task<IReadOnlyList<LedgerDto>> Handle(GetLedgersByTenantQuery request, CancellationToken ct)
    {
        var ledgers = await repository.GetByTenantAsync(request.TenantId, ct);
        return ledgers.Select(l => LedgerMapper.MapToDto(l)).ToList();
    }
}

public class GetRootLedgersHandler(ILedgerRepository repository)
    : IRequestHandler<GetRootLedgersQuery, IReadOnlyList<LedgerDto>>
{
    public async Task<IReadOnlyList<LedgerDto>> Handle(GetRootLedgersQuery request, CancellationToken ct)
    {
        var ledgers = await repository.GetRootLedgersAsync(request.TenantId, ct);
        return ledgers.Select(l => LedgerMapper.MapToDto(l)).ToList();
    }
}

public class GetLedgerChildrenHandler(ILedgerRepository repository)
    : IRequestHandler<GetLedgerChildrenQuery, IReadOnlyList<LedgerDto>>
{
    public async Task<IReadOnlyList<LedgerDto>> Handle(GetLedgerChildrenQuery request, CancellationToken ct)
    {
        var ledgers = await repository.GetChildrenAsync(request.ParentLedgerId, ct);
        return ledgers.Select(l => LedgerMapper.MapToDto(l)).ToList();
    }
}

public class GetLedgerHierarchyHandler(ILedgerHierarchyService hierarchyService)
    : IRequestHandler<GetLedgerHierarchyQuery, LedgerTreeNode>
{
    public async Task<LedgerTreeNode> Handle(GetLedgerHierarchyQuery request, CancellationToken ct)
    {
        return await hierarchyService.GetHierarchyTreeAsync(request.RootLedgerId, request.MaxDepth, ct);
    }
}

public class GetLedgerAncestorsHandler(ILedgerHierarchyService hierarchyService)
    : IRequestHandler<GetLedgerAncestorsQuery, IReadOnlyList<LedgerDto>>
{
    public async Task<IReadOnlyList<LedgerDto>> Handle(GetLedgerAncestorsQuery request, CancellationToken ct)
    {
        var ledgers = await hierarchyService.GetAncestorPathAsync(request.LedgerId, ct);
        return ledgers.Select(l => LedgerMapper.MapToDto(l)).ToList();
    }
}

public class GetLedgerDescendantsHandler(ILedgerClosureRepository closureRepository, ILedgerRepository ledgerRepository)
    : IRequestHandler<GetLedgerDescendantsQuery, IReadOnlyList<LedgerDto>>
{
    public async Task<IReadOnlyList<LedgerDto>> Handle(GetLedgerDescendantsQuery request, CancellationToken ct)
    {
        var closures = await closureRepository.GetDescendantsAsync(request.LedgerId, request.MaxDepth, ct);
        var result = new List<LedgerDto>();

        foreach (var closure in closures)
        {
            var ledger = closure.Descendant ?? await ledgerRepository.GetByIdAsync(closure.DescendantId, ct);
            if (ledger != null)
            {
                result.Add(LedgerMapper.MapToDto(ledger));
            }
        }

        return result;
    }
}

public class GetVirtualLedgersHandler(ILedgerRepository repository)
    : IRequestHandler<GetVirtualLedgersQuery, IReadOnlyList<LedgerDto>>
{
    public async Task<IReadOnlyList<LedgerDto>> Handle(GetVirtualLedgersQuery request, CancellationToken ct)
    {
        var ledgers = await repository.GetVirtualLedgersAsync(request.TenantId, ct);
        return ledgers.Select(l => LedgerMapper.MapToDto(l)).ToList();
    }
}
