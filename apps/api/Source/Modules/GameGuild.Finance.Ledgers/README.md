# GameGuild.Finance.Ledgers Module

A comprehensive multi-ledger financial management module for GameGuild, inspired by the Astrolabos TypeScript implementation. This module provides hierarchical ledger management with efficient rollup queries using the Closure Table pattern.

## Features

### Core Capabilities
- **Multi-Ledger Hierarchy**: Organize ledgers in a tree structure (projects, departments, cost centers, etc.)
- **Closure Table Pattern**: O(1) subtree queries for efficient balance consolidation
- **Virtual Ledgers**: Filter-based views without data duplication
- **Multi-Tenancy**: Full tenant isolation with TenantId discriminator
- **CQRS Pattern**: Separation of commands and queries via MediatR
- **Domain Events**: Audit trail for all significant operations

### Ledger Types
| Type | Use Case |
|------|----------|
| `Root` | Main organizational ledger |
| `Department` | Department-level tracking |
| `Project` | Project-based financial tracking |
| `CostCenter` | Cost center management |
| `RevenueCenter` | Revenue stream tracking |
| `Personal` | Individual user ledgers |
| `Business` | Business entity ledgers |
| `Family` | Family/household ledgers |
| `Product` | Product-level P&L |
| `IncomeSource` | Income categorization |
| `BudgetCategory` | Budget allocation |
| `Virtual` | Filtered aggregate views |

### Entry Types
- **Credit**: Money coming in (income, deposits)
- **Debit**: Money going out (expenses, withdrawals)

## Architecture

```
GameGuild.Finance.Ledgers/
├── Abstractions/           # Interfaces for repositories and services
├── Commands/               # CQRS command definitions
├── Configuration/          # EF Core entity configurations
├── Controllers/            # REST API endpoints
├── Entities/               # Domain entities (Ledger, LedgerEntry, LedgerClosure)
├── Enums/                  # Enumeration types
├── Events/                 # Domain event definitions
├── Handlers/               # MediatR command/query handlers
├── Models/                 # DTOs and request/response models
├── Queries/                # CQRS query definitions
├── Repositories/           # EF Core repository implementations
├── Services/               # Business logic services
├── Validators/             # FluentValidation validators
└── ServiceCollectionExtensions.cs  # DI registration
```

## Hierarchy Pattern: Closure Table

The module uses a **Closure Table** combined with **Adjacency List** for optimal hierarchy queries:

```
Company (Root)
├── Engineering (Department)
│   ├── Backend Team (Project)
│   └── Frontend Team (Project)
└── Sales (Department)
    └── Enterprise (Project)
```

### Closure Table Structure
| Ancestor | Descendant | Depth |
|----------|------------|-------|
| Company | Company | 0 |
| Company | Engineering | 1 |
| Company | Backend Team | 2 |
| Engineering | Engineering | 0 |
| Engineering | Backend Team | 1 |
| Backend Team | Backend Team | 0 |

### Benefits
- **O(1) Subtree Queries**: Get all descendants without recursive CTEs
- **Efficient Rollups**: Sum balances across entire subtrees
- **Move Operations**: Relocate subtrees by updating closure entries

## API Endpoints

### Ledger Management
```
GET    /api/v1/ledgers/{id}              # Get ledger by ID
GET    /api/v1/ledgers?tenantId=...      # List tenant ledgers
GET    /api/v1/ledgers/roots             # Get root ledgers
POST   /api/v1/ledgers/root              # Create root ledger
POST   /api/v1/ledgers/{id}/children     # Create child ledger
POST   /api/v1/ledgers/{id}/move         # Move ledger to new parent
POST   /api/v1/ledgers/{id}/status       # Change ledger status
```

### Hierarchy Queries
```
GET    /api/v1/ledgers/{id}/hierarchy    # Get full subtree
GET    /api/v1/ledgers/{id}/ancestors    # Path to root
GET    /api/v1/ledgers/{id}/descendants  # All children (recursive)
GET    /api/v1/ledgers/{id}/children     # Direct children only
```

### Balance & Rollup
```
GET    /api/v1/ledgers/{id}/balance               # Single ledger balance
GET    /api/v1/ledgers/{id}/balance/consolidated  # Include descendants
GET    /api/v1/ledgers/{id}/rollup                # Hierarchical rollup tree
GET    /api/v1/ledgers/{id}/rollup/period         # Time-series rollup
POST   /api/v1/ledgers/{id}/refresh-stats         # Refresh cached stats
```

### Entry Management
```
GET    /api/v1/ledgers/{id}/entries                 # List entries
GET    /api/v1/ledgers/{id}/entries/with-descendants # Include child ledgers
POST   /api/v1/ledgers/{id}/entries                 # Post new entry
POST   /api/v1/ledgers/{id}/entries/batch           # Batch post
POST   /api/v1/ledgers/{id}/entries/{entryId}/reverse # Reverse entry
```

