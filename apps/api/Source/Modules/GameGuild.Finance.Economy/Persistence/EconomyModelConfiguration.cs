using GameGuild.Finance.Economy.Integrations.AI;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Transfers;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Persistence;

public sealed class EconomyModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureWallets(modelBuilder);
        ConfigureSources(modelBuilder);
        ConfigureFunding(modelBuilder);
        ConfigureDisputes(modelBuilder);
        ConfigurePostings(modelBuilder);
        ConfigureLedger(modelBuilder);
        ConfigureLineage(modelBuilder);
        ConfigureOperations(modelBuilder);
        ConfigureChain(modelBuilder);
        ConfigureReserves(modelBuilder);
        ConfigureRisk(modelBuilder);
        ConfigureSelfServiceTransfers(modelBuilder);
        ConfigureControlPlane(modelBuilder);
        ConfigureLegacyShadowMigration(modelBuilder);
        ConfigureRegisteredPostingReceipt(modelBuilder);
        ConfigureHardToSoftConversionRiskDecisionReceipt(modelBuilder);
        ConfigureFifoFragmentReservationReceipt(modelBuilder);
        ConfigureMarketplaceFifoReservationReceipt(modelBuilder);
        ConfigureProviderReversalReceipt(modelBuilder);
        ConfigureEconomyWalletProvisioningReceipt(modelBuilder);
        modelBuilder.ApplyConfiguration(new AiProviderCostFactEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AiCreditReservationConfiguration());
        modelBuilder.ApplyConfiguration(new AiCreditRateCardConfiguration());
    }

    private static void ConfigureWallets(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyWalletRow>(builder =>
        {
            builder.ToTable("economy_wallets");
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => new { row.TenantId, row.OwnerId }).IsUnique();
        });

        modelBuilder.Entity<EconomyWalletBalanceProjectionRow>(builder =>
        {
            builder.ToTable("economy_wallet_balance_projections", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_wallet_balance_projections_amounts_nonnegative",
                    "\"PendingHard\" >= 0 AND \"PendingSoft\" >= 0 AND \"PurchasedHard\" >= 0 AND " +
                    "\"EarnedHard\" >= 0 AND \"RestrictedHard\" >= 0 AND \"Soft\" >= 0 AND " +
                    "\"ImmatureEarnedHard\" >= 0 AND \"HeldHard\" >= 0 AND \"HeldSoft\" >= 0 AND " +
                    "\"AvailableHardToSpend\" >= 0 AND \"AvailableSoftToSpend\" >= 0 AND \"WithdrawableHard\" >= 0");
                table.HasCheckConstraint(
                    "ck_economy_wallet_balance_projections_sequence_nonnegative",
                    "\"SourceJournalSequence\" >= 0");
            });
            builder.HasKey(row => row.WalletId);
            builder.Property(row => row.ProjectionHash).HasMaxLength(128);
            builder.HasIndex(row => row.ReviewState)
                .HasDatabaseName("ix_economy_wallet_balance_projections_review_state");
            builder.HasOne<EconomyWalletRow>()
                .WithOne()
                .HasForeignKey<EconomyWalletBalanceProjectionRow>(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyProjectionReconciliationEventRow>(builder =>
        {
            builder.ToTable("economy_projection_reconciliation_events", table =>
                table.HasCheckConstraint(
                    "ck_economy_projection_events_sequence_nonnegative",
                    "\"SourceJournalSequence\" >= 0"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.PreviousHash).HasMaxLength(128);
            builder.Property(row => row.RebuiltHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.WalletId, row.DetectedAt })
                .HasDatabaseName("ix_economy_projection_reconciliation_events_wallet_detected");
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyAccountRow>(builder =>
        {
            builder.ToTable("economy_accounts", table =>
                table.HasCheckConstraint(
                    "ck_economy_accounts_wallet_partition",
                    "(\"WalletId\" IS NULL AND \"Code\" NOT IN (2, 3, 4)) OR (\"WalletId\" IS NOT NULL AND \"Code\" IN (2, 3, 4))"));
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => new { row.WalletId, row.Code, row.Currency, row.Provenance }).IsUnique();
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSources(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomySourceStampRow>(builder =>
        {
            builder.ToTable("economy_source_stamps", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_source_stamps_units_nonnegative",
                    "\"AuthoritativeUnits\" >= 0");
                table.HasCheckConstraint(
                    "ck_economy_source_stamps_confirmation",
                    "(\"State\" IN (2, 5, 6) AND \"ConfirmedAt\" IS NOT NULL AND \"ConfirmedAt\" >= \"ObservedAt\") OR " +
                    "(\"State\" IN (1, 3, 4) AND \"ConfirmedAt\" IS NULL)");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.SourceKind).HasMaxLength(100);
            builder.Property(row => row.InternalSourceId).HasMaxLength(256);
            builder.Property(row => row.SourceLegId).HasMaxLength(256);
            builder.Property(row => row.Provider).HasMaxLength(100);
            builder.Property(row => row.ProviderReference).HasMaxLength(256);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.SourceKind, row.InternalSourceId, row.SourceLegId })
                .IsUnique()
                .HasDatabaseName("ux_economy_source_stamps_internal_leg");
            builder.HasIndex(row => new { row.Provider, row.ProviderReference })
                .IsUnique()
                .HasFilter("\"Provider\" IS NOT NULL AND \"ProviderReference\" IS NOT NULL")
                .HasDatabaseName("ux_economy_source_stamps_provider_reference");
        });

        modelBuilder.Entity<EconomySourceStampEventRow>(builder =>
        {
            builder.ToTable("economy_source_stamp_events", table =>
                table.HasCheckConstraint("ck_economy_source_stamp_events_sequence_positive", "\"Sequence\" > 0"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.SourceStampId, row.Sequence })
                .IsUnique()
                .HasDatabaseName("ux_economy_source_stamp_events_source_sequence");
            builder.HasOne<EconomySourceStampRow>()
                .WithMany()
                .HasForeignKey(row => row.SourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFunding(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyFundingClaimRow>(builder =>
        {
            builder.ToTable("economy_funding_claims", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_funding_claims_amount_positive",
                    "\"AuthoritativeUsdMinorUnits\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_funding_claims_version_positive",
                    "\"Version\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_funding_claims_provider_reversal_bounds",
                    "\"CumulativeProviderReversalUnits\" >= 0 AND \"CumulativeProviderReversalUnits\" <= \"AuthoritativeUsdMinorUnits\"");
                table.HasCheckConstraint(
                    "ck_economy_funding_claims_lifecycle",
                    "(\"State\" = 1 AND \"ConfirmedAt\" IS NULL AND \"StateChangedAt\" = \"ObservedAt\" AND \"PostingGroupId\" IS NULL AND \"RootCreditLotId\" IS NULL AND \"CumulativeProviderReversalUnits\" = 0) OR " +
                    "(\"State\" = 2 AND \"ConfirmedAt\" >= \"ObservedAt\" AND \"StateChangedAt\" >= \"ConfirmedAt\" AND \"PostingGroupId\" IS NOT NULL AND \"RootCreditLotId\" IS NOT NULL) OR " +
                    "(\"State\" IN (3, 4) AND \"ConfirmedAt\" IS NULL AND \"StateChangedAt\" >= \"ObservedAt\" AND \"PostingGroupId\" IS NULL AND \"RootCreditLotId\" IS NULL AND \"CumulativeProviderReversalUnits\" = 0) OR " +
                    "(\"State\" IN (5, 6) AND \"ConfirmedAt\" >= \"ObservedAt\" AND \"StateChangedAt\" >= \"ConfirmedAt\" AND \"PostingGroupId\" IS NOT NULL AND \"RootCreditLotId\" IS NOT NULL AND \"CumulativeProviderReversalUnits\" > 0)");
            });
            builder.HasKey(row => row.SourceStampId);
            builder.Property(row => row.Provider).HasMaxLength(100);
            builder.Property(row => row.Environment).HasMaxLength(50);
            builder.Property(row => row.ConnectedAccount).HasMaxLength(256);
            builder.Property(row => row.ProviderObject).HasMaxLength(256);
            builder.Property(row => row.ProviderMonetaryLeg).HasMaxLength(256);
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasIndex(row => new
            {
                row.Provider,
                row.Environment,
                row.ConnectedAccount,
                row.ProviderObject,
                row.ProviderMonetaryLeg
            })
                .IsUnique()
                .HasDatabaseName("ux_economy_funding_claims_provider_leg");
            builder.HasIndex(row => row.PostingGroupId)
                .IsUnique()
                .HasFilter("\"PostingGroupId\" IS NOT NULL")
                .HasDatabaseName("ux_economy_funding_claims_posting_group");
            builder.HasIndex(row => row.RootCreditLotId)
                .IsUnique()
                .HasFilter("\"RootCreditLotId\" IS NOT NULL")
                .HasDatabaseName("ux_economy_funding_claims_root_lot");
            builder.HasOne<EconomySourceStampRow>()
                .WithOne()
                .HasForeignKey<EconomyFundingClaimRow>(row => row.SourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyPostingGroupRow>()
                .WithOne()
                .HasForeignKey<EconomyFundingClaimRow>(row => row.PostingGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyCreditLotRow>()
                .WithOne()
                .HasForeignKey<EconomyFundingClaimRow>(row => row.RootCreditLotId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDisputes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyProviderDisputeRow>(builder =>
        {
            builder.ToTable("economy_provider_disputes", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_provider_disputes_sequence_positive",
                    "\"LatestProviderSequence\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_provider_disputes_version_positive",
                    "\"Version\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_provider_disputes_amount_partition",
                    "\"CumulativeDisputedHardUnits\" > 0 AND \"BaselineReversedHardUnits\" >= 0 AND " +
                    "\"BaselineReversedHardUnits\" <= \"CumulativeDisputedHardUnits\" AND " +
                    "\"FrozenHardEquivalentUnits\" >= 0 AND " +
                    "\"FrozenHardEquivalentUnits\" <= (\"CumulativeDisputedHardUnits\" - \"BaselineReversedHardUnits\")");
                table.HasCheckConstraint(
                    "ck_economy_provider_disputes_lifecycle",
                    "(\"Status\" = 1 AND \"ReversalIdempotencyKey\" IS NULL) OR " +
                    "(\"Status\" = 2 AND \"FrozenHardEquivalentUnits\" = 0 AND \"ReversalIdempotencyKey\" IS NULL) OR " +
                    "(\"Status\" = 3 AND \"FrozenHardEquivalentUnits\" = 0 AND \"ReversalIdempotencyKey\" IS NOT NULL)");
            });
            builder.HasKey(row => row.ProviderDisputeReference);
            builder.Property(row => row.ProviderDisputeReference).HasMaxLength(256);
            builder.Property(row => row.ReversalIdempotencyKey).HasMaxLength(128);
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasIndex(row => row.SourceStampId)
                .IsUnique()
                .HasFilter("\"Status\" = 1")
                .HasDatabaseName("ux_economy_provider_disputes_active_source");
            builder.HasOne<EconomySourceStampRow>()
                .WithMany()
                .HasForeignKey(row => row.SourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.ResponsibleWalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyProviderDisputeEventRow>(builder =>
        {
            builder.ToTable("economy_provider_dispute_events", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_provider_dispute_events_sequence_positive",
                    "\"ProviderSequence\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_provider_dispute_events_amount_positive",
                    "\"CumulativeDisputedHardUnits\" > 0");
            });
            builder.HasKey(row => row.ProviderEventId);
            builder.Property(row => row.ProviderEventId).HasMaxLength(256);
            builder.Property(row => row.ProviderDisputeReference).HasMaxLength(256);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.ProviderDisputeReference, row.ProviderSequence })
                .IsUnique()
                .HasDatabaseName("ux_economy_provider_dispute_events_dispute_sequence");
            builder.HasOne<EconomyProviderDisputeRow>()
                .WithMany()
                .HasForeignKey(row => row.ProviderDisputeReference)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomySourceStampRow>()
                .WithMany()
                .HasForeignKey(row => row.SourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyDisputeFragmentFreezeRow>(builder =>
        {
            builder.ToTable("economy_dispute_fragment_freezes", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_dispute_fragment_freezes_amount_positive",
                    "\"AmountUnits\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_dispute_fragment_freezes_state_timestamp",
                    "(\"Status\" = 1 AND \"TerminalAt\" IS NULL) OR " +
                    "(\"Status\" IN (2, 3) AND \"TerminalAt\" >= \"PlacedAt\")");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.ProviderDisputeReference).HasMaxLength(256);
            builder.HasIndex(row => new { row.RootSourceStampId, row.Status })
                .HasDatabaseName("ix_economy_dispute_fragment_freezes_root_status");
            builder.HasOne<EconomyProviderDisputeRow>()
                .WithMany()
                .HasForeignKey(row => row.ProviderDisputeReference)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomySourceStampRow>()
                .WithMany()
                .HasForeignKey(row => row.RootSourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyCreditLotRow>()
                .WithMany()
                .HasForeignKey(row => row.CreditLotId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyDisputeFragmentRangeRow>(builder =>
        {
            builder.ToTable("economy_dispute_fragment_ranges", table =>
                table.HasCheckConstraint(
                    "ck_economy_dispute_fragment_ranges_half_open",
                    "\"StartInclusive\" >= 0 AND \"EndExclusive\" > \"StartInclusive\" AND \"ReversalEpoch\" >= 0"));
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => new
                {
                    row.DisputeFragmentFreezeId,
                    row.StartInclusive,
                    row.EndExclusive
                })
                .IsUnique()
                .HasDatabaseName("ux_economy_dispute_fragment_ranges_freeze_interval");
            builder.HasOne<EconomyDisputeFragmentFreezeRow>()
                .WithMany()
                .HasForeignKey(row => row.DisputeFragmentFreezeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyWalletDebtRow>(builder =>
        {
            builder.ToTable("economy_wallet_debts", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_wallet_debts_nonnegative",
                    "\"OutstandingHardUnits\" >= 0");
                table.HasCheckConstraint(
                    "ck_economy_wallet_debts_version_positive",
                    "\"Version\" > 0");
            });
            builder.HasKey(row => row.WalletId);
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasOne<EconomyWalletRow>()
                .WithOne()
                .HasForeignKey<EconomyWalletDebtRow>(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyWalletDebtEventRow>(builder =>
        {
            builder.ToTable("economy_wallet_debt_events", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_wallet_debt_events_sequence_positive",
                    "\"Sequence\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_wallet_debt_events_delta_nonzero",
                    "\"DeltaHardUnits\" <> 0 AND \"OutstandingHardUnits\" >= 0");
            });
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => new { row.WalletId, row.Sequence })
                .IsUnique()
                .HasDatabaseName("ux_economy_wallet_debt_events_wallet_sequence");
            builder.HasOne<EconomyWalletDebtRow>()
                .WithMany()
                .HasForeignKey(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomySourceStampRow>()
                .WithMany()
                .HasForeignKey(row => row.SourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePostings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyPostingGroupRow>(builder =>
        {
            builder.ToTable("economy_posting_groups", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_posting_groups_reserve_authorization",
                    "\"ReserveVersion\" > 0 AND \"ReserveAuthorizationEpoch\" > 0 AND \"RiskDecisionId\" IS NOT NULL");
                table.HasCheckConstraint(
                    "ck_economy_posting_groups_template_state",
                    "\"TemplateKind\" BETWEEN 1 AND 26 AND \"TemplateVersion\" = 1 AND \"Status\" = 1");
                table.HasCheckConstraint(
                    "ck_economy_posting_groups_authority_template",
                    "(\"TemplateKind\" IN (1, 2, 3, 18, 19, 20) AND \"Authority\" = 1) OR " +
                    "(\"TemplateKind\" IN (4, 5, 7, 8, 17, 22) AND \"Authority\" = 2) OR " +
                    "(\"TemplateKind\" IN (6, 21) AND \"Authority\" = 3) OR " +
                    "(\"TemplateKind\" IN (9, 10, 23, 24) AND \"Authority\" = 4) OR " +
                    "(\"TemplateKind\" IN (11, 12, 13) AND \"Authority\" = 5) OR " +
                    "(\"TemplateKind\" IN (14, 15, 16) AND \"Authority\" = 6) OR " +
                    "(\"TemplateKind\" IN (25, 26) AND \"Authority\" = 7)");
                table.HasCheckConstraint(
                    "ck_economy_posting_groups_source_requirement",
                    "\"TemplateKind\" NOT IN (1, 2, 3, 18, 19, 20) OR \"SourceStampId\" IS NOT NULL");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.IdempotencyKey).HasMaxLength(128);
            builder.HasIndex(row => row.IdempotencyKey)
                .IsUnique()
                .HasDatabaseName("ux_economy_posting_groups_idempotency_key");
            builder.HasIndex(row => row.SourceStampId)
                .IsUnique()
                .HasFilter("\"SourceStampId\" IS NOT NULL AND \"TemplateKind\" = 1")
                .HasDatabaseName("ux_economy_posting_groups_source_stamp");
            builder.HasOne<EconomySourceStampRow>()
                .WithMany()
                .HasForeignKey(row => row.SourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyJournalEntryRow>(builder =>
        {
            builder.ToTable("economy_journal_entries", table =>
                table.HasCheckConstraint(
                    "ck_economy_journal_entries_hash_algorithm",
                    "(\"HashAlgorithmVersion\" = 0 AND \"CanonicalPayloadHash\" IS NULL) OR " +
                    "(\"HashAlgorithmVersion\" IN (1, 2) AND length(btrim(\"CanonicalPayloadHash\")) > 0)"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.PreviousHash).HasMaxLength(128);
            builder.Property(row => row.CanonicalPayloadHash).HasMaxLength(128);
            builder.Property(row => row.Hash).HasMaxLength(128);
            builder.HasIndex(row => row.PostingGroupId)
                .IsUnique()
                .HasDatabaseName("ux_economy_journal_entries_posting_group_id");
            builder.HasIndex(row => row.Sequence)
                .IsUnique()
                .HasDatabaseName("ux_economy_journal_entries_sequence");
            builder.HasOne<EconomyPostingGroupRow>()
                .WithMany()
                .HasForeignKey(row => row.PostingGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyJournalLineRow>(builder =>
        {
            builder.ToTable("economy_journal_lines", table =>
                table.HasCheckConstraint("ck_economy_journal_lines_amount_positive", "\"AmountUnits\" > 0"));
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => new { row.JournalEntryId, row.Sequence }).IsUnique();
            builder.HasOne<EconomyJournalEntryRow>()
                .WithMany()
                .HasForeignKey(row => row.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyAccountRow>()
                .WithMany()
                .HasForeignKey(row => row.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyCreditLotRow>()
                .WithMany()
                .HasForeignKey(row => row.CreditLotId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLedger(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyCreditLotRow>(builder =>
        {
            builder.ToTable("economy_credit_lots", table =>
            {
                table.HasCheckConstraint("ck_economy_credit_lots_amount_positive", "\"AmountUnits\" > 0");
                table.HasCheckConstraint("ck_economy_credit_lots_maturity_order", "\"OriginalMaturesAt\" >= \"ConfirmedAt\"");
                table.HasCheckConstraint(
                    "ck_economy_credit_lots_maturity_policy",
                    "(\"Provenance\" = 2 AND \"Currency\" = 1 AND \"CashOutEligible\" AND \"OriginalMaturesAt\" = \"ConfirmedAt\" + INTERVAL '120 days') OR (\"Provenance\" <> 2 AND NOT \"CashOutEligible\")");
            });
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => row.RootSourceStampId)
                .HasDatabaseName("ix_economy_credit_lots_root_source");
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomySourceStampRow>()
                .WithMany()
                .HasForeignKey(row => row.RootSourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyEntryAllocationRow>(builder =>
        {
            builder.ToTable("economy_entry_allocations", table =>
                table.HasCheckConstraint("ck_economy_entry_allocations_amount_positive", "\"AmountUnits\" > 0"));
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => row.ParentLotId).HasDatabaseName("ix_economy_entry_allocations_parent_lot");
            builder.HasIndex(row => new { row.JournalLineId, row.ParentLotId })
                .IsUnique()
                .HasDatabaseName("ux_economy_entry_allocations_line_parent");
            builder.HasOne<EconomyJournalLineRow>()
                .WithMany()
                .HasForeignKey(row => row.JournalLineId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyCreditLotRow>()
                .WithMany()
                .HasForeignKey(row => row.ParentLotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyProviderFactAllocationRow>(builder =>
        {
            builder.ToTable("economy_provider_fact_allocations", table =>
                table.HasCheckConstraint(
                    "ck_economy_provider_fact_allocations_cumulative_bounds",
                    "\"AllocatedUnits\" > 0 AND \"CumulativeCreditedUnits\" >= \"AllocatedUnits\" AND \"CumulativeCreditedUnits\" <= \"AuthoritativeUnits\""));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Provider).HasMaxLength(100);
            builder.Property(row => row.Environment).HasMaxLength(50);
            builder.Property(row => row.ConnectedAccount).HasMaxLength(256);
            builder.Property(row => row.ProviderObject).HasMaxLength(256);
            builder.Property(row => row.ProviderMonetaryLeg).HasMaxLength(256);
            builder.HasIndex(row => new
            {
                row.Provider,
                row.Environment,
                row.ConnectedAccount,
                row.ProviderObject,
                row.ProviderMonetaryLeg
            })
                .IsUnique()
                .HasDatabaseName("ux_economy_provider_fact_allocations_provider_leg");
            builder.HasOne<EconomySourceStampRow>()
                .WithMany()
                .HasForeignKey(row => row.SourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyJournalLineRow>()
                .WithMany()
                .HasForeignKey(row => row.JournalLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLineage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyLotLineageEdgeRow>(builder =>
        {
            builder.ToTable("economy_lot_lineage_edges", table =>
                table.HasCheckConstraint("ck_economy_lot_lineage_edges_amount_positive", "\"AmountUnits\" > 0"));
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => row.ParentLotId).HasDatabaseName("ix_economy_lot_lineage_edges_parent_lot");
            builder.HasIndex(row => new { row.ParentLotId, row.ChildLotId })
                .IsUnique()
                .HasDatabaseName("ux_economy_lot_lineage_edges_parent_child");
            builder.HasOne<EconomyCreditLotRow>()
                .WithMany()
                .HasForeignKey(row => row.ParentLotId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyCreditLotRow>()
                .WithMany()
                .HasForeignKey(row => row.ChildLotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyFragmentRootRangeRow>(builder =>
        {
            builder.ToTable("economy_fragment_root_ranges", table =>
            {
                table.HasCheckConstraint("ck_economy_fragment_root_ranges_half_open", "\"StartInclusive\" >= 0 AND \"EndExclusive\" > \"StartInclusive\"");
                table.HasCheckConstraint("ck_economy_fragment_root_ranges_single_owner", "(\"CreditLotId\" IS NULL) <> (\"EntryAllocationId\" IS NULL)");
            });
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => new { row.RootSourceStampId, row.ReversalEpoch })
                .HasDatabaseName("ix_economy_fragment_root_ranges_root_epoch");
            builder.HasOne<EconomySourceStampRow>()
                .WithMany()
                .HasForeignKey(row => row.RootSourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyCreditLotRow>()
                .WithMany()
                .HasForeignKey(row => row.CreditLotId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyEntryAllocationRow>()
                .WithMany()
                .HasForeignKey(row => row.EntryAllocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyRootReversalStateRow>(builder =>
        {
            builder.ToTable("economy_root_reversal_states", table =>
            {
                table.HasCheckConstraint("ck_economy_root_reversal_states_epoch_nonnegative", "\"Epoch\" >= 0");
                table.HasCheckConstraint(
                    "ck_economy_root_reversal_states_cumulative_bounds",
                    "\"CumulativeProviderUnits\" >= 0 AND \"ReversedUnits\" >= 0 AND \"ReversedUnits\" <= \"CumulativeProviderUnits\"");
            });
            builder.HasKey(row => row.RootSourceStampId);
            builder.HasIndex(row => new { row.RootSourceStampId, row.Epoch })
                .IsUnique()
                .HasDatabaseName("ux_economy_root_reversal_states_root_epoch");
            builder.Property(row => row.State).HasMaxLength(50);
            builder.Property(row => row.TargetedRanges).HasColumnType("jsonb");
            builder.HasOne<EconomySourceStampRow>()
                .WithMany()
                .HasForeignKey(row => row.RootSourceStampId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOperations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyDispatchSnapshotRow>(builder =>
        {
            builder.ToTable("economy_dispatch_snapshots", table =>
            {
                table.HasCheckConstraint("ck_economy_dispatch_snapshots_amount_positive", "\"AmountUnits\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_dispatch_snapshots_reserve_authorization",
                    "\"ReserveVersion\" > 0 AND \"ReserveAuthorizationEpoch\" > 0");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.SnapshotHash).HasMaxLength(128);
            builder.Property(row => row.Destination).HasMaxLength(512);
            builder.Property(row => row.EligibilityPayload).HasColumnType("jsonb");
            builder.Property(row => row.ChainHash).HasMaxLength(128);
            builder.HasIndex(row => row.SnapshotHash)
                .IsUnique()
                .HasDatabaseName("ux_economy_dispatch_snapshots_hash");
            builder.HasOne<EconomyPostingGroupRow>()
                .WithMany()
                .HasForeignKey(row => row.PostingGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyOutboxMessageRow>(builder =>
        {
            builder.ToTable("economy_outbox_messages");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Type).HasMaxLength(200);
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.HasIndex(row => row.PayloadHash)
                .IsUnique()
                .HasDatabaseName("ux_economy_outbox_messages_payload_hash");
            builder.HasOne<EconomyPostingGroupRow>()
                .WithMany()
                .HasForeignKey(row => row.PostingGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyIdempotencyRecordRow>(builder =>
        {
            builder.ToTable("economy_idempotency_records");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Key).HasMaxLength(128);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.HasIndex(row => row.Key)
                .IsUnique()
                .HasDatabaseName("ux_economy_idempotency_records_key");
            builder.HasOne<EconomyPostingGroupRow>()
                .WithMany()
                .HasForeignKey(row => row.PostingGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureChain(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyChainHeadRow>(builder =>
        {
            builder.ToTable("economy_chain_head", table =>
                table.HasCheckConstraint("ck_economy_chain_head_singleton", "\"Id\" = 1"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Hash).HasMaxLength(128);
        });

        modelBuilder.Entity<EconomyExternalAnchorRow>(builder =>
        {
            builder.ToTable("economy_external_anchors");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.JournalHash).HasMaxLength(128);
            builder.Property(row => row.Signature).HasMaxLength(1024);
            builder.Property(row => row.WormReference).HasMaxLength(1024);
            builder.Property(row => row.DispatchSnapshotHash).HasMaxLength(128);
            builder.Property(row => row.Provider).HasMaxLength(100);
            builder.Property(row => row.ProviderReference).HasMaxLength(256);
            builder.HasIndex(row => row.JournalSequence)
                .HasDatabaseName("ix_economy_external_anchors_chain_sequence");
        });
    }

    private static void ConfigureReserves(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyReserveHeadRow>(builder =>
        {
            builder.ToTable("economy_reserve_heads", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_reserve_heads_versions_positive",
                    "\"Version\" > 0 AND \"PolicyVersion\" > 0 AND \"AuthorizationEpoch\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_reserve_heads_window",
                    "\"ExpiresAt\" > \"ObservedAt\" AND \"ActivatedAt\" >= \"ObservedAt\"");
                table.HasCheckConstraint(
                    "ck_economy_reserve_heads_amounts_nonnegative",
                    "\"HardFaceValueUsdMinor\" >= 0 AND \"RequiredHardReserveUsdMinor\" >= 0 AND " +
                    "\"SoftFaceValueUsdNanos\" >= 0 AND \"StressedExpectedRedemptionCostUsdNanos\" >= 0 AND " +
                    "\"RequiredSoftReserveUsdNanos\" >= 0 AND \"HardBackingUsdNanos\" >= 0 AND \"SoftBackingUsdNanos\" >= 0");
                table.HasCheckConstraint(
                    "ck_economy_reserve_heads_values_valid",
                    "\"Coverage\" IN (1, 2) AND length(btrim(\"EvidenceHash\")) > 0");
            });
            builder.HasKey(row => row.Version);
            builder.Property(row => row.Version).ValueGeneratedNever();
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => row.IsActive)
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE")
                .HasDatabaseName("ux_economy_reserve_heads_active");
            builder.HasIndex(row => row.AuthorizationEpoch)
                .IsUnique()
                .HasDatabaseName("ux_economy_reserve_heads_authorization_epoch");
        });

        modelBuilder.Entity<EconomyReserveAssetAllocationRow>(builder =>
        {
            builder.ToTable("economy_reserve_asset_allocations", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_reserve_asset_allocations_value_positive",
                    "\"EligibleUsdNanos\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_reserve_asset_allocations_values_valid",
                    "\"Purpose\" IN (1, 2) AND length(btrim(\"AssetKey\")) > 0");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.AssetKey).HasMaxLength(256);
            builder.HasIndex(row => new { row.ReserveVersion, row.AssetKey })
                .IsUnique()
                .HasDatabaseName("ux_economy_reserve_asset_allocations_version_asset");
            builder.HasOne<EconomyReserveHeadRow>()
                .WithMany()
                .HasForeignKey(row => row.ReserveVersion)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRisk(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyRegisteredCapabilityRow>(builder =>
        {
            builder.ToTable("economy_registered_capabilities", table =>
                table.HasCheckConstraint(
                    "ck_economy_registered_capabilities_state",
                    "(\"IsEnabled\" AND \"RevokedAt\" IS NULL) OR (NOT \"IsEnabled\" AND \"RevokedAt\" IS NOT NULL)"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Name).HasMaxLength(100);
            builder.Property(row => row.AllowedTemplateKinds).HasColumnType("jsonb");
            builder.HasIndex(row => row.Name)
                .IsUnique()
                .HasDatabaseName("ux_economy_registered_capabilities_name");
        });

        modelBuilder.Entity<EconomyRiskDecisionRow>(builder =>
        {
            builder.ToTable("economy_risk_decisions", table =>
            {
                table.HasCheckConstraint("ck_economy_risk_decisions_amount_positive", "\"AmountUnits\" > 0");
                table.HasCheckConstraint("ck_economy_risk_decisions_lifetime", "\"ExpiresAt\" > \"IssuedAt\"");
                table.HasCheckConstraint(
                    "ck_economy_risk_decisions_versions_positive",
                    "\"PolicyVersion\" > 0 AND \"ReserveVersion\" > 0 AND \"ReserveAuthorizationEpoch\" > 0 AND " +
                    "\"FeatureVersion\" > 0 AND \"CounterVersion\" > 0 AND \"EntityGraphVersion\" >= 0");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.OperationFingerprint).HasMaxLength(128);
            builder.Property(row => row.IdempotencyKey).HasMaxLength(128);
            builder.Property(row => row.ActorHash).HasMaxLength(128);
            builder.Property(row => row.ProviderReferenceHash).HasMaxLength(128);
            builder.Property(row => row.EntityGraphEvidenceHash).HasMaxLength(128);
            builder.Property(row => row.CurrencyLegs).HasColumnType("jsonb");
            builder.Property(row => row.SourceRoots).HasColumnType("jsonb");
            builder.Property(row => row.ReasonCodes).HasColumnType("jsonb");
            builder.HasIndex(row => row.OperationFingerprint)
                .IsUnique()
                .HasDatabaseName("ix_economy_risk_decisions_operation_fingerprint");
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.SourceWalletId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.DestinationWalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyRiskDecisionConsumptionRow>(builder =>
        {
            builder.ToTable("economy_risk_decision_consumptions");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.OperationFingerprint).HasMaxLength(128);
            builder.HasIndex(row => row.RiskDecisionId)
                .IsUnique()
                .HasDatabaseName("ux_economy_risk_decision_consumptions_decision");
            builder.HasIndex(row => row.PostingGroupId)
                .IsUnique()
                .HasDatabaseName("ux_economy_risk_decision_consumptions_posting");
            builder.HasOne<EconomyRiskDecisionRow>()
                .WithMany()
                .HasForeignKey(row => row.RiskDecisionId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyPostingGroupRow>()
                .WithMany()
                .HasForeignKey(row => row.PostingGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyRiskCounterRow>(builder =>
        {
            builder.ToTable("economy_risk_counters", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_risk_counters_bounds",
                    "\"CounterVersion\" > 0 AND \"MaxUnits\" > 0 AND \"UsedUnits\" >= 0 AND \"UsedUnits\" <= \"MaxUnits\"");
                table.HasCheckConstraint(
                    "ck_economy_risk_counters_window",
                    "\"WindowEndsAt\" > \"WindowStartedAt\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.SubjectHash).HasMaxLength(128);
            builder.HasIndex(row => new
            {
                row.TenantId,
                row.Dimension,
                row.SubjectHash,
                row.Operation,
                row.Currency,
                row.WindowStartedAt
            })
                .IsUnique()
                .HasDatabaseName("ux_economy_risk_counters_scope_window");
        });

        modelBuilder.Entity<EconomyRiskCounterReservationRow>(builder =>
        {
            builder.ToTable("economy_risk_counter_reservations", table =>
            {
                table.HasCheckConstraint("ck_economy_risk_counter_reservations_amount_positive", "\"AmountUnits\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_risk_counter_reservations_lifetime",
                    "\"ExpiresAt\" > \"ReservedAt\"");
                table.HasCheckConstraint(
                    "ck_economy_risk_counter_reservations_state",
                    "(\"Status\" = 1 AND \"ConsumedAt\" IS NULL AND \"ReleasedAt\" IS NULL) OR " +
                    "(\"Status\" = 2 AND \"ConsumedAt\" >= \"ReservedAt\" AND \"ReleasedAt\" IS NULL) OR " +
                    "(\"Status\" = 3 AND \"ReleasedAt\" >= \"ReservedAt\" AND \"ConsumedAt\" IS NULL) OR " +
                    "(\"Status\" = 4 AND \"ReleasedAt\" >= \"ExpiresAt\" AND \"ConsumedAt\" IS NULL)");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.InputFingerprint).HasMaxLength(128);
            builder.HasIndex(row => new { row.ReservationGroupId, row.RiskCounterId })
                .IsUnique()
                .HasDatabaseName("ux_economy_risk_counter_reservations_group_counter");
            builder.HasIndex(row => new { row.RiskDecisionId, row.RiskCounterId })
                .IsUnique()
                .HasDatabaseName("ux_economy_risk_counter_reservations_decision_counter");
            builder.HasOne<EconomyRiskDecisionRow>()
                .WithMany()
                .HasForeignKey(row => row.RiskDecisionId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyRiskCounterRow>()
                .WithMany()
                .HasForeignKey(row => row.RiskCounterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyProtectedChangeCooldownRow>(builder =>
        {
            builder.ToTable("economy_protected_change_cooldowns", table =>
            {
                table.HasCheckConstraint("ck_economy_protected_change_cooldowns_version", "\"Version\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_protected_change_cooldowns_window",
                    "\"AvailableAt\" > \"ChangedAt\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.ValueHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.TenantId, row.SubjectId, row.Kind, row.Version })
                .IsUnique()
                .HasDatabaseName("ux_economy_protected_change_cooldowns_subject_kind_version");
        });

        modelBuilder.Entity<EconomyHoldRow>(builder =>
        {
            builder.ToTable("economy_holds", table =>
            {
                table.HasCheckConstraint("ck_economy_holds_amount_positive", "\"AmountUnits\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_holds_state_timestamp",
                    "(\"Status\" = 1 AND \"ReleasedAt\" IS NULL) OR (\"Status\" <> 1 AND \"ReleasedAt\" >= \"EffectiveAt\")");
            });
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => new { row.WalletId, row.Status })
                .HasDatabaseName("ix_economy_holds_wallet_status");
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyHoldEventRow>(builder =>
        {
            builder.ToTable("economy_hold_events", table =>
                table.HasCheckConstraint("ck_economy_hold_events_sequence_positive", "\"Sequence\" > 0"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.HoldId, row.Sequence })
                .IsUnique()
                .HasDatabaseName("ux_economy_hold_events_hold_sequence");
            builder.HasOne<EconomyHoldRow>()
                .WithMany()
                .HasForeignKey(row => row.HoldId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyRiskReviewCaseRow>(builder =>
        {
            builder.ToTable("economy_risk_review_cases", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_risk_review_cases_approvals",
                    "\"RequiredApprovals\" BETWEEN 1 AND 2");
                table.HasCheckConstraint(
                    "ck_economy_risk_review_cases_state",
                    "(\"Status\" = 1 AND \"ResolvedAt\" IS NULL AND \"ResolvedBy\" IS NULL AND \"Resolution\" IS NULL) OR (\"Status\" IN (2, 3) AND \"ResolvedAt\" >= \"SubmittedAt\" AND \"ResolvedBy\" IS NOT NULL AND length(btrim(\"Resolution\")) > 0)");
            });
            builder.HasKey(row => row.Id);
            builder.HasIndex(row => new { row.TenantId, row.RiskDecisionId })
                .IsUnique()
                .HasDatabaseName("ux_economy_risk_review_cases_tenant_decision");
            builder.HasOne<EconomyRiskDecisionRow>()
                .WithMany()
                .HasForeignKey(row => row.RiskDecisionId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyRiskReviewCaseRow>()
                .WithMany()
                .HasForeignKey(row => row.AppealOf)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyRiskReviewEventRow>(builder =>
        {
            builder.ToTable("economy_risk_review_events", table =>
                table.HasCheckConstraint("ck_economy_risk_review_events_sequence_positive", "\"Sequence\" > 0"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.EvidenceHashes).HasColumnType("jsonb");
            builder.HasIndex(row => new { row.RiskReviewCaseId, row.Sequence })
                .IsUnique()
                .HasDatabaseName("ux_economy_risk_review_events_case_sequence");
            builder.HasOne<EconomyRiskReviewCaseRow>()
                .WithMany()
                .HasForeignKey(row => row.RiskReviewCaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyRiskAuditEvidenceRow>(builder =>
        {
            builder.ToTable("economy_risk_audit_evidence");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.EventKind).HasMaxLength(100);
            builder.Property(row => row.OperationFingerprint).HasMaxLength(128);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.Property(row => row.Payload).HasColumnType("jsonb");
            builder.HasIndex(row => new { row.RiskDecisionId, row.EvidenceHash })
                .IsUnique()
                .HasDatabaseName("ux_economy_risk_audit_evidence_decision_hash");
            builder.HasOne<EconomyRiskDecisionRow>()
                .WithMany()
                .HasForeignKey(row => row.RiskDecisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyTopUpIntentRow>(builder =>
        {
            builder.ToTable("economy_top_up_intents", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_top_up_intents_amount_positive",
                    "\"HardCoinUnits\" > 0 AND \"UsdMinorUnits\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_top_up_intents_provider_binding",
                    "(\"Status\" = 1 AND \"ProviderEnvironment\" IS NULL AND \"ProviderAccountId\" IS NULL AND " +
                    "\"ProviderObjectId\" IS NULL AND \"ProviderObjectType\" IS NULL AND " +
                    "\"ProviderMonetaryLeg\" IS NULL AND \"ProviderBoundAt\" IS NULL) OR " +
                    "(\"Status\" <> 1 AND \"ProviderEnvironment\" IS NOT NULL AND \"ProviderAccountId\" IS NOT NULL AND " +
                    "\"ProviderObjectId\" IS NOT NULL AND \"ProviderObjectType\" IS NOT NULL AND " +
                    "\"ProviderMonetaryLeg\" IS NOT NULL AND \"ProviderBoundAt\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_economy_top_up_intents_version_positive",
                    "\"Version\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_top_up_intents_event_state",
                    "(\"LastProviderEventId\" IS NULL AND \"LastProviderEventAt\" IS NULL AND " +
                    "\"LastProviderEvidenceHash\" IS NULL) OR (\"LastProviderEventId\" IS NOT NULL AND " +
                    "\"LastProviderEventAt\" IS NOT NULL AND \"LastProviderEvidenceHash\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_economy_top_up_intents_posting_state",
                    "(\"Status\" = 5 AND \"PostingGroupId\" IS NOT NULL) OR " +
                    "(\"Status\" <> 5 AND \"PostingGroupId\" IS NULL)");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.JurisdictionCode).HasMaxLength(16);
            builder.Property(row => row.PolicyHash).HasMaxLength(128);
            builder.Property(row => row.Provider).HasMaxLength(64);
            builder.Property(row => row.IdempotencyKey).HasMaxLength(128);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.Property(row => row.ProviderEnvironment).HasMaxLength(32);
            builder.Property(row => row.ProviderAccountId).HasMaxLength(255);
            builder.Property(row => row.ProviderObjectId).HasMaxLength(255);
            builder.Property(row => row.ProviderObjectType).HasMaxLength(100);
            builder.Property(row => row.ProviderMonetaryLeg).HasMaxLength(100);
            builder.Property(row => row.LastProviderEventId).HasMaxLength(255);
            builder.Property(row => row.LastProviderEvidenceHash).HasMaxLength(128);
            builder.Property(row => row.FailureCode).HasMaxLength(100);
            builder.Property(row => row.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasIndex(row => new { row.TenantId, row.ActorId, row.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ux_economy_top_up_intents_actor_key");
            builder.HasIndex(row => row.PaymentId)
                .IsUnique()
                .HasDatabaseName("ux_economy_top_up_intents_payment");
            builder.HasIndex(row => new
                {
                    row.Provider,
                    row.ProviderEnvironment,
                    row.ProviderAccountId,
                    row.ProviderObjectId,
                    row.ProviderObjectType,
                    row.ProviderMonetaryLeg
                })
                .IsUnique()
                .HasFilter("\"ProviderObjectId\" IS NOT NULL")
                .HasDatabaseName("ux_economy_top_up_intents_provider_object");
            builder.HasOne<EconomyWalletRow>()
                .WithMany()
                .HasForeignKey(row => row.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyPostingGroupRow>()
                .WithMany()
                .HasForeignKey(row => row.PostingGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSelfServiceTransfers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomySelfServiceTransferIntentRow>(builder =>
        {
            builder.ToTable("economy_self_service_transfer_intents", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_self_service_transfer_intents_amount_positive",
                    "\"AmountUnits\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_self_service_transfer_intents_type_valid",
                    "\"TransferType\" IN (1, 2, 3)");
                table.HasCheckConstraint(
                    "ck_economy_self_service_transfer_intents_currency_provenance",
                    "(\"Currency\" = 1 AND \"Provenance\" = 1) OR " +
                    "(\"Currency\" = 2 AND \"Provenance\" = 3)");
                table.HasCheckConstraint(
                    "ck_economy_self_service_transfer_intents_parties_distinct",
                    "\"ActorId\" <> \"RecipientUserId\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.IdempotencyKey).HasMaxLength(128);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.Property(row => row.ProviderReferenceHash).HasMaxLength(128);
            builder.Property(row => row.DestinationHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.TenantId, row.ActorId, row.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ux_economy_self_service_transfer_intents_actor_key");
            builder.HasIndex(row => new { row.TenantId, row.RecipientUserId, row.RequestedAt })
                .HasDatabaseName("ix_economy_self_service_transfer_intents_recipient_time");
        });
    }

    private static void ConfigureEconomyWalletProvisioningReceipt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyWalletProvisioningReceiptRow>(builder =>
        {
            builder.HasNoKey();
            builder.ToView(null);
            builder.Property(row => row.WalletId).HasColumnName("wallet_id");
            builder.Property(row => row.Created).HasColumnName("created");
        });
    }

    private static void ConfigureLegacyShadowMigration(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyLegacyShadowBatchRow>(builder =>
        {
            builder.ToTable("economy_legacy_shadow_batches", table =>
            {
                table.HasCheckConstraint("ck_economy_legacy_shadow_batches_counts",
                    "\"WalletCount\" >= 0 AND \"TransactionCount\" >= 0 AND \"FinancialLedgerEntryCount\" >= 0");
                table.HasCheckConstraint("ck_economy_legacy_shadow_batches_units",
                    "\"ExpectedHardUnits\" >= 0 AND \"BackfilledHardUnits\" >= 0 AND \"ReconciledHardUnits\" >= 0");
                table.HasCheckConstraint("ck_economy_legacy_shadow_batches_state", "\"State\" BETWEEN 1 AND 8");
                table.HasCheckConstraint("ck_economy_legacy_shadow_batches_version", "\"Version\" > 0");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.JurisdictionCode).HasMaxLength(16);
            builder.Property(row => row.WalletSnapshotHash).HasMaxLength(128);
            builder.Property(row => row.TransactionSnapshotHash).HasMaxLength(128);
            builder.Property(row => row.FinancialLedgerSnapshotHash).HasMaxLength(128);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.Property(row => row.FailureCode).HasMaxLength(128);
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasIndex(row => new { row.TenantId, row.State });
            builder.HasIndex(row => row.RequestHash).IsUnique();
        });

        modelBuilder.Entity<EconomyLegacyShadowWalletRow>(builder =>
        {
            builder.ToTable("economy_legacy_shadow_wallets", table =>
            {
                table.HasCheckConstraint("ck_economy_legacy_shadow_wallets_units",
                    "\"LegacyBalanceMinorUnits\" >= 0 AND \"CompletedCreditsMinorUnits\" >= 0 AND \"CompletedDebitsMinorUnits\" >= 0");
                table.HasCheckConstraint("ck_economy_legacy_shadow_wallets_transactions", "\"TransactionCount\" >= 0");
                table.HasCheckConstraint("ck_economy_legacy_shadow_wallets_state", "\"State\" BETWEEN 1 AND 5");
                table.HasCheckConstraint("ck_economy_legacy_shadow_wallets_version", "\"Version\" > 0");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.SnapshotHash).HasMaxLength(128);
            builder.Property(row => row.JournalHash).HasMaxLength(128);
            builder.Property(row => row.ReconciliationHash).HasMaxLength(128);
            builder.Property(row => row.FailureCode).HasMaxLength(128);
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasIndex(row => new { row.BatchId, row.LegacyWalletId }).IsUnique();
            builder.HasIndex(row => row.SourceStampId).IsUnique();
            builder.HasIndex(row => row.PostingId).IsUnique();
            builder.HasIndex(row => row.CreditLotId).IsUnique();
            builder.HasOne<EconomyLegacyShadowBatchRow>().WithMany().HasForeignKey(row => row.BatchId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyWalletRow>().WithMany().HasForeignKey(row => row.EconomyWalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyLegacyCutoverRow>(builder =>
        {
            builder.ToTable("economy_legacy_cutovers", table =>
            {
                table.HasCheckConstraint("ck_economy_legacy_cutovers_state", "\"State\" BETWEEN 1 AND 4");
                table.HasCheckConstraint("ck_economy_legacy_cutovers_epoch", "\"Epoch\" > 0 AND \"Version\" > 0");
            });
            builder.HasKey(row => row.TenantId);
            builder.Property(row => row.ReauthenticationHash).HasMaxLength(128);
            builder.Property(row => row.Reason).HasMaxLength(500);
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasIndex(row => row.BatchId).IsUnique();
            builder.HasOne<EconomyLegacyShadowBatchRow>().WithOne().HasForeignKey<EconomyLegacyCutoverRow>(row => row.BatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyLegacyCutoverAuditRow>(builder =>
        {
            builder.ToTable("economy_legacy_cutover_audit", table =>
            {
                table.HasCheckConstraint("ck_economy_legacy_cutover_audit_sequence", "\"Sequence\" > 0");
                table.HasCheckConstraint("ck_economy_legacy_cutover_audit_state", "\"State\" BETWEEN 1 AND 4");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Reason).HasMaxLength(500);
            builder.Property(row => row.ReauthenticationHash).HasMaxLength(128);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.TenantId, row.Sequence }).IsUnique();
            builder.HasOne<EconomyLegacyCutoverRow>().WithMany().HasForeignKey(row => row.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureControlPlane(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomyCapabilityPolicyRow>(builder =>
        {
            builder.ToTable("economy_capability_policies", table =>
            {
                table.HasCheckConstraint("ck_economy_capability_policies_version", "\"Version\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_capability_policies_dual_control",
                    "(\"ApprovedBy\" IS NULL AND \"ApprovedAt\" IS NULL AND NOT \"IsActive\") OR " +
                    "(\"ApprovedBy\" IS NOT NULL AND \"ApprovedBy\" <> \"ProposedBy\" AND \"ApprovedAt\" >= \"ProposedAt\")");
                table.HasCheckConstraint(
                    "ck_economy_capability_policies_window",
                    "\"ExpiresAt\" > \"EffectiveAt\" AND (\"ApprovedAt\" IS NULL OR \"EffectiveAt\" >= \"ApprovedAt\")");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.ScopeKey).HasMaxLength(256);
            builder.Property(row => row.JurisdictionCode).HasMaxLength(16);
            builder.Property(row => row.CanonicalPayload).HasColumnType("jsonb");
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.Property(row => row.KeyId).HasMaxLength(256);
            builder.Property(row => row.Signature).HasMaxLength(2048);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.HasIndex(row => row.RequestHash)
                .IsUnique()
                .HasDatabaseName("ux_economy_capability_policies_request_hash");
            builder.HasIndex(row => new { row.ScopeKey, row.Version })
                .IsUnique()
                .HasDatabaseName("ux_economy_capability_policies_scope_version");
            builder.HasIndex(row => row.ScopeKey)
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("ux_economy_capability_policies_active_scope");
        });

        modelBuilder.Entity<EconomyCapabilityPolicyApprovalRow>(builder =>
        {
            builder.ToTable("economy_capability_policy_approvals");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.ReauthenticationHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.PolicyId, row.ActorId })
                .IsUnique()
                .HasDatabaseName("ux_economy_capability_policy_approvals_policy_actor");
            builder.HasOne<EconomyCapabilityPolicyRow>()
                .WithMany()
                .HasForeignKey(row => row.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyCapabilityReceiptRow>(builder =>
        {
            builder.ToTable("economy_capability_receipts", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_capability_receipts_versions",
                    "\"PolicyVersion\" > 0 AND \"ReserveVersion\" > 0 AND \"KillSwitchEpoch\" >= 0");
                table.HasCheckConstraint(
                    "ck_economy_capability_receipts_lifetime",
                    "\"ExpiresAt\" > \"IssuedAt\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.SubjectReference).HasMaxLength(256);
            builder.Property(row => row.JurisdictionCode).HasMaxLength(16);
            builder.Property(row => row.OperationFingerprint).HasMaxLength(128);
            builder.Property(row => row.ProviderHash).HasMaxLength(128);
            builder.Property(row => row.DestinationHash).HasMaxLength(128);
            builder.Property(row => row.SourceRootHashes).HasColumnType("jsonb");
            builder.Property(row => row.EvidenceHashes).HasColumnType("jsonb");
            builder.Property(row => row.ReceiptHash).HasMaxLength(128);
            builder.Property(row => row.KeyId).HasMaxLength(256);
            builder.Property(row => row.Signature).HasMaxLength(2048);
            builder.HasIndex(row => row.ReceiptHash)
                .IsUnique()
                .HasDatabaseName("ux_economy_capability_receipts_hash");
            builder.HasIndex(row => new { row.TenantId, row.OperationFingerprint })
                .IsUnique()
                .HasDatabaseName("ux_economy_capability_receipts_tenant_operation");
            builder.HasOne<EconomyRiskDecisionRow>()
                .WithMany()
                .HasForeignKey(row => row.RiskDecisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyCapabilityReceiptConsumptionRow>(builder =>
        {
            builder.ToTable("economy_capability_receipt_consumptions");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.OperationFingerprint).HasMaxLength(128);
            builder.HasIndex(row => row.ReceiptId)
                .IsUnique()
                .HasDatabaseName("ux_economy_capability_receipt_consumptions_receipt");
            builder.HasOne<EconomyCapabilityReceiptRow>()
                .WithMany()
                .HasForeignKey(row => row.ReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyKillSwitchRow>(builder =>
        {
            builder.ToTable("economy_kill_switches", table =>
            {
                table.HasCheckConstraint("ck_economy_kill_switches_epoch", "\"Epoch\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_kill_switches_state",
                    "(\"IsActive\" AND \"ReleasedAt\" IS NULL) OR (NOT \"IsActive\" AND \"ReleasedAt\" >= \"ActivatedAt\")");
                table.HasCheckConstraint(
                    "ck_economy_kill_switches_release_proposal",
                    "(\"ReleaseProposedBy\" IS NULL AND \"ReleaseProposedAt\" IS NULL AND \"ReleaseProposalReauthenticationHash\" IS NULL) OR " +
                    "(\"ReleaseProposedBy\" IS NOT NULL AND \"ReleaseProposedAt\" >= \"ActivatedAt\" AND length(btrim(\"ReleaseProposalReauthenticationHash\")) > 0)");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.ScopeKey).HasMaxLength(256);
            builder.Property(row => row.Reason).HasMaxLength(1000);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.Property(row => row.ReleaseProposalReauthenticationHash).HasMaxLength(128);
            builder.HasIndex(row => row.RequestHash)
                .IsUnique()
                .HasDatabaseName("ux_economy_kill_switches_request_hash");
            builder.HasIndex(row => new { row.ScopeKey, row.Epoch })
                .IsUnique()
                .HasDatabaseName("ux_economy_kill_switches_scope_epoch");
            builder.HasIndex(row => row.ScopeKey)
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("ux_economy_kill_switches_active_scope");
        });

        modelBuilder.Entity<EconomyKillSwitchReleaseApprovalRow>(builder =>
        {
            builder.ToTable("economy_kill_switch_release_approvals");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.ReauthenticationHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.KillSwitchId, row.ActorId })
                .IsUnique()
                .HasDatabaseName("ux_economy_kill_switch_release_approvals_switch_actor");
            builder.HasOne<EconomyKillSwitchRow>()
                .WithMany()
                .HasForeignKey(row => row.KillSwitchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyEntityGraphNodeRow>(builder =>
        {
            builder.ToTable("economy_entity_graph_nodes", table =>
                table.HasCheckConstraint("ck_economy_entity_graph_nodes_version", "\"Version\" > 0"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.IdentityHash).HasMaxLength(128);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.TenantId, row.Type, row.IdentityHash, row.Version })
                .IsUnique()
                .HasDatabaseName("ux_economy_entity_graph_nodes_identity_version");
        });

        modelBuilder.Entity<EconomyEntityGraphEdgeRow>(builder =>
        {
            builder.ToTable("economy_entity_graph_edges", table =>
            {
                table.HasCheckConstraint("ck_economy_entity_graph_edges_version", "\"Version\" > 0");
                table.HasCheckConstraint("ck_economy_entity_graph_edges_distinct_nodes", "\"LeftNodeId\" <> \"RightNodeId\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Relationship).HasMaxLength(100);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.TenantId, row.LeftNodeId, row.RightNodeId, row.Version })
                .IsUnique()
                .HasDatabaseName("ux_economy_entity_graph_edges_pair_version");
            builder.HasOne<EconomyEntityGraphNodeRow>()
                .WithMany()
                .HasForeignKey(row => row.LeftNodeId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyEntityGraphNodeRow>()
                .WithMany()
                .HasForeignKey(row => row.RightNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyComplianceEvidenceRow>(builder =>
        {
            builder.ToTable("economy_compliance_evidence", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_compliance_evidence_versions",
                    "\"Version\" > 0 AND \"PolicyVersion\" > 0 AND length(btrim(\"EvidenceKind\")) > 0");
                table.HasCheckConstraint(
                    "ck_economy_compliance_evidence_lifetime",
                    "\"ExpiresAt\" > \"IssuedAt\" AND \"ReceivedAt\" >= \"IssuedAt\"");
                table.HasCheckConstraint(
                    "ck_economy_compliance_evidence_jurisdiction",
                    "\"JurisdictionCode\" IS NULL OR \"JurisdictionCode\" ~ '^[A-Z]{3}$'");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Provider).HasMaxLength(100);
            builder.Property(row => row.Environment).HasMaxLength(50);
            builder.Property(row => row.ProviderEventId).HasMaxLength(256);
            builder.Property(row => row.SubjectHash).HasMaxLength(128);
            builder.Property(row => row.EvidenceKind).HasMaxLength(100);
            builder.Property(row => row.JurisdictionCode).HasMaxLength(3).IsFixedLength();
            builder.Property(row => row.Result).HasMaxLength(100);
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.Property(row => row.RawObjectReference).HasMaxLength(1000);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.Provider, row.Environment, row.ProviderEventId })
                .IsUnique()
                .HasDatabaseName("ux_economy_compliance_evidence_provider_event");
            builder.HasIndex(row => new { row.TenantId, row.SubjectHash, row.EvidenceKind, row.Version })
                .IsUnique()
                .HasDatabaseName("ux_economy_compliance_evidence_subject_version");
        });

        modelBuilder.Entity<EconomyComplianceInboxRow>(builder =>
        {
            builder.ToTable("economy_compliance_inbox");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Provider).HasMaxLength(100);
            builder.Property(row => row.Environment).HasMaxLength(50);
            builder.Property(row => row.ProviderEventId).HasMaxLength(256);
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.Property(row => row.RawObjectReference).HasMaxLength(1000);
            builder.Property(row => row.ProcessingError).HasMaxLength(2000);
            builder.HasIndex(row => new { row.Provider, row.Environment, row.ProviderEventId })
                .IsUnique()
                .HasDatabaseName("ux_economy_compliance_inbox_provider_event");
        });

        modelBuilder.Entity<EconomyComplianceOutboxRow>(builder =>
        {
            builder.ToTable("economy_compliance_outbox");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Type).HasMaxLength(100);
            builder.Property(row => row.Payload).HasColumnType("jsonb");
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.HasIndex(row => row.EvidenceId)
                .IsUnique()
                .HasDatabaseName("ux_economy_compliance_outbox_evidence");
            builder.HasOne<EconomyComplianceEvidenceRow>()
                .WithMany()
                .HasForeignKey(row => row.EvidenceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyComplianceHoldRow>(builder =>
        {
            builder.ToTable("economy_compliance_holds", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_compliance_holds_lifetime",
                    "\"ExpiresAt\" > \"ActivatedAt\"");
                table.HasCheckConstraint(
                    "ck_economy_compliance_holds_release",
                    "(\"ReleasedAt\" IS NULL AND \"ReleasedBy\" IS NULL) OR " +
                    "(\"ReleasedAt\" >= \"ActivatedAt\" AND \"ReleasedBy\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_economy_compliance_holds_release_proposal",
                    "(\"ReleaseProposedAt\" IS NULL AND \"ReleaseProposedBy\" IS NULL AND " +
                    "\"RequiredReleaseApprovals\" IS NULL AND \"ReleasePolicyEvidenceHash\" IS NULL) OR " +
                    "(\"ReleaseProposedAt\" >= \"ActivatedAt\" AND \"ReleaseProposedBy\" IS NOT NULL AND " +
                    "\"RequiredReleaseApprovals\" BETWEEN 1 AND 2 AND " +
                    "length(btrim(\"ReleasePolicyEvidenceHash\")) > 0)");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.ScopeKey).HasMaxLength(512);
            builder.Property(row => row.SubjectHash).HasMaxLength(128);
            builder.Property(row => row.CaseReferenceHash).HasMaxLength(128);
            builder.Property(row => row.ReasonCode).HasMaxLength(100);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.Property(row => row.IdempotencyKeyHash).HasMaxLength(128);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.Property(row => row.ReleasePolicyEvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => row.IdempotencyKeyHash).IsUnique()
                .HasDatabaseName("ux_economy_compliance_holds_idempotency");
            builder.HasIndex(row => new { row.ScopeKey, row.ReleasedAt, row.ExpiresAt })
                .HasDatabaseName("ix_economy_compliance_holds_active_scope");
            builder.HasIndex(row => new { row.TenantId, row.SubjectHash, row.ExpiresAt });
        });

        modelBuilder.Entity<EconomyComplianceHoldEventRow>(builder =>
        {
            builder.ToTable("economy_compliance_hold_events", table =>
                table.HasCheckConstraint(
                    "ck_economy_compliance_hold_events_sequence",
                    "\"Sequence\" > 0"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Kind).HasMaxLength(50);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.HoldId, row.Sequence }).IsUnique();
            builder.HasIndex(row => new { row.HoldId, row.Kind, row.ActorId }).IsUnique();
            builder.HasOne<EconomyComplianceHoldRow>().WithMany().HasForeignKey(row => row.HoldId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyCustodyObservationRow>(builder =>
        {
            builder.ToTable("economy_custody_observations", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_custody_observations_values",
                    "\"Version\" > 0 AND \"EligibleUsdNanos\" >= 0");
                table.HasCheckConstraint(
                    "ck_economy_custody_observations_lifetime",
                    "\"ExpiresAt\" > \"ObservedAt\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Provider).HasMaxLength(100);
            builder.Property(row => row.AssetKey).HasMaxLength(256);
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.Property(row => row.KeyId).HasMaxLength(256);
            builder.Property(row => row.Signature).HasMaxLength(2048);
            builder.HasIndex(row => new { row.Provider, row.AssetKey, row.Version })
                .IsUnique()
                .HasDatabaseName("ux_economy_custody_observations_provider_asset_version");
        });

        modelBuilder.Entity<EconomyCustodyReconciliationRow>(builder =>
        {
            builder.ToTable("economy_custody_reconciliations", table =>
                table.HasCheckConstraint(
                    "ck_economy_custody_reconciliations_values",
                    "\"ReserveVersion\" > 0 AND \"LiabilityUsdNanos\" >= 0 AND \"EligibleAssetUsdNanos\" >= 0"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.ObservationIds).HasColumnType("jsonb");
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => row.ReserveVersion)
                .IsUnique()
                .HasDatabaseName("ux_economy_custody_reconciliations_reserve");
        });

        modelBuilder.Entity<EconomyReserveProposalRow>(builder =>
        {
            builder.ToTable("economy_reserve_proposals", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_reserve_proposals_values",
                    "\"Version\" > 0 AND \"PolicyVersion\" > 0 AND \"AuthorizationEpoch\" > 0 AND " +
                    "\"LiabilityUsdNanos\" >= 0 AND \"EligibleAssetUsdNanos\" >= 0 AND " +
                    "\"HardFaceValueUsdMinor\" >= 0 AND \"RequiredHardReserveUsdMinor\" >= 0 AND " +
                    "\"SoftFaceValueUsdNanos\" >= 0 AND \"RequiredSoftReserveUsdNanos\" >= 0 AND " +
                    "\"HardBackingUsdNanos\" >= 0 AND \"SoftBackingUsdNanos\" >= 0");
                table.HasCheckConstraint(
                    "ck_economy_reserve_proposals_dual_control",
                    "\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"ProposedBy\"");
                table.HasCheckConstraint(
                    "ck_economy_reserve_proposals_window",
                    "\"ExpiresAt\" > \"ObservedAt\" AND \"ProposedAt\" >= \"ObservedAt\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.SnapshotHash).HasMaxLength(128);
            builder.Property(row => row.ObservationIds).HasColumnType("jsonb");
            builder.Property(row => row.AssetAllocations).HasColumnType("jsonb");
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.Property(row => row.ApprovalReauthenticationHash).HasMaxLength(128);
            builder.Property(row => row.Status).HasMaxLength(50);
            builder.HasIndex(row => row.Version)
                .IsUnique()
                .HasDatabaseName("ux_economy_reserve_proposals_version");
        });

        modelBuilder.Entity<EconomyJournalVerificationCheckpointRow>(builder =>
        {
            builder.ToTable("economy_journal_verification_checkpoints", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_journal_verification_checkpoints_range",
                    "\"FromSequence\" >= 0 AND \"ToSequence\" >= \"FromSequence\"");
                table.HasCheckConstraint(
                    "ck_economy_journal_verification_checkpoints_time",
                    "\"CompletedAt\" >= \"StartedAt\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.PreviousHash).HasMaxLength(128);
            builder.Property(row => row.CurrentHash).HasMaxLength(128);
            builder.Property(row => row.FailureCode).HasMaxLength(100);
            builder.HasIndex(row => new { row.ToSequence, row.CompletedAt })
                .HasDatabaseName("ix_economy_journal_verification_checkpoints_sequence");
        });

        modelBuilder.Entity<EconomyProjectionGenerationRow>(builder =>
        {
            builder.ToTable("economy_projection_generations", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_projection_generations_range",
                    "\"Generation\" > 0 AND \"FromSequence\" >= 0 AND \"ToSequence\" >= \"FromSequence\"");
                table.HasCheckConstraint(
                    "ck_economy_projection_generations_dual_control",
                    "(\"ApprovedBy\" IS NULL OR \"ApprovedBy\" <> \"ProposedBy\") AND " +
                    "(\"SecondApprovedBy\" IS NULL OR (\"SecondApprovedBy\" <> \"ProposedBy\" AND \"SecondApprovedBy\" <> \"ApprovedBy\"))");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.ProjectionHash).HasMaxLength(128);
            builder.Property(row => row.JournalHash).HasMaxLength(128);
            builder.Property(row => row.State).HasMaxLength(50);
            builder.HasIndex(row => row.Generation).IsUnique();
            builder.HasIndex(row => row.IsActive)
                .IsUnique()
                .HasFilter("\"IsActive\"")
                .HasDatabaseName("ux_economy_projection_generations_active");
        });

        modelBuilder.Entity<EconomyWalletProjectionGenerationRow>(builder =>
        {
            builder.ToTable("economy_wallet_projection_generations", table =>
            {
                table.HasCheckConstraint(
                    "ck_economy_wallet_projection_generations_amounts",
                    "\"Generation\" > 0 AND \"PendingHard\" >= 0 AND \"PendingSoft\" >= 0 AND " +
                    "\"PurchasedHard\" >= 0 AND \"EarnedHard\" >= 0 AND \"RestrictedHard\" >= 0 AND " +
                    "\"Soft\" >= 0 AND \"ImmatureEarnedHard\" >= 0 AND \"HeldHard\" >= 0 AND " +
                    "\"HeldSoft\" >= 0 AND \"AvailableHardToSpend\" >= 0 AND " +
                    "\"AvailableSoftToSpend\" >= 0 AND \"WithdrawableHard\" >= 0");
            });
            builder.HasKey(row => new { row.Generation, row.WalletId });
            builder.Property(row => row.ProjectionHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.Generation, row.MatchesLive });
            builder.HasOne<EconomyProjectionGenerationRow>()
                .WithMany().HasForeignKey(row => row.Generation).HasPrincipalKey(row => row.Generation)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<EconomyWalletRow>()
                .WithMany().HasForeignKey(row => row.WalletId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyProjectionGenerationApprovalRow>(builder =>
        {
            builder.ToTable("economy_projection_generation_approvals");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.ReauthenticationHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.Generation, row.ActorId }).IsUnique();
            builder.HasOne<EconomyProjectionGenerationRow>()
                .WithMany().HasForeignKey(row => row.Generation).HasPrincipalKey(row => row.Generation)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyAnchorVerificationRow>(builder =>
        {
            builder.ToTable("economy_anchor_verifications");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.KeyId).HasMaxLength(256);
            builder.Property(row => row.ObjectVersion).HasMaxLength(256);
            builder.Property(row => row.ETag).HasMaxLength(256);
            builder.Property(row => row.ObjectHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.ExternalAnchorId, row.VerifiedAt });
            builder.HasOne<EconomyExternalAnchorRow>()
                .WithMany()
                .HasForeignKey(row => row.ExternalAnchorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EconomyWorkerLeaseRow>(builder =>
        {
            builder.ToTable("economy_worker_leases", table =>
            {
                table.HasCheckConstraint("ck_economy_worker_leases_fencing", "\"FencingToken\" > 0");
                table.HasCheckConstraint("ck_economy_worker_leases_lifetime", "\"ExpiresAt\" > \"AcquiredAt\"");
            });
            builder.HasKey(row => row.Name);
            builder.Property(row => row.Name).HasMaxLength(100);
            builder.Property(row => row.Owner).HasMaxLength(256);
            builder.HasIndex(row => row.Name)
                .IsUnique()
                .HasDatabaseName("ux_economy_worker_leases_name");
        });
    }

    private static void ConfigureFifoFragmentReservationReceipt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FifoFragmentReservationReceiptRow>(builder =>
        {
            builder.HasNoKey();
            builder.ToView(null);
            builder.Property(row => row.ReservationId).HasColumnName("reservation_id");
            builder.Property(row => row.ParentLotId).HasColumnName("parent_lot_id");
            builder.Property(row => row.RootSourceStampId).HasColumnName("root_source_stamp_id");
            builder.Property(row => row.ReversalEpoch).HasColumnName("reversal_epoch");
            builder.Property(row => row.StartInclusive).HasColumnName("start_inclusive");
            builder.Property(row => row.EndExclusive).HasColumnName("end_exclusive");
            builder.Property(row => row.AmountUnits).HasColumnName("amount_units");
        });
    }

    private static void ConfigureMarketplaceFifoReservationReceipt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MarketplaceFifoReservationReceiptRow>(builder =>
        {
            builder.HasNoKey();
            builder.ToView(null);
            builder.Property(row => row.ReservationId).HasColumnName("reservation_id");
            builder.Property(row => row.ParentLotId).HasColumnName("parent_lot_id");
            builder.Property(row => row.RootSourceStampId).HasColumnName("root_source_stamp_id");
            builder.Property(row => row.ReversalEpoch).HasColumnName("reversal_epoch");
            builder.Property(row => row.StartInclusive).HasColumnName("start_inclusive");
            builder.Property(row => row.EndExclusive).HasColumnName("end_exclusive");
            builder.Property(row => row.Currency).HasColumnName("currency");
            builder.Property(row => row.AmountUnits).HasColumnName("amount_units");
        });
    }

    private static void ConfigureRegisteredPostingReceipt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegisteredPostingReceiptRow>(builder =>
        {
            builder.HasNoKey();
            builder.ToView(null);
            builder.Property(row => row.PostingId).HasColumnName("posting_id");
            builder.Property(row => row.JournalSequence).HasColumnName("journal_sequence");
            builder.Property(row => row.JournalHash).HasColumnName("journal_hash");
            builder.Property(row => row.Duplicate).HasColumnName("duplicate");
        });
    }

    private static void ConfigureHardToSoftConversionRiskDecisionReceipt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HardToSoftConversionRiskDecisionReceiptRow>(builder =>
        {
            builder.HasNoKey();
            builder.ToView(null);
            builder.Property(row => row.RiskDecisionId).HasColumnName("risk_decision_id");
            builder.Property(row => row.SourceRoots).HasColumnName("source_roots");
        });
    }

    private static void ConfigureProviderReversalReceipt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProviderReversalReceiptRow>(builder =>
        {
            builder.HasNoKey();
            builder.ToView(null);
            builder.Property(row => row.OperationId).HasColumnName("operation_id");
            builder.Property(row => row.RecoveredHardUnits).HasColumnName("recovered_hard_units");
            builder.Property(row => row.RecoveredConvertedSoftUnits).HasColumnName("recovered_converted_soft_units");
            builder.Property(row => row.ResponsibleDebtHardUnits).HasColumnName("responsible_debt_hard_units");
            builder.Property(row => row.PlatformLossHardUnits).HasColumnName("platform_loss_hard_units");
            builder.Property(row => row.Duplicate).HasColumnName("duplicate");
        });
    }
}
