using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>Frozen v1 schema shared by the initial migration target and model snapshot.</summary>
/// <remarks>Future changes require a new schema version and migration; never mutate this version.</remarks>
internal static class EvmCertificateJournalSchemaV1
{
    internal static void Build(ModelBuilder builder)
    {
        builder.HasAnnotation("ProductVersion", "10.0.9");
        builder.HasDefaultSchema(EvmCertificateJournalDbContext.SchemaName);
        var op = builder.Entity<EvmCertificateJournalOperationRow>();
        op.ToTable("evm_certificate_operations", t =>
        {
            t.HasCheckConstraint("ck_evm_operation_nonce", "\"Nonce\" IS NULL OR (\"Nonce\" >= 0 AND \"Nonce\" <= 18446744073709551615)");
            t.HasCheckConstraint("ck_evm_operation_identity", "\"ChainId\" > 0 AND \"OwnerUserId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DocumentId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            t.HasCheckConstraint("ck_evm_operation_lease", "(\"LeaseToken\" IS NULL) = (\"LeaseUntil\" IS NULL)");
            t.HasCheckConstraint("ck_evm_operation_status", "\"Status\" BETWEEN 0 AND 8 AND \"Kind\" BETWEEN 0 AND 1 AND \"Revision\" >= 0");
        });
        op.HasKey(x => x.Id);
        op.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        op.Property(x => x.Profile).HasMaxLength(100).IsRequired();
        op.Property(x => x.ChainId).HasColumnType("bigint");
        op.Property(x => x.ContractAddress).HasMaxLength(42).IsRequired();
        op.Property(x => x.IssuerId).HasMaxLength(66).IsRequired();
        op.Property(x => x.RuntimeCodeSha256).HasMaxLength(64).IsRequired();
        op.Property(x => x.FinalityMode).HasConversion<int>().HasColumnType("integer");
        op.Property(x => x.MinimumConfirmations).HasColumnType("integer");
        op.Property(x => x.DocumentId).HasColumnType("uuid");
        op.Property(x => x.OwnerUserId).HasColumnType("uuid");
        op.Property(x => x.TenantId).HasColumnType("uuid");
        op.Property(x => x.Producer).HasMaxLength(100).IsRequired();
        op.Property(x => x.CertificateHash).HasMaxLength(66).IsRequired();
        op.Property(x => x.Kind).HasConversion<int>().HasColumnType("integer");
        op.Property(x => x.Status).HasConversion<int>().HasColumnType("integer");
        op.Property(x => x.SignerReference).HasMaxLength(100);
        op.Property(x => x.SignerAddress).HasMaxLength(42);
        op.Property(x => x.Nonce).HasPrecision(20, 0);
        op.Property(x => x.GasLimit).HasPrecision(20, 0);
        op.Property(x => x.MaximumFeePerGasWei).HasMaxLength(78);
        op.Property(x => x.MaximumTransactionFeeWei).HasMaxLength(78);
        op.Property(x => x.TransactionHash).HasMaxLength(66);
        op.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        op.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
        op.Property(x => x.NextAttemptAt).HasColumnType("timestamp with time zone");
        op.Property(x => x.LeaseToken).HasColumnType("uuid");
        op.Property(x => x.LeaseUntil).HasColumnType("timestamp with time zone");
        op.Property(x => x.Revision).HasColumnType("bigint").IsConcurrencyToken();
        op.Property(x => x.LastFailure).HasConversion<int?>().HasColumnType("integer");
        op.HasIndex(x => new { x.ChainId, x.ContractAddress, x.CertificateHash, x.Kind }).IsUnique().HasDatabaseName("ux_evm_operation_digest");
        op.HasIndex(x => new { x.ChainId, x.ContractAddress, x.Producer, x.DocumentId, x.Kind }).IsUnique().HasDatabaseName("ux_evm_operation_document");
        op.HasIndex(x => new { x.ChainId, x.SignerAddress, x.Nonce }).IsUnique().HasDatabaseName("ux_evm_operation_nonce");
        op.HasIndex(x => new { x.Status, x.NextAttemptAt, x.LeaseUntil }).HasDatabaseName("ix_evm_operation_due");
        op.HasIndex(x => new { x.OwnerUserId, x.TenantId }).HasDatabaseName("ix_evm_operation_owner");

        var nonce = builder.Entity<EvmCertificateJournalNonceRow>();
        nonce.ToTable("evm_certificate_nonces", t => t.HasCheckConstraint("ck_evm_next_nonce", "\"NextNonce\" >= 0 AND \"NextNonce\" <= 18446744073709551615 AND \"ChainId\" > 0 AND \"Revision\" >= 0"));
        nonce.HasKey(x => new { x.ChainId, x.SignerAddress });
        nonce.Property(x => x.ChainId).HasColumnType("bigint");
        nonce.Property(x => x.SignerAddress).HasMaxLength(42);
        nonce.Property(x => x.NextNonce).HasPrecision(20, 0);
        nonce.Property(x => x.Revision).HasColumnType("bigint").IsConcurrencyToken();

        var attempt = builder.Entity<EvmCertificateJournalAttemptRow>();
        attempt.ToTable("evm_certificate_attempts");
        attempt.HasKey(x => x.Id);
        attempt.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        attempt.Property(x => x.OperationId).HasColumnType("uuid");
        attempt.Property(x => x.Sequence).HasColumnType("integer");
        attempt.Property(x => x.TransactionHash).HasMaxLength(66).IsRequired();
        attempt.Property(x => x.RawTransaction).HasMaxLength(8194).IsRequired();
        attempt.Property(x => x.MaximumFeePerGasWei).HasMaxLength(78).IsRequired();
        attempt.Property(x => x.PriorityFeePerGasWei).HasMaxLength(78).IsRequired();
        attempt.Property(x => x.PreparedAt).HasColumnType("timestamp with time zone");
        attempt.Property(x => x.BroadcastAt).HasColumnType("timestamp with time zone");
        attempt.HasIndex(x => new { x.OperationId, x.Sequence }).IsUnique().HasDatabaseName("ux_evm_attempt_sequence");
        attempt.HasIndex(x => x.TransactionHash).IsUnique().HasDatabaseName("ux_evm_attempt_hash");
        attempt.HasOne<EvmCertificateJournalOperationRow>().WithMany().HasForeignKey(x => x.OperationId)
            .HasConstraintName("FK_evm_certificate_attempts_operations_OperationId").OnDelete(DeleteBehavior.Restrict);

        var audit = builder.Entity<EvmCertificateJournalEventRow>();
        audit.ToTable("evm_certificate_events");
        audit.HasKey(x => x.Id);
        audit.Property(x => x.Id).HasColumnType("uuid").ValueGeneratedNever();
        audit.Property(x => x.OperationId).HasColumnType("uuid");
        audit.Property(x => x.Revision).HasColumnType("bigint");
        audit.Property(x => x.Code).HasMaxLength(50).IsRequired();
        audit.Property(x => x.Status).HasConversion<int>().HasColumnType("integer");
        audit.Property(x => x.TransactionHash).HasMaxLength(66);
        audit.Property(x => x.ProofStatus).HasConversion<int?>().HasColumnType("integer");
        audit.Property(x => x.BlockHash).HasMaxLength(66);
        audit.Property(x => x.BlockNumber).HasPrecision(20, 0);
        audit.Property(x => x.At).HasColumnType("timestamp with time zone");
        audit.HasIndex(x => new { x.OperationId, x.Revision }).IsUnique().HasDatabaseName("ux_evm_event_revision");
        audit.HasOne<EvmCertificateJournalOperationRow>().WithMany().HasForeignKey(x => x.OperationId)
            .HasConstraintName("FK_evm_certificate_events_operations_OperationId").OnDelete(DeleteBehavior.Restrict);
    }
}
