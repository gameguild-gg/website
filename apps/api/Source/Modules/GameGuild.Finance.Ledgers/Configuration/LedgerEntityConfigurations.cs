using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameGuild.Finance.Ledgers.Entities;

namespace GameGuild.Finance.Ledgers.Configuration;

/// <summary>
/// EF Core configuration for the Ledger entity.
/// </summary>
public class LedgerConfiguration : IEntityTypeConfiguration<Entities.Ledger>
{
    public void Configure(EntityTypeBuilder<Entities.Ledger> builder)
    {
        builder.ToTable("Ledgers", "finance");

        // Primary Key
        builder.HasKey(e => e.Id);

        // Tenant isolation
        builder.HasIndex(e => e.TenantId);

        // Unique constraint: Code per tenant
        builder.HasIndex(e => new { e.TenantId, e.Code })
            .IsUnique()
            .HasDatabaseName("IX_Ledgers_TenantId_Code");

        // Unique constraint: Slug per tenant
        builder.HasIndex(e => new { e.TenantId, e.Slug })
            .IsUnique()
            .HasDatabaseName("IX_Ledgers_TenantId_Slug");

        // Properties
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.Slug)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.FiscalYearStartMonth)
            .HasDefaultValue(1);

        builder.Property(e => e.HierarchyPath)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(e => e.HierarchyDepth)
            .IsRequired();

        builder.Property(e => e.BudgetLimit)
            .HasPrecision(19, 4);

        builder.Property(e => e.CachedNetBalance)
            .HasPrecision(19, 4);

        // Tags as JSON array
        var tagsProperty = builder.Property(e => e.Tags)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasMaxLength(2000);
        tagsProperty.Metadata.SetValueComparer(LedgerValueComparers.StringList);

        // Virtual Ledger Filter (JSON) - PostgreSQL uses jsonb
        builder.Property(e => e.VirtualFilterSpec)
            .HasColumnType("jsonb");

        // Self-referential relationship (Parent-Child)
        builder.HasOne(e => e.ParentLedger)
            .WithMany(e => e.Children)
            .HasForeignKey(e => e.ParentLedgerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Ledgers_ParentLedger");

        // Index for hierarchy queries
        builder.HasIndex(e => e.ParentLedgerId)
            .HasDatabaseName("IX_Ledgers_ParentLedgerId");

        // Index for type queries
        builder.HasIndex(e => new { e.TenantId, e.Type })
            .HasDatabaseName("IX_Ledgers_TenantId_Type");

        // Index for status queries
        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("IX_Ledgers_TenantId_Status");

        // Audit fields
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .IsRequired();
    }
}

/// <summary>
/// EF Core configuration for the LedgerClosure entity (Closure Table for hierarchy).
/// </summary>
public class LedgerClosureConfiguration : IEntityTypeConfiguration<LedgerClosure>
{
    public void Configure(EntityTypeBuilder<LedgerClosure> builder)
    {
        builder.ToTable("LedgerClosures", "finance");

        // Primary Key
        builder.HasKey(e => e.Id);

        // Unique constraint on ancestor-descendant pair
        builder.HasIndex(e => new { e.AncestorId, e.DescendantId })
            .IsUnique()
            .HasDatabaseName("IX_LedgerClosures_Ancestor_Descendant");

        // Tenant isolation
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("IX_LedgerClosures_TenantId");

        // Index for ancestor queries (get all descendants)
        builder.HasIndex(e => new { e.AncestorId, e.Depth })
            .HasDatabaseName("IX_LedgerClosures_Ancestor_Depth");

        // Index for descendant queries (get all ancestors)
        builder.HasIndex(e => new { e.DescendantId, e.Depth })
            .HasDatabaseName("IX_LedgerClosures_Descendant_Depth");

        // Index for tenant + ancestor (common query pattern)
        builder.HasIndex(e => new { e.TenantId, e.AncestorId })
            .HasDatabaseName("IX_LedgerClosures_TenantId_Ancestor");

        // Properties
        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.AncestorId)
            .IsRequired();

        builder.Property(e => e.DescendantId)
            .IsRequired();

        builder.Property(e => e.Depth)
            .IsRequired();

        // Relationships
        builder.HasOne(e => e.Ancestor)
            .WithMany(e => e.DescendantClosures)
            .HasForeignKey(e => e.AncestorId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_LedgerClosures_AncestorLedger");

        builder.HasOne(e => e.Descendant)
            .WithMany(e => e.AncestorClosures)
            .HasForeignKey(e => e.DescendantId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_LedgerClosures_DescendantLedger");
    }
}

/// <summary>
/// EF Core configuration for the LedgerEntry entity.
/// </summary>
public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", "finance");

        // Primary Key
        builder.HasKey(e => e.Id);

        // Index for ledger + date queries (most common query)
        builder.HasIndex(e => new { e.LedgerId, e.TransactionDate })
            .HasDatabaseName("IX_LedgerEntries_LedgerId_TransactionDate");

