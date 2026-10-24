using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class CommerceMigrationDiscoveryPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task MigratedDatabaseContainsThePreviouslyUndiscoveredCommerceSchema()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        const string promoMigration = "20261022124500_AddPromoPlanEligibilityConversionSubscriptionApplication";
        Assert.Contains("20261020120000_AddRefundKindAndDisputeFees", applied);
        Assert.Contains(promoMigration, applied);
        Assert.Contains("20261022150000_AddDisputeEvidenceSubmissionTracking", applied);

        Type[] stores = [typeof(Refund), typeof(PaymentDispute), typeof(DisputeEvidence),
            typeof(PromoCode), typeof(PromoCodeUse), typeof(PromoCodeValidationAttempt)];
        var sqlGeneration = db.GetService<ISqlGenerationHelper>();
        foreach (var type in stores)
        {
            var entity = db.Model.FindEntityType(type)!;
            var table = entity.GetTableName()!;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var columns = string.Join(", ", entity.GetProperties().Select(property =>
                sqlGeneration.DelimitIdentifier(property.GetColumnName(store)!)));
            // SQL parameters cannot represent identifiers. Quote fixed model identifiers with
            // the provider helper; LIMIT 0 still makes PostgreSQL bind every mapped column.
            var sql = $"SELECT {columns} FROM {sqlGeneration.DelimitIdentifier(table, entity.GetSchema())} LIMIT 0";
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[promoMigration], db.Database.ProviderName!);
        var indexes = migration.UpOperations.OfType<CreateIndexOperation>().ToArray();
        Assert.Equal(7, indexes.Length);
        foreach (var index in indexes)
        {
            var entity = db.Model.GetEntityTypes().Single(type => type.GetTableName() == index.Table);
            Assert.Contains(entity.GetIndexes(), modelIndex => modelIndex.GetDatabaseName() == index.Name);
            Assert.Equal(1, await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value" FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = {index.Table} AND indexname = {index.Name}
                """).SingleAsync());
        }
    }
}
