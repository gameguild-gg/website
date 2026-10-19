using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GameGuild.Identity.Authentication;

/// <summary>Initial opt-in journal migration; does not alter existing anchor or application tables.</summary>
[DbContext(typeof(EvmCertificateJournalDbContext))]
[Migration("20261019190000_EvmCertificateJournalInitial")]
public sealed class EvmCertificateJournalInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var schema = EvmCertificateJournalDbContext.SchemaName;
        migrationBuilder.EnsureSchema(schema);
        migrationBuilder.CreateTable("evm_certificate_operations", schema: schema, columns: table => new
        {
            Id = table.Column<Guid>("uuid", nullable: false),
            Profile = table.Column<string>("character varying(100)", maxLength: 100, nullable: false),
            ChainId = table.Column<long>("bigint", nullable: false),
            ContractAddress = table.Column<string>("character varying(42)", maxLength: 42, nullable: false),
            IssuerId = table.Column<string>("character varying(66)", maxLength: 66, nullable: false),
            RuntimeCodeSha256 = table.Column<string>("character varying(64)", maxLength: 64, nullable: false),
            FinalityMode = table.Column<int>("integer", nullable: false),
            MinimumConfirmations = table.Column<int>("integer", nullable: false),
            DocumentId = table.Column<Guid>("uuid", nullable: false),
            OwnerUserId = table.Column<Guid>("uuid", nullable: false),
            TenantId = table.Column<Guid>("uuid", nullable: true),
            Producer = table.Column<string>("character varying(100)", maxLength: 100, nullable: false),
            CertificateHash = table.Column<string>("character varying(66)", maxLength: 66, nullable: false),
            Kind = table.Column<int>("integer", nullable: false),
            Status = table.Column<int>("integer", nullable: false),
            SignerReference = table.Column<string>("character varying(100)", maxLength: 100, nullable: true),
            SignerAddress = table.Column<string>("character varying(42)", maxLength: 42, nullable: true),
            Nonce = table.Column<decimal>("numeric(20,0)", precision: 20, scale: 0, nullable: true),
            GasLimit = table.Column<decimal>("numeric(20,0)", precision: 20, scale: 0, nullable: true),
            MaximumFeePerGasWei = table.Column<string>("character varying(78)", maxLength: 78, nullable: true),
            MaximumTransactionFeeWei = table.Column<string>("character varying(78)", maxLength: 78, nullable: true),
            TransactionHash = table.Column<string>("character varying(66)", maxLength: 66, nullable: true),
            CreatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
            NextAttemptAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
            LeaseToken = table.Column<Guid>("uuid", nullable: true),
            LeaseUntil = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
            Revision = table.Column<long>("bigint", nullable: false),
            LastFailure = table.Column<int>("integer", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_evm_certificate_operations", x => x.Id);
            table.CheckConstraint("ck_evm_operation_nonce", "\"Nonce\" IS NULL OR (\"Nonce\" >= 0 AND \"Nonce\" <= 18446744073709551615)");
            table.CheckConstraint("ck_evm_operation_identity", "\"ChainId\" > 0 AND \"OwnerUserId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DocumentId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.CheckConstraint("ck_evm_operation_lease", "(\"LeaseToken\" IS NULL) = (\"LeaseUntil\" IS NULL)");
            table.CheckConstraint("ck_evm_operation_status", "\"Status\" BETWEEN 0 AND 8 AND \"Kind\" BETWEEN 0 AND 1 AND \"Revision\" >= 0");
        });
        migrationBuilder.CreateTable("evm_certificate_nonces", schema: schema, columns: table => new
        {
            ChainId = table.Column<long>("bigint", nullable: false),
            SignerAddress = table.Column<string>("character varying(42)", maxLength: 42, nullable: false),
            NextNonce = table.Column<decimal>("numeric(20,0)", precision: 20, scale: 0, nullable: false),
            Revision = table.Column<long>("bigint", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_evm_certificate_nonces", x => new { x.ChainId, x.SignerAddress });
            table.CheckConstraint("ck_evm_next_nonce", "\"NextNonce\" >= 0 AND \"NextNonce\" <= 18446744073709551615 AND \"ChainId\" > 0 AND \"Revision\" >= 0");
        });
        migrationBuilder.CreateTable("evm_certificate_attempts", schema: schema, columns: table => new
        {
            Id = table.Column<Guid>("uuid", nullable: false),
            OperationId = table.Column<Guid>("uuid", nullable: false),
            Sequence = table.Column<int>("integer", nullable: false),
            TransactionHash = table.Column<string>("character varying(66)", maxLength: 66, nullable: false),
            RawTransaction = table.Column<string>("character varying(8194)", maxLength: 8194, nullable: false),
            MaximumFeePerGasWei = table.Column<string>("character varying(78)", maxLength: 78, nullable: false),
            PriorityFeePerGasWei = table.Column<string>("character varying(78)", maxLength: 78, nullable: false),
            PreparedAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
            BroadcastAt = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_evm_certificate_attempts", x => x.Id);
            table.ForeignKey("FK_evm_certificate_attempts_operations_OperationId", x => x.OperationId,
                "evm_certificate_operations", "Id", principalSchema: schema, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable("evm_certificate_events", schema: schema, columns: table => new
        {
            Id = table.Column<Guid>("uuid", nullable: false),
            OperationId = table.Column<Guid>("uuid", nullable: false),
            Revision = table.Column<long>("bigint", nullable: false),
            Code = table.Column<string>("character varying(50)", maxLength: 50, nullable: false),
            Status = table.Column<int>("integer", nullable: false),
            TransactionHash = table.Column<string>("character varying(66)", maxLength: 66, nullable: true),
            ProofStatus = table.Column<int>("integer", nullable: true),
            BlockHash = table.Column<string>("character varying(66)", maxLength: 66, nullable: true),
            BlockNumber = table.Column<decimal>("numeric(20,0)", precision: 20, scale: 0, nullable: true),
            At = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_evm_certificate_events", x => x.Id);
            table.ForeignKey("FK_evm_certificate_events_operations_OperationId", x => x.OperationId,
                "evm_certificate_operations", "Id", principalSchema: schema, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("ux_evm_operation_digest", "evm_certificate_operations",
            new[] { "ChainId", "ContractAddress", "CertificateHash", "Kind" }, schema: schema, unique: true);
        migrationBuilder.CreateIndex("ux_evm_operation_document", "evm_certificate_operations",
            new[] { "ChainId", "ContractAddress", "Producer", "DocumentId", "Kind" }, schema: schema, unique: true);
        migrationBuilder.CreateIndex("ux_evm_operation_nonce", "evm_certificate_operations",
            new[] { "ChainId", "SignerAddress", "Nonce" }, schema: schema, unique: true);
        migrationBuilder.CreateIndex("ix_evm_operation_due", "evm_certificate_operations",
            new[] { "Status", "NextAttemptAt", "LeaseUntil" }, schema: schema);
        migrationBuilder.CreateIndex("ix_evm_operation_owner", "evm_certificate_operations", new[] { "OwnerUserId", "TenantId" }, schema: schema);
        migrationBuilder.CreateIndex("ux_evm_attempt_sequence", "evm_certificate_attempts", new[] { "OperationId", "Sequence" }, schema: schema, unique: true);
        migrationBuilder.CreateIndex("ux_evm_attempt_hash", "evm_certificate_attempts", "TransactionHash", schema: schema, unique: true);
        migrationBuilder.CreateIndex("ux_evm_event_revision", "evm_certificate_events", new[] { "OperationId", "Revision" }, schema: schema, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        var schema = EvmCertificateJournalDbContext.SchemaName;
        migrationBuilder.DropTable("evm_certificate_events", schema);
        migrationBuilder.DropTable("evm_certificate_attempts", schema);
        migrationBuilder.DropTable("evm_certificate_operations", schema);
        migrationBuilder.DropTable("evm_certificate_nonces", schema);
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        EvmCertificateJournalSchemaV1.Build(modelBuilder);
    }
}

/// <summary>Snapshot pinned to the immutable v1 schema; future changes select a new version.</summary>
[DbContext(typeof(EvmCertificateJournalDbContext))]
public sealed class EvmCertificateJournalModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        EvmCertificateJournalSchemaV1.Build(modelBuilder);
    }
}
