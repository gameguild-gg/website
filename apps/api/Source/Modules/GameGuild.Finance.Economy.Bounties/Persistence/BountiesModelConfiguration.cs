using GameGuild.Finance.Economy.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Bounties.Persistence;

internal sealed class BountyRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PosterId { get; set; }
    public Guid PosterWalletId { get; set; }
    public Guid EscrowWalletId { get; set; }
    public CurrencyCode Currency { get; set; }
    public long AmountUnits { get; set; }
    public int ReclaimFeePpm { get; set; }
    public bool RequiresPrerequisite { get; set; }
    public int MinimumReputation { get; set; }
    public bool RequiresInstructorVerification { get; set; }
    public BountyStatus Status { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? RequestHash { get; set; }
    public DateTimeOffset PostedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public long Version { get; set; }
}

internal sealed class BountyEscrowFragmentRow
{
    public Guid Id { get; set; }
    public Guid BountyId { get; set; }
    public Guid ParentLotId { get; set; }
    public Guid? EscrowLotId { get; set; }
    public CurrencyCode Currency { get; set; }
    public ProvenanceKind Provenance { get; set; }
    public long AmountUnits { get; set; }
    public long TraceUnitsPerCoinUnit { get; set; }
    public string SelectedRootRanges { get; set; } = "[]";
}

internal sealed class BountyTerminalEventRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BountyId { get; set; }
    public BountyStatus Status { get; set; }
    public Guid ActorId { get; set; }
    public Guid DestinationWalletId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? RiskDecisionId { get; set; }
    public Guid? ProceedsSourceStampId { get; set; }
    public Guid? ProceedsLotId { get; set; }
    public long ReturnedUnits { get; set; }
    public long FeeUnits { get; set; }
    public long FirstJournalSequence { get; set; }
    public string OutputLots { get; set; } = "[]";
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class BountyExpirationEventRow
{
    public Guid Id { get; set; }
    public Guid BountyId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public long BountyVersion { get; set; }
}

public sealed class BountiesModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<BountyRow>(builder =>
        {
            builder.ToTable("economy_bounties", table =>
            {
                table.HasCheckConstraint("ck_economy_bounties_amount_positive", "\"AmountUnits\" > 0");
                table.HasCheckConstraint("ck_economy_bounties_fee", "\"ReclaimFeePpm\" BETWEEN 0 AND 999999");
                table.HasCheckConstraint("ck_economy_bounties_reputation", "\"MinimumReputation\" >= 0");
                table.HasCheckConstraint("ck_economy_bounties_window", "\"ExpiresAt\" > \"PostedAt\"");
                table.HasCheckConstraint("ck_economy_bounties_state", "\"Status\" BETWEEN 1 AND 4");
                table.HasCheckConstraint("ck_economy_bounties_version", "\"Version\" > 0");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).ValueGeneratedNever();
            builder.Property(row => row.IdempotencyKey).HasMaxLength(256);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.HasIndex(row => row.IdempotencyKey).IsUnique();
            builder.HasIndex(row => new { row.TenantId, row.PosterId, row.Status, row.ExpiresAt });
            builder.HasIndex(row => new { row.TenantId, row.Status, row.ExpiresAt });
        });

        modelBuilder.Entity<BountyEscrowFragmentRow>(builder =>
        {
            builder.ToTable("economy_bounty_escrow_fragments", table =>
            {
                table.HasCheckConstraint("ck_economy_bounty_escrow_fragments_amount_positive", "\"AmountUnits\" > 0");
                table.HasCheckConstraint("ck_economy_bounty_escrow_fragments_scale_positive", "\"TraceUnitsPerCoinUnit\" > 0");
                table.HasCheckConstraint("ck_economy_bounty_escrow_fragments_provenance", "\"Provenance\" BETWEEN 1 AND 7");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).ValueGeneratedNever();
            builder.Property(row => row.SelectedRootRanges).HasColumnType("jsonb");
            builder.HasIndex(row => new { row.BountyId, row.ParentLotId }).IsUnique();
            builder.HasIndex(row => new { row.BountyId, row.EscrowLotId })
                .IsUnique()
                .HasDatabaseName("ux_economy_bounty_escrow_fragments_bounty_escrow_lot")
                .HasFilter("\"EscrowLotId\" IS NOT NULL");
            builder.HasOne<BountyRow>().WithMany().HasForeignKey(row => row.BountyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BountyTerminalEventRow>(builder =>
        {
            builder.ToTable("economy_bounty_terminal_events", table =>
            {
                table.HasCheckConstraint("ck_economy_bounty_terminal_events_state", "\"Status\" IN (3, 4)");
                table.HasCheckConstraint("ck_economy_bounty_terminal_events_units", "\"ReturnedUnits\" >= 0 AND \"FeeUnits\" >= 0 AND \"FirstJournalSequence\" > 0");
                table.HasCheckConstraint("ck_economy_bounty_terminal_events_claim_binding", "\"Status\" <> 3 OR (\"RiskDecisionId\" IS NOT NULL AND \"ProceedsSourceStampId\" IS NOT NULL AND \"ProceedsLotId\" IS NOT NULL)");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).ValueGeneratedNever();
            builder.Property(row => row.IdempotencyKey).HasMaxLength(256);
            builder.Property(row => row.OutputLots).HasColumnType("jsonb");
            builder.HasIndex(row => row.BountyId).IsUnique();
            builder.HasIndex(row => row.IdempotencyKey).IsUnique();
            builder.HasOne<BountyRow>().WithOne().HasForeignKey<BountyTerminalEventRow>(row => row.BountyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BountyExpirationEventRow>(builder =>
        {
            builder.ToTable("economy_bounty_expiration_events", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_bounty_expiration_events_version",
                    "\"BountyVersion\" > 1");
                table.HasCheckConstraint(
                    "ck_economy_bounty_expiration_events_time",
                    "\"RecordedAt\" >= \"ExpiresAt\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).ValueGeneratedNever();
            builder.HasIndex(row => row.BountyId).IsUnique();
            builder.HasIndex(row => new { row.RecordedAt, row.Id });
            builder.HasOne<BountyRow>().WithOne().HasForeignKey<BountyExpirationEventRow>(row => row.BountyId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
