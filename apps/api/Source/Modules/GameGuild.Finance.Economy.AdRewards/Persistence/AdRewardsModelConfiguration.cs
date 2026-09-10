using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.AdRewards.Persistence;

internal sealed class AdNetworkPolicyVersionRow
{
    public Guid TenantId { get; set; }
    public string Network { get; set; } = string.Empty;
    public long Version { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public AdRewardIssuanceMode IssuanceMode { get; set; }
    public AdNetworkYieldState YieldState { get; set; }
    public long EstimatedNetEcpmUsdNanos { get; set; }
    public int ContractedRevenueSharePpm { get; set; }
    public int SafetyBufferPpm { get; set; }
    public int MinimumVisiblePpm { get; set; }
    public long MaximumFocusLossTicks { get; set; }
    public long MaximumRewardSoftUnits { get; set; }
    public DateTimeOffset ReportsCurrentThrough { get; set; }
    public long ReportStaleAfterTicks { get; set; }
    public int Ranking { get; set; }
    public long MaximumUserSoftUnits { get; set; }
    public long MaximumDeviceSoftUnits { get; set; }
    public long MaximumIpSoftUnits { get; set; }
    public long MaximumAsnSoftUnits { get; set; }
    public long MaximumNetworkSoftUnits { get; set; }
    public long MaximumGlobalSoftUnits { get; set; }
    public long FundedLossBudgetUsdNanos { get; set; }
    public long BudgetWindowTicks { get; set; }
    public string ProviderHash { get; set; } = string.Empty;
    public bool ProviderCertified { get; set; }
    public string CanonicalPayload { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public Guid ProposedBy { get; set; }
    public Guid ApprovedBy { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}

internal sealed class AdRewardSessionRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid WalletId { get; set; }
    public string Network { get; set; } = string.Empty;
    public long PolicyVersion { get; set; }
    public string CreativeId { get; set; } = string.Empty;
    public string DeviceRiskHash { get; set; } = string.Empty;
    public string IpRiskHash { get; set; } = string.Empty;
    public string AsnRiskHash { get; set; } = string.Empty;
    public string NonceHash { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string TokenKeyId { get; set; } = string.Empty;
    public long RequiredDurationTicks { get; set; }
    public DurableAdRewardSessionState State { get; set; }
    public string StartIdempotencyKeyHash { get; set; } = string.Empty;
    public string StartRequestHash { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; }
}

internal sealed class AdRewardPlaybackMilestoneRow
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public int Sequence { get; set; }
    public int Percentage { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
}

internal sealed class AdRewardSessionEventRow
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public long Sequence { get; set; }
    public DurableAdRewardSessionState State { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class AdRewardProviderProofInboxRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public string Network { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
    public bool SignatureVerified { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
}

internal sealed class AdRewardPendingClaimRow
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SourceStampId { get; set; }
    public string CompletionIdempotencyKeyHash { get; set; } = string.Empty;
    public string CompletionRequestHash { get; set; } = string.Empty;
    public DateTimeOffset DeferredAt { get; set; }
    public Guid? ProviderReportId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? ConfirmationIdempotencyKeyHash { get; set; }
    public string? ConfirmationRequestHash { get; set; }
}

internal sealed class AdRewardCapConsumptionRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public AdRewardCapScope Scope { get; set; }
    public string SubjectHash { get; set; } = string.Empty;
    public DateTimeOffset WindowStartedAt { get; set; }
    public DateTimeOffset WindowEndsAt { get; set; }
    public long SoftUnits { get; set; }
    public long LossBudgetUsdNanos { get; set; }
    public DateTimeOffset ConsumedAt { get; set; }
}

internal sealed class AdRewardCompletionRow
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid WalletId { get; set; }
    public string Network { get; set; } = string.Empty;
    public long PolicyVersion { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public AdRewardCompletionState State { get; set; }
    public long RewardSoftUnits { get; set; }
    public Guid? SourceStampId { get; set; }
    public Guid? PostingId { get; set; }
    public Guid? OutputLotId { get; set; }
    public string? ProviderEventId { get; set; }
    public Guid? CapabilityReceiptId { get; set; }
    public string? CapabilityReceiptHash { get; set; }
    public long? ReserveVersion { get; set; }
    public Guid? RiskDecisionId { get; set; }
    public long? KillSwitchEpoch { get; set; }
    public string? JurisdictionCode { get; set; }
    public string? ProviderHash { get; set; }
    public string? DestinationHash { get; set; }
    public string EvidenceHashes { get; set; } = "[]";
    public DateTimeOffset CompletedAt { get; set; }
    public long Version { get; set; }
}

internal sealed class AdRewardAccumulatorRow
{
    public Guid TenantId { get; set; }
    public Guid WalletId { get; set; }
    public string Network { get; set; } = string.Empty;
    public long PolicyVersion { get; set; }
    public string RemainderNumerator { get; set; } = "0";
    public string CanonicalDenominator { get; set; } = "1";
    public long Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class AdRewardBudgetConsumptionRow
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string DeviceRiskHash { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public long SoftUnits { get; set; }
    public long LossBudgetUsdNanos { get; set; }
    public DateTimeOffset ConsumedAt { get; set; }
}

internal sealed class AdRewardAttributionRow
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public string Network { get; set; } = string.Empty;
    public long PolicyVersion { get; set; }
    public string ProviderBatchId { get; set; } = string.Empty;
    public long EstimatedRevenueUsdNanos { get; set; }
    public long RewardSoftUnits { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}

internal sealed class AdProviderReportRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Network { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public long ActualRevenueUsdNanos { get; set; }
    public string VerifiedSessionIds { get; set; } = "[]";
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; set; }
    public string Signature { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public bool SignatureVerified { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
}

internal sealed class AdRewardReconciliationRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProviderReportId { get; set; }
    public string Network { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public long EstimatedRevenueUsdNanos { get; set; }
    public long PreviousActualRevenueUsdNanos { get; set; }
    public long ActualRevenueUsdNanos { get; set; }
    public long ActualDeltaUsdNanos { get; set; }
    public long VarianceUsdNanos { get; set; }
    public long HistoricalRewardSoftUnits { get; set; }
    public DateTimeOffset ReconciledAt { get; set; }
}

internal sealed class AdRewardProviderBatchClaimRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProviderReportId { get; set; }
    public Guid SessionId { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public DateTimeOffset ClaimedAt { get; set; }
}

public sealed class AdRewardsModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<AdNetworkPolicyVersionRow>(builder =>
        {
            builder.ToTable("economy_ad_network_policy_versions", table =>
            {
                table.HasCheckConstraint("ck_economy_ad_network_policy_versions_window", "\"ExpiresAt\" > \"EffectiveAt\"");
                table.HasCheckConstraint("ck_economy_ad_network_policy_versions_ppm", "\"ContractedRevenueSharePpm\" BETWEEN 0 AND 1000000 AND \"SafetyBufferPpm\" BETWEEN 0 AND 999999 AND \"MinimumVisiblePpm\" BETWEEN 0 AND 1000000");
                table.HasCheckConstraint("ck_economy_ad_network_policy_versions_values", "\"Version\" > 0 AND \"EstimatedNetEcpmUsdNanos\" > 0 AND \"MaximumRewardSoftUnits\" > 0 AND \"MaximumFocusLossTicks\" >= 0 AND \"ReportStaleAfterTicks\" > 0 AND \"Ranking\" >= 0 AND \"BudgetWindowTicks\" > 0");
                table.HasCheckConstraint("ck_economy_ad_network_policy_versions_caps", "\"MaximumUserSoftUnits\" > 0 AND \"MaximumDeviceSoftUnits\" > 0 AND \"MaximumIpSoftUnits\" > 0 AND \"MaximumAsnSoftUnits\" > 0 AND \"MaximumNetworkSoftUnits\" > 0 AND \"MaximumGlobalSoftUnits\" > 0 AND \"FundedLossBudgetUsdNanos\" > 0");
                table.HasCheckConstraint("ck_economy_ad_network_policy_versions_dual_control", "\"ProposedBy\" <> \"ApprovedBy\"");
            });
            builder.HasKey(row => new { row.TenantId, row.Network, row.Version });
            builder.Property(row => row.Network).HasMaxLength(100);
            builder.Property(row => row.ProviderHash).HasMaxLength(128);
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.Property(row => row.KeyId).HasMaxLength(256);
            builder.Property(row => row.CanonicalPayload).HasColumnType("text");
            builder.Property(row => row.Signature).HasColumnType("text");
            builder.HasIndex(row => new { row.TenantId, row.Network, row.EffectiveAt, row.ExpiresAt });
        });

        modelBuilder.Entity<AdRewardSessionRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_sessions", table =>
            {
                table.HasCheckConstraint("ck_economy_ad_reward_sessions_window", "\"ExpiresAt\" > \"IssuedAt\" AND \"UpdatedAt\" >= \"IssuedAt\"");
                table.HasCheckConstraint("ck_economy_ad_reward_sessions_values", "\"PolicyVersion\" > 0 AND \"RequiredDurationTicks\" > 0 AND \"Version\" > 0");
                table.HasCheckConstraint("ck_economy_ad_reward_sessions_state", "\"State\" BETWEEN 1 AND 7");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Network).HasMaxLength(100);
            builder.Property(row => row.CreativeId).HasMaxLength(256);
            builder.Property(row => row.DeviceRiskHash).HasMaxLength(128);
            builder.Property(row => row.IpRiskHash).HasMaxLength(128);
            builder.Property(row => row.AsnRiskHash).HasMaxLength(128);
            builder.Property(row => row.NonceHash).HasMaxLength(128);
            builder.Property(row => row.TokenHash).HasMaxLength(128);
            builder.Property(row => row.TokenKeyId).HasMaxLength(256);
            builder.Property(row => row.StartIdempotencyKeyHash).HasMaxLength(128);
            builder.Property(row => row.StartRequestHash).HasMaxLength(128);
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasIndex(row => new { row.TenantId, row.StartIdempotencyKeyHash }).IsUnique();
            builder.HasIndex(row => new { row.TenantId, row.NonceHash }).IsUnique();
            builder.HasIndex(row => new { row.TenantId, row.UserId, row.IssuedAt });
        });

        modelBuilder.Entity<AdRewardPlaybackMilestoneRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_playback_milestones", table =>
                table.HasCheckConstraint("ck_economy_ad_reward_playback_milestones_percentage", "\"Percentage\" BETWEEN 0 AND 100 AND \"Sequence\" > 0"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.SessionId, row.Sequence }).IsUnique();
            builder.HasOne<AdRewardSessionRow>().WithMany().HasForeignKey(row => row.SessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdRewardSessionEventRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_session_events", table =>
            {
                table.HasCheckConstraint("ck_economy_ad_reward_session_events_sequence", "\"Sequence\" > 0");
                table.HasCheckConstraint("ck_economy_ad_reward_session_events_state", "\"State\" BETWEEN 1 AND 7");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.SessionId, row.Sequence }).IsUnique();
            builder.HasOne<AdRewardSessionRow>().WithMany().HasForeignKey(row => row.SessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdRewardProviderProofInboxRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_provider_proof_inbox");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Network).HasMaxLength(100);
            builder.Property(row => row.ProviderEventId).HasMaxLength(256);
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.Property(row => row.ProcessingError).HasMaxLength(256);
            builder.HasIndex(row => new { row.TenantId, row.Network, row.ProviderEventId }).IsUnique();
            builder.HasOne<AdRewardSessionRow>().WithMany().HasForeignKey(row => row.SessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdRewardPendingClaimRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_pending_claims");
            builder.HasKey(row => row.SessionId);
            builder.Property(row => row.CompletionIdempotencyKeyHash).HasMaxLength(128);
            builder.Property(row => row.CompletionRequestHash).HasMaxLength(128);
            builder.Property(row => row.ConfirmationIdempotencyKeyHash).HasMaxLength(128);
            builder.Property(row => row.ConfirmationRequestHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.TenantId, row.CompletionIdempotencyKeyHash }).IsUnique();
            builder.HasIndex(row => new { row.TenantId, row.ConfirmationIdempotencyKeyHash })
                .IsUnique().HasFilter("\"ConfirmationIdempotencyKeyHash\" IS NOT NULL");
            builder.HasOne<AdRewardSessionRow>().WithOne().HasForeignKey<AdRewardPendingClaimRow>(row => row.SessionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AdProviderReportRow>().WithMany().HasForeignKey(row => row.ProviderReportId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdRewardCapConsumptionRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_cap_consumptions", table =>
            {
                table.HasCheckConstraint("ck_economy_ad_reward_cap_consumptions_window", "\"WindowEndsAt\" > \"WindowStartedAt\" AND \"ConsumedAt\" >= \"WindowStartedAt\" AND \"ConsumedAt\" < \"WindowEndsAt\"");
                table.HasCheckConstraint("ck_economy_ad_reward_cap_consumptions_positive", "\"SoftUnits\" > 0 AND \"LossBudgetUsdNanos\" >= 0");
                table.HasCheckConstraint("ck_economy_ad_reward_cap_consumptions_scope", "\"Scope\" BETWEEN 1 AND 6");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.SubjectHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.TenantId, row.Scope, row.SubjectHash, row.ConsumedAt });
            builder.HasIndex(row => new { row.SessionId, row.Scope }).IsUnique();
            builder.HasOne<AdRewardSessionRow>().WithMany().HasForeignKey(row => row.SessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdRewardCompletionRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_completions", table =>
            {
                table.HasCheckConstraint("ck_economy_ad_reward_completions_reward_nonnegative", "\"RewardSoftUnits\" >= 0");
                table.HasCheckConstraint("ck_economy_ad_reward_completions_state", "\"State\" BETWEEN 1 AND 3");
                table.HasCheckConstraint("ck_economy_ad_reward_completions_issued_binding", "\"State\" <> 1 OR (\"RewardSoftUnits\" > 0 AND \"SourceStampId\" IS NOT NULL AND \"PostingId\" IS NOT NULL AND \"OutputLotId\" IS NOT NULL)");
            });
            builder.HasKey(row => row.SessionId);
            builder.Property(row => row.Network).HasMaxLength(100);
            builder.Property(row => row.IdempotencyKey).HasMaxLength(256);
            builder.Property(row => row.ProviderEventId).HasMaxLength(256);
            builder.Property(row => row.CapabilityReceiptHash).HasMaxLength(128);
            builder.Property(row => row.JurisdictionCode).HasMaxLength(16);
            builder.Property(row => row.ProviderHash).HasMaxLength(128);
            builder.Property(row => row.DestinationHash).HasMaxLength(128);
            builder.Property(row => row.EvidenceHashes).HasColumnType("jsonb");
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasIndex(row => row.IdempotencyKey).IsUnique();
            builder.HasIndex(row => row.ProviderEventId).IsUnique().HasFilter("\"ProviderEventId\" IS NOT NULL");
            builder.HasIndex(row => new { row.UserId, row.CompletedAt });
            builder.HasIndex(row => new { row.Network, row.PolicyVersion });
        });

        modelBuilder.Entity<AdRewardAccumulatorRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_accumulators", table =>
            {
                table.HasCheckConstraint("ck_economy_ad_reward_accumulators_numbers", "\"RemainderNumerator\" ~ '^[0-9]+$' AND \"CanonicalDenominator\" ~ '^[1-9][0-9]*$'");
                table.HasCheckConstraint("ck_economy_ad_reward_accumulators_version", "\"PolicyVersion\" > 0 AND \"Version\" > 0");
            });
            builder.HasKey(row => new { row.TenantId, row.WalletId, row.Network });
            builder.Property(row => row.Network).HasMaxLength(100);
            builder.Property(row => row.RemainderNumerator).HasColumnType("text");
            builder.Property(row => row.CanonicalDenominator).HasColumnType("text");
        });

        modelBuilder.Entity<AdRewardBudgetConsumptionRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_budget_consumptions", table =>
                table.HasCheckConstraint("ck_economy_ad_reward_budget_consumptions_positive", "\"SoftUnits\" > 0 AND \"LossBudgetUsdNanos\" > 0"));
            builder.HasKey(row => row.SessionId);
            builder.Property(row => row.DeviceRiskHash).HasMaxLength(128);
            builder.Property(row => row.Network).HasMaxLength(100);
            builder.HasIndex(row => new { row.UserId, row.ConsumedAt });
            builder.HasIndex(row => new { row.DeviceRiskHash, row.ConsumedAt });
            builder.HasIndex(row => new { row.Network, row.ConsumedAt });
        });

        modelBuilder.Entity<AdRewardAttributionRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_attributions", table =>
                table.HasCheckConstraint("ck_economy_ad_reward_attributions_nonnegative", "\"EstimatedRevenueUsdNanos\" >= 0 AND \"RewardSoftUnits\" >= 0"));
            builder.HasKey(row => row.SessionId);
            builder.Property(row => row.Network).HasMaxLength(100);
            builder.Property(row => row.ProviderBatchId).HasMaxLength(256);
            builder.HasIndex(row => new { row.Network, row.ProviderBatchId, row.CompletedAt });
        });

        modelBuilder.Entity<AdProviderReportRow>(builder =>
        {
            builder.ToTable("economy_ad_provider_reports", table =>
            {
                table.HasCheckConstraint("ck_economy_ad_provider_reports_version", "\"Version\" > 0");
                table.HasCheckConstraint("ck_economy_ad_provider_reports_window", "\"PeriodEnd\" > \"PeriodStart\" AND \"ImportedAt\" >= \"PeriodEnd\"");
                table.HasCheckConstraint("ck_economy_ad_provider_reports_revenue", "\"ActualRevenueUsdNanos\" >= 0");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).ValueGeneratedNever();
            builder.Property(row => row.Network).HasMaxLength(100);
            builder.Property(row => row.ReportId).HasMaxLength(256);
            builder.Property(row => row.BatchId).HasMaxLength(256);
            builder.Property(row => row.VerifiedSessionIds).HasColumnType("jsonb");
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.Property(row => row.Signature).HasColumnType("text");
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.Property(row => row.ProcessingError).HasMaxLength(256);
            builder.HasIndex(row => new { row.TenantId, row.Network, row.ReportId, row.Version }).IsUnique();
            builder.HasIndex(row => new { row.TenantId, row.Network, row.BatchId, row.Version }).IsUnique();
        });

        modelBuilder.Entity<AdRewardReconciliationRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_reconciliations", table =>
            {
                table.HasCheckConstraint("ck_economy_ad_reward_reconciliations_version", "\"Version\" > 0");
                table.HasCheckConstraint("ck_economy_ad_reward_reconciliations_nonnegative", "\"EstimatedRevenueUsdNanos\" >= 0 AND \"PreviousActualRevenueUsdNanos\" >= 0 AND \"ActualRevenueUsdNanos\" >= 0 AND \"HistoricalRewardSoftUnits\" >= 0");
                table.HasCheckConstraint("ck_economy_ad_reward_reconciliations_conservation", "\"ActualDeltaUsdNanos\" = \"ActualRevenueUsdNanos\" - \"PreviousActualRevenueUsdNanos\" AND \"VarianceUsdNanos\" = \"ActualRevenueUsdNanos\" - \"EstimatedRevenueUsdNanos\"");
            });
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).ValueGeneratedNever();
            builder.Property(row => row.Network).HasMaxLength(100);
            builder.Property(row => row.ReportId).HasMaxLength(256);
            builder.Property(row => row.BatchId).HasMaxLength(256);
            builder.HasIndex(row => new { row.TenantId, row.Network, row.ReportId, row.Version }).IsUnique();
            builder.HasOne<AdProviderReportRow>().WithOne().HasForeignKey<AdRewardReconciliationRow>(row => row.ProviderReportId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdRewardProviderBatchClaimRow>(builder =>
        {
            builder.ToTable("economy_ad_reward_provider_batch_claims");
            builder.HasKey(row => row.Id);
            builder.Property(row => row.BatchId).HasMaxLength(256);
            builder.HasIndex(row => new { row.ProviderReportId, row.SessionId }).IsUnique();
            builder.HasOne<AdProviderReportRow>().WithMany().HasForeignKey(row => row.ProviderReportId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AdRewardSessionRow>().WithMany().HasForeignKey(row => row.SessionId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
