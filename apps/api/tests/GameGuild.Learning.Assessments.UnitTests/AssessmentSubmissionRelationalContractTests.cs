using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class AssessmentSubmissionRelationalContractTests
{
    [Fact]
    public async Task SubmittedModalitiesConstraint_RejectsUnknownBits()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation("C", string.CompareOrdinal);
        await using var db = await CreateContextAsync(connection);
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        db.Add(submission);
        await db.SaveChangesAsync();

        Func<Task> action = () => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"AssessmentSubmissions\" SET \"SubmittedModalities\" = 128 WHERE \"Id\" = {0}",
            submission.Id);

        await action.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task PayloadConsistencyConstraint_RejectsModalityWithoutPayload()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation("C", string.CompareOrdinal);
        await using var db = await CreateContextAsync(connection);
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        db.Add(submission);
        await db.SaveChangesAsync();

        Func<Task> action = () => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"AssessmentSubmissions\" SET \"SubmittedModalities\" = 1 WHERE \"Id\" = {0}",
            submission.Id);

        await action.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task PayloadConsistencyConstraint_RejectsPayloadWithoutModalityBit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation("C", string.CompareOrdinal);
        await using var db = await CreateContextAsync(connection);
        var submission = AssessmentSubmission.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        db.Add(submission);
        await db.SaveChangesAsync();

        Func<Task> action = () => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"AssessmentSubmissions\" SET \"TextPayload\" = 'orphaned' WHERE \"Id\" = {0}",
            submission.Id);

        await action.Should().ThrowAsync<SqliteException>();
    }

    private static async Task<TestAssessmentDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TestAssessmentDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new TestAssessmentDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private sealed class TestAssessmentDbContext(DbContextOptions<TestAssessmentDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);

            // This focused harness exercises the portable submission constraints on SQLite.
            // PostgreSQL-only regex/JSON checks are covered by the baseline database tests.
            modelBuilder.Entity<Assessment>().Metadata.RemoveCheckConstraint("CK_Assessments_ScoreRange");
            modelBuilder.Entity<Assessment>().Metadata.RemoveCheckConstraint("CK_Assessments_ReviewConfiguration");
            modelBuilder.Entity<AssessmentGroup>().Metadata.RemoveCheckConstraint("CK_AssessmentGroups_WeightPercent");
            modelBuilder.Entity<AssessmentSubmission>().Metadata.RemoveCheckConstraint("CK_AssessmentSubmissions_ScoreCanonical");
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not required for relational contract tests.");
        }
    }
}
