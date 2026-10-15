using FluentAssertions;
using GameGuild.API.Core.Compliance;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using Moq;

// The host's own GameGuild.API.Program shadows the course entity inside this namespace.
using CourseProgram = GameGuild.Learning.Courses.Program;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.API.UnitTests.Compliance;

/// <summary>
/// Universal audit coverage for learning-course mutations (issues #180/#181/#184): every save
/// that mutates an audited course entity records its audit row in the same save batch, and a
/// failure while producing the audit row fails the whole save so the mutation cannot commit
/// without its audit event (fail closed).
/// </summary>
public sealed class LearningCoursesMutationAuditInterceptorTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private IActorContextAccessor ActorAccessor() =>
        Mock.Of<IActorContextAccessor>(accessor => accessor.ActorContext == new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = _actorId.ToString(),
            TenantId = _tenantId,
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });

    [Fact]
    public async Task MutatingACourseEntityRecordsItsAuditRowInTheSameSave()
    {
        await using var context = CreateContext(ActorAccessor(), Mock.Of<IHttpContextAccessor>());

        var program = new CourseProgram { Title = "Audited Course", Slug = "audited-course", TenantId = _tenantId };
        context.Set<CourseProgram>().Add(program);
        await context.SaveChangesAsync();

        var auditRow = await context.Set<AuditLog>().AsNoTracking().SingleAsync();
        auditRow.ActionType.Should().Be("Learning.Courses.ProgramCreated");
        auditRow.ResourceType.Should().Be("Program");
        auditRow.ResourceId.Should().Be(program.Id.ToString());
        auditRow.UserId.Should().Be(_actorId);
        auditRow.TenantId.Should().Be(_tenantId);
        auditRow.Category.Should().Be(AuditCategory.Data);

        program.Title = "Audited Course v2";
        program.Touch();
        await context.SaveChangesAsync();

        (await context.Set<AuditLog>().AsNoTracking().CountAsync()).Should().Be(2);
        (await context.Set<AuditLog>().AsNoTracking()
            .CountAsync(row => row.ActionType == "Learning.Courses.ProgramUpdated")).Should().Be(1);
    }

    [Fact]
    public async Task NonAuditedEntitiesDoNotProduceAuditRows()
    {
        await using var context = CreateContext(ActorAccessor(), Mock.Of<IHttpContextAccessor>());

        context.Set<PageAuditProbe>().Add(new PageAuditProbe { Name = "probe" });
        await context.SaveChangesAsync();

        (await context.Set<AuditLog>().AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AuditRowFailureFailsTheSaveAndRollsBackTheMutation()
    {
        var actors = new Mock<IActorContextAccessor>();
        actors.SetupGet(accessor => accessor.ActorContext).Throws(new InvalidOperationException("Synthetic audit enrichment failure."));
        await using var context = CreateContext(actors.Object, Mock.Of<IHttpContextAccessor>());

        context.Set<CourseProgram>().Add(new CourseProgram { Title = "Must Not Exist", Slug = "must-not-exist" });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await context.Set<CourseProgram>().AsNoTracking().CountAsync()).Should().Be(0);
        (await context.Set<AuditLog>().AsNoTracking().CountAsync()).Should().Be(0);
    }

    private LearningAuditSqliteContext CreateContext(IActorContextAccessor actors, IHttpContextAccessor httpContextAccessor)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<LearningAuditSqliteContext>()
            .UseSqlite(connection)
            .AddInterceptors(new LearningCoursesMutationAuditInterceptor(actors, httpContextAccessor))
            .Options;
        var context = new LearningAuditSqliteContext(options);
        context.Database.EnsureCreated();
        return context;
    }


    private sealed class LearningAuditSqliteContext(DbContextOptions<LearningAuditSqliteContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Focused model: only the audited entity, its PercentValue converter, and the audit row.
            // Applying the production Courses configuration would pull cross-module entities
            // (commerce products) whose relationships are configured on the application context.
            modelBuilder.Entity<CourseProgram>(program =>
            {
                program.HasKey(entity => entity.Id);
                program.Ignore(entity => entity.ProgramContents);
                program.Ignore(entity => entity.ProgramUsers);
                program.Ignore(entity => entity.ProgramRatings);
                program.Ignore(entity => entity.ProgramWishlists);
                program.Property(entity => entity.PassingScore)
                    .HasConversion(
                        value => value.Units,
                        units => GameGuild.Learning.Grading.Contracts.PercentValue.FromUnits(units))
                    .HasColumnType("integer");
            });
            modelBuilder.Entity<AuditLog>(entity => entity.HasKey(auditLog => auditLog.Id));
            modelBuilder.Entity<PageAuditProbe>(entity => entity.HasKey(probe => probe.Id));
        }
    }

    private sealed class PageAuditProbe
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
    }
}
