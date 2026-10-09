using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.Database.Migrations;
using GameGuild.CQRS;
using GameGuild.Learning.Assessments;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class AssignmentDeliveryPostgreSqlMigrationTests
{
    [DockerFact]
    public async Task RegisteredWorkflow_SerializesCueLinksAgainstUpdateAndTreeDeletion()
    {
        var container = await EconomyPostgreSqlTestDatabase.CreateAsync("task2_workflow_locks");
        await using var provider = CreateWorkflowProvider(container.ConnectionString);
        try
        {
            var fixture = await SeedWorkflowAsync(provider);
            await AssertSingleGuardedDeleteRegistrationAsync(provider, fixture.UpdateContentId);

            await LinkAndUpdateConcurrentlyAsync(provider, fixture);
            await LinkAndDeleteTreeConcurrentlyAsync(provider, fixture);
            await AssertNoActiveCueReferencesInvalidContentAsync(provider);
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    [DockerFact]
    public async Task SortedMultiContentLocks_CompleteWhenOpposingRequestsWouldDeadlockInReverseOrder()
    {
        var container = await EconomyPostgreSqlTestDatabase.CreateAsync("task2_ordered_locks");
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(container.ConnectionString)
                .Options;
            var ordered = new[] { Guid.NewGuid(), Guid.NewGuid() }.OrderBy(id => id).ToArray();
            var lowId = ordered[0];
            var highId = ordered[1];
            await using var gateContext = new ApplicationDbContext(options);
            await using var highIdGate = await ProgramContentLifecycleDatabaseLock.AcquireAsync(gateContext, [highId]);

            var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstTask = AcquireAndCommitAsync(options, [highId, lowId], firstStarted);
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(250);

            var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondTask = AcquireAndCommitAsync(options, [lowId, highId], secondStarted);
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(250);
            firstTask.IsCompleted.Should().BeFalse();
            secondTask.IsCompleted.Should().BeFalse();

            // With reverse input ordering, the first request would hold highId while the
            // second holds lowId once this gate is released. Sorting both requests low-first
            // leaves only one waiter and lets both requests complete without a deadlock.
            await ProgramContentLifecycleDatabaseLock.CommitAsync(highIdGate);

            var outcomes = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(10));
            outcomes.Should().OnlyContain(outcome => outcome.Success && outcome.Exception == null);
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    [DockerFact]
    public async Task AdvisoryLifecycleLocks_SerializeConcurrentCueLinkAndContentMutation()
    {
        var container = await EconomyPostgreSqlTestDatabase.CreateAsync("task2_locks");
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(container.ConnectionString)
                .Options;
            var contentId = Guid.NewGuid();
            await using var firstContext = new ApplicationDbContext(options);
            await using var firstLock = await ProgramContentLifecycleDatabaseLock.AcquireAsync(firstContext, [contentId]);
            firstLock.Should().NotBeNull();

            var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondTask = Task.Run(async () =>
            {
                await using var secondContext = new ApplicationDbContext(options);
                await using var secondLock = await ProgramContentLifecycleDatabaseLock.AcquireAsync(secondContext, [contentId]);
                secondEntered.SetResult();
                await ProgramContentLifecycleDatabaseLock.CommitAsync(secondLock);
            });

            await Task.Delay(300);
            secondEntered.Task.IsCompleted.Should().BeFalse("the first transaction owns the content lifecycle lock");
            await ProgramContentLifecycleDatabaseLock.CommitAsync(firstLock);
            await secondTask.WaitAsync(TimeSpan.FromSeconds(10));
            secondEntered.Task.IsCompleted.Should().BeTrue();
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    [DockerFact]
    public async Task Up_AppliesLegacyRepairAndRejectsInvalidDeliveryContracts()
    {
        var container = await EconomyPostgreSqlTestDatabase.CreateAsync("task2_migration");
        try
        {
            await using var connection = new NpgsqlConnection(container.ConnectionString);
            await connection.OpenAsync();
            var assessmentId = Guid.NewGuid();
            var submissionId = Guid.NewGuid();
            var from = DateTime.UtcNow.AddDays(2);
            var until = from.AddDays(-1);
            await ExecuteAsync(connection, """
                CREATE TABLE "Assessments" ("Id" uuid PRIMARY KEY, "AvailableFrom" timestamp with time zone NULL, "AvailableUntil" timestamp with time zone NULL);
                CREATE TABLE "AssessmentSubmissions" ("Id" uuid PRIMARY KEY);
                """);
            await using (var seed = new NpgsqlCommand("INSERT INTO \"Assessments\" (\"Id\", \"AvailableFrom\", \"AvailableUntil\") VALUES (@id, @from, @until); INSERT INTO \"AssessmentSubmissions\" (\"Id\") VALUES (@submission);", connection))
            {
                seed.Parameters.AddWithValue("id", assessmentId);
                seed.Parameters.AddWithValue("from", from);
                seed.Parameters.AddWithValue("until", until);
                seed.Parameters.AddWithValue("submission", submissionId);
                await seed.ExecuteNonQueryAsync();
            }

            await ApplyUpAsync(connection);

            await using (var verify = new NpgsqlCommand("SELECT \"AvailableFrom\", \"AvailableUntil\" FROM \"Assessments\" WHERE \"Id\" = @id", connection))
            {
                verify.Parameters.AddWithValue("id", assessmentId);
                await using var reader = await verify.ExecuteReaderAsync();
                (await reader.ReadAsync()).Should().BeTrue();
                reader.GetDateTime(0).Should().BeCloseTo(until, TimeSpan.FromMicroseconds(1));
                reader.GetDateTime(1).Should().BeCloseTo(from, TimeSpan.FromMicroseconds(1));
            }

            await RejectAsync(connection, "UPDATE \"Assessments\" SET \"AvailableFrom\" = now() + interval '2 days', \"AvailableUntil\" = now() + interval '1 day' WHERE \"Id\" = '" + assessmentId + "';");
            await RejectAsync(connection, "UPDATE \"AssessmentSubmissions\" SET \"SubmittedModalities\" = 128 WHERE \"Id\" = '" + submissionId + "';");
            await RejectAsync(connection, "UPDATE \"AssessmentSubmissions\" SET \"SubmittedModalities\" = 1 WHERE \"Id\" = '" + submissionId + "';");
            await RejectAsync(connection, "UPDATE \"AssessmentSubmissions\" SET \"TextPayload\" = 'orphaned' WHERE \"Id\" = '" + submissionId + "';");
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    private static async Task ApplyUpAsync(NpgsqlConnection connection)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(builder);
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection.ConnectionString)
                .Options);
        var generator = context.GetService<IMigrationsSqlGenerator>();
        foreach (var command in generator.Generate(builder.Operations, null))
        {
            await ExecuteAsync(connection, command.CommandText);
        }
    }

    private static async Task RejectAsync(NpgsqlConnection connection, string sql)
    {
        Func<Task> action = () => ExecuteAsync(connection, sql);
        await action.Should().ThrowAsync<PostgresException>();
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class ExposedMigration : AddAssignmentDeliveryAndGradingContracts
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
    }

    private static ServiceProvider CreateWorkflowProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddCoursesModule();
        services.AddAssessmentsModule();
        services.AddCqrs(typeof(RemoveProgramContentCommandHandler).Assembly);
        return services.BuildServiceProvider();
    }

    private static async Task<WorkflowFixture> SeedWorkflowAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();

        var courseId = Guid.NewGuid();
        var orderedIds = new[] { Guid.NewGuid(), Guid.NewGuid() }.OrderBy(id => id).ToArray();
        var childId = orderedIds[0];
        var rootId = orderedIds[1];
        var updateContentId = Guid.NewGuid();
        var assessment = Assessment.Create(courseId, "Cue delivery", AssessmentType.Assignment, ScoreValue.FromPoints("100"));
        var program = new GameGuild.Learning.Courses.Program { Id = courseId, Title = "Course", Slug = $"course-{courseId:N}" };
        var updateContent = VideoContent(courseId, updateContentId, "Update target");
        var root = VideoContent(courseId, rootId, "Root video");
        var child = VideoContent(courseId, childId, "Child video", rootId);
        context.AddRange(program, assessment, updateContent, root, child);
        await context.SaveChangesAsync();

        return new WorkflowFixture(courseId, assessment.Id, updateContentId, rootId, childId);
    }

    private static async Task AssertSingleGuardedDeleteRegistrationAsync(IServiceProvider provider, Guid contentId)
    {
        await using var scope = provider.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IRequestHandler<RemoveProgramContentCommand, bool>>().ToArray();
        handlers.Should().ContainSingle().Which.Should().BeOfType<RemoveProgramContentCommandHandler>();
        scope.ServiceProvider.GetServices<ICommandHandler<RemoveProgramContentCommand, bool>>()
            .Should().ContainSingle().Which.Should().BeOfType<RemoveProgramContentCommandHandler>();

        var lifecycleGuard = scope.ServiceProvider.GetRequiredService<IProgramContentLifecycleGuard>();
        lifecycleGuard.Should().BeOfType<AssessmentProgramContentLifecycleGuard>();
        (await lifecycleGuard.HasBlockingDeleteReference(contentId)).Should().BeFalse();
    }

    private static async Task LinkAndUpdateConcurrentlyAsync(IServiceProvider provider, WorkflowFixture fixture)
    {
        await using var gateScope = provider.CreateAsyncScope();
        var gateContext = gateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var gate = await ProgramContentLifecycleDatabaseLock.AcquireAsync(gateContext, [fixture.UpdateContentId]);
        gate.Should().NotBeNull();

        var linkStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var updateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var linkTask = Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            linkStarted.SetResult();
            return await scope.ServiceProvider.GetRequiredService<IAssessmentService>().LinkInteractiveVideoCueAsync(
                fixture.AssessmentId,
                new LinkInteractiveVideoCueRequest(fixture.UpdateContentId, "update-race"));
        });
        var updateTask = Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            updateStarted.SetResult();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IProgramContentService>().UpdateContentAsync(
                    new ProgramContent
                    {
                        Id = fixture.UpdateContentId,
                        ProgramId = fixture.CourseId,
                        Title = "Incompatible update",
                        Type = ProgramContentType.Lesson,
                        LessonFormat = LessonContentFormat.Markdown
                    });
                return (Exception?)null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        });

        await Task.WhenAll(linkStarted.Task, updateStarted.Task).WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(300);
        linkTask.IsCompleted.Should().BeFalse("the real cue-link service must wait on the lifecycle lock");
        updateTask.IsCompleted.Should().BeFalse("the real content-update service must wait on the lifecycle lock");
        await ProgramContentLifecycleDatabaseLock.CommitAsync(gate);

        var linkResult = await linkTask.WaitAsync(TimeSpan.FromSeconds(10));
        var updateException = await updateTask.WaitAsync(TimeSpan.FromSeconds(10));
        var updateSucceeded = updateException is null;
        (linkResult.IsSuccess ^ updateSucceeded).Should().BeTrue(
            "the serialized workflow must have exactly one winner");

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var content = await verificationContext.Set<ProgramContent>().SingleAsync(candidate => candidate.Id == fixture.UpdateContentId);
        var activeCueCount = await verificationContext.Set<InteractiveVideoAssessmentCue>()
            .CountAsync(cue => cue.ContentId == fixture.UpdateContentId && cue.DeletedAt == null);
        if (linkResult.IsSuccess)
        {
            updateException.Should().BeOfType<RequestValidationException>();
            content.Type.Should().Be(ProgramContentType.Lesson);
            content.LessonFormat.Should().Be(LessonContentFormat.Video);
            activeCueCount.Should().Be(1);
        }
        else
        {
            updateException.Should().BeNull();
            content.LessonFormat.Should().Be(LessonContentFormat.Markdown);
            activeCueCount.Should().Be(0);
        }
    }

    private static async Task LinkAndDeleteTreeConcurrentlyAsync(IServiceProvider provider, WorkflowFixture fixture)
    {
        await using var gateScope = provider.CreateAsyncScope();
        var gateContext = gateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var gate = await ProgramContentLifecycleDatabaseLock.AcquireAsync(
            gateContext,
            [fixture.RootContentId, fixture.ChildContentId]);
        gate.Should().NotBeNull();

        var rootLinkStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rootLinkTask = Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            rootLinkStarted.SetResult();
            return await scope.ServiceProvider.GetRequiredService<IAssessmentService>().LinkInteractiveVideoCueAsync(
                fixture.AssessmentId,
                new LinkInteractiveVideoCueRequest(fixture.RootContentId, "root-race"));
        });
        var deleteTask = Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            deleteStarted.SetResult();
            try
            {
                return new DeleteOutcome(
                    await scope.ServiceProvider
                    .GetRequiredService<IRequestHandler<RemoveProgramContentCommand, bool>>()
                    .Handle(new RemoveProgramContentCommand(fixture.CourseId, fixture.RootContentId), CancellationToken.None),
                    null);
            }
            catch (Exception exception)
            {
                return new DeleteOutcome(null, exception);
            }
        });

        await Task.WhenAll(rootLinkStarted.Task, deleteStarted.Task).WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(300);
        rootLinkTask.IsCompleted.Should().BeFalse();
        deleteTask.IsCompleted.Should().BeFalse("the CQRS delete path must acquire the same sorted multi-content locks");
        await ProgramContentLifecycleDatabaseLock.CommitAsync(gate);

        var rootLinkResult = await rootLinkTask.WaitAsync(TimeSpan.FromSeconds(15));
        var deleteOutcome = await deleteTask.WaitAsync(TimeSpan.FromSeconds(15));
        var deleteSucceeded = deleteOutcome.Result is true && deleteOutcome.Exception is null;
        (rootLinkResult.IsSuccess ^ deleteSucceeded).Should().BeTrue(
            "the cue link and physical tree delete must have exactly one coherent winner");

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (deleteSucceeded)
        {
            rootLinkResult.IsSuccess.Should().BeFalse();
            (await verificationContext.Set<ProgramContent>()
                .AnyAsync(content => (content.Id == fixture.RootContentId || content.Id == fixture.ChildContentId) && content.DeletedAt == null))
                .Should().BeFalse();
            (await verificationContext.Set<InteractiveVideoAssessmentCue>()
                .AnyAsync(cue => cue.ContentId == fixture.RootContentId && cue.DeletedAt == null))
                .Should().BeFalse();
        }
        else
        {
            rootLinkResult.IsSuccess.Should().BeTrue();
            deleteOutcome.Exception.Should().BeOfType<RequestValidationException>();
            (await verificationContext.Set<ProgramContent>()
                .CountAsync(content => (content.Id == fixture.RootContentId || content.Id == fixture.ChildContentId) &&
                                       content.DeletedAt == null &&
                                       content.Type == ProgramContentType.Lesson &&
                                       content.LessonFormat == LessonContentFormat.Video))
                .Should().Be(2);
            (await verificationContext.Set<InteractiveVideoAssessmentCue>()
                .CountAsync(cue => cue.ContentId == fixture.RootContentId && cue.DeletedAt == null))
                .Should().Be(1);
        }
    }

    private static async Task AssertNoActiveCueReferencesInvalidContentAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staleCueExists = await context.Set<InteractiveVideoAssessmentCue>()
            .AnyAsync(cue => cue.DeletedAt == null && !context.Set<ProgramContent>().Any(content =>
                content.Id == cue.ContentId &&
                content.DeletedAt == null &&
                content.Type == ProgramContentType.Lesson &&
                content.LessonFormat == LessonContentFormat.Video));

        staleCueExists.Should().BeFalse();
    }

    private static ProgramContent VideoContent(Guid courseId, Guid contentId, string title, Guid? parentId = null) => new()
    {
        Id = contentId,
        ProgramId = courseId,
        ParentId = parentId,
        Title = title,
        Type = ProgramContentType.Lesson,
        LessonFormat = LessonContentFormat.Video
    };

    private sealed record WorkflowFixture(
        Guid CourseId,
        Guid AssessmentId,
        Guid UpdateContentId,
        Guid RootContentId,
        Guid ChildContentId);

    private static async Task<LockOutcome> AcquireAndCommitAsync(
        DbContextOptions<ApplicationDbContext> options,
        IReadOnlyCollection<Guid> contentIds,
        TaskCompletionSource started)
    {
        await using var context = new ApplicationDbContext(options);
        started.SetResult();
        try
        {
            await using var transaction = await ProgramContentLifecycleDatabaseLock.AcquireAsync(context, contentIds);
            await ProgramContentLifecycleDatabaseLock.CommitAsync(transaction);
            return new LockOutcome(true, null);
        }
        catch (Exception exception)
        {
            return new LockOutcome(false, exception);
        }
    }

    private sealed record DeleteOutcome(bool? Result, Exception? Exception);
    private sealed record LockOutcome(bool Success, Exception? Exception);

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
