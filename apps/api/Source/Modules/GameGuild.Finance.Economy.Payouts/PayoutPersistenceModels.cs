using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Payouts;

internal sealed class PayoutOperationRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public Guid PayeeId { get; set; }
    public Guid WalletId { get; set; }
    public long AmountUnits { get; set; }
    public string ProviderAccountId { get; set; } = string.Empty;
    public string DestinationHash { get; set; } = string.Empty;
    public string ProviderBindingHash { get; set; } = string.Empty;
    public string EligibilityHash { get; set; } = string.Empty;
    public string? DispatchSnapshotHash { get; set; }
    public string? ProviderPayoutId { get; set; }
    public PayoutOperationState State { get; set; }
    public long Version { get; set; }
    public long FencingToken { get; set; }
    public long KillSwitchEpoch { get; set; }
    public long ReserveVersion { get; set; }
    public long ReserveAuthorizationEpoch { get; set; }
    public long PolicyVersion { get; set; }
    public Guid RiskDecisionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class PayoutProviderEventRow
{
    public string EventId { get; set; } = string.Empty;
    public string EventHash { get; set; } = string.Empty;
    public Guid OperationId { get; set; }
    public PayoutOperationState ResultingState { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

internal sealed class PayoutConnectAccountRow
{
    public Guid PayeeId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string ProviderAccountId { get; set; } = string.Empty;
    public string DestinationHash { get; set; } = string.Empty;
    public ConnectAccountState State { get; set; }
    public bool ChargesEnabled { get; set; }
    public bool PayoutsEnabled { get; set; }
    public long Version { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
}

public sealed class PayoutDispatchOutboxRow
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public string? LeaseOwner { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastErrorCode { get; set; }
}

internal sealed class PayoutAuthorizationEvidenceRow
{
    public Guid OperationId { get; set; }
    public PayoutAuthorizationPhase Phase { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public Guid RiskDecisionId { get; set; }
    public string ReauthenticationEvidenceHash { get; set; } = string.Empty;
    public string OperationFingerprintHash { get; set; } = string.Empty;
    public Guid CapabilityReceiptId { get; set; }
    public string CapabilityReceiptHash { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class PayoutsModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<PayoutOperationRow>(builder =>
        {
            builder.ToTable("economy_payout_operations", table =>
            {
                table.HasCheckConstraint("ck_economy_payout_operations_state", "\"State\" BETWEEN 1 AND 6");
                table.HasCheckConstraint(
                    "ck_economy_payout_operations_positive_values",
                    "\"AmountUnits\" > 0 AND \"Version\" > 0 AND \"FencingToken\" > 0 AND \"KillSwitchEpoch\" >= 0 AND \"ReserveVersion\" > 0 AND \"ReserveAuthorizationEpoch\" > 0 AND \"PolicyVersion\" > 0");
                table.HasCheckConstraint(
                    "ck_economy_payout_operations_timestamps",
                    "\"UpdatedAt\" >= \"CreatedAt\"");
                table.HasCheckConstraint(
                    "ck_economy_payout_operations_dispatch",
                    "(\"State\" = 1 AND \"DispatchSnapshotHash\" IS NULL) OR (\"State\" BETWEEN 2 AND 6 AND \"DispatchSnapshotHash\" IS NOT NULL)");
            });

            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).ValueGeneratedNever();
            builder.Property(row => row.IdempotencyKey).HasMaxLength(256);
            builder.Property(row => row.RequestHash).HasMaxLength(128);
            builder.Property(row => row.ProviderAccountId).HasMaxLength(256);
            builder.Property(row => row.DestinationHash).HasMaxLength(128);
            builder.Property(row => row.ProviderBindingHash).HasMaxLength(128);
            builder.Property(row => row.EligibilityHash).HasMaxLength(128);
            builder.Property(row => row.DispatchSnapshotHash).HasMaxLength(128);
            builder.Property(row => row.ProviderPayoutId).HasMaxLength(256);
            builder.Property(row => row.Version).IsConcurrencyToken();
            builder.HasIndex(row => new { row.TenantId, row.IdempotencyKey }).IsUnique()
                .HasDatabaseName("ux_economy_payout_operations_tenant_idempotency");
            builder.HasIndex(row => new { row.State, row.UpdatedAt })
                .HasDatabaseName("ix_economy_payout_operations_state_updated");
            builder.HasIndex(row => new { row.TenantId, row.State, row.UpdatedAt })
                .HasDatabaseName("ix_economy_payout_operations_tenant_state_updated");
        });

        modelBuilder.Entity<PayoutProviderEventRow>(builder =>
        {
            builder.ToTable("economy_payout_provider_events");
            builder.HasKey(row => row.EventId);
            builder.Property(row => row.EventId).HasMaxLength(256);
            builder.Property(row => row.EventHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.OperationId, row.RecordedAt })
                .HasDatabaseName("ix_economy_payout_provider_events_operation_recorded");
            builder.HasOne<PayoutOperationRow>()
                .WithMany()
                .HasForeignKey(row => row.OperationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PayoutConnectAccountRow>(builder =>
        {
            builder.ToTable("economy_payout_connect_accounts", table =>
            {
                table.HasCheckConstraint("ck_economy_payout_connect_accounts_state", "\"State\" BETWEEN 1 AND 4");
                table.HasCheckConstraint("ck_economy_payout_connect_accounts_version", "\"Version\" > 0");
                table.HasCheckConstraint("ck_economy_payout_connect_accounts_window", "\"ExpiresAt\" > \"ObservedAt\"");
            });
            builder.HasKey(row => row.PayeeId);
            builder.Property(row => row.Provider).HasMaxLength(50);
            builder.Property(row => row.Environment).HasMaxLength(50);
            builder.Property(row => row.ProviderAccountId).HasMaxLength(256);
            builder.Property(row => row.DestinationHash).HasMaxLength(128);
            builder.Property(row => row.EvidenceHash).HasMaxLength(128);
            builder.HasIndex(row => new { row.Provider, row.Environment, row.ProviderAccountId }).IsUnique();
            builder.HasIndex(row => new { row.State, row.ExpiresAt });
        });

        modelBuilder.Entity<PayoutDispatchOutboxRow>(builder =>
        {
            builder.ToTable("economy_payout_dispatch_outbox", table =>
                table.HasCheckConstraint("ck_economy_payout_dispatch_outbox_attempts", "\"AttemptCount\" >= 0"));
            builder.HasKey(row => row.Id);
            builder.Property(row => row.Id).ValueGeneratedNever();
            builder.Property(row => row.IdempotencyKey).HasMaxLength(256);
            builder.Property(row => row.Payload).HasColumnType("jsonb");
            builder.Property(row => row.PayloadHash).HasMaxLength(128);
            builder.Property(row => row.LeaseOwner).HasMaxLength(200);
            builder.Property(row => row.LastErrorCode).HasMaxLength(100);
            builder.HasIndex(row => row.OperationId).IsUnique();
            builder.HasIndex(row => new { row.CompletedAt, row.AvailableAt, row.LeaseExpiresAt });
            builder.HasOne<PayoutOperationRow>().WithMany().HasForeignKey(row => row.OperationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PayoutAuthorizationEvidenceRow>(builder =>
        {
            builder.ToTable("economy_payout_authorization_evidence", table =>
                table.HasCheckConstraint(
                    "ck_economy_payout_authorization_evidence_phase",
                    "\"Phase\" BETWEEN 1 AND 2"));
            builder.HasKey(row => new { row.OperationId, row.Phase });
            builder.Property(row => row.ReauthenticationEvidenceHash).HasMaxLength(128);
            builder.Property(row => row.OperationFingerprintHash).HasMaxLength(128);
            builder.Property(row => row.CapabilityReceiptHash).HasMaxLength(128);
            builder.HasIndex(row => row.CapabilityReceiptId).IsUnique();
            builder.HasIndex(row => new { row.TenantId, row.RecordedAt });
            builder.HasOne<PayoutOperationRow>().WithMany().HasForeignKey(row => row.OperationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
