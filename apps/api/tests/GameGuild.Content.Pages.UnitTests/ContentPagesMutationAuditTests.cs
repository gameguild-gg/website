using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Content.Pages.UnitTests;

/// <summary>
/// Universal audit coverage for content mutations (issues #180/#181/#184): every mutating
/// service operation emits one content audit event, and an audit emission failure rolls the
/// mutation back inside the owning transaction (fail closed).
/// </summary>
public class ContentPagesMutationAuditTests
{
    [Fact]
    public async Task EveryPageMutationEmitsItsAuditEvent()
    {
        await using var db = CreateSqliteContext();
        var sink = new RecordingContentPagesAuditEventSink();
        var service = new PageService(db, sink);
        var publisher = Guid.NewGuid();

        var page = await service.CreateAsync(new CreatePageDto { Slug = "audit", Title = "Audit", PageType = PageType.Landing });
        await service.UpdateAsync(page.Id, new UpdatePageDto { Title = "Audit v2" });
        await service.PublishAsync(page.Id, publisher);
        await service.UnpublishAsync(page.Id);
        await service.DeleteAsync(page.Id);

        sink.Events.Select(auditEvent => auditEvent.ActionType).Should().Equal(
            "Content.Pages.PageCreated",
            "Content.Pages.PageUpdated",
            "Content.Pages.PagePublished",
            "Content.Pages.PageUnpublished",
            "Content.Pages.PageDeleted");
        sink.Events.Should().OnlyContain(auditEvent => auditEvent.ResourceType == "Page");
        sink.Events.Should().OnlyContain(auditEvent => auditEvent.ResourceId == page.Id.ToString());
        var published = sink.Events.Single(auditEvent => auditEvent.ActionType == "Content.Pages.PagePublished");
        published.ActorUserId.Should().Be(publisher);
    }

    [Fact]
    public async Task EverySectionMutationEmitsItsAuditEvent()
    {
        await using var db = CreateSqliteContext();
        var sink = new RecordingContentPagesAuditEventSink();
        var service = new PageService(db, sink);
        var page = await service.CreateAsync(new CreatePageDto { Slug = "sections", Title = "Sections" });

        var section = await service.CreateSectionAsync(page.Id, new CreatePageSectionDto { Heading = "One" });
        await service.UpdateSectionAsync(section.Id, new UpdatePageSectionDto { Heading = "One v2" });
        await service.ReorderSectionsAsync(page.Id, [section.Id]);
        await service.DeleteSectionAsync(section.Id);

        sink.Events.Select(auditEvent => auditEvent.ActionType).Should().ContainInOrder(
            "Content.Pages.PageSectionCreated",
            "Content.Pages.PageSectionUpdated",
            "Content.Pages.PageSectionsReordered",
            "Content.Pages.PageSectionDeleted");
        sink.Events.Where(auditEvent => auditEvent.ActionType.StartsWith("Content.Pages.PageSection", StringComparison.Ordinal))
            .Should().OnlyContain(auditEvent => auditEvent.ResourceId == section.Id.ToString());
    }

    [Fact]
    public async Task EveryContentResourceMutationEmitsItsAuditEvent()
    {
        await using var db = CreateSqliteContext();
        var sink = new RecordingContentPagesAuditEventSink();
        var service = new ContentResourceService(db, sink);
        var publisher = Guid.NewGuid();

        var resource = await service.CreateAsync(new CreateContentResourceDto { Slug = "guide", Title = "Guide", ResourceType = ContentResourceType.Article });
        await service.UpdateAsync(resource.Id, new UpdateContentResourceDto { Title = "Guide v2" });
        await service.PublishAsync(resource.Id, publisher);
        await service.DeleteAsync(resource.Id);

        sink.Events.Select(auditEvent => auditEvent.ActionType).Should().Equal(
            "Content.Pages.ContentResourceCreated",
            "Content.Pages.ContentResourceUpdated",
            "Content.Pages.ContentResourcePublished",
            "Content.Pages.ContentResourceDeleted");
    }

