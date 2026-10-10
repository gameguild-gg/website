using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using Moq;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class ProgramContentServiceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task UpdateContent_ShouldCheckPersistedProtectionEvenWhenTheReplacementErasesIt(bool useWriteService, bool mutateTracked)
    {
        await using var context = CreateContext();
        var content = PersistedContent(Guid.NewGuid(), "Graded quiz");
        content.Type = ProgramContentType.Questionnaire;
        content.JsonBody = """{"grading":{}}""";
        context.Add(content);
        await context.SaveChangesAsync();
        var replacement = mutateTracked ? content : PersistedContent(content.ProgramId, content.Title);
        replacement.Id = content.Id;
        replacement.Type = ProgramContentType.Lesson;
        replacement.JsonBody = null;
        replacement.Body = "replacement";
        if (!mutateTracked)
        {
            context.ChangeTracker.Clear();
        }
        var guard = new Mock<IProgramContentAcademicMutationGuard>();
        guard.Setup(value => value.GetRejection(It.IsAny<ProgramContent>(), ProgramContentAcademicMutation.Authoring))
            .Returns((ProgramContent candidate, ProgramContentAcademicMutation _) =>
                candidate.Type == ProgramContentType.Questionnaire && candidate.JsonBody is not null ? "Use the atomic workflow." : null);
        Func<Task<ProgramContent>> update = useWriteService
            ? () => new ProgramWriteService(context, academicGuards: [guard.Object]).UpdateContentAsync(replacement)
            : () => new ProgramContentService(context, Mock.Of<IProgramContentScheduleGuard>(),
                Mock.Of<IProgramContentLifecycleGuard>(), [guard.Object]).UpdateContentAsync(replacement);

        await update.Should().ThrowAsync<InvalidOperationException>().WithMessage("Use the atomic workflow.");
        context.ChangeTracker.Clear();
        var persisted = await context.Set<ProgramContent>().SingleAsync();
        persisted.Type.Should().Be(ProgramContentType.Questionnaire);
        persisted.JsonBody.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateContentAsync_ShouldInheritTenantFromProgram()
    {
        await using var context = CreateContext();
        var program = new Program
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Title = "Tenant course",
            Slug = $"tenant-course-{Guid.NewGuid():N}",
        };
        context.Set<Program>().Add(program);
        await context.SaveChangesAsync();
        var service = new ProgramContentService(
            context,
            Mock.Of<IProgramContentScheduleGuard>(),
            Mock.Of<IProgramContentLifecycleGuard>());

        var created = await service.CreateContentAsync(new ProgramContent
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            Title = "Tenant lesson",
        });

        created.TenantId.Should().Be(program.TenantId);
    }

    [Fact]
    public async Task DeleteContentAsync_ShouldSoftDeleteNestedDescendants()
    {
        await using var context = CreateContext();
        var programId = Guid.NewGuid();
        var module = PersistedContent(programId, "Module");
        var submodule = PersistedContent(programId, "Submodule", module.Id);
        var lesson = PersistedContent(programId, "Lesson", submodule.Id);

        context.Set<ProgramContent>().AddRange(module, submodule, lesson);
        await context.SaveChangesAsync();

        var service = new ProgramContentService(
            context,
            Mock.Of<IProgramContentScheduleGuard>(),
            Mock.Of<IProgramContentLifecycleGuard>());

        var deleted = await service.DeleteContentAsync(module.Id);

        deleted.Should().BeTrue();
        var contents = await context.Set<ProgramContent>().IgnoreQueryFilters().ToListAsync();
        contents.Should().OnlyContain(content => content.DeletedAt != null);
    }

    [Fact]
    public async Task DeleteContentAsync_WithCorruptCycle_TerminatesAndDeletesEachItemOnce()
    {
        await using var context = CreateContext();
        var programId = Guid.NewGuid();
        var root = PersistedContent(programId, "Root");
        var child = PersistedContent(programId, "Child", root.Id);
        root.ParentId = child.Id;
        context.Set<ProgramContent>().AddRange(root, child);
        await context.SaveChangesAsync();
        var service = new ProgramContentService(
            context,
            Mock.Of<IProgramContentScheduleGuard>(),
            Mock.Of<IProgramContentLifecycleGuard>());

        var deleted = await service.DeleteContentAsync(root.Id);

        deleted.Should().BeTrue();
        var contents = await context.Set<ProgramContent>().IgnoreQueryFilters().ToListAsync();
        contents.Should().HaveCount(2).And.OnlyContain(content => content.DeletedAt != null);
    }

    [Fact]
    public async Task DeleteContentAsync_PreparesFeatureOwnedRecordsInsideTheContentWorkflow()
    {
        await using var context = CreateContext();
        var content = PersistedContent(Guid.NewGuid(), "Graded quiz");
        content.Type = ProgramContentType.Questionnaire;
        context.Set<ProgramContent>().Add(content);
        await context.SaveChangesAsync();
        var participant = new Mock<IProgramContentDeleteParticipant>(MockBehavior.Strict);
        participant.Setup(value => value.CanHandle(content)).Returns(true);
        participant.Setup(value => value.PrepareDeleteAsync(content, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new ProgramContentService(
            context,
            Mock.Of<IProgramContentScheduleGuard>(),
            Mock.Of<IProgramContentLifecycleGuard>(),
            deleteParticipants: [participant.Object]);

        await service.DeleteContentAsync(content.Id);

        participant.VerifyAll();
    }

    private static LearningCoursesTestContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LearningCoursesTestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LearningCoursesTestContext(options);
    }

    private static ProgramContent PersistedContent(Guid programId, string title, Guid? parentId = null)
    {
        return new ProgramContent
        {
            ProgramId = programId,
            ParentId = parentId,
            Title = title,
            Version = 1,
        };
    }

    private sealed class LearningCoursesTestContext(DbContextOptions<LearningCoursesTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<ProgramContent> ProgramContents => Set<ProgramContent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Program>(entity =>
            {
                entity.Ignore(program => program.ProgramContents);
                entity.Ignore(program => program.ProgramUsers);
                entity.Ignore(program => program.ProgramRatings);
                entity.Ignore(program => program.ProgramWishlists);
            });
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.Ignore(content => content.Program);
                entity.Ignore(content => content.ContentInteractions);
                entity.HasMany(content => content.Children)
                    .WithOne(content => content.Parent)
                    .HasForeignKey(content => content.ParentId);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Transactions are not needed for these service tests.");
        }
    }
}
