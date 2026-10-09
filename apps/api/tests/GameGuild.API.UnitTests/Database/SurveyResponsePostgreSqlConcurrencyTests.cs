using FluentAssertions;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Data.Common;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class SurveyResponsePostgreSqlConcurrencyTests
{
    [PostgreSqlFact]
    public async Task DirectSubmissions_RespectSingleAndMultipleSurveyResponsePoliciesUnderConcurrency()
    {
        var container = await EconomyPostgreSqlTestDatabase.CreateAsync("survey_response_policy");
        try
        {
            var options = new DbContextOptionsBuilder<SurveyPolicyDbContext>()
                .UseNpgsql(container.ConnectionString)
                .Options;
            await using (var setup = new SurveyPolicyDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
            }

            var single = await SeedSurveyAsync(options, allowMultipleResponses: false);
            var singleResults = await Task.WhenAll(
                SubmitAsync(options, single),
                SubmitAsync(options, single));
            singleResults.Select(result => result.Id).Distinct().Should().ContainSingle();
            await using (var verify = new SurveyPolicyDbContext(options))
            {
                (await verify.Set<ContentInteraction>().CountAsync()).Should().Be(1);
            }

            var mixed = await SeedSurveyAsync(options, allowMultipleResponses: false);
            var started = StartAsync(options, mixed);
            var submitted = SubmitAsync(options, mixed);
            await Task.WhenAll(IgnoreSingleResponseRejectionAsync(started), submitted);
            await using (var verify = new SurveyPolicyDbContext(options))
            {
                (await verify.Set<ContentInteraction>().CountAsync(item => item.ContentId == mixed.ContentId)).Should().Be(1);
            }

            var multiple = await SeedSurveyAsync(options, allowMultipleResponses: true);
            var multipleResults = await Task.WhenAll(
                SubmitAsync(options, multiple),
                SubmitAsync(options, multiple));
            multipleResults.Select(result => result.Id).Distinct().Should().HaveCount(2);
            await using (var verify = new SurveyPolicyDbContext(options))
            {
                (await verify.Set<ContentInteraction>().CountAsync(item => item.ContentId == multiple.ContentId)).Should().Be(2);
            }
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    [PostgreSqlFact]
    public async Task InteractionSubmission_UsesFreshPolicyAndSerializesWithDirectSubmission()
    {
        var container = await EconomyPostgreSqlTestDatabase.CreateAsync("survey_interaction_policy");
        try
        {
            var options = new DbContextOptionsBuilder<SurveyPolicyDbContext>()
                .UseNpgsql(container.ConnectionString)
                .Options;
            await using (var setup = new SurveyPolicyDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
            }

            var single = await SeedSurveyAsync(options, allowMultipleResponses: false);
            var activeId = await AddInteractionAsync(options, single, submitted: false);
            await Task.WhenAll(
                IgnoreSingleResponseRejectionAsync(SubmitInteractionAsync(options, single, activeId)),
                IgnoreSingleResponseRejectionAsync(SubmitAsync(options, single)));
            await using (var verify = new SurveyPolicyDbContext(options))
            {
                (await verify.Set<ContentInteraction>()
                    .CountAsync(item => item.ContentId == single.ContentId && item.SubmittedAt != null)).Should().Be(1);
            }

            var multiple = await SeedSurveyAsync(options, allowMultipleResponses: true);
            var multipleActiveId = await AddInteractionAsync(options, multiple, submitted: false);
            var secondLearner = await AddEnrollmentAsync(options, multiple);
            var responses = await Task.WhenAll(
                SubmitInteractionAsync(options, multiple, multipleActiveId),
                SubmitAsync(options, secondLearner));
            var interactionResponse = responses[0];
            var directResponse = responses[1];
            interactionResponse.Id.Should().NotBe(directResponse.Id);
            await using (var verify = new SurveyPolicyDbContext(options))
            {
                (await verify.Set<ContentInteraction>()
                    .CountAsync(item => item.ContentId == multiple.ContentId && item.SubmittedAt != null)).Should().Be(2);
            }

            var stale = await SeedSurveyAsync(options, allowMultipleResponses: true);
            await AddInteractionAsync(options, stale, submitted: true);
            var staleActiveId = await AddInteractionAsync(options, stale, submitted: false);
            await using var staleContext = new SurveyPolicyDbContext(options);
            _ = await staleContext.Set<ContentInteraction>()
                .Include(item => item.Content)
                .SingleAsync(item => item.Id == staleActiveId);
            await UpdateSurveyPolicyAsync(options, stale.ContentId, allowMultipleResponses: false);
            var staleService = new ContentInteractionService(staleContext, new TestRequestContextAccessor(stale.UserId));

            Func<Task> staleSubmit = () => staleService.SubmitContentAsync(
                staleActiveId,
                """{"kind":"survey","answers":{"fresh":true}}""");

            await staleSubmit.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("This survey accepts only one response.");
            await using (var verify = new SurveyPolicyDbContext(options))
            {
                (await verify.Set<ContentInteraction>()
                    .CountAsync(item => item.ContentId == stale.ContentId && item.SubmittedAt != null)).Should().Be(1);
            }
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    [PostgreSqlFact]
    public async Task NonSurveyInteractionPaths_DoNotAcquireSurveyAdvisoryLocks()
    {
        var container = await CreateContainer("non_survey_transaction_policy");
        try
        {
            var setupOptions = CreateOptions(container.ConnectionString);
            await using (var setup = new SurveyPolicyDbContext(setupOptions))
            {
                await setup.Database.EnsureCreatedAsync();
            }

            var fixture = await SeedLessonAsync(setupOptions);
            var probe = new AdvisoryLockProbe();
            var options = CreateOptions(container.ConnectionString, probe);

            await using (var startContext = new SurveyPolicyDbContext(options))
            {
                var start = new ContentInteractionService(startContext, new TestRequestContextAccessor(fixture.UserId));
                await start.StartContentAsync(fixture.EnrollmentId, fixture.StartContentId);
            }
            await using (var submitContext = new SurveyPolicyDbContext(options))
            {
                var submit = new ContentInteractionService(submitContext, new TestRequestContextAccessor(fixture.UserId));
                await submit.SubmitContentAsync(fixture.InteractionId, "legacy lesson submission");
            }
            await using (var directContext = new SurveyPolicyDbContext(options))
            {
                var direct = new ProgramWriteService(directContext, requestContextAccessor: new TestRequestContextAccessor(fixture.UserId));
                await direct.SubmitUserContentAsync(fixture.ProgramId, fixture.UserId, fixture.DirectContentId, "legacy direct submission");
            }

            probe.AcquisitionCount.Should().Be(0);
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    [PostgreSqlFact]
    public async Task LifecycleLock_WhenCallerOwnsTransaction_ShouldNotCommitOrDisposeIt()
    {
        var container = await CreateContainer("lifecycle_existing_transaction");
        try
        {
            var options = CreateOptions(container.ConnectionString);
            await using var context = new SurveyPolicyDbContext(options);
            await using var outerTransaction = await context.Database.BeginTransactionAsync();

            var lockHandle = await ProgramContentLifecycleDatabaseLock.AcquireAsync(context, [Guid.NewGuid()]);

            lockHandle.Should().BeNull();
            context.Database.CurrentTransaction.Should().BeSameAs(outerTransaction);
            await ProgramContentLifecycleDatabaseLock.CommitAsync(lockHandle);
            context.Database.CurrentTransaction.Should().BeSameAs(outerTransaction);
            await outerTransaction.CommitAsync();
            context.Database.CurrentTransaction.Should().BeNull();
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    private static async Task<(Guid ProgramId, Guid UserId, Guid EnrollmentId, Guid ContentId)> SeedSurveyAsync(
        DbContextOptions<SurveyPolicyDbContext> options,
        bool allowMultipleResponses)
    {
        var programId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        await using var context = new SurveyPolicyDbContext(options);
        var survey = new ProgramContent { Id = contentId, ProgramId = programId, Title = "Survey", Type = ProgramContentType.Survey };
        survey.SetActivitySettings(new SurveyActivitySettings(AllowMultipleResponses: allowMultipleResponses));
        var enrollment = new ProgramUser { Id = Guid.NewGuid(), ProgramId = programId, UserId = userId, IsActive = true };
        context.AddRange(survey, enrollment);
        await context.SaveChangesAsync();
        return (programId, userId, enrollment.Id, contentId);
    }

    private static async Task<(Guid ProgramId, Guid UserId, Guid EnrollmentId, Guid StartContentId, Guid InteractionId, Guid DirectContentId)> SeedLessonAsync(
        DbContextOptions<SurveyPolicyDbContext> options)
    {
        var programId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var startContent = new ProgramContent { Id = Guid.NewGuid(), ProgramId = programId, Title = "Start lesson", Type = ProgramContentType.Lesson };
        var submitContent = new ProgramContent { Id = Guid.NewGuid(), ProgramId = programId, Title = "Submit lesson", Type = ProgramContentType.Lesson };
        var directContent = new ProgramContent { Id = Guid.NewGuid(), ProgramId = programId, Title = "Direct lesson", Type = ProgramContentType.Lesson };
        var enrollment = new ProgramUser { Id = Guid.NewGuid(), ProgramId = programId, UserId = userId, IsActive = true };
        var interaction = new ContentInteraction { Id = Guid.NewGuid(), ProgramUserId = enrollment.Id, UserId = userId, ContentId = submitContent.Id, Status = GameGuild.Learning.Courses.ProgressStatus.InProgress };
        await using var context = new SurveyPolicyDbContext(options);
        context.AddRange(startContent, submitContent, directContent, enrollment, interaction);
        await context.SaveChangesAsync();
        return (programId, userId, enrollment.Id, startContent.Id, interaction.Id, directContent.Id);
    }

    private static Task<EconomyPostgreSqlTestDatabase> CreateContainer(string database) =>
        EconomyPostgreSqlTestDatabase.CreateAsync(database);

    private static DbContextOptions<SurveyPolicyDbContext> CreateOptions(string connectionString, params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<SurveyPolicyDbContext>().UseNpgsql(connectionString);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return builder.Options;
    }

    private static async Task<ContentInteraction> SubmitAsync(
        DbContextOptions<SurveyPolicyDbContext> options,
        (Guid ProgramId, Guid UserId, Guid EnrollmentId, Guid ContentId) survey)
    {
        await using var context = new SurveyPolicyDbContext(options);
        var service = new ProgramWriteService(context, requestContextAccessor: new TestRequestContextAccessor(survey.UserId));
        return (await service.SubmitUserContentAsync(
            survey.ProgramId,
            survey.UserId,
            survey.ContentId,
            """{"kind":"survey","answers":{"answer":true}}"""))!;
    }

    private static async Task<ContentInteraction> SubmitInteractionAsync(
        DbContextOptions<SurveyPolicyDbContext> options,
        (Guid ProgramId, Guid UserId, Guid EnrollmentId, Guid ContentId) survey,
        Guid interactionId)
    {
        await using var context = new SurveyPolicyDbContext(options);
        var service = new ContentInteractionService(context, new TestRequestContextAccessor(survey.UserId));
        return await service.SubmitContentAsync(
            interactionId,
            """{"kind":"survey","answers":{"interaction":true}}""");
    }

    private static async Task<Guid> AddInteractionAsync(
        DbContextOptions<SurveyPolicyDbContext> options,
        (Guid ProgramId, Guid UserId, Guid EnrollmentId, Guid ContentId) survey,
        bool submitted)
    {
        await using var context = new SurveyPolicyDbContext(options);
        var interaction = new ContentInteraction
        {
            Id = Guid.NewGuid(),
            ProgramUserId = survey.EnrollmentId,
            UserId = survey.UserId,
            ContentId = survey.ContentId,
            Status = submitted
                ? GameGuild.Learning.Courses.ProgressStatus.Completed
                : GameGuild.Learning.Courses.ProgressStatus.InProgress,
            SubmittedAt = submitted ? SystemClock.UtcNow : null,
            SubmissionData = submitted ? """{"kind":"survey","answers":{"existing":true}}""" : null,
        };
        context.Add(interaction);
        await context.SaveChangesAsync();
        return interaction.Id;
    }

    private static async Task<(Guid ProgramId, Guid UserId, Guid EnrollmentId, Guid ContentId)> AddEnrollmentAsync(
        DbContextOptions<SurveyPolicyDbContext> options,
        (Guid ProgramId, Guid UserId, Guid EnrollmentId, Guid ContentId) survey)
    {
        await using var context = new SurveyPolicyDbContext(options);
        var learner = new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = survey.ProgramId,
            UserId = Guid.NewGuid(),
            IsActive = true,
        };
        context.Add(learner);
        await context.SaveChangesAsync();
        return (survey.ProgramId, learner.UserId, learner.Id, survey.ContentId);
    }

    private static async Task UpdateSurveyPolicyAsync(
        DbContextOptions<SurveyPolicyDbContext> options,
        Guid contentId,
        bool allowMultipleResponses)
    {
        await using var context = new SurveyPolicyDbContext(options);
        var content = await context.Set<ProgramContent>().SingleAsync(item => item.Id == contentId);
        content.SetActivitySettings(new SurveyActivitySettings(AllowMultipleResponses: allowMultipleResponses));
        await context.SaveChangesAsync();
    }

    private static async Task StartAsync(
        DbContextOptions<SurveyPolicyDbContext> options,
        (Guid ProgramId, Guid UserId, Guid EnrollmentId, Guid ContentId) survey)
    {
        await using var context = new SurveyPolicyDbContext(options);
        var service = new ContentInteractionService(context, new TestRequestContextAccessor(survey.UserId));
        await service.StartContentAsync(survey.EnrollmentId, survey.ContentId);
    }

    private static async Task IgnoreSingleResponseRejectionAsync(Task start)
    {
        try
        {
            await start;
        }
        catch (InvalidOperationException exception) when (
            exception.Message == "This survey accepts only one response." ||
            exception.Message == "Interaction has already been submitted and cannot be changed.")
        {
        }
    }

    private sealed class TestRequestContextAccessor(Guid userId) : IRequestContextAccessor
    {
        public Guid? CurrentUserId => userId;
        public Guid? CurrentTenantId => null;
        public bool IsAuthenticated => true;
        public bool HasTenantContext => false;
        public Task<UserInfo?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<UserInfo?>(new UserInfo(userId, "learner@example.com", "Learner", true));
        public Task<TenantInfo?> GetCurrentTenantAsync(CancellationToken cancellationToken = default) => Task.FromResult<TenantInfo?>(null);
    }

    private sealed class SurveyPolicyDbContext(DbContextOptions<SurveyPolicyDbContext> options) : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.ToTable("program_contents");
                entity.Ignore(content => content.Program);
                entity.Ignore(content => content.Parent);
                entity.Ignore(content => content.Children);
                entity.Ignore(content => content.ContentInteractions);
            });
            modelBuilder.Entity<ProgramUser>(entity =>
            {
                entity.ToTable("program_users");
                entity.Property(enrollment => enrollment.CompletionPercentage)
                    .HasConversion(
                        value => value.Units,
                        value => PercentValue.FromUnits(value))
                    .HasColumnType("integer");
                entity.Property(enrollment => enrollment.FinalGrade)
                    .HasConversion(new ValueConverter<PercentValue?, int?>(
                        value => value.HasValue ? value.Value.Units : null,
                        value => value.HasValue ? PercentValue.FromUnits(value.Value) : null))
                    .HasColumnType("integer");
                entity.Ignore(enrollment => enrollment.User);
                entity.Ignore(enrollment => enrollment.Program);
                entity.Ignore(enrollment => enrollment.ContentInteractions);
                entity.Ignore(enrollment => enrollment.ReceivedGrades);
                entity.Ignore(enrollment => enrollment.GivenGrades);
                entity.Ignore(enrollment => enrollment.ProgramRatings);
            });
            modelBuilder.Entity<ContentInteraction>(entity =>
            {
                entity.ToTable("content_interactions");
                entity.Property(interaction => interaction.ProgressPercentage)
                    .HasConversion(new ValueConverter<PercentValue?, int?>(
                        value => value.HasValue ? value.Value.Units : null,
                        value => value.HasValue ? PercentValue.FromUnits(value.Value) : null))
                    .HasColumnType("integer");
                entity.Property(interaction => interaction.BestScore)
                    .HasConversion(new ValueConverter<ScoreValue?, int?>(
                        value => value.HasValue ? value.Value.Units : null,
                        value => value.HasValue ? ScoreValue.FromUnits(value.Value) : null))
                    .HasColumnType("integer");
                entity.Ignore(interaction => interaction.User);
                entity.Ignore(interaction => interaction.ProgramUser);
                entity.Ignore(interaction => interaction.ActivityGrades);
                entity.Ignore(interaction => interaction.Events);
                entity.HasOne(interaction => interaction.Content).WithMany().HasForeignKey(interaction => interaction.ContentId);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"), "1", StringComparison.Ordinal))
            {
                Skip = "Docker tests disabled by SKIP_DOCKER_TESTS=1.";
            }
        }
    }

    private sealed class AdvisoryLockProbe : DbCommandInterceptor
    {
        public int AcquisitionCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CountAdvisoryLock(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            CountAdvisoryLock(command);
            return ValueTask.FromResult(result);
        }

        private void CountAdvisoryLock(DbCommand command)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal))
            {
                AcquisitionCount++;
            }
        }
    }
}
