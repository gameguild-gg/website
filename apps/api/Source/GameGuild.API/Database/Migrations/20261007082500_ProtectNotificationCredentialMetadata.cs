using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

/// <inheritdoc />
public partial class ProtectNotificationCredentialMetadata : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Metadata",
            table: "Notifications",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(4000)",
            oldMaxLength: 4000,
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Hold the lock across the guard and type change: a concurrent wide insert must not be truncated.
        migrationBuilder.Sql("""
            LOCK TABLE "Notifications" IN ACCESS EXCLUSIVE MODE;
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM "Notifications" WHERE char_length("Metadata") > 4000) THEN
                    RAISE EXCEPTION 'Notification metadata cannot be narrowed without data loss.';
                END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Metadata",
            table: "Notifications",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }
}