    [Fact]
    public async Task MarketingLeadCreationEmitsAuditEventWithoutSubmissionValues()
    {
        await using var db = CreateSqliteContext();
        var sink = new RecordingContentPagesAuditEventSink();
        var service = new MarketingLeadService(db, sink);

        await service.CreateAsync(new CreateMarketingLeadDto
        {
            Source = "contact",
            Email = "person@example.com",
            Name = "Ada",
            Company = "Modu",
            Message = "please call me"
        });

        var auditEvent = sink.Events.Should().ContainSingle().Subject;
        auditEvent.ActionType.Should().Be("Content.Pages.MarketingLeadCreated");
        auditEvent.ResourceType.Should().Be("MarketingLead");
        var serialized = System.Text.Json.JsonSerializer.Serialize(auditEvent.Metadata);
        serialized.Should().NotContain("person@example.com").And.NotContain("Ada").And.NotContain("please call me");
    }

    [Fact]
    public async Task AuditEmissionFailureRollsBackThePageMutation()
    {
        await using var db = CreateSqliteContext();
        var sink = new ThrowingContentPagesAuditEventSink();
        var service = new PageService(db, sink);

        var act = () => service.CreateAsync(new CreatePageDto { Slug = "must-not-exist", Title = "Rolled back" });

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await db.Set<Page>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AuditEmissionFailureRollsBackTheContentResourceMutation()
    {
        await using var db = CreateSqliteContext();
        var sink = new ThrowingContentPagesAuditEventSink();
        var service = new ContentResourceService(db, sink);

        var resource = new ContentResource { Slug = "must-not-publish", Title = "Rolled back", ResourceType = ContentResourceType.Article, Status = ContentResourceStatus.Draft };
        db.Set<ContentResource>().Add(resource);
        await db.SaveChangesAsync();

        var publish = () => service.PublishAsync(resource.Id, Guid.NewGuid());
        await publish.Should().ThrowAsync<InvalidOperationException>();
        (await db.Set<ContentResource>().CountAsync(entity => entity.Status == ContentResourceStatus.Published)).Should().Be(0);
    }

    private static ContentPagesAuditSqliteContext CreateSqliteContext()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ContentPagesAuditSqliteContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ContentPagesAuditSqliteContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class ContentPagesAuditSqliteContext(DbContextOptions<ContentPagesAuditSqliteContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Mirrors ApplicationDbContext: entity versions increment on every save so
            // soft-deleting a persisted entity is allowed.
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is EntityBase<Guid> &&
                    entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Property(nameof(EntityBase<Guid>.Version)).CurrentValue =
                        (int)entry.Property(nameof(EntityBase<Guid>.Version)).CurrentValue! + 1;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Page>(page =>
            {
                page.HasKey(entity => entity.Id);
                page.Ignore(entity => entity.Sections);
                page.Ignore(entity => entity.ChildPages);
                page.Ignore(entity => entity.ParentPage);
            });
            modelBuilder.Entity<PageSection>(section =>
            {
                section.HasKey(entity => entity.Id);
                section.Ignore(entity => entity.Page);
            });
            modelBuilder.Entity<ContentResource>(resource => resource.HasKey(entity => entity.Id));
            modelBuilder.Entity<MarketingLead>(lead => lead.HasKey(entity => entity.Id));
        }
    }
}

public sealed class RecordingContentPagesAuditEventSink : IContentPagesAuditEventSink
{
    public List<ContentPagesAuditEvent> Events { get; } = [];

    public Task RecordAsync(ContentPagesAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}

public sealed class ThrowingContentPagesAuditEventSink : IContentPagesAuditEventSink
{
    public Task RecordAsync(ContentPagesAuditEvent auditEvent, CancellationToken cancellationToken)
        => Task.FromException(new InvalidOperationException("Synthetic content audit persistence failure."));
}
