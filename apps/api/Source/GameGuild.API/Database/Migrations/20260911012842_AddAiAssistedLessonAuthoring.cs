using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAssistedLessonAuthoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_authoring_conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_authoring_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_authoring_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_authoring_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_authoring_proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseDraftRevision = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OriginalContent = table.Column<string>(type: "text", nullable: false),
                    ProposedContent = table.Column<string>(type: "text", nullable: false),
                    ProposedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_authoring_proposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_authoring_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseDraftRevision = table.Column<int>(type: "integer", nullable: false),
                    ProposalKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Instruction = table.Column<string>(type: "text", nullable: false),
                    Selection = table.Column<string>(type: "text", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Model = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MaximumInputTokens = table.Column<int>(type: "integer", nullable: false),
                    MaximumOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    MaximumEstimatedCost = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    SettledCost = table.Column<long>(type: "bigint", nullable: false),
                    ReleasedAmount = table.Column<long>(type: "bigint", nullable: false),
                    ResponseText = table.Column<string>(type: "text", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_authoring_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_authoring_stream_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Delta = table.Column<string>(type: "text", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_authoring_stream_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_credit_rate_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InputSoftUnitsPerMillion = table.Column<long>(type: "bigint", nullable: false),
                    OutputSoftUnitsPerMillion = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_credit_rate_cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ai_credit_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RateCardVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InputSoftUnitsPerMillion = table.Column<long>(type: "bigint", nullable: false),
                    OutputSoftUnitsPerMillion = table.Column<long>(type: "bigint", nullable: false),
                    ReservedSoftUnits = table.Column<long>(type: "bigint", nullable: false),
                    SettledSoftUnits = table.Column<long>(type: "bigint", nullable: false),
                    ReleasedSoftUnits = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    ProviderUsageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReservationIdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SettlementIdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReleaseReason = table.Column<string>(type: "text", nullable: true),
                    ReservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_credit_reservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "program_content_drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasePublishedVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    LastEditedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastEditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_content_drafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_content_drafts_program_contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "program_contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_content_publication_audits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftRevision = table.Column<int>(type: "integer", nullable: false),
                    PreviousPublishedVersion = table.Column<int>(type: "integer", nullable: false),
                    PublishedVersion = table.Column<int>(type: "integer", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_content_publication_audits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_authoring_conversations_AuthorId_LastMessageAt",
                table: "ai_authoring_conversations",
                columns: new[] { "AuthorId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_authoring_conversations_TenantId_ContentId_AuthorId",
                table: "ai_authoring_conversations",
                columns: new[] { "TenantId", "ContentId", "AuthorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_authoring_messages_ConversationId_CreatedAt",
                table: "ai_authoring_messages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_authoring_messages_RunId",
                table: "ai_authoring_messages",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_authoring_proposals_ContentId_Status",
                table: "ai_authoring_proposals",
                columns: new[] { "ContentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_authoring_proposals_RunId",
                table: "ai_authoring_proposals",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_authoring_runs_ContentId_ActorId_CreatedAt",
                table: "ai_authoring_runs",
                columns: new[] { "ContentId", "ActorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_authoring_runs_Status",
                table: "ai_authoring_runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ai_authoring_runs_TenantId_ActorId_IdempotencyKey",
                table: "ai_authoring_runs",
                columns: new[] { "TenantId", "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_authoring_stream_events_RunId_Sequence",
                table: "ai_authoring_stream_events",
                columns: new[] { "RunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_credit_rate_cards_ServiceCode_Provider_Model_Version",
                table: "ai_credit_rate_cards",
                columns: new[] { "ServiceCode", "Provider", "Model", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_credit_reservations_ReservationIdempotencyKey",
                table: "ai_credit_reservations",
                column: "ReservationIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_credit_reservations_RunId",
                table: "ai_credit_reservations",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_credit_reservations_SettlementIdempotencyKey",
                table: "ai_credit_reservations",
                column: "SettlementIdempotencyKey",
                unique: true,
                filter: "\"SettlementIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ai_credit_reservations_TenantId_ActorId_ReservedAt",
                table: "ai_credit_reservations",
                columns: new[] { "TenantId", "ActorId", "ReservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_program_content_drafts_ContentId",
                table: "program_content_drafts",
                column: "ContentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_program_content_drafts_TenantId_ProgramId",
                table: "program_content_drafts",
                columns: new[] { "TenantId", "ProgramId" });

            migrationBuilder.CreateIndex(
                name: "IX_program_content_publication_audits_ContentId_PublishedAt",
                table: "program_content_publication_audits",
                columns: new[] { "ContentId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_program_content_publication_audits_TenantId_PublishedBy",
                table: "program_content_publication_audits",
                columns: new[] { "TenantId", "PublishedBy" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_authoring_conversations");

            migrationBuilder.DropTable(
                name: "ai_authoring_messages");

            migrationBuilder.DropTable(
                name: "ai_authoring_proposals");

            migrationBuilder.DropTable(
                name: "ai_authoring_runs");

            migrationBuilder.DropTable(
                name: "ai_authoring_stream_events");

            migrationBuilder.DropTable(
                name: "ai_credit_rate_cards");

            migrationBuilder.DropTable(
                name: "ai_credit_reservations");

            migrationBuilder.DropTable(
                name: "program_content_drafts");

            migrationBuilder.DropTable(
                name: "program_content_publication_audits");
        }
    }
}
