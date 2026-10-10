using FluentAssertions;
using GameGuild.API.Database.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class RepairLegacyProjectTeamOwnersMigrationTests
{
    [Fact]
    public void Up_Repairs_Only_Orphaned_Owner_Teams_Using_Auditable_Project_Actors()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(builder);

        var sql = builder.Operations.Should().ContainSingle()
            .Which.Should().BeOfType<SqlOperation>().Subject.Sql;

        sql.Should().Contain("p.\"CreatedById\"");
        sql.Should().Contain("project_versions");
        sql.Should().Contain("\"ProjectCollaborators\"");
        sql.Should().Contain("tm.\"Authority\" = 'Owner'");
        sql.Should().Contain("tm.\"TenantId\" IS NOT DISTINCT FROM candidate.tenant_id");
        sql.Should().Contain("ON CONFLICT (\"TeamId\", \"UserId\") DO UPDATE");
        sql.Should().NotContain("TenantRole");
    }

    [DockerFact]
    public async Task Up_Uses_A_SameTenant_Version_Author_And_Is_Idempotent_On_PostgreSql()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("project_owner_repair");
        await using var connection = new NpgsqlConnection(container.ConnectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, """
            CREATE TABLE projects (
                "Id" uuid PRIMARY KEY, "TenantId" uuid, "CreatedById" uuid, "DeletedAt" timestamptz);
            CREATE TABLE project_teams (
                "Id" uuid PRIMARY KEY, "ProjectId" uuid NOT NULL, "TeamId" uuid NOT NULL,
                "Role" text NOT NULL, "IsActive" boolean NOT NULL, "EndedAt" timestamptz,
                "DeletedAt" timestamptz, "TenantId" uuid, "AssignedAt" timestamptz NOT NULL);
            CREATE TABLE project_versions (
                "Id" uuid PRIMARY KEY, "ProjectId" uuid NOT NULL, "CreatedById" uuid NOT NULL,
                "TenantId" uuid, "CreatedAt" timestamptz NOT NULL, "DeletedAt" timestamptz);
            CREATE TABLE "ProjectCollaborators" (
                "Id" uuid PRIMARY KEY, "ProjectId" uuid NOT NULL, "UserId" uuid NOT NULL,
                "TenantId" uuid, "Role" text NOT NULL, "IsActive" boolean NOT NULL,
                "LeftAt" timestamptz, "DeletedAt" timestamptz, "JoinedAt" timestamptz NOT NULL);
            CREATE TABLE "TenantMembers" (
                "Id" uuid PRIMARY KEY, "TenantId" uuid NOT NULL, "UserId" uuid NOT NULL,
                "IsActive" boolean NOT NULL, "LeftAt" timestamptz, "DeletedAt" timestamptz);
            CREATE TABLE project_collaboration_team_members (
                "Id" uuid PRIMARY KEY, "TeamId" uuid NOT NULL, "UserId" uuid NOT NULL,
                "Authority" text NOT NULL, "ProfessionalTitle" text, "JoinedAt" timestamptz NOT NULL,
                "LeftAt" timestamptz, "IsActive" boolean NOT NULL, "TenantId" uuid,
                "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NOT NULL,
                "DeletedAt" timestamptz, "Version" integer NOT NULL,
                CONSTRAINT "UX_repair_team_user" UNIQUE ("TeamId", "UserId"));
            """);
        await ExecuteAsync(connection, """
            INSERT INTO projects ("Id", "TenantId", "CreatedById", "DeletedAt") VALUES
              ('00000000-0000-0000-0000-000000000101', '00000000-0000-0000-0000-000000000201', NULL, NULL);
            INSERT INTO project_teams
              ("Id", "ProjectId", "TeamId", "Role", "IsActive", "EndedAt", "DeletedAt", "TenantId", "AssignedAt") VALUES
              ('00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000101',
               '00000000-0000-0000-0000-000000000401', 'Owner', TRUE, NULL, NULL,
               '00000000-0000-0000-0000-000000000201', now());
            INSERT INTO "TenantMembers" ("Id", "TenantId", "UserId", "IsActive", "LeftAt", "DeletedAt") VALUES
              ('00000000-0000-0000-0000-000000000501', '00000000-0000-0000-0000-000000000201',
               '00000000-0000-0000-0000-000000000601', TRUE, NULL, NULL),
              ('00000000-0000-0000-0000-000000000502', '00000000-0000-0000-0000-000000000202',
               '00000000-0000-0000-0000-000000000602', TRUE, NULL, NULL);
            INSERT INTO project_versions
              ("Id", "ProjectId", "CreatedById", "TenantId", "CreatedAt", "DeletedAt") VALUES
              ('00000000-0000-0000-0000-000000000701', '00000000-0000-0000-0000-000000000101',
               '00000000-0000-0000-0000-000000000602', '00000000-0000-0000-0000-000000000202', now() - interval '2 days', NULL),
              ('00000000-0000-0000-0000-000000000702', '00000000-0000-0000-0000-000000000101',
               '00000000-0000-0000-0000-000000000601', '00000000-0000-0000-0000-000000000201', now() - interval '1 day', NULL);
            """);

        await ExecuteAsync(connection, MigrationSql());
        await ExecuteAsync(connection, MigrationSql());

        await using var command = new NpgsqlCommand("""
            SELECT "UserId", "TenantId", "Authority", count(*) OVER ()
            FROM project_collaboration_team_members;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetGuid(0).Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000601"));
        reader.GetGuid(1).Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000201"));
        reader.GetString(2).Should().Be("Owner");
        reader.GetInt64(3).Should().Be(1);
    }

    private static string MigrationSql()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(builder);
        return builder.Operations.OfType<SqlOperation>().Single().Sql;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class ExposedMigration : RepairLegacyProjectTeamOwners
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
    }

    private sealed class DockerFactAttribute : FactAttribute
    {
        public DockerFactAttribute()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"), "1", StringComparison.Ordinal))
            {
                Skip = "Docker tests disabled by SKIP_DOCKER_TESTS=1.";
            }
        }
    }
}
