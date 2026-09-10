namespace GameGuild.Finance.Ledgers.Enums;

/// <summary>
/// Discriminator for ledger types in the hierarchy.
/// Each type has specific behaviors and constraints.
/// </summary>
public enum LedgerType
{
    /// <summary>Root ledger - organization level, no parent allowed.</summary>
    Root = 0,

    /// <summary>Department ledger - organizational unit.</summary>
    Department = 1,

    /// <summary>Project ledger - time-bound initiative with budget.</summary>
    Project = 2,

    /// <summary>Cost center - expense tracking unit.</summary>
    CostCenter = 3,

    /// <summary>Revenue center - income tracking unit.</summary>
    RevenueCenter = 4,

    /// <summary>Personal ledger - individual finances.</summary>
    Personal = 5,

    /// <summary>Business ledger - company finances.</summary>
    Business = 6,

    /// <summary>Family ledger - shared household finances.</summary>
    Family = 7,

    /// <summary>Product ledger - specific product/service tracking.</summary>
    Product = 8,

    /// <summary>Income source ledger - revenue stream grouping.</summary>
    IncomeSource = 9,

    /// <summary>Budget category ledger - expense category tracking.</summary>
    BudgetCategory = 10,

    /// <summary>Virtual ledger - filter/view without own data.</summary>
    Virtual = 99
}

/// <summary>
/// Lifecycle status for ledgers.
/// </summary>
public enum LedgerStatus
{
    /// <summary>Draft - being configured, not operational.</summary>
    Draft = 0,

    /// <summary>Active - operational and accepting entries.</summary>
    Active = 1,

    /// <summary>Inactive - temporarily suspended.</summary>
    Inactive = 2,

    /// <summary>Archived - read-only historical data.</summary>
    Archived = 3,

    /// <summary>Closed - permanently closed, period ended.</summary>
    Closed = 4
}

/// <summary>
/// Type of ledger entry (transaction direction).
/// </summary>
public enum EntryType
{
    /// <summary>Debit - increases assets/expenses, decreases liabilities/income.</summary>
    Debit = 1,

    /// <summary>Credit - decreases assets/expenses, increases liabilities/income.</summary>
    Credit = -1
}

/// <summary>
/// Status of a ledger entry.
/// </summary>
public enum EntryStatus
{
    /// <summary>Pending - not yet finalized.</summary>
    Pending = 0,

    /// <summary>Posted - finalized and affecting balances.</summary>
    Posted = 1,

    /// <summary>Voided - cancelled entry (creates reversal).</summary>
    Voided = 2,

    /// <summary>Reconciled - matched with external source.</summary>
    Reconciled = 3
}

/// <summary>
/// Category for ledger entries.
/// </summary>
public enum EntryCategory
{
    /// <summary>Operating income.</summary>
    Revenue = 1,

    /// <summary>Operating expense.</summary>
    Expense = 2,

    /// <summary>Asset acquisition or disposal.</summary>
    Asset = 3,

    /// <summary>Liability creation or payment.</summary>
    Liability = 4,

    /// <summary>Equity transaction.</summary>
    Equity = 5,

    /// <summary>Internal transfer between ledgers.</summary>
    Transfer = 6,

    /// <summary>Adjustment entry (corrections).</summary>
    Adjustment = 7
}

/// <summary>
/// Rollup aggregation type for hierarchy queries.
/// </summary>
public enum RollupType
{
    /// <summary>Sum of all descendant values.</summary>
    Sum = 0,

    /// <summary>Average of descendant values.</summary>
    Average = 1,

    /// <summary>Count of descendants with values.</summary>
    Count = 2,

    /// <summary>Minimum value among descendants.</summary>
    Min = 3,

    /// <summary>Maximum value among descendants.</summary>
    Max = 4
}
