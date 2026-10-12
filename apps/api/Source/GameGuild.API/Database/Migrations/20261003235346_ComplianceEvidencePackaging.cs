using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class ComplianceEvidencePackaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplianceEvidenceDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ControlIdsJson = table.Column<string>(type: "text", nullable: false),
                    ValidationFieldsJson = table.Column<string>(type: "text", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Review = table.Column<int>(type: "integer", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceEvidenceDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceSealedPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PreparedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadyForAuditorReview = table.Column<bool>(type: "boolean", nullable: false),
                    GapCount = table.Column<int>(type: "integer", nullable: false),
                    ArtifactLength = table.Column<int>(type: "integer", nullable: false),
                    ArtifactSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SigningKeyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ArtifactContent = table.Column<byte[]>(type: "bytea", nullable: false),
                    ManifestJson = table.Column<string>(type: "text", nullable: false),
                    SealJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceSealedPackages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceEvidenceDocuments_TenantId_CreatedAt",
                table: "ComplianceEvidenceDocuments",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceSealedPackages_TenantId_CreatedAt",
                table: "ComplianceSealedPackages",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.Sql("""
                ALTER TABLE "ComplianceEvidenceDocuments" ADD CONSTRAINT "CK_ComplianceEvidenceDocuments_Content"
                    CHECK (octet_length("Content") BETWEEN 1 AND 1048576 AND "ContentSha256" ~ '^[0-9a-f]{64}$'
                        AND "Revision" >= 1 AND "ValidFromUtc" <= "ValidUntilUtc"
                        AND "TenantId" <> '00000000-0000-0000-0000-000000000000'::uuid
                        AND "UploadedByUserId" <> '00000000-0000-0000-0000-000000000000'::uuid);
                ALTER TABLE "ComplianceSealedPackages" ADD CONSTRAINT "CK_ComplianceSealedPackages_Artifact"
                    CHECK ("ArtifactLength" = octet_length("ArtifactContent") AND "ArtifactLength" BETWEEN 1 AND 41943040
                        AND "ArtifactSha256" ~ '^[0-9a-f]{64}$' AND "GapCount" >= 0
                        AND "PeriodStartUtc" <= "PeriodEndUtc" AND "PeriodEndUtc" <= "CreatedAt"
                        AND "TenantId" <> '00000000-0000-0000-0000-000000000000'::uuid
                        AND "PreparedByUserId" <> '00000000-0000-0000-0000-000000000000'::uuid);

                CREATE FUNCTION gameguild_guard_compliance_document_update() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF ROW(NEW."Id", NEW."TenantId", NEW."TemplateId", NEW."Name", NEW."Type", NEW."MediaType",
                        NEW."Content", NEW."ContentSha256", NEW."SourceUri", NEW."ValidFromUtc", NEW."ValidUntilUtc",
                        NEW."ControlIdsJson", NEW."ValidationFieldsJson", NEW."UploadedByUserId", NEW."CreatedAt", NEW."DeletedAt")
                        IS DISTINCT FROM
                        ROW(OLD."Id", OLD."TenantId", OLD."TemplateId", OLD."Name", OLD."Type", OLD."MediaType",
                        OLD."Content", OLD."ContentSha256", OLD."SourceUri", OLD."ValidFromUtc", OLD."ValidUntilUtc",
                        OLD."ControlIdsJson", OLD."ValidationFieldsJson", OLD."UploadedByUserId", OLD."CreatedAt", OLD."DeletedAt")
                        OR NEW."Revision"::bigint <> OLD."Revision"::bigint + 1
                        OR NEW."Review" NOT IN (1, 2) OR NEW."ReviewedByUserId" IS NULL
                        OR NEW."ReviewedByUserId" = '00000000-0000-0000-0000-000000000000'::uuid
                        OR NEW."ReviewedAtUtc" IS NULL OR NULLIF(btrim(NEW."ReviewNotes"), '') IS NULL
                    THEN
                        RAISE EXCEPTION 'Evidence content is immutable; only a versioned review may change.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER "ComplianceEvidenceDocuments_ImmutableContent" BEFORE UPDATE ON "ComplianceEvidenceDocuments"
                    FOR EACH ROW EXECUTE FUNCTION gameguild_guard_compliance_document_update();

                CREATE FUNCTION gameguild_guard_compliance_package_update() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'A sealed evidence package cannot be updated.' USING ERRCODE = '23514';
                END $$;
                CREATE TRIGGER "ComplianceSealedPackages_ImmutableCapture" BEFORE UPDATE ON "ComplianceSealedPackages"
                    FOR EACH ROW EXECUTE FUNCTION gameguild_guard_compliance_package_update();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceEvidenceDocuments");

            migrationBuilder.DropTable(
                name: "ComplianceSealedPackages");
            migrationBuilder.Sql("""
                DROP FUNCTION gameguild_guard_compliance_document_update();
                DROP FUNCTION gameguild_guard_compliance_package_update();
                """);
        }
    }
}
