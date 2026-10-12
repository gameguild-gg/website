using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.Database.Migrations;
using GameGuild.Learning.Assessments;
using GameGuild.Learning.Assessments.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class AssessmentGradingWorkflowMigrationTests
{
    [Fact]
    public void Up_PreservesLegacyAssessmentPayloadsAndIntroducesReviewMethodsSeparately()
    {
        var builder = BuildUp();

        builder.Operations.OfType<DropColumnOperation>()
            .Where(operation => operation.Table is "Assessments" or "AssessmentSubmissions")
            .Select(operation => operation.Name)
            .Should().NotContain([
                "DefinitionPayload",
                "DefinitionSchemaVersion",
                "GradingMethods",
                "PeerReviewsRequiredCount",
                "StructuredAnswerPayload"
            ]);
        builder.Operations.OfType<RenameColumnOperation>()
            .Should().NotContain(operation => operation.Name == "PeerReviewsRequiredCount");

        var reviewMethods = builder.Operations.OfType<AddColumnOperation>()
            .Single(operation => operation.Table == "Assessments" && operation.Name == "ReviewMethods");
        reviewMethods.IsNullable.Should().BeTrue();

        OperationIndex<SqlOperation>(builder, operation => operation.Sql.Contains("review-method-backfill", StringComparison.Ordinal))
            .Should().BeLessThan(OperationIndex<AlterColumnOperation>(builder,
                operation => operation.Table == "Assessments" && operation.Name == "ReviewMethods"));
    }

    [Fact]
    public void Up_BackfillsSubmissionActorsAndPoliciesBeforeEnforcingRequiredColumns()
    {
        var builder = BuildUp();

        var nullableStarter = builder.Operations.OfType<AddColumnOperation>()
            .Single(operation => operation.Table == "AssessmentSubmissions" && operation.Name == "StartedByUserId");
        nullableStarter.IsNullable.Should().BeTrue();

        var actorBackfill = OperationIndex<SqlOperation>(builder,
            operation => operation.Sql.Contains("submission-actor-backfill", StringComparison.Ordinal));
        var requiredStarter = OperationIndex<AlterColumnOperation>(builder,
            operation => operation.Table == "AssessmentSubmissions" && operation.Name == "StartedByUserId");
        var starterConstraint = OperationIndex<AddCheckConstraintOperation>(builder,
            operation => operation.Table == "AssessmentSubmissions" && operation.Name == "CK_AssessmentSubmissions_Starter");
        actorBackfill.Should().BeLessThan(requiredStarter);
        requiredStarter.Should().BeLessThan(starterConstraint);

        OperationIndex<SqlOperation>(builder,
                operation => operation.Sql.Contains("assessment-policy-backfill", StringComparison.Ordinal))
            .Should().BeLessThan(OperationIndex<AlterColumnOperation>(builder,
                operation => operation.Table == "Assessments" && operation.Name == "MaxAttempts"));
    }

    [Fact]
    public void Model_PreservesLegacyColumnsAndUsesValidPolicyDefaults()
    {
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=model_only;Username=model;Password=model")
                .Options);

        var assessment = context.Model.FindEntityType(typeof(Assessment));
        assessment.Should().NotBeNull();
        assessment!.FindProperty("DefinitionPayload").Should().NotBeNull();
        assessment.FindProperty("DefinitionSchemaVersion")!.GetDefaultValue().Should().Be(1);
        assessment.FindProperty("GradingMethods")!.GetDefaultValue().Should().Be(8);
        assessment.FindProperty("PeerReviewsRequiredCount")!.GetDefaultValue().Should().Be(0);
        assessment.FindProperty(nameof(Assessment.ReviewMethods))!.GetDefaultValue()
            .Should().Be(ReviewMethods.InstructorReview);
        assessment.FindProperty(nameof(Assessment.ContentCompletionMode))!.GetDefaultValue()
            .Should().Be(ContentCompletionMode.OnReleaseAndPass);
        assessment.FindProperty(nameof(Assessment.ResultReleaseMode))!.GetDefaultValue()
            .Should().Be(ResultReleaseMode.Manual);

        var submission = context.Model.FindEntityType(typeof(AssessmentSubmission));
        submission.Should().NotBeNull();
        submission!.FindProperty("StructuredAnswerPayload").Should().NotBeNull();
    }

    [DockerFact]
    public async Task MigrationChain_CreatesTheCurrentSchemaFromAnEmptyDatabase()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("assessment_grading_clean");
        await using var context = CreateContext(database.ConnectionString);

        await context.Database.MigrateAsync();

        var definedMigrations = context.Database.GetMigrations().ToArray();
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToArray();
        appliedMigrations.Should().Equal(definedMigrations);
        pendingMigrations.Should().BeEmpty();
        context.Database.HasPendingModelChanges().Should().BeFalse();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        (await ScalarAsync<string>(connection, "SELECT to_regclass('\"Assessments\"')::text;"))
            .Should().Be("\"Assessments\"");
        (await ScalarAsync<string>(connection, "SELECT to_regclass('\"GradingExecutions\"')::text;"))
            .Should().Be("\"GradingExecutions\"");
        (await ScalarAsync<string>(connection, "SELECT to_regclass('\"GradeResultReleases\"')::text;"))
            .Should().Be("\"GradeResultReleases\"");
    }

    [DockerFact]
    public async Task MigrationChain_UpgradesThePopulatedPredecessorAndPreservesGradingData()
    {
        const string predecessorMigration = "20260911012842_AddAiAssistedLessonAuthoring";
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("assessment_grading_upgrade");
        await using var context = CreateContext(database.ConnectionString);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(predecessorMigration);
        await context.Database.ExecuteSqlRawAsync(PredecessorSeedSql);
        await migrator.MigrateAsync();

        var definedMigrations = context.Database.GetMigrations().ToArray();
        var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        appliedMigrations.Should().Equal(definedMigrations);
        context.Database.HasPendingModelChanges().Should().BeFalse();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        (await ScalarAsync<int>(connection,
            "SELECT \"PassingScore\" FROM \"programs\" WHERE \"Id\" = '10000000-0000-0000-0000-000000000001';"))
            .Should().Be(6050);
        (await ScalarAsync<int>(connection,
            "SELECT \"WeightPercent\" FROM \"AssessmentGroups\" WHERE \"Id\" = '20000000-0000-0000-0000-000000000001';"))
            .Should().Be(2525);
        (await ScalarAsync<int>(connection,
            "SELECT \"MaxScore\" FROM \"Assessments\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000001';"))
            .Should().Be(200);
        (await ScalarAsync<int>(connection,
            "SELECT \"PassingScore\" FROM \"Assessments\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000001';"))
            .Should().Be(125);
        (await ScalarAsync<int>(connection,
            "SELECT \"Score\" FROM \"AssessmentSubmissions\" WHERE \"Id\" = '40000000-0000-0000-0000-000000000001';"))
            .Should().Be(175);

        var actor = Guid.Parse("50000000-0000-0000-0000-000000000001");
        (await ScalarAsync<Guid>(connection,
            "SELECT \"StartedByUserId\" FROM \"AssessmentSubmissions\" WHERE \"Id\" = '40000000-0000-0000-0000-000000000001';"))
            .Should().Be(actor);
        (await ScalarAsync<Guid>(connection,
            "SELECT \"SubmittedByUserId\" FROM \"AssessmentSubmissions\" WHERE \"Id\" = '40000000-0000-0000-0000-000000000001';"))
            .Should().Be(actor);

        (await ScalarAsync<int>(connection,
            "SELECT \"MaxAttempts\" FROM \"Assessments\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000001';"))
            .Should().Be(1);
        (await ScalarAsync<int>(connection,
            "SELECT \"MaxAttempts\" FROM \"Assessments\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000002';"))
            .Should().Be(3);
        (await ScalarAsync<int>(connection,
            "SELECT \"ReviewMethods\" FROM \"Assessments\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000001';"))
            .Should().Be(9);
        (await ScalarAsync<int>(connection,
            "SELECT \"ReviewMethods\" FROM \"Assessments\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000002';"))
            .Should().Be((int)ReviewMethods.InstructorReview);
        (await ScalarAsync<string>(connection,
            "SELECT \"ContentCompletionMode\" FROM \"Assessments\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000001';"))
            .Should().Be("on-release-and-pass");
        (await ScalarAsync<string>(connection,
            "SELECT \"ResultReleaseMode\" FROM \"Assessments\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000001';"))
            .Should().Be("manual");

        (await ScalarAsync<string>(connection,
            "SELECT \"DefinitionPayload\"::text FROM \"Assessments\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000001';"))
            .Should().Be("{\"legacy\": true}");
        (await ScalarAsync<string>(connection,
            "SELECT \"StructuredAnswerPayload\"::text FROM \"AssessmentSubmissions\" WHERE \"Id\" = '40000000-0000-0000-0000-000000000001';"))
            .Should().Be("{\"answer\": true}");

        (await ScalarAsync<string>(connection, """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'programs' AND column_name = 'PassingScore';
            """)).Should().Be("integer");
        (await ScalarAsync<string>(connection, """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'AssessmentGroups' AND column_name = 'WeightPercent';
            """)).Should().Be("integer");
    }

    private const string PredecessorSeedSql = """
        INSERT INTO "programs" (
            "Id", "CreatedAt", "UpdatedAt", "Version", "Title", "Slug", "Status", "Visibility",
            "EnrollmentStatus", "Category", "Difficulty", "PassingScore")
        VALUES (
            '10000000-0000-0000-0000-000000000001', now(), now(), 0, 'Migration course',
            'migration-course', 0, 0, 0, 0, 0, 60.50);

        INSERT INTO "AssessmentGroups" (
            "Id", "CourseId", "CreatedAt", "UpdatedAt", "Version", "Name", "Order", "WeightPercent")
        VALUES (
            '20000000-0000-0000-0000-000000000001',
            '10000000-0000-0000-0000-000000000001', now(), now(), 0, 'Exams', 0, 25.25);

        INSERT INTO "Assessments" (
            "Id", "CourseId", "AssessmentGroupId", "CreatedAt", "UpdatedAt", "Version", "Title", "Slug",
            "Type", "MaxScore", "PassingScore", "IsRequired", "Order", "SubmissionModalities",
            "PresentationMode", "GradingMethods", "PeerReviewsRequiredCount", "AllowLateSubmissions",
            "MaxAttempts", "DefinitionPayload")
        VALUES (
            '30000000-0000-0000-0000-000000000001',
            '10000000-0000-0000-0000-000000000001',
            '20000000-0000-0000-0000-000000000001', now(), now(), 0, 'Quiz one', 'quiz-one',
            2, 200, 125, true, 0, 64, 0, 9, 0, false, NULL, '{{"legacy":true}}'::jsonb),
            (
            '30000000-0000-0000-0000-000000000002',
            '10000000-0000-0000-0000-000000000001', NULL, now(), now(), 0, 'Quiz two', 'quiz-two',
            2, 300, 150, false, 1, 64, 0, 15, 0, false, 3, '{{"legacy":true}}'::jsonb);

        INSERT INTO "AssessmentSubmissions" (
            "Id", "AssessmentId", "EnrollmentId", "UserId", "AttemptNumber", "CreatedAt", "UpdatedAt",
            "Version", "StartedAt", "Status", "IsLate", "SubmittedModalities", "StructuredAnswerPayload",
            "SubmittedAt", "Score")
        VALUES (
            '40000000-0000-0000-0000-000000000001',
            '30000000-0000-0000-0000-000000000001',
            '60000000-0000-0000-0000-000000000001',
            '50000000-0000-0000-0000-000000000001', 1, now(), now(), 0, now(), 2, false, 64,
            '{{"answer":true}}'::jsonb, now(), 175);
        """;

    private static ApplicationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static MigrationBuilder BuildUp()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(builder);
        return builder;
    }

    private static int OperationIndex<TOperation>(
        MigrationBuilder builder,
        Func<TOperation, bool> predicate)
        where TOperation : MigrationOperation =>
        builder.Operations.Select((operation, index) => (operation, index))
            .Where(item => item.operation is TOperation typed && predicate(typed))
            .Select(item => item.index)
            .Single();

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private sealed class ExposedMigration : AddAssessmentGradingWorkflow
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
    }

    private sealed class DockerFactAttribute : FactAttribute
    {
        public DockerFactAttribute()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"), "1", StringComparison.Ordinal))
                Skip = "Docker tests disabled by SKIP_DOCKER_TESTS=1.";
        }
    }
}
