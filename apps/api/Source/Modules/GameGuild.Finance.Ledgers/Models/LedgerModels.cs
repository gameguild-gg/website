using GameGuild.Finance.Ledgers.Enums;

namespace GameGuild.Finance.Ledgers.Models;

// ============================================================================
// Request Models
// ============================================================================

/// <summary>
/// Request to create a ledger entry.
/// </summary>
public record CreateEntryRequest(
    Guid LedgerId,
    EntryType Type,
    EntryCategory Category,
    decimal Amount,
    DateOnly TransactionDate,
    string Description,
    string? ExternalReferenceId = null,
    Guid? BudgetId = null,
    string? BudgetCategoryCode = null,
    string? CounterpartyName = null,
    IEnumerable<string>? Tags = null);

/// <summary>
/// Request to create a ledger.
/// </summary>
public record CreateLedgerRequest(
    string Code,
    string Name,
    LedgerType Type,
    string? Description = null,
    string? CurrencyCode = null,
    Guid? ParentLedgerId = null,
    bool IsShared = false,
    decimal? BudgetLimit = null,
    DateOnly? ProjectStartDate = null,
    DateOnly? ProjectEndDate = null,
    IEnumerable<string>? Tags = null);

/// <summary>
/// Request to update a ledger.
/// </summary>
public record UpdateLedgerRequest(
    string Name,
    string? Description = null,
    LedgerStatus? Status = null,
    decimal? BudgetLimit = null,
    DateOnly? ProjectStartDate = null,
    DateOnly? ProjectEndDate = null,
    IEnumerable<string>? Tags = null);

/// <summary>
/// Filter specification for virtual ledgers.
/// </summary>
public record VirtualLedgerFilter(
    IEnumerable<Guid>? SourceLedgerIds = null,
    IEnumerable<LedgerType>? LedgerTypes = null,
    IEnumerable<EntryCategory>? Categories = null,
    IEnumerable<string>? Tags = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    string? CounterpartyPattern = null,
    bool IncludeDescendants = true);

// ============================================================================
// Response / DTO Models
// ============================================================================

/// <summary>
/// Ledger balance summary.
/// </summary>
public record LedgerBalance(
    Guid LedgerId,
    string LedgerCode,
    string LedgerName,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal NetBalance,
    int EntryCount,
    string CurrencyCode,
    DateTime CalculatedAt,
    bool IncludesDescendants = false);

/// <summary>
/// Ledger with balance for listing.
/// </summary>
public record LedgerSummary(
    Guid Id,
    string Code,
    string Name,
    LedgerType Type,
    LedgerStatus Status,
    string CurrencyCode,
    string HierarchyPath,
    int HierarchyDepth,
    Guid? ParentLedgerId,
    decimal NetBalance,
    int EntryCount,
    int ChildCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Tree node for hierarchy display.
/// </summary>
public class LedgerTreeNode
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required LedgerType Type { get; init; }
    public required LedgerStatus Status { get; init; }
    public required string CurrencyCode { get; init; }
    public required int Depth { get; init; }
    public decimal NetBalance { get; init; }
    public int EntryCount { get; init; }
    public List<LedgerTreeNode> Children { get; init; } = [];
}

/// <summary>
/// Rollup node with own balance + children aggregation.
/// </summary>
public class LedgerRollupNode
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required LedgerType Type { get; init; }
    public required int Depth { get; init; }

    /// <summary>Balance from direct entries only.</summary>
    public required LedgerBalance OwnBalance { get; init; }

    /// <summary>Consolidated balance (own + all descendants).</summary>
    public required LedgerBalance ConsolidatedBalance { get; init; }

    /// <summary>Children rollup nodes.</summary>
    public List<LedgerRollupNode> Children { get; init; } = [];
}

/// <summary>
/// Category-based rollup.
/// </summary>
public record CategoryRollup(
    EntryCategory Category,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal NetAmount,
    int EntryCount,
    decimal PercentageOfTotal);

/// <summary>
/// Time-period rollup for trends.
/// </summary>
public record PeriodRollup(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string PeriodLabel,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal NetChange,
    decimal RunningBalance,
    int EntryCount);

/// <summary>
/// Granularity for period-based reports.
/// </summary>
public enum PeriodGranularity
{
    Day,
    Week,
    Month,
    Quarter,
    Year
}

/// <summary>
/// Entry DTO for API responses.
/// </summary>
public record LedgerEntryDto(
    Guid Id,
    string ReferenceNumber,
    Guid LedgerId,
    string LedgerCode,
    string LedgerPath,
    EntryType Type,
    EntryStatus Status,
    EntryCategory Category,
    decimal Amount,
    string CurrencyCode,
    decimal? OriginalAmount,
    string? OriginalCurrencyCode,
    DateOnly TransactionDate,
    DateOnly PostingDate,
    string Description,
    string? Notes,
    string? CounterpartyName,
    string? ExternalReferenceId,
    Guid? TransferPairEntryId,
    Guid? ReversesEntryId,
    Guid? ReversedByEntryId,
    Guid? BudgetId,
    string? BudgetCategoryCode,
    IReadOnlyList<string> Tags,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Ledger DTO for API responses.
/// </summary>
public record LedgerDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string? Description,
    string Slug,
    LedgerType Type,
    LedgerStatus Status,
    string CurrencyCode,
    int FiscalYearStartMonth,
    Guid? ParentLedgerId,
    string? ParentLedgerCode,
    string HierarchyPath,
    int HierarchyDepth,
    bool IsShared,
    bool AllowDirectEntries,
    decimal? BudgetLimit,
    DateOnly? ProjectStartDate,
    DateOnly? ProjectEndDate,
    decimal CachedNetBalance,
    int CachedEntryCount,
    DateTime? StatsCalculatedAt,
    IReadOnlyList<string> Tags,
    int ChildCount,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// ============================================================================
// Pagination
// ============================================================================

/// <summary>
/// Paginated result wrapper.
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
/// Result of a transfer operation between two ledgers.
/// </summary>
public record TransferResult(
    LedgerEntryDto DebitEntry,
    LedgerEntryDto CreditEntry);

/// <summary>
/// Virtual ledger filter specification stored as JSON.
/// </summary>
public record VirtualFilterSpec(
    string FilterType,
    IEnumerable<EntryCategory>? Categories = null,
    IEnumerable<Guid>? BaseLedgerIds = null,
    bool IncludeDescendants = true,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    IEnumerable<string>? Tags = null);

/// <summary>
/// Batch entry request for posting multiple entries.
/// </summary>
public record BatchEntryRequest(
    Guid LedgerId,
    EntryType EntryType,
    decimal Amount,
    string Description,
    DateTimeOffset TransactionDate,
    EntryCategory? Category = null,
    string? Reference = null,
    string? ExternalId = null,
    Dictionary<string, object>? Metadata = null);
