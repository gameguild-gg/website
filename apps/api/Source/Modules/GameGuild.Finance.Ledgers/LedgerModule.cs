// ============================================================================
// GameGuild.Finance.Ledgers - Multi-Ledger Core Module
// ============================================================================
// Implements a hierarchical multi-ledger system supporting:
// - Tree-structured ledgers (projects, departments, cost centers)
// - Closure table for efficient hierarchy queries and rollups
// - Virtual ledgers (filtered views without data duplication)
// - Multi-currency support with consolidation
// - Full audit trail with domain events
// ============================================================================

namespace GameGuild.Finance.Ledgers;

/// <summary>
/// Module marker for assembly scanning and dependency injection.
/// </summary>
public static class LedgerModule
{
    public const string ModuleName = "Finance.Ledgers";
    public const string ModuleVersion = "1.0.0";
}