### Transfers
```
POST   /api/v1/ledger-transfers          # Transfer between ledgers
```

### Virtual Ledgers
```
GET    /api/v1/virtual-ledgers           # List virtual ledgers
POST   /api/v1/virtual-ledgers           # Create virtual ledger
PUT    /api/v1/virtual-ledgers/{id}/filter # Update filter
DELETE /api/v1/virtual-ledgers/{id}      # Delete virtual ledger
GET    /api/v1/virtual-ledgers/{id}/entries # Get filtered entries
GET    /api/v1/virtual-ledgers/{id}/balance # Get filtered balance
```

## Usage Examples

### 1. Create a Ledger Hierarchy
```csharp
// Create root ledger
var root = await mediator.Send(new CreateRootLedgerCommand(
    TenantId: tenantId,
    Code: "COMPANY",
    Name: "Acme Corporation",
    CurrencyCode: "USD",
    CreatedByUserId: userId));

// Create department under root
var dept = await mediator.Send(new CreateChildLedgerCommand(
    ParentLedgerId: root.Id,
    Type: LedgerType.Department,
    Code: "ENG",
    Name: "Engineering",
    CreatedByUserId: userId));

// Create project under department
var project = await mediator.Send(new CreateChildLedgerCommand(
    ParentLedgerId: dept.Id,
    Type: LedgerType.Project,
    Code: "PROJ-001",
    Name: "Platform Migration",
    CreatedByUserId: userId));
```

### 2. Post Entries
```csharp
// Post expense to project
await mediator.Send(new PostEntryCommand(
    LedgerId: project.Id,
    EntryType: EntryType.Debit,
    Amount: 5000.00m,
    Description: "Cloud infrastructure costs",
    TransactionDate: DateTimeOffset.UtcNow,
    CreatedByUserId: userId,
    Category: EntryCategory.Infrastructure));
```

### 3. Get Consolidated Balance
```csharp
// Get balance for entire Engineering department (including all projects)
var balance = await mediator.Send(new GetConsolidatedBalanceQuery(
    LedgerId: dept.Id,
    AsOfDate: null));

// balance.NetBalance includes Engineering + all child project balances
```

### 4. Create Virtual Ledger
```csharp
// Create a view showing all infrastructure expenses across all projects
var virtualLedger = await mediator.Send(new CreateVirtualLedgerCommand(
    TenantId: tenantId,
    Code: "ALL-INFRA",
    Name: "All Infrastructure Costs",
    Filter: new VirtualFilterSpec
    {
        FilterType = "category",
        Categories = [EntryCategory.Infrastructure],
        IncludeDescendants = true,
        BaseLedgerIds = [root.Id]
    },
    CreatedByUserId: userId));
```

## Registration

### Program.cs
```csharp
// Add Ledger module services
builder.Services.AddLedgerModule();

// Add MediatR with assembly scanning
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(LedgerModule).Assembly);
});
```

### DbContext
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyLedgerModuleConfigurations();
    base.OnModelCreating(modelBuilder);
}
```

## Database Schema

The module creates tables in the `finance` schema:
- `finance.Ledgers` - Main ledger entities
- `finance.LedgerClosures` - Closure table for hierarchy
- `finance.LedgerEntries` - Financial transactions

### Key Indexes
- Unique: `(TenantId, Code)` on Ledgers
- Unique: `(LedgerId, SequenceNumber)` on Entries
- Composite: `(LedgerId, TransactionDate)` for date-range queries
- Composite: `(AncestorLedgerId, Depth)` for subtree queries

## Design Decisions

### Why Closure Table + Adjacency List?
- **Closure Table**: Enables O(1) subtree queries without recursive CTEs
- **Adjacency List** (ParentLedgerId): Simple direct parent lookup and navigation

### Why ParentLedgerIds on Entries?
Entries store denormalized `ParentLedgerIds[]` array for efficient rollup queries:
```sql
-- Get all entries affecting a ledger's consolidated balance
WHERE LedgerId = @id OR @id IN ParentLedgerIds
```
This avoids expensive JOIN operations on every rollup query.

### Why Virtual Ledgers?
Virtual ledgers provide filtered views without duplicating entries:
- Cross-cutting views (all expenses by category)
- Ad-hoc reporting without schema changes
- Saved query patterns for users

## Domain Events

The module emits events for audit and integration:
- `LedgerCreatedEvent`
- `LedgerMovedEvent`
- `LedgerStatusChangedEvent`
- `EntryPostedEvent`
- `TransferCompletedEvent`
- `EntryReversedEvent`
- `VirtualLedgerCreatedEvent`

## Future Enhancements
- [ ] Currency conversion support
- [ ] Budget threshold alerts
- [ ] Recurring entry templates
- [ ] Approval workflows for large entries
- [ ] Export to accounting formats (QIF, OFX)
- [ ] GraphQL endpoint support
