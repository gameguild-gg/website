using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GameGuild.Identity.Authentication;

/// <summary>Opt-in PostgreSQL journal with its own model and migration history.</summary>
/// <remarks>It is not added to the application model or activated by the none/local provider.</remarks>
public sealed class EvmCertificateJournalDbContext : DbContext
{
    public static string SchemaName => "gameguild.authentication";
    public static string MigrationsHistoryTable => "__EF_EvmCertificateJournal";

    public EvmCertificateJournalDbContext(DbContextOptions<EvmCertificateJournalDbContext> options) : base(options)
    {
        var relational = RelationalOptionsExtension.Extract(options);
        if (relational.MigrationsHistoryTableName != MigrationsHistoryTable || relational.MigrationsHistoryTableSchema != SchemaName)
        {
            throw new ArgumentException("The journal requires its dedicated migration history table and schema.", nameof(options));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        EvmCertificateJournalSchemaV1.Build(modelBuilder);
    }
}

internal sealed class EvmCertificateJournalOperationRow
{
    public Guid Id { get; set; }
    public string Profile { get; set; } = string.Empty;
    public long ChainId { get; set; }
    public string ContractAddress { get; set; } = string.Empty;
    public string IssuerId { get; set; } = string.Empty;
    public string RuntimeCodeSha256 { get; set; } = string.Empty;
    public Configuration.ApplicationLayer.EvmCertificateFinalityMode FinalityMode { get; set; }
    public int MinimumConfirmations { get; set; }
    public Guid DocumentId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid? TenantId { get; set; }
    public string Producer { get; set; } = string.Empty;
    public string CertificateHash { get; set; } = string.Empty;
    public EvmCertificateOperationKind Kind { get; set; }
    public EvmCertificatePublicationStatus Status { get; set; }
    public string? SignerReference { get; set; }
    public string? SignerAddress { get; set; }
    public decimal? Nonce { get; set; }
    public decimal? GasLimit { get; set; }
    public string? MaximumFeePerGasWei { get; set; }
    public string? MaximumTransactionFeeWei { get; set; }
    public string? TransactionHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public long Revision { get; set; }
    public EvmCertificateJournalFailure? LastFailure { get; set; }
}

internal sealed class EvmCertificateJournalNonceRow
{
    public long ChainId { get; set; }
    public string SignerAddress { get; set; } = string.Empty;
    public decimal NextNonce { get; set; }
    public long Revision { get; set; }
}

internal sealed class EvmCertificateJournalAttemptRow
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public int Sequence { get; set; }
    public string TransactionHash { get; set; } = string.Empty;
    public string RawTransaction { get; set; } = string.Empty;
    public string MaximumFeePerGasWei { get; set; } = string.Empty;
    public string PriorityFeePerGasWei { get; set; } = string.Empty;
    public DateTimeOffset PreparedAt { get; set; }
    public DateTimeOffset? BroadcastAt { get; set; }
}

internal sealed class EvmCertificateJournalEventRow
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public long Revision { get; set; }
    public string Code { get; set; } = string.Empty;
    public EvmCertificatePublicationStatus Status { get; set; }
    public string? TransactionHash { get; set; }
    public EvmCertificateProofStatus? ProofStatus { get; set; }
    public string? BlockHash { get; set; }
    public decimal? BlockNumber { get; set; }
    public DateTimeOffset At { get; set; }
}
