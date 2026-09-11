using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests.Authoring;

public sealed class ProgramContentAuthoringServiceTests
{
    [Fact]
    public async Task GetDraft_CreatesSharedDraftFromPublishedContent()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var content = PublishedContent("Published title", "Published body");
        context.Add(content);
        await context.SaveChangesAsync();
        var service = new ProgramContentAuthoringService(context);

        var draft = await service.GetOrCreateDraft(content.ProgramId, content.Id, actorId, CancellationToken.None);

        draft.Revision.Should().Be(1);
        draft.BasePublishedVersion.Should().Be(content.Version);
        draft.Payload.Title.Should().Be("Published title");
        draft.Payload.Body.Should().Be("Published body");
        (await context.Set<ProgramContentDraft>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SaveDraft_WithStaleRevision_DoesNotOverwriteNewerPayload()
    {
        await using var context = CreateContext();
        var content = PublishedContent("Lesson", "Original");
        context.Add(content);
        await context.SaveChangesAsync();
        var service = new ProgramContentAuthoringService(context);
        var draft = await service.GetOrCreateDraft(content.ProgramId, content.Id, Guid.NewGuid(), CancellationToken.None);
        var first = draft.Payload with { Body = "First save" };
        await service.SaveDraft(content.ProgramId, content.Id, 1, first, Guid.NewGuid(), CancellationToken.None);

        var act = () => service.SaveDraft(
            content.ProgramId,
            content.Id,
            1,
            draft.Payload with { Body = "Stale save" },
            Guid.NewGuid(),
            CancellationToken.None);

        await act.Should().ThrowAsync<AuthoringRevisionConflictException>();
        var persisted = await context.Set<ProgramContentDraft>().SingleAsync();
        persisted.PayloadJson.Should().Contain("First save");
        persisted.PayloadJson.Should().NotContain("Stale save");
    }

    [Fact]
    public async Task Publish_AppliesDraftAtomicallyAndRecordsActorAudit()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var content = PublishedContent("Lesson", "Published body");
        context.Add(content);
        await context.SaveChangesAsync();
        var service = new ProgramContentAuthoringService(context);
        var draft = await service.GetOrCreateDraft(content.ProgramId, content.Id, actorId, CancellationToken.None);
        await service.SaveDraft(
            content.ProgramId,
            content.Id,
            draft.Revision,
            draft.Payload with { Title = "New title", Body = "New published body" },
            actorId,
            CancellationToken.None);

        var published = await service.Publish(content.ProgramId, content.Id, 2, actorId, CancellationToken.None);

        published.PublishedContent.Title.Should().Be("New title");
        published.PublishedContent.Body.Should().Be("New published body");
        published.Draft.BasePublishedVersion.Should().Be(published.PublishedContent.Version);
        var audit = await context.Set<ProgramContentPublicationAudit>().SingleAsync();
        audit.PublishedBy.Should().Be(actorId);
        audit.ContentId.Should().Be(content.Id);
    }

    private static ProgramContent PublishedContent(string title, string body) => new()
    {
        Id = Guid.NewGuid(),
        ProgramId = Guid.NewGuid(),
        Title = title,
        Slug = title.ToSlugCase(),
        Body = body,
        Type = ProgramContentType.Lesson,
        LessonFormat = LessonContentFormat.Markdown,
    };

    private static AuthoringTestDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AuthoringTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class AuthoringTestDbContext(DbContextOptions<AuthoringTestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProgramContent>().HasKey(candidate => candidate.Id);
            modelBuilder.Entity<ProgramContent>().Ignore(candidate => candidate.Program);
            modelBuilder.Entity<ProgramContent>().Ignore(candidate => candidate.Parent);
            modelBuilder.Entity<ProgramContent>().Ignore(candidate => candidate.Children);
            modelBuilder.Entity<ProgramContent>().Ignore(candidate => candidate.ContentInteractions);
            modelBuilder.Entity<ProgramContent>().Ignore(candidate => candidate.FullPath);
            modelBuilder.Entity<ProgramContent>().Ignore(candidate => candidate.ChildCount);
            modelBuilder.Entity<ProgramContent>().Ignore(candidate => candidate.HasChildren);
            modelBuilder.Entity<ProgramContentDraft>().HasKey(candidate => candidate.Id);
            modelBuilder.Entity<ProgramContentDraft>().Ignore(candidate => candidate.ETag);
            modelBuilder.Entity<ProgramContentPublicationAudit>().HasKey(candidate => candidate.Id);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<EntityBase<Guid>>()
                         .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
            {
                entry.Entity.Version++;
            }
            return base.SaveChangesAsync(cancellationToken);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
