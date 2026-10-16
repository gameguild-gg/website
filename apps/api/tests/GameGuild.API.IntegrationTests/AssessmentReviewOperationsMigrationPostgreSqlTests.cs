using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Tenants;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AssessmentReviewOperationsMigrationPostgreSqlTests
{
    [Fact]
    public async Task UpgradeAndRollbackPreserveExistingDataAndMatchTheCurrentModel()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("assessment_review_operations_migration");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using var db = new ApplicationDbContext(options);
        Assert.False(db.Database.HasPendingModelChanges());

        var migrations = db.Database.GetMigrations().ToList();
        var evidenceMigration = Assert.Single(
            migrations,
            value => value.EndsWith("_AddReviewEvidenceReuseReference", StringComparison.Ordinal));
        var operationsMigration = Assert.Single(
            migrations,
            value => value.EndsWith("_AddPeerReviewClaimsAndReleaseSchedules", StringComparison.Ordinal));
        var evidenceIndex = migrations.IndexOf(evidenceMigration);
        Assert.Equal(evidenceIndex + 1, migrations.IndexOf(operationsMigration));
        var predecessor = migrations[evidenceIndex - 1];
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(predecessor);
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = $"Review migration {Guid.NewGuid():N}",
            Slug = $"review-migration-{Guid.NewGuid():N}",
            AdminEmail = $"review-migration-{Guid.NewGuid():N}@example.test",
            IsActive = true,
        };
        db.Set<Tenant>().Add(tenant);
        await db.SaveChangesAsync();

        await migrator.MigrateAsync(operationsMigration);
        Assert.True(await db.Set<Tenant>().AsNoTracking().AnyAsync(value => value.Id == tenant.Id));
        Assert.Equal(2, await CountPhaseFourTablesAsync(db));
        Assert.Equal(2, await CountPhaseFourColumnsAsync(db));
        Assert.Equal(9, await CountPhaseFourIndexesAsync(db));

        await migrator.MigrateAsync(predecessor);
        Assert.True(await db.Set<Tenant>().AsNoTracking().AnyAsync(value => value.Id == tenant.Id));
        Assert.Equal(0, await CountPhaseFourTablesAsync(db));
        Assert.Equal(0, await CountPhaseFourColumnsAsync(db));

        await migrator.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.True(await db.Set<Tenant>().AsNoTracking().AnyAsync(value => value.Id == tenant.Id));
        Assert.Empty(await db.Set<PeerReviewClaim>().AsNoTracking().ToListAsync());
        Assert.Empty(await db.Set<GradeResultReleaseSchedule>().AsNoTracking().ToListAsync());
    }

    private static Task<int> CountPhaseFourTablesAsync(ApplicationDbContext db) =>
        db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::int AS "Value"
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN ('PeerReviewClaims', 'GradeResultReleaseSchedules')
            """).SingleAsync();

    private static Task<int> CountPhaseFourColumnsAsync(ApplicationDbContext db) =>
        db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::int AS "Value"
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND ((table_name = 'ReviewEvidence' AND column_name = 'SourceReviewEvidenceId')
                OR (table_name = 'ReviewStages' AND column_name = 'EvidenceDeadlineAt'))
            """).SingleAsync();

    private static Task<int> CountPhaseFourIndexesAsync(ApplicationDbContext db) =>
        db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::int AS "Value"
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname IN (
                'IX_ReviewEvidence_SourceReviewEvidenceId',
                'IX_ReviewStages_Status_EvidenceDeadlineAt',
                'IX_GradeResultReleaseSchedules_GradeRoundId',
                'IX_GradeResultReleaseSchedules_Status_ScheduledFor',
                'IX_PeerReviewClaims_ReviewerTestRunSubjectId',
                'IX_PeerReviewClaims_ReviewEvidenceId',
                'IX_PeerReviewClaims_ReviewStageId_ReviewerTestRunSubjectId',
                'IX_PeerReviewClaims_ReviewStageId_ReviewerUserId',
                'IX_PeerReviewClaims_Status_LeaseExpiresAt')
            """).SingleAsync();
}
