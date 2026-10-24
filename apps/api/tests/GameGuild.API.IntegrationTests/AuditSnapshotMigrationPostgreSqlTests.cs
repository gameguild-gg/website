using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using GameGuild.Identity.Tenants;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AuditSnapshotMigrationPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    private const string MigrationId = "20261024010000_AddMutationValueSnapshotsAndChainOfCustody";

    private static readonly Type[] AuditStores =
    [
        typeof(MutationValueSnapshot), typeof(MutationValuePayloadChunk),
        typeof(ValueSnapshotRetentionPolicy), typeof(ValueSnapshotRetentionExecution),
        typeof(ValueSnapshotRollbackPlan), typeof(CustodyEvidenceItem), typeof(CustodyEvent),
        typeof(EvidenceCustodian), typeof(EvidenceTransfer), typeof(LegalHold),
        typeof(LegalHoldNotification), typeof(CustodyAccessLog)
    ];

    [Fact]
    public async Task UpgradeRepairsDefaultCaptureAndRollbackPreservesExistingRecords()
    {
        using var hostScope = fixture.Factory.Services.CreateScope();
        var services = hostScope.ServiceProvider;
        _ = services.GetRequiredService<ApplicationDbContext>().Model;
        var operation = services.GetRequiredService<IUseCaseOperationContextAccessor>();
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("audit_snapshot_migration");
        // Historical upgrades intentionally precede the current model. The dedicated snapshot
        // test below checks the new audit stores against the native host model independently.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var schema = new ApplicationDbContext(options);
        var migrations = schema.Database.GetMigrations().ToList();
        Assert.Contains(MigrationId, migrations);
        var predecessor = migrations[migrations.IndexOf(MigrationId) - 1];
        var migrator = schema.GetService<IMigrator>();
        await migrator.MigrateAsync(predecessor);

        var marker = "audit-schema-" + Guid.NewGuid().ToString("N");
        schema.Set<DataProtectionKey>().Add(new DataProtectionKey { FriendlyName = marker, Xml = "<test />" });
        await schema.SaveChangesAsync();

        // Reproduce the production failure on the fully migrated previous schema using the
        // same snapshot-enabled constructor as the host, not the options-only test shortcut.
        await using (var before = CreateCaptureContext(options, operation))
        {
            before.Set<Tenant>().Add(CreateTenant());
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => before.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(error.InnerException);
            Assert.Equal(PostgresErrorCodes.UndefinedTable, postgres.SqlState);
            Assert.Contains("MutationValueSnapshots", postgres.MessageText, StringComparison.Ordinal);
        }

        await migrator.MigrateAsync(MigrationId);
        await AssertStoresReadableAsync(schema);
        var persisted = CreateTenant();
        await using (var after = CreateCaptureContext(options, operation))
        {
            after.Set<Tenant>().Add(persisted);
            Assert.Equal(1, await after.SaveChangesAsync());
        }

        var snapshot = Assert.Single(await schema.Set<MutationValueSnapshot>().IgnoreQueryFilters()
            .Where(row => row.EntityName == nameof(Tenant) && row.KeyId == persisted.Id.ToString()).ToListAsync());
        Assert.Equal("Added", snapshot.ChangeKind);
        Assert.NotNull(snapshot.AfterPayload);
        Assert.NotNull(snapshot.AfterStoredSha256);

        var rolledBack = CreateTenant();
        await using (var after = CreateCaptureContext(options, operation))
        {
            await using var transaction = await after.Database.BeginTransactionAsync();
            after.Set<Tenant>().Add(rolledBack);
            await after.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        Assert.False(await schema.Set<Tenant>().IgnoreQueryFilters().AnyAsync(row => row.Id == rolledBack.Id));
        Assert.False(await schema.Set<MutationValueSnapshot>().IgnoreQueryFilters()
            .AnyAsync(row => row.KeyId == rolledBack.Id.ToString()));

        // Down removes only these new stores. Existing business and data-protection records survive.
        await migrator.MigrateAsync(predecessor);
        Assert.True(await schema.Set<DataProtectionKey>().AnyAsync(row => row.FriendlyName == marker));
        Assert.True(await schema.Set<Tenant>().IgnoreQueryFilters().AnyAsync(row => row.Id == persisted.Id));
        foreach (var type in AuditStores)
        {
            var table = schema.Model.FindEntityType(type)!.GetTableName()!;
            Assert.Equal(0, await schema.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value" FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = {table}
                """).SingleAsync());
        }
        await migrator.MigrateAsync(MigrationId);
        await AssertStoresReadableAsync(schema);
    }

    [Fact]
    public async Task FreshMigratedDatabaseContainsEveryMappedAuditColumn()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        await AssertStoresReadableAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    [Fact]
    public void CommittedSnapshotMatchesNativeAuditStorePropertiesAndIndexes()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        foreach (var type in AuditStores)
        {
            var current = db.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;
            var stored = snapshot.FindEntityType(type.FullName!)!;
            Assert.NotNull(stored);
            Assert.Equal(current.GetTableName(), stored.GetTableName());
            Assert.Equal(current.GetSchema(), stored.GetSchema());
            Assert.Equal(Properties(current), Properties(stored));
            Assert.Equal(Indexes(current), Indexes(stored));
        }
    }

    private static string[] Properties(IEntityType entity) => entity.GetProperties()
        .Select(property => string.Join("|", property.Name, property.GetColumnType(), property.IsNullable,
            property.GetMaxLength(), property.IsConcurrencyToken, property.ValueGenerated))
        .Order(StringComparer.Ordinal).ToArray();

    private static string[] Indexes(IEntityType entity) => entity.GetIndexes()
        .Select(index => string.Join(",", index.Properties.Select(property => property.Name)) + ":" + index.IsUnique)
        .Order(StringComparer.Ordinal).ToArray();

    private static ApplicationDbContext CreateCaptureContext(DbContextOptions<ApplicationDbContext> options,
        IUseCaseOperationContextAccessor operation) => new(options, publisher: null,
        useCaseOperationContextAccessor: operation, valueSnapshotOptions: Options.Create(new ValueSnapshotOptions()));

    private static Tenant CreateTenant()
    {
        var marker = Guid.NewGuid().ToString("N");
        return new Tenant { Id = Guid.NewGuid(), Name = "Audit schema " + marker, Slug = "audit-schema-" + marker,
            AdminEmail = "schema-" + marker + "@example.test", IsActive = true };
    }

    private static async Task AssertStoresReadableAsync(ApplicationDbContext db)
    {
        foreach (var type in AuditStores)
        {
            var entity = db.Model.FindEntityType(type)!;
            var table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var columns = string.Join(", ", entity.GetProperties().Select(property =>
                '"' + property.GetColumnName(store)!.Replace("\"", "\"\"", StringComparison.Ordinal) + '"'));
            // Identifiers come exclusively from the fixed audit-store model, never request input.
            var sql = $"SELECT {columns} FROM \"{table}\" LIMIT 0";
            await db.Database.ExecuteSqlRawAsync(sql);
        }
    }
}