        // Index for status queries
        builder.HasIndex(e => new { e.LedgerId, e.Status })
            .HasDatabaseName("IX_LedgerEntries_LedgerId_Status");

        // Index for category queries
        builder.HasIndex(e => new { e.LedgerId, e.Category })
            .HasDatabaseName("IX_LedgerEntries_LedgerId_Category");

        // Index for external reference ID lookups
        builder.HasIndex(e => e.ExternalReferenceId)
            .HasDatabaseName("IX_LedgerEntries_ExternalReferenceId");

        // Index for reference number lookups
        builder.HasIndex(e => e.ReferenceNumber)
            .HasDatabaseName("IX_LedgerEntries_ReferenceNumber");

        // Properties
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.Property(e => e.LedgerId)
            .IsRequired();

        builder.Property(e => e.ReferenceNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ExternalReferenceId)
            .HasMaxLength(200);

        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasPrecision(19, 4)
            .IsRequired();

        builder.Property(e => e.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.OriginalAmount)
            .HasPrecision(19, 4);

        builder.Property(e => e.OriginalCurrencyCode)
            .HasMaxLength(3);

        builder.Property(e => e.ExchangeRate)
            .HasPrecision(19, 10);

        builder.Property(e => e.TransactionDate)
            .IsRequired();

        builder.Property(e => e.PostingDate)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.Property(e => e.LedgerPath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.CounterpartyName)
            .HasMaxLength(200);

        builder.Property(e => e.CounterpartyReference)
            .HasMaxLength(100);

        builder.Property(e => e.BudgetCategoryCode)
            .HasMaxLength(50);

        builder.Property(e => e.BudgetPeriodId)
            .HasMaxLength(50);

        // ParentLedgerIds stored as JSON array for efficient rollup queries (PostgreSQL jsonb)
        var parentLedgerIdsProperty = builder.Property(e => e.ParentLedgerIds)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>())
            .HasColumnType("jsonb");
        parentLedgerIdsProperty.Metadata.SetValueComparer(LedgerValueComparers.GuidList);

        // Tags stored as JSON array
        var entryTagsProperty = builder.Property(e => e.Tags)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnType("jsonb");
        entryTagsProperty.Metadata.SetValueComparer(LedgerValueComparers.StringList);

        // AttachmentIds stored as JSON array
        var attachmentIdsProperty = builder.Property(e => e.AttachmentIds)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>())
            .HasColumnType("jsonb");
        attachmentIdsProperty.Metadata.SetValueComparer(LedgerValueComparers.GuidList);

        // Metadata as JSON (PostgreSQL jsonb)
        builder.Property(e => e.Metadata)
            .HasColumnType("jsonb");

        // Relationship to Ledger
        builder.HasOne(e => e.Ledger)
            .WithMany(l => l.Entries)
            .HasForeignKey(e => e.LedgerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_LedgerEntries_Ledger");

        // Self-referential for transfer pair
        builder.HasOne(e => e.TransferPairEntry)
            .WithOne()
            .HasForeignKey<LedgerEntry>(e => e.TransferPairEntryId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_LedgerEntries_TransferPair");

        // Self-referential for reversal
        builder.HasOne(e => e.ReversesEntry)
            .WithOne()
            .HasForeignKey<LedgerEntry>(e => e.ReversesEntryId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_LedgerEntries_ReversesEntry");

        // Self-referential for reversed by
        builder.HasOne(e => e.ReversedByEntry)
            .WithOne()
            .HasForeignKey<LedgerEntry>(e => e.ReversedByEntryId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_LedgerEntries_ReversedByEntry");

        // Audit fields
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedByUserId)
            .IsRequired();

        // Row version for concurrency
        builder.Property(e => e.RowVersion)
            .IsRowVersion();
    }
}

/// <summary>
/// EF Core model configuration bridge for the Finance.Ledgers module.
/// Discovered by ApplicationDbContext via IModelConfiguration assembly scanning.
/// </summary>
public sealed class FinanceLedgersModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(LedgerConfiguration).Assembly,
            type => type.Namespace?.StartsWith("GameGuild.Finance.Ledgers", StringComparison.Ordinal) == true);
    }
}

internal static class LedgerValueComparers
{
    public static readonly ValueComparer<List<Guid>> GuidList = new(
        (left, right) => ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
        list => list == null ? 0 : list.Aggregate(0, (hash, value) => (hash * 397) ^ value.GetHashCode()),
        list => list == null ? new List<Guid>() : list.ToList());

    public static readonly ValueComparer<List<string>> StringList = new(
        (left, right) => ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
        list => list == null ? 0 : list.Aggregate(0, (hash, value) => (hash * 397) ^ value.GetHashCode()),
        list => list == null ? new List<string>() : list.ToList());
}
