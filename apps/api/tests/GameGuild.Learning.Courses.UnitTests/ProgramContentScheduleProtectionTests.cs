using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Learning.Assessments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class ProgramContentScheduleProtectionTests
{
    [Fact]
    public async Task ProgramWriteService_DeleteContentAsync_WhenContentHasAssessmentCue_RejectsDeletion()
    {
        await using var context = CreateContext();
        var content = PersistedContent(Guid.NewGuid(), "Video lesson");
        context.Add(content);
        await context.SaveChangesAsync();
        var lifecycle = new Mock<IProgramContentLifecycleGuard>();
        lifecycle.Setup(guard => guard.HasBlockingDeleteReference(content.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new ProgramWriteService(context, lifecycle.Object);

        var act = () => service.DeleteContentAsync(content.Id);

        await act.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("*assessment cue*");
    }

    [Fact]
    public async Task ProgramWriteService_UpdateContentAsync_WhenLinkedVideoWouldBecomeIncompatible_RejectsUpdate()
    {
        await using var context = CreateContext();
        var content = PersistedContent(Guid.NewGuid(), "Video lesson");
        content.Type = ProgramContentType.Lesson;
        content.LessonFormat = LessonContentFormat.Video;
        context.Add(content);
        await context.SaveChangesAsync();
        var updated = PersistedContent(content.ProgramId, content.Title);
        updated.Id = content.Id;
        updated.Type = ProgramContentType.Lesson;
        updated.LessonFormat = LessonContentFormat.Markdown;
        var lifecycle = new Mock<IProgramContentLifecycleGuard>();
        lifecycle.Setup(guard => guard.HasBlockingIncompatibleUpdateReference(
                content.Id,
                ProgramContentType.Lesson,
                LessonContentFormat.Markdown,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new ProgramWriteService(context, lifecycle.Object);

        var act = () => service.UpdateContentAsync(updated);

        await act.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("*assessment cue*");
    }

    [Fact]
    public async Task RemoveProgramContentCommandHandler_WhenContentHasAssessmentCue_RejectsDeletion()
    {
        await using var context = CreateContext();
        var content = PersistedContent(Guid.NewGuid(), "Video lesson");
        context.Add(content);
        await context.SaveChangesAsync();
        var lifecycle = new Mock<IProgramContentLifecycleGuard>();
        lifecycle.Setup(guard => guard.HasBlockingDeleteReference(content.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new RemoveProgramContentCommandHandler(
            context,
            Mock.Of<IProgramContentScheduleGuard>(),
            lifecycle.Object,
            NullLogger<RemoveProgramContentCommandHandler>.Instance);

        var act = () => handler.Handle(new RemoveProgramContentCommand(content.ProgramId, content.Id), CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("*assessment cue*");
    }

    [Fact]
    public async Task RegisteredRemoveProgramContentHandler_IsSingleGuardedHandler_AndBlocksCueReferencedDeletion()
    {
        await using var context = CreateContext();
        var content = PersistedContent(Guid.NewGuid(), "Video lesson");
        context.Add(content);
        await context.SaveChangesAsync();
        var lifecycle = new Mock<IProgramContentLifecycleGuard>();
        lifecycle.Setup(guard => guard.HasBlockingDeleteReference(content.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IApplicationDbContext>(_ => context);
        services.AddScoped<IProgramContentScheduleGuard>(_ => Mock.Of<IProgramContentScheduleGuard>());
        services.AddScoped<IProgramContentLifecycleGuard>(_ => lifecycle.Object);
        services.AddCqrs(typeof(RemoveProgramContentCommandHandler).Assembly);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var handlers = scope.ServiceProvider
            .GetServices<IRequestHandler<RemoveProgramContentCommand, bool>>()
            .ToArray();

        handlers.Should().ContainSingle()
            .Which.Should().BeOfType<RemoveProgramContentCommandHandler>();
        scope.ServiceProvider.GetServices<ICommandHandler<RemoveProgramContentCommand, bool>>()
            .Should().ContainSingle()
            .Which.Should().BeOfType<RemoveProgramContentCommandHandler>();

        var act = () => handlers.Single().Handle(new RemoveProgramContentCommand(content.ProgramId, content.Id), CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("*assessment cue*");
    }

    [Fact]
    public async Task DeleteContentAsync_WhenDescendantIsActivelyScheduled_RejectsEntireDeletion()
    {
        await using var context = CreateContext();
        var programId = Guid.NewGuid();
        var module = PersistedContent(programId, "Module");
        var lesson = PersistedContent(programId, "Lesson", module.Id);
        context.AddRange(module, lesson);
        await context.SaveChangesAsync();
        var guard = new Mock<IProgramContentScheduleGuard>();
        guard.Setup(candidate => candidate.HasActiveScheduleReference(lesson.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new ProgramContentService(context, guard.Object, Mock.Of<IProgramContentLifecycleGuard>());

        var act = () => service.DeleteContentAsync(module.Id);

        await act.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("*active class schedule*");
        (await context.Set<ProgramContent>().ToArrayAsync())
            .Should().OnlyContain(content => content.DeletedAt == null);
    }

    [Fact]
    public async Task DeleteContentAsync_WhenContentHasAssessmentCue_RejectsDeletion()
    {
        await using var context = CreateContext();
        var content = PersistedContent(Guid.NewGuid(), "Video lesson");
        context.Add(content);
        await context.SaveChangesAsync();
        var lifecycle = new Mock<IProgramContentLifecycleGuard>();
        lifecycle.Setup(guard => guard.HasBlockingDeleteReference(content.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new ProgramContentService(context, Mock.Of<IProgramContentScheduleGuard>(), lifecycle.Object);

        var act = () => service.DeleteContentAsync(content.Id);

        await act.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("*assessment cue*");
    }

    [Fact]
    public async Task UpdateContentAsync_WhenLinkedVideoWouldBecomeIncompatible_RejectsUpdate()
    {
        await using var context = CreateContext();
        var content = PersistedContent(Guid.NewGuid(), "Video lesson");
        content.LessonFormat = LessonContentFormat.Video;
        context.Add(content);
        await context.SaveChangesAsync();
        var lifecycle = new Mock<IProgramContentLifecycleGuard>();
        lifecycle.Setup(guard => guard.HasBlockingIncompatibleUpdateReference(
                content.Id,
                ProgramContentType.Lesson,
                LessonContentFormat.Markdown,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var update = PersistedContent(content.ProgramId, content.Title);
        update.Id = content.Id;
        update.LessonFormat = LessonContentFormat.Markdown;
        var service = new ProgramContentService(context, Mock.Of<IProgramContentScheduleGuard>(), lifecycle.Object);

        var act = () => service.UpdateContentAsync(update);

        await act.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("*assessment cue*");
    }

    [Fact]
    public async Task AssessmentLifecycleGuard_WhenAssessmentIsDeleted_DoesNotBlockContentDeletion()
    {
        await using var context = CreateContext();
        var assessment = Assessment.Create(Guid.NewGuid(), "Checkpoint", AssessmentType.Quiz, Score("10"));
        assessment.Version = 1;
        var cue = assessment.AddInteractiveVideoCue(Guid.NewGuid(), "chapter-1");
        cue.Version = 1;
        context.AddRange(assessment, cue);
        await context.SaveChangesAsync();
        var guard = new AssessmentProgramContentLifecycleGuard(context);

        (await guard.HasBlockingDeleteReference(cue.ContentId)).Should().BeTrue();
        assessment.SoftDelete();
        context.Update(assessment);
        await context.SaveChangesAsync();

        (await guard.HasBlockingDeleteReference(cue.ContentId)).Should().BeFalse();
    }

    private static ProgramContentProtectionTestContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProgramContentProtectionTestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ProgramContentProtectionTestContext(options);
    }

    private static ProgramContent PersistedContent(Guid programId, string title, Guid? parentId = null) =>
        new()
        {
            ProgramId = programId,
            ParentId = parentId,
            Title = title,
            Version = 1
        };

    private sealed class ProgramContentProtectionTestContext(DbContextOptions<ProgramContentProtectionTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.Ignore(content => content.Program);
                entity.Ignore(content => content.ContentInteractions);
                entity.HasMany(content => content.Children)
                    .WithOne(content => content.Parent)
                    .HasForeignKey(content => content.ParentId);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
