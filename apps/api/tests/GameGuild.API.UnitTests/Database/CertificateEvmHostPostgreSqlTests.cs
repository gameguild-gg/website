using GameGuild.API.Core.Integration;
using GameGuild.API.UnitTests.Core;
using GameGuild.Identity.Authentication;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace GameGuild.API.UnitTests.Database;

/// <summary>Actual PostgreSQL startup/schema/role boundaries. No public-chain or RPC acceptance is claimed.</summary>
public sealed class CertificateEvmHostPostgreSqlTests(CertificateEvmHostPostgreSqlFixture fixture)
    : IClassFixture<CertificateEvmHostPostgreSqlFixture>
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task ExistingMigrationPrerequisiteAppliesTheDedicatedHistoryIdempotentlyWithoutTheApplicationHistory()
    {
        await fixture.ResetAsync();
        await using var app = fixture.Application();
        var prerequisite = new GameGuild.API.Database.CertificateEvmJournalMigrationPrerequisite();
        await prerequisite.PrepareAsync(app, Ct);
        await prerequisite.PrepareAsync(app, Ct);
        using var provider = Provider();
        await using var journal = await provider.GetRequiredService<IDbContextFactory<EvmCertificateJournalDbContext>>().CreateDbContextAsync(Ct);
        Assert.Equal(["20261019190000_EvmCertificateJournalInitial"], await journal.Database.GetAppliedMigrationsAsync(Ct));
        Assert.Empty(await app.Database.GetAppliedMigrationsAsync(Ct));
        Assert.Equal(4, journal.Model.GetEntityTypes().Count());
        await new CertificateEvmJournalReadiness(provider.GetRequiredService<IDbContextFactory<EvmCertificateJournalDbContext>>()).StartingAsync(Ct);
    }

    [Fact]
    public async Task ReadinessCannotCreateItsOwnMissingSchema()
    {
        await fixture.ResetAsync(); using var provider = Provider();
        var factory = provider.GetRequiredService<IDbContextFactory<EvmCertificateJournalDbContext>>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new CertificateEvmJournalReadiness(factory).StartingAsync(Ct));
        Assert.Null(error.InnerException);
        await using var journal = await factory.CreateDbContextAsync(Ct);
        Assert.Empty(await journal.Database.GetAppliedMigrationsAsync(Ct));
    }

    [Fact]
    public async Task MissingJournalStopsTheActualHostBeforeAnEarlierHostedServiceCanStart()
    {
        await fixture.ResetAsync();
        var builder = Host.CreateApplicationBuilder();
        var probe = new StartProbe(); builder.Services.AddSingleton<IHostedService>(probe);
        builder.Services.AddCertificateEvmHost(Configuration(fixture.ConnectionString));
        using var host = builder.Build();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(Ct));
        Assert.Contains("journal", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, probe.StartCount);
    }

    [Fact]
    public async Task ReadyHostFactoryPersistsCapturedPublicIntentsWithoutAllocatingANonceOrInventingAReceipt()
    {
        await fixture.ResetAsync(); await Migrate();
        using var provider = Provider(); using var scope = provider.CreateScope();
        await new CertificateEvmJournalReadiness(provider.GetRequiredService<IDbContextFactory<EvmCertificateJournalDbContext>>()).StartingAsync(Ct);
        var policy = provider.GetRequiredService<EvmCertificatePublicationPolicy>();
        var journal = scope.ServiceProvider.GetRequiredService<EvmCertificateJournal>();
        var request = new EvmCertificateJournalRequest(policy.Network, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            CertificateEvmEvidenceProducer.ProducerName, "0x" + new string('4', 64), EvmCertificateOperationKind.Anchor);
        var queued = await journal.EnqueueAsync(request, Ct);
        Assert.Equal(EvmCertificatePublicationStatus.Queued, queued.Status);
        Assert.Null(queued.Nonce); Assert.Null(queued.TransactionHash);
        Assert.Equal(policy.Network.ContractAddress, queued.Network.ContractAddress);
        Assert.Equal(queued.Id, (await journal.FindAsync(queued.Id, Ct))!.Id);
    }

    [Fact]
    public async Task ARemovedTableFailsReadinessEvenWhenTheMigrationHistoryLooksCurrent()
    {
        await fixture.ResetAsync(); await Migrate();
        using var provider = Provider(); var factory = provider.GetRequiredService<IDbContextFactory<EvmCertificateJournalDbContext>>();
        await using var journal = await factory.CreateDbContextAsync(Ct);
        await journal.Database.ExecuteSqlRawAsync("DROP TABLE \"gameguild.authentication\".evm_certificate_attempts", Ct);
        Assert.Single(await journal.Database.GetAppliedMigrationsAsync(Ct));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new CertificateEvmJournalReadiness(factory).StartingAsync(Ct));
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task RestrictedRuntimeRoleCanPersistIntentsWithoutPermissionToChangeTheSchema()
    {
        await fixture.ResetAsync(); await Migrate();
        var password = Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(fixture.ConnectionString); await admin.OpenAsync(Ct);
        // This one class owns the role in its disposable database cluster. Existing roles are never reused.
        await CreateRuntimeRole(admin, password);
        try
        {
            var runtime = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Username = "gg_evm_runtime_fixture", Password = password }.ConnectionString;
            using var provider = ProviderFor(runtime); using var scope = provider.CreateScope();
            await new CertificateEvmJournalReadiness(provider.GetRequiredService<IDbContextFactory<EvmCertificateJournalDbContext>>()).StartingAsync(Ct);
            var policy = provider.GetRequiredService<EvmCertificatePublicationPolicy>();
            var queued = await scope.ServiceProvider.GetRequiredService<EvmCertificateJournal>().EnqueueAsync(new(policy.Network,
                Guid.NewGuid(), Guid.NewGuid(), null, CertificateEvmEvidenceProducer.ProducerName,
                "0x" + new string('5', 64), EvmCertificateOperationKind.Anchor), Ct);
            Assert.Equal(EvmCertificatePublicationStatus.Queued, queued.Status);
            await using var connection = new NpgsqlConnection(runtime); await connection.OpenAsync(Ct);
            var error = await Assert.ThrowsAsync<PostgresException>(() => AttemptRuntimeDdl(connection));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        }
        finally
        {
            await RemoveRuntimeRole(admin);
        }
    }

    [Fact]
    public async Task EnabledMigrationPrerequisiteRejectsAnInMemoryProviderWithoutRetainingAnInnerException()
    {
        await using var app = new GameGuild.API.Database.ApplicationDbContext(new DbContextOptionsBuilder<GameGuild.API.Database.ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GameGuild.API.Database.CertificateEvmJournalMigrationPrerequisite().PrepareAsync(app, Ct));
        Assert.Null(error.InnerException);
    }

    private async Task Migrate()
    {
        await using var app = fixture.Application();
        await new GameGuild.API.Database.CertificateEvmJournalMigrationPrerequisite().PrepareAsync(app, Ct);
    }

    private ServiceProvider Provider() => ProviderFor(fixture.ConnectionString);
    private static ServiceProvider ProviderFor(string connection)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddCertificateEvmHost(Configuration(connection));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static Microsoft.Extensions.Configuration.IConfiguration Configuration(string connection)
    {
        var values = CertificateEvmHostCompositionTests.ValidValues(); values["ConnectionStrings:DefaultConnection"] = connection;
        return CertificateEvmHostCompositionTests.Configuration(values);
    }

    private static async Task CreateRuntimeRole(NpgsqlConnection connection, string password)
    {
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        await using var command = new NpgsqlCommand("""
            SELECT set_config('gameguild.evm_test_role_password', @password, true);
            DO $create_runtime_role$
            BEGIN
                EXECUTE format('CREATE ROLE gg_evm_runtime_fixture LOGIN PASSWORD %L',
                    current_setting('gameguild.evm_test_role_password'));
            END;
            $create_runtime_role$;
            GRANT USAGE ON SCHEMA "gameguild.authentication" TO gg_evm_runtime_fixture;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA "gameguild.authentication" TO gg_evm_runtime_fixture;
            """, connection, transaction);
        command.Parameters.AddWithValue("password", password);
        await command.ExecuteNonQueryAsync(Ct);
        await transaction.CommitAsync(Ct);
    }

    private static async Task AttemptRuntimeDdl(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "CREATE TABLE \"gameguild.authentication\".unexpected_runtime_ddl (id integer)", connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task RemoveRuntimeRole(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("DROP OWNED BY gg_evm_runtime_fixture; DROP ROLE gg_evm_runtime_fixture", connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private sealed class StartProbe : IHostedService
    {
        public int StartCount { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); StartCount++; return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask;
        }
    }
}

public sealed class CertificateEvmHostPostgreSqlFixture : IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase? database;
    public string ConnectionString => database!.ConnectionString;
    public async Task InitializeAsync() => database = await EconomyPostgreSqlTestDatabase.CreateAsync("certificate_evm_host");
    public Task ResetAsync() => database!.ResetAsync();
    public GameGuild.API.Database.ApplicationDbContext Application() => new(new DbContextOptionsBuilder<GameGuild.API.Database.ApplicationDbContext>()
        .UseNpgsql(ConnectionString).Options, enableEntityChangeAuditCapture: false);
    public async Task DisposeAsync()
    {
        if (database is not null) { await database.DisposeAsync(); }
    }
}
