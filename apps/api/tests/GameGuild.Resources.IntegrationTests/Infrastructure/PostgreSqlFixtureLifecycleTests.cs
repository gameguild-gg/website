using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GameGuild.Resources.IntegrationTests.Infrastructure;

[Collection("PostgreSql")]
public sealed class PostgreSqlFixtureLifecycleTests(PostgreSqlTestFixture sharedFixture)
{
    [Fact]
    public void HostPreservesTheScopedAbstractContextForRealAuthorizationRepositories()
    {
        using var factory = new PostgreSqlWebApplicationFactory(sharedFixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Same(context, scope.ServiceProvider.GetRequiredService<DbContext>());
        Assert.IsType<DataMaskingRuleRepository>(scope.ServiceProvider.GetRequiredService<IDataMaskingRuleRepository>());
    }

    [Fact]
    public async Task Initialize_PreparesTheApiSchemaBeforeAnyHostAndIsolatesQuotaDatabases()
    {
        var fixture = new PostgreSqlTestFixture();
        try
        {
            await fixture.InitializeAsync();
            fixture.IsRunning.Should().BeTrue();
            await using var parent = new NpgsqlConnection(fixture.ConnectionString);
            await parent.OpenAsync();
            await using var schemaReady = parent.CreateCommand();
            schemaReady.CommandText = """
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'AssessmentDefinitionRevisions'
                )
                """;
            (await schemaReady.ExecuteScalarAsync()).Should().Be(true,
                "API hosted preflight queries this table before the first request");

            string quotaDatabaseName;
            await using (var quotaDatabase = await fixture.CreateDatabaseAsync("fixture_lifecycle"))
            {
                quotaDatabaseName = new NpgsqlConnectionStringBuilder(quotaDatabase.ConnectionString).Database!;
                await using var quota = new NpgsqlConnection(quotaDatabase.ConnectionString);
                await quota.OpenAsync();
                await using var tables = quota.CreateCommand();
                tables.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'";
                (await tables.ExecuteScalarAsync()).Should().Be(0L,
                    "focused quota contexts must not inherit the API or gate-template schema");
            }

            await using var databaseExists = parent.CreateCommand();
            databaseExists.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)";
            databaseExists.Parameters.AddWithValue("name", quotaDatabaseName);
            (await databaseExists.ExecuteScalarAsync()).Should().Be(false);
            (await schemaReady.ExecuteScalarAsync()).Should().Be(true,
                "disposing a quota database must not remove its fixture's API schema");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
        fixture.IsRunning.Should().BeFalse();
    }
}
