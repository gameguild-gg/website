using GameGuild.CQRS;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Models;

namespace GameGuild.Finance.Ledgers.Queries;

// ============================================================================
// Ledger Queries
// ============================================================================

/// <summary>
/// Gets a ledger by ID.
/// </summary>
public record GetLedgerByIdQuery(
    Guid LedgerId) : IRequest<LedgerDto?>;

/// <summary>
/// Gets a ledger by code within tenant.
/// </summary>
public record GetLedgerByCodeQuery(
    Guid TenantId,
    string Code) : IRequest<LedgerDto?>;

/// <summary>
/// Gets all ledgers for a tenant.
/// </summary>
public record GetLedgersByTenantQuery(
    Guid TenantId,
    LedgerStatus? Status = null,
    LedgerType? Type = null,
    int Skip = 0,
    int Take = 50) : IRequest<IReadOnlyList<LedgerDto>>;

/// <summary>
/// Gets root ledgers for a tenant.
/// </summary>
public record GetRootLedgersQuery(
    Guid TenantId) : IRequest<IReadOnlyList<LedgerDto>>;

/// <summary>
/// Gets direct children of a ledger.
/// </summary>
public record GetChildLedgersQuery(
    Guid ParentLedgerId) : IRequest<IReadOnlyList<LedgerSummary>>;

/// <summary>
/// Gets direct children of a ledger (alternative name).
/// </summary>
public record GetLedgerChildrenQuery(
    Guid ParentLedgerId) : IRequest<IReadOnlyList<LedgerDto>>;

/// <summary>
/// Gets the hierarchy tree starting from a ledger.
/// </summary>
public record GetLedgerHierarchyQuery(
    Guid RootLedgerId,
    int? MaxDepth = null) : IRequest<LedgerTreeNode>;

/// <summary>
/// Gets the ancestor path from root to ledger.
/// </summary>
public record GetLedgerAncestorsQuery(
    Guid LedgerId) : IRequest<IReadOnlyList<LedgerDto>>;

/// <summary>
/// Gets descendants of a ledger.
/// </summary>
public record GetLedgerDescendantsQuery(
    Guid LedgerId,
    int? MaxDepth = null) : IRequest<IReadOnlyList<LedgerDto>>;

/// <summary>
/// Gets virtual ledgers for a tenant.
/// </summary>
public record GetVirtualLedgersQuery(
    Guid TenantId) : IRequest<IReadOnlyList<LedgerDto>>;

/// <summary>
/// Searches ledgers.
/// </summary>
public record SearchLedgersQuery(
    Guid TenantId,
    string? SearchTerm = null,
    LedgerType? Type = null,
    LedgerStatus? Status = null,
    IEnumerable<string>? Tags = null,
    int Skip = 0,
    int Take = 50) : IRequest<PagedResult<LedgerSummary>>;

// ============================================================================
// Entry Queries
// ============================================================================

/// <summary>
/// Gets an entry by ID.
/// </summary>
public record GetEntryByIdQuery(
    Guid EntryId) : IRequest<LedgerEntryDto?>;

/// <summary>
/// Gets an entry by reference number.
/// </summary>
public record GetEntryByReferenceQuery(
    Guid TenantId,
    string ReferenceNumber) : IRequest<LedgerEntryDto?>;

/// <summary>
/// Gets entries for a ledger (direct only).
/// </summary>
public record GetLedgerEntriesQuery(
    Guid LedgerId,
    EntryStatus? Status = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Skip = 0,
    int Take = 50) : IRequest<PagedResult<LedgerEntryDto>>;

/// <summary>
/// Gets entries for a ledger (direct only) - returns list.
/// </summary>
public record GetEntriesByLedgerQuery(
    Guid LedgerId,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null) : IRequest<IReadOnlyList<LedgerEntryDto>>;

/// <summary>
/// Gets entries for a ledger including descendants.
/// </summary>
public record GetLedgerEntriesWithDescendantsQuery(
    Guid LedgerId,
    EntryStatus? Status = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Skip = 0,
    int Take = 50) : IRequest<PagedResult<LedgerEntryDto>>;

/// <summary>
/// Gets entries for a ledger including descendants - returns list.
/// </summary>
public record GetEntriesIncludingDescendantsQuery(
    Guid LedgerId,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null) : IRequest<IReadOnlyList<LedgerEntryDto>>;

/// <summary>
/// Gets entries for a virtual ledger.
/// </summary>
public record GetVirtualLedgerEntriesQuery(
    Guid VirtualLedgerId,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Skip = 0,
    int Take = 50) : IRequest<IReadOnlyList<LedgerEntryDto>>;

/// <summary>
/// Searches entries across tenant.
/// </summary>
public record SearchEntriesQuery(
    Guid TenantId,
    string? SearchTerm = null,
    Guid? LedgerId = null,
    bool IncludeDescendants = false,
    EntryType? Type = null,
    EntryCategory? Category = null,
    EntryStatus? Status = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    IEnumerable<string>? Tags = null,
    int Skip = 0,
    int Take = 50) : IRequest<PagedResult<LedgerEntryDto>>;

// ============================================================================
// Balance & Rollup Queries
// ============================================================================

/// <summary>
/// Gets balance for a ledger (direct entries only).
/// </summary>
public record GetLedgerBalanceQuery(
    Guid LedgerId,
    DateOnly? AsOfDate = null) : IRequest<LedgerBalance>;

/// <summary>
/// Gets consolidated balance including all descendants.
/// </summary>
public record GetConsolidatedBalanceQuery(
    Guid LedgerId,
    DateOnly? AsOfDate = null) : IRequest<LedgerBalance>;

/// <summary>
/// Gets hierarchical rollup for a subtree.
/// </summary>
public record GetHierarchicalRollupQuery(
    Guid RootLedgerId,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IRequest<LedgerRollupNode>;

/// <summary>
/// Gets rollup by category.
/// </summary>
public record GetCategoryRollupQuery(
    Guid LedgerId,
    bool IncludeDescendants = false,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IRequest<IReadOnlyList<CategoryRollup>>;

/// <summary>
/// Gets rollup by time period for trend analysis.
/// </summary>
public record GetPeriodRollupQuery(
    Guid LedgerId,
    DateOnly FromDate,
    DateOnly ToDate,
    PeriodGranularity Granularity,
    bool IncludeDescendants = false) : IRequest<IReadOnlyList<PeriodRollup>>;

/// <summary>
/// Gets balance for a virtual ledger.
/// </summary>
public record GetVirtualLedgerBalanceQuery(
    Guid VirtualLedgerId,
    DateOnly? AsOfDate = null) : IRequest<LedgerBalance>;
