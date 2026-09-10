using GameGuild.Finance.Ledgers.Enums;

namespace GameGuild.Finance.Ledgers.Entities;

/// <summary>
/// Core ledger entity representing a financial tracking unit.
/// Ledgers form a hierarchical tree structure via LedgerClosure.
/// Supports multi-tenancy, multi-currency, and audit trail.
/// </summary>
public class Ledger
{
    // ========================================================================
    // Identity
    // ========================================================================

    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Tenant discriminator for multi-tenancy.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>User-friendly unique code within tenant (e.g., "DEPT-001").</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; private set; }

    /// <summary>URL-friendly slug for API routes.</summary>
    public string Slug { get; private set; } = string.Empty;

    // ========================================================================
    // Classification
    // ========================================================================

    /// <summary>Type discriminator determining behavior.</summary>
    public LedgerType Type { get; private set; }

    /// <summary>Lifecycle status.</summary>
    public LedgerStatus Status { get; private set; }

    /// <summary>Base currency for this ledger (ISO 4217).</summary>
    public string CurrencyCode { get; private set; } = "USD";

    /// <summary>Fiscal year start month (1-12).</summary>
    public int FiscalYearStartMonth { get; private set; } = 1;

    /// <summary>Tags for categorization and filtering.</summary>
    public List<string> Tags { get; private set; } = [];

    // ========================================================================
    // Hierarchy (Adjacency List - for direct parent reference)
    // ========================================================================

    /// <summary>Direct parent ledger ID (null for root).</summary>
    public Guid? ParentLedgerId { get; private set; }

    /// <summary>Navigation to parent ledger.</summary>
    public Ledger? ParentLedger { get; private set; }

    /// <summary>Materialized path for display (e.g., "/root/dept/project").</summary>
    public string HierarchyPath { get; private set; } = "/";

    /// <summary>Depth level in hierarchy (root = 0).</summary>
    public int HierarchyDepth { get; private set; }

    /// <summary>Direct children (via adjacency).</summary>
    public ICollection<Ledger> Children { get; private set; } = [];

    // ========================================================================
    // Closure Table Navigation
    // ========================================================================

    /// <summary>Closure entries where this ledger is the ancestor.</summary>
    public ICollection<LedgerClosure> DescendantClosures { get; private set; } = [];

    /// <summary>Closure entries where this ledger is the descendant.</summary>
    public ICollection<LedgerClosure> AncestorClosures { get; private set; } = [];

    // ========================================================================
    // Entries Navigation
    // ========================================================================

    /// <summary>Entries directly posted to this ledger.</summary>
    public ICollection<LedgerEntry> Entries { get; private set; } = [];

    // ========================================================================
    // Virtual Ledger Configuration
    // ========================================================================

    /// <summary>For virtual ledgers: JSON filter specification.</summary>
    public string? VirtualFilterSpec { get; private set; }

    /// <summary>For virtual ledgers: source ledger IDs to aggregate.</summary>
    public List<Guid>? VirtualSourceLedgerIds { get; private set; }

    // ========================================================================
    // Settings & Constraints
    // ========================================================================

    /// <summary>Whether this ledger is shared (multi-user).</summary>
    public bool IsShared { get; private set; }

    /// <summary>Whether entries can be posted directly (false = rollup only).</summary>
    public bool AllowDirectEntries { get; private set; } = true;

    /// <summary>Optional budget limit for constraint checking.</summary>
    public decimal? BudgetLimit { get; private set; }

    /// <summary>Project start date (for project ledgers).</summary>
    public DateOnly? ProjectStartDate { get; private set; }

    /// <summary>Project end date (for project ledgers).</summary>
    public DateOnly? ProjectEndDate { get; private set; }

    // ========================================================================
    // Computed/Cached Stats (denormalized for performance)
    // ========================================================================

    /// <summary>Cached total debit amount (direct entries only).</summary>
    public decimal CachedTotalDebit { get; private set; }

    /// <summary>Cached total credit amount (direct entries only).</summary>
    public decimal CachedTotalCredit { get; private set; }

    /// <summary>Cached net balance (debit - credit).</summary>
    public decimal CachedNetBalance { get; private set; }

    /// <summary>Cached entry count.</summary>
    public int CachedEntryCount { get; private set; }

    /// <summary>Last time stats were recalculated.</summary>
    public DateTime? StatsCalculatedAt { get; private set; }

    // ========================================================================
    // Audit
    // ========================================================================

    /// <summary>User who created this ledger.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Last modification timestamp (UTC).</summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>User who last modified.</summary>
    public Guid? UpdatedByUserId { get; private set; }

    /// <summary>Soft delete timestamp.</summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>Optimistic concurrency token.</summary>
    public uint RowVersion { get; private set; }

    // ========================================================================
    // Factory Methods
    // ========================================================================

    private Ledger() { } // EF Core

    /// <summary>
    /// Creates a new root ledger (no parent).
    /// </summary>
    public static Ledger CreateRoot(
        Guid tenantId,
        string code,
        string name,
        string currencyCode,
        Guid createdByUserId,
        string? description = null)
    {
        var ledger = new Ledger
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Description = description,
            Slug = GenerateSlug(code),
            Type = LedgerType.Root,
            Status = LedgerStatus.Active,
            CurrencyCode = currencyCode,
            ParentLedgerId = null,
            HierarchyPath = $"/{code}",
            HierarchyDepth = 0,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        return ledger;
    }

    /// <summary>
    /// Creates a child ledger under a parent.
    /// </summary>
    public static Ledger CreateChild(
        Ledger parent,
        LedgerType type,
        string code,
        string name,
        Guid createdByUserId,
        string? description = null,
        string? currencyCode = null)
    {
        if (parent.Type == LedgerType.Virtual)
            throw new InvalidOperationException("Cannot create children under virtual ledgers.");

        if (type == LedgerType.Root)
            throw new InvalidOperationException("Cannot create root ledger as child.");

        var ledger = new Ledger
        {
            Id = Guid.NewGuid(),
            TenantId = parent.TenantId,
            Code = code,
            Name = name,
            Description = description,
            Slug = GenerateSlug(code),
            Type = type,
            Status = LedgerStatus.Active,
            CurrencyCode = currencyCode ?? parent.CurrencyCode,
            ParentLedgerId = parent.Id,
            ParentLedger = parent,
            HierarchyPath = $"{parent.HierarchyPath}/{code}",
            HierarchyDepth = parent.HierarchyDepth + 1,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        parent.Children.Add(ledger);
        return ledger;
    }

    /// <summary>
    /// Creates a virtual ledger (filtered view).
    /// </summary>
    public static Ledger CreateVirtual(
        Guid tenantId,
        string code,
        string name,
        string filterSpec,
        List<Guid> sourceLedgerIds,
        Guid createdByUserId,
        string? description = null)
    {
        var ledger = new Ledger
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Description = description,
            Slug = GenerateSlug(code),
            Type = LedgerType.Virtual,
            Status = LedgerStatus.Active,
            CurrencyCode = "USD", // Virtual ledgers aggregate in reporting currency
            VirtualFilterSpec = filterSpec,
            VirtualSourceLedgerIds = sourceLedgerIds,
            AllowDirectEntries = false,
            HierarchyPath = $"/virtual/{code}",
            HierarchyDepth = 0,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        return ledger;
    }

    // ========================================================================
    // Domain Methods
    // ========================================================================

    /// <summary>
    /// Updates ledger metadata.
    /// </summary>
    public void Update(string name, string? description, Guid updatedByUserId)
    {
        Name = name;
        Description = description;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Changes ledger status with validation.
    /// </summary>
    public void ChangeStatus(LedgerStatus newStatus, Guid updatedByUserId)
    {
        // Validate transitions
        var isValidTransition = (Status, newStatus) switch
        {
            (LedgerStatus.Draft, LedgerStatus.Active) => true,
            (LedgerStatus.Active, LedgerStatus.Inactive) => true,
            (LedgerStatus.Active, LedgerStatus.Archived) => true,
            (LedgerStatus.Active, LedgerStatus.Closed) => true,
            (LedgerStatus.Inactive, LedgerStatus.Active) => true,
            (LedgerStatus.Inactive, LedgerStatus.Archived) => true,
            (LedgerStatus.Archived, LedgerStatus.Active) => true, // Unarchive
            _ => false
        };

        if (!isValidTransition)
            throw new InvalidOperationException($"Cannot transition from {Status} to {newStatus}.");

        Status = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets budget limit for constraint checking.
    /// </summary>
    public void SetBudgetLimit(decimal? limit, Guid updatedByUserId)
    {
        BudgetLimit = limit;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets project dates (for project ledgers).
    /// </summary>
    public void SetProjectDates(DateOnly? startDate, DateOnly? endDate, Guid updatedByUserId)
    {
        if (Type != LedgerType.Project)
            throw new InvalidOperationException("Project dates can only be set on project ledgers.");

        if (startDate.HasValue && endDate.HasValue)
        {
            if (startDate.Value > endDate.Value)
                throw new ArgumentException("Start date must be before end date.");
        }

        ProjectStartDate = startDate;
        ProjectEndDate = endDate;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates cached statistics.
    /// </summary>
    public void UpdateCachedStats(decimal totalDebit, decimal totalCredit, int entryCount)
    {
        CachedTotalDebit = totalDebit;
        CachedTotalCredit = totalCredit;
        CachedNetBalance = totalDebit - totalCredit;
        CachedEntryCount = entryCount;
        StatsCalculatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft delete the ledger.
    /// </summary>
    public void SoftDelete(Guid deletedByUserId)
    {
        if (Children.Any(c => c.DeletedAt == null))
            throw new InvalidOperationException("Cannot delete ledger with active children.");

        DeletedAt = DateTime.UtcNow;
        UpdatedByUserId = deletedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates if an entry can be posted to this ledger.
    /// </summary>
    public bool CanAcceptEntry()
    {
        return Status == LedgerStatus.Active
               && AllowDirectEntries
               && Type != LedgerType.Virtual
               && DeletedAt == null;
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static string GenerateSlug(string code)
    {
        return code.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");
    }
}
